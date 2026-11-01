// POST /api/dev?a=<action>   Mod Dev (launcher 2.0): mod dev accounts, uploads, player feedback and reports.
// One function for all of it (Vercel Hobby allows 12). Every action takes and returns JSON.
//
// Accounts: a launcher install that has registered (name + email, /api/register) can become a mod dev by choosing a
// public author name. The launcher makes a random author key and keeps it; the hub keeps only its hash, plus the hash of
// a one-time recovery code that moves the account to another PC. No passwords, no sign-in, no email sent.
//
// Uploads: submit (details + file list) -> chunk (2 MB pieces, base64) -> finish. The upload then waits for Tonnie's
// safety check (tools/modmanager/review.mjs, which uses /api/admin). Chunks are deleted once it's decided.
//
//   author   { installId, authorKey, name }        -> { name, recoveryCode }
//   recover  { installId, authorKey, code }        -> { name }       (authorKey = a new key for this PC)
//   newcode  { authorKey }                         -> { recoveryCode }
//   mine     { authorKey }                         -> { author, mods, submissions, inbox }
//   submit   { authorKey, modId?, name, version, category, description, changelog, conflicts, requires, testedBuild, files, images }
//   chunk    { authorKey, id, file, n, data }
//   finish   { authorKey, id }
//   withdraw { authorKey, id }
//   seen     { authorKey }                         marks decisions as seen
//   read     { authorKey, ids }                    marks feedback as read (ids empty = all)
//   feedback { installId, modId, kind, text }      player -> the mod's developer
//   report   { installId, modId, reason, text }    player -> Tonnie

import { sql, ensureSchema, manifestMods, modIds, ipHash, validInstall, body, sha256, randomHex, authorOf, notify, CHUNK } from './_shared.js';

const MAX_MOD = 100 * 1024 * 1024, MAX_IMAGE = 2 * 1024 * 1024, MAX_WAITING = 250 * 1024 * 1024;
const CATEGORIES = ['Lights and sirens', 'Vehicles and liveries', 'Uniforms and gear', 'Sounds', 'Maps and scenarios', 'Tools', 'Other'];
const FEEDBACK_KINDS = ['bug', 'idea', 'thanks'];
const REPORT_REASONS = ['broken', 'stolen', 'harmful', 'inappropriate', 'other'];

class Bad extends Error { constructor(status, msg) { super(msg); this.status = status; } }
const bad = (msg, status = 400) => { throw new Bad(status, msg); };
const str = (v) => (typeof v === 'string' ? v.trim() : '');

function recoveryCode() {
	const abc = 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789';
	let s = '';
	for (const b of Buffer.from(randomHex(16), 'hex')) s += abc[b % 32];
	return s.match(/.{4}/g).join('-');
}
const normCode = (c) => str(c).toUpperCase().replace(/[^A-Z0-9]/g, '');
// "Tönnie_" and "tonnie" count as the same name
const plain = (n) => n.normalize('NFKD').replace(/\p{M}/gu, '').toLowerCase().replace(/[^a-z0-9]/g, '');

async function needAuthor(key) {
	const a = await authorOf(key);
	if (!a) bad('This PC isn\'t set up as a mod dev, or its key was replaced. Restore it with your recovery code.', 403);
	return a;
}
async function needUser(installId) {
	if (!validInstall(installId)) bad('Bad install id.');
	const [u] = await sql`select install_id from users where install_id = ${installId}`;
	if (!u) bad('register', 403);
}
// "My Cool Mod!" -> "My_Cool_Mod"
const idFromName = (name) => name.normalize('NFKD').replace(/[^\x00-\x7f]/g, '').replace(/[^A-Za-z0-9]+/g, '_').replace(/^_+|_+$/g, '').slice(0, 40);

// Every live mod (mods.json and community), as { id, name, version, files, author_id? }.
async function liveMods() {
	const list = (await manifestMods()).map((m) => ({ id: m.id, name: m.name, version: m.version, files: m.files || [] }));
	for (const r of await sql`select mod_id, author_id, entry from catalog`) {
		const i = list.findIndex((m) => m.id === r.mod_id);
		const m = { id: r.mod_id, name: r.entry.name, version: r.entry.version, files: r.entry.files || [], author_id: r.author_id };
		if (i >= 0) list[i] = m; else list.push(m);
	}
	return list;
}

const actions = {
	async author(b, req) {
		await needUser(b.installId);
		const name = str(b.name);
		// Latin letters only (accents are fine), so look-alike letters from other alphabets can't imitate another author.
		if (!/^[A-Za-z0-9À-ÖØ-öø-ÿ ._'-]{2,32}$/.test(name) || !/[A-Za-z0-9]/.test(name)) bad('Pick an author name of 2 to 32 letters, numbers, spaces, dots, dashes or underscores.');
		if (typeof b.authorKey !== 'string' || !/^[a-f0-9]{64}$/.test(b.authorKey)) bad('Bad author key.');
		const [mine] = await sql`select display_name from authors where install_id = ${b.installId}`;
		if (mine) bad('This PC already has a mod dev account (' + mine.display_name + '). Restore it with your recovery code.', 409);
		const taken = (await manifestMods()).some((m) => plain(m.author || 'Tonnie') === plain(name));
		const others = await sql`select display_name from authors`;
		const dup = others.some((o) => plain(o.display_name) === plain(name));
		if (taken || dup) bad('That author name is taken. Pick another one.', 409);
		const ip = ipHash(req);
		const [recent] = await sql`select count(*)::int as n from authors where ip_hash = ${ip} and created_at > now() - interval '1 day'`;
		if (recent.n >= 3) bad('Too many new mod dev accounts from this connection today. Try again tomorrow.', 429);
		const code = recoveryCode();
		await sql`insert into authors (author_id, display_name, key_hash, recovery_hash, install_id, ip_hash)
			values (${randomHex(12)}, ${name}, ${sha256(b.authorKey)}, ${sha256(normCode(code))}, ${b.installId}, ${ip})`;
		return { name, recoveryCode: code };
	},

	async recover(b) {
		if (typeof b.authorKey !== 'string' || !/^[a-f0-9]{64}$/.test(b.authorKey)) bad('Bad author key.');
		const code = normCode(b.code);
		if (code.length !== 16) bad('A recovery code has 16 letters and numbers, like ABCD-EFGH-JKLM-NPQR.');
		const [a] = await sql`select author_id, display_name from authors where recovery_hash = ${sha256(code)}`;
		if (!a) bad('That recovery code doesn\'t match any mod dev account.', 404);
		await sql`update authors set key_hash = ${sha256(b.authorKey)}, install_id = coalesce((select install_id from users where install_id = ${validInstall(b.installId) ? b.installId : ''}), install_id) where author_id = ${a.author_id}`;
		return { name: a.display_name };
	},

	async newcode(b) {
		const a = await needAuthor(b.authorKey);
		const code = recoveryCode();
		await sql`update authors set recovery_hash = ${sha256(normCode(code))} where author_id = ${a.author_id}`;
		return { recoveryCode: code };
	},

	async mine(b) {
		const a = await needAuthor(b.authorKey);
		const owned = await sql`select o.mod_id, c.entry from mod_owners o left join catalog c on c.mod_id = o.mod_id where o.author_id = ${a.author_id}`;
		const manifest = await manifestMods();
		const counts = await sql`select f.mod_id, count(*)::int as total, count(*) filter (where not f.read)::int as unread
			from feedback f join mod_owners o on o.mod_id = f.mod_id where o.author_id = ${a.author_id} group by f.mod_id`;
		const mods = owned.map((o) => {
			const e = o.entry || manifest.find((m) => m.id === o.mod_id) || { name: o.mod_id };
			const c = counts.find((x) => x.mod_id === o.mod_id) || { total: 0, unread: 0 };
			return { modId: o.mod_id, name: e.name, version: e.version, thumb: e.thumb, category: e.category, live: true, feedback: c.total, unread: c.unread };
		});
		const subs = await sql`select id, mod_id, kind, status, reason, meta, total_bytes, created_at, decided_at, seen_by_author
			from submissions where author_id = ${a.author_id} order by id desc limit 50`;
		const inbox = await sql`select f.id, f.mod_id, f.kind, f.text, f.read, f.created_at, u.name as from_name
			from feedback f join mod_owners o on o.mod_id = f.mod_id join users u on u.install_id = f.install_id
			where o.author_id = ${a.author_id} order by f.id desc limit 200`;
		return {
			author: { name: a.display_name }, mods,
			submissions: subs.map((s) => ({ id: Number(s.id), modId: s.mod_id, kind: s.kind, status: s.status, reason: s.reason, name: s.meta.name, version: s.meta.version,
				size: Number(s.total_bytes), createdAt: s.created_at, decidedAt: s.decided_at, unseen: !s.seen_by_author })),
			inbox: inbox.map((f) => ({ id: Number(f.id), modId: f.mod_id, kind: f.kind, text: f.text, read: f.read, from: f.from_name, createdAt: f.created_at }))
		};
	},

	async submit(b) {
		const a = await needAuthor(b.authorKey);
		// uploads abandoned for a day are cleared out
		await sql`delete from submissions where status = 'uploading' and created_at < now() - interval '1 day'`;
		const name = str(b.name), version = str(b.version), category = str(b.category), description = str(b.description), changelog = str(b.changelog);
		if (name.length < 3 || name.length > 60) bad('The mod name needs 3 to 60 characters.');
		if (!/^[0-9][0-9A-Za-z.]{0,15}$/.test(version)) bad('The version should start with a number, like 1 or 1.2.');
		if (!CATEGORIES.includes(category)) bad('Pick a category.');
		if (description.length < 20 || description.length > 2000) bad('The description needs 20 to 2000 characters: say what the mod changes and how to use it.');
		if (changelog.length > 1000) bad('Keep the changes to 1000 characters.');
		const live = await liveMods();
		let modId, kind;
		if (b.modId) {
			modId = str(b.modId);
			const [own] = await sql`select 1 as x from mod_owners where mod_id = ${modId} and author_id = ${a.author_id}`;
			if (!own) bad('You can only update your own mods.', 403);
			const cur = live.find((m) => m.id === modId);
			if (cur && cur.version === version) bad('That version is already live. Give the update a new version number.');
			kind = 'update';
		} else {
			modId = idFromName(name);
			if (modId.length < 3) bad('The mod name needs at least 3 letters or numbers.');
			const lower = modId.toLowerCase();
			if (live.some((m) => m.id.toLowerCase() === lower || m.name.toLowerCase() === name.toLowerCase())) bad('There\'s already a mod called that. Pick another name.', 409);
			const [other] = await sql`select 1 as x from submissions where lower(mod_id) = ${lower} and author_id <> ${a.author_id} and status in ('uploading', 'pending')`;
			if (other) bad('Someone else is uploading a mod with that name. Pick another name.', 409);
			kind = 'new';
		}
		const [open] = await sql`select count(*)::int as n from submissions where author_id = ${a.author_id} and status in ('uploading', 'pending')`;
		const [same] = await sql`select id from submissions where author_id = ${a.author_id} and mod_id = ${modId} and status in ('uploading', 'pending')`;
		if (same) bad('This mod already has an upload waiting. Withdraw it first (Mod Dev > My mods) to send a new one.', 409);
		if (open.n >= 3) bad('You have 3 uploads waiting for their safety check. Wait for those first.', 429);

		const files = Array.isArray(b.files) ? b.files : [], images = Array.isArray(b.images) ? b.images : [];
		const fileRe = /^[A-Za-z0-9_.-]{1,100}\.(pak|utoc|ucas)$/i, hex = /^[a-fA-F0-9]{64}$/;
		if (!files.length || files.length > 30) bad('Add the mod\'s .pak files (with their .utoc and .ucas, if it has them).');
		const names = new Set();
		for (const f of files) {
			if (!f || !fileRe.test(f.name) || !Number.isInteger(f.size) || f.size <= 0 || !hex.test(f.sha256 || '')) bad('Only .pak, .utoc and .ucas files can be uploaded.');
			if (/^(wildfire|global)/i.test(f.name)) bad(f.name + ' looks like one of the game\'s own files. Rename your mod\'s files.');
			if (names.has(f.name.toLowerCase())) bad('Two files are called ' + f.name + '.');
			names.add(f.name.toLowerCase());
		}
		if (![...names].some((n) => n.endsWith('.pak'))) bad('A mod needs at least one .pak file.');
		for (const n of names) {
			const stem = n.replace(/\.(pak|utoc|ucas)$/, '');
			if ((n.endsWith('.utoc') || n.endsWith('.ucas')) && !(names.has(stem + '.pak') && names.has(stem + '.utoc') && names.has(stem + '.ucas')))
				bad(stem + ' needs all three files: .pak, .utoc and .ucas.');
		}
		for (const m of live) {
			if (m.id === modId) continue;
			const clash = (m.files || []).find((f) => names.has(f.name.toLowerCase()));
			if (clash) bad(clash.name + ' is already used by ' + m.name + '. Give your files their own names (for example pakchunk1234-YourMod-Windows_P).', 409);
		}
		if (!images.length || images.length > 7) bad('Add a picture of the mod (and up to 6 screenshots).');
		for (const im of images) if (!im || !/^[a-z0-9_-]{1,40}\.jpg$/.test(im.name) || !Number.isInteger(im.size) || im.size <= 0 || im.size > MAX_IMAGE || !hex.test(im.sha256 || '')) bad('Pictures must be JPEGs under 2 MB.');
		const modBytes = files.reduce((s, f) => s + f.size, 0), total = modBytes + images.reduce((s, i) => s + i.size, 0);
		if (modBytes > MAX_MOD) bad('Mods can be up to 100 MB for now.');
		const [waiting] = await sql`select coalesce(sum(total_bytes), 0)::bigint as n from submissions where status in ('uploading', 'pending')`;
		if (Number(waiting.n) + total > MAX_WAITING) bad('The upload space is full right now. Try again tomorrow.', 507);
		const ids = await modIds();
		const listOf = (v) => (Array.isArray(v) ? v.map(str).filter(Boolean) : []).slice(0, 10);
		const conflicts = listOf(b.conflicts), requires = listOf(b.requires);
		for (const x of [...conflicts, ...requires]) if (!ids.has(x) || x === modId) bad('Unknown mod: ' + x);
		const meta = { name, version, category, description, changelog, conflicts, requires, testedBuild: /^[0-9,]{0,40}$/.test(str(b.testedBuild)) ? str(b.testedBuild) : '',
			files: files.map((f) => ({ name: f.name, size: f.size, sha256: f.sha256.toUpperCase() })),
			images: images.map((i) => ({ name: i.name, size: i.size, sha256: i.sha256.toUpperCase() })) };
		const [row] = await sql`insert into submissions (author_id, mod_id, kind, meta, total_bytes) values (${a.author_id}, ${modId}, ${kind}, ${JSON.stringify(meta)}, ${total}) returning id`;
		return { id: Number(row.id), modId, chunkSize: CHUNK };
	},

	async chunk(b) {
		const a = await needAuthor(b.authorKey);
		const [s] = await sql`select id, status, meta from submissions where id = ${Number(b.id) || 0} and author_id = ${a.author_id}`;
		if (!s) bad('Upload not found.', 404);
		if (s.status !== 'uploading') bad('This upload is already finished.', 409);
		const all = [...s.meta.files, ...s.meta.images], f = all[b.file], n = b.n;
		if (!f || !Number.isInteger(n) || n < 0 || n >= Math.max(1, Math.ceil(f.size / CHUNK))) bad('Bad chunk.');
		if (typeof b.data !== 'string') bad('Bad chunk.');
		const len = Buffer.from(b.data, 'base64').length, want = Math.min(CHUNK, f.size - n * CHUNK);
		if (len !== want) bad('A piece of ' + f.name + ' arrived damaged. Try again.');
		await sql`insert into submission_chunks (submission_id, file_index, n, data) values (${s.id}, ${b.file}, ${n}, ${b.data})
			on conflict (submission_id, file_index, n) do update set data = excluded.data`;
		return { ok: true };
	},

	async finish(b) {
		const a = await needAuthor(b.authorKey);
		const [s] = await sql`select id, mod_id, kind, status, meta from submissions where id = ${Number(b.id) || 0} and author_id = ${a.author_id}`;
		if (!s) bad('Upload not found.', 404);
		if (s.status !== 'uploading') bad('This upload is already finished.', 409);
		const all = [...s.meta.files, ...s.meta.images];
		const have = await sql`select file_index, count(*)::int as n from submission_chunks where submission_id = ${s.id} group by file_index`;
		for (let i = 0; i < all.length; i++) {
			const got = have.find((h) => h.file_index === i);
			if (!got || got.n !== Math.max(1, Math.ceil(all[i].size / CHUNK))) bad(all[i].name + ' didn\'t finish uploading. Try again.');
		}
		// Quick look inside: real Unreal files and real JPEGs. The full check (hashes, contents) is done at review.
		const head = async (i, n) => { const [c] = await sql`select data from submission_chunks where submission_id = ${s.id} and file_index = ${i} and n = ${n}`; return Buffer.from(c.data, 'base64'); };
		for (let i = 0; i < all.length; i++) {
			const f = all[i], ext = f.name.split('.').pop().toLowerCase();
			if (ext === 'utoc' && (await head(i, 0)).subarray(0, 16).toString('latin1') !== '-==--==--==--==-') bad(f.name + ' isn\'t an Unreal .utoc file.');
			if (ext === 'jpg' && !(await head(i, 0)).subarray(0, 3).equals(Buffer.from([0xff, 0xd8, 0xff]))) bad(f.name + ' isn\'t a JPEG.');
			if (ext === 'pak') {
				const last = Math.max(1, Math.ceil(f.size / CHUNK)) - 1;
				let tail = await head(i, last);
				if (tail.length < 300 && last > 0) tail = Buffer.concat([await head(i, last - 1), tail]);
				if (tail.subarray(-300).indexOf(Buffer.from([0xe1, 0x12, 0x6f, 0x5a])) < 0) bad(f.name + ' isn\'t an Unreal .pak file.');
			}
		}
		await sql`update submissions set status = 'pending' where id = ${s.id}`;
		await notify(`New mod upload #${s.id}: **${s.meta.name}** ${s.meta.version} by ${a.display_name} (${s.kind}). Check it with: node tools/modmanager/review.mjs show ${s.id}`);
		return { ok: true, status: 'pending' };
	},

	async withdraw(b) {
		const a = await needAuthor(b.authorKey);
		const [s] = await sql`update submissions set status = 'withdrawn', decided_at = now() where id = ${Number(b.id) || 0} and author_id = ${a.author_id}
			and status in ('uploading', 'pending') returning id`;
		if (!s) bad('Only uploads waiting for their check can be withdrawn.', 409);
		await sql`delete from submission_chunks where submission_id = ${s.id}`;
		return { ok: true };
	},

	async seen(b) {
		const a = await needAuthor(b.authorKey);
		await sql`update submissions set seen_by_author = true where author_id = ${a.author_id} and not seen_by_author`;
		return { ok: true };
	},

	async read(b) {
		const a = await needAuthor(b.authorKey);
		const ids = Array.isArray(b.ids) ? b.ids.map(Number).filter(Number.isInteger) : [];
		if (ids.length) await sql`update feedback set read = true where id = any(${ids}) and mod_id in (select mod_id from mod_owners where author_id = ${a.author_id})`;
		else await sql`update feedback set read = true where mod_id in (select mod_id from mod_owners where author_id = ${a.author_id})`;
		return { ok: true };
	},

	async feedback(b) {
		await needUser(b.installId);
		const modId = str(b.modId), kind = str(b.kind), text = str(b.text);
		if (!(await modIds()).has(modId)) bad('That mod isn\'t in the mod list.', 404);
		if (!FEEDBACK_KINDS.includes(kind)) bad('Pick what kind of feedback it is.');
		if (text.length < 10 || text.length > 1500) bad('Write 10 to 1500 characters.');
		const [recent] = await sql`select count(*)::int as n from feedback where install_id = ${b.installId} and created_at > now() - interval '1 hour'`;
		if (recent.n >= 10) bad('That\'s a lot of feedback in an hour. Try again later.', 429);
		const [owner] = await sql`select 1 as x from mod_owners where mod_id = ${modId}`;
		await sql`insert into feedback (mod_id, install_id, kind, text) values (${modId}, ${b.installId}, ${kind}, ${text})`;
		return { ok: true, delivered: !!owner };
	},

	async report(b) {
		await needUser(b.installId);
		const modId = str(b.modId), reason = str(b.reason), text = str(b.text).slice(0, 1500);
		if (!(await modIds()).has(modId)) bad('That mod isn\'t in the mod list.', 404);
		if (!REPORT_REASONS.includes(reason)) bad('Pick a reason.');
		const [recent] = await sql`select count(*)::int as n from reports where install_id = ${b.installId} and created_at > now() - interval '1 day'`;
		if (recent.n >= 10) bad('That\'s a lot of reports today. Try again tomorrow.', 429);
		await sql`insert into reports (mod_id, install_id, reason, text) values (${modId}, ${b.installId}, ${reason}, ${text})`;
		await notify(`Mod report: **${modId}** (${reason}) ${text ? '- ' + text : ''}`);
		return { ok: true };
	}
};

export default async function handler(req, res) {
	if (req.method !== 'POST') return res.status(405).json({ error: 'POST only.' });
	if (!sql) return res.status(503).json({ error: 'Mod Dev isn\'t switched on yet.' });
	const fn = actions[String((req.query && req.query.a) || '')];
	if (!fn) return res.status(404).json({ error: 'Unknown action.' });
	try {
		await ensureSchema();
		res.status(200).json(await fn(body(req), req));
	} catch (err) {
		if (err instanceof Bad) return res.status(err.status).json({ error: err.message });
		console.error('dev', req.query.a, err.message);
		res.status(500).json({ error: 'Something went wrong on the modding hub. Try again later.' });
	}
}

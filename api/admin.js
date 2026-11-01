// /api/admin?a=<action>   Tonnie's side of Mod Dev, used by tools/modmanager/review.mjs (not by the launcher).
// Needs the header "Authorization: Bearer <ADMIN_TOKEN>" (Vercel environment variable). Without ADMIN_TOKEN it's off.
//
//   GET  list     ?status=pending        uploads with their details
//   GET  chunk    ?id&file&n             one piece of an uploaded file (base64)
//   GET  catalog                         the live community entries
//   GET  reports                         open reports
//   POST approve  { id, entry }          puts the mods.json-style entry live, deletes the upload's chunks
//   POST reject   { id, reason }         tells the author why (shown in their launcher), deletes the chunks
//   POST remove   { modId, reason }      takes a live community mod down
//   POST resolve  { id }                 closes a report
//   POST claim    { authorName, modIds } links mods.json mods to a mod dev account (feedback + updates go to it)

import { timingSafeEqual } from 'node:crypto';
import { sql, ensureSchema, body, notify } from './_shared.js';

function allowed(req) {
	const want = process.env.ADMIN_TOKEN || '';
	const got = String(req.headers.authorization || '').replace(/^Bearer\s+/i, '');
	return want.length >= 24 && got.length === want.length && timingSafeEqual(Buffer.from(got), Buffer.from(want));
}

const actions = {
	async list(q) {
		const status = q.status || 'pending';
		const rows = await sql`select s.id, s.mod_id, s.kind, s.status, s.meta, s.reason, s.total_bytes, s.created_at, s.decided_at, a.display_name as author, a.author_id
			from submissions s join authors a on a.author_id = s.author_id where s.status = ${status} order by s.id`;
		return { submissions: rows.map((r) => ({ ...r, id: Number(r.id), total_bytes: Number(r.total_bytes) })) };
	},
	async chunk(q) {
		const [c] = await sql`select data from submission_chunks where submission_id = ${Number(q.id)} and file_index = ${Number(q.file)} and n = ${Number(q.n)}`;
		if (!c) throw Object.assign(new Error('No such chunk.'), { status: 404 });
		return { data: c.data };
	},
	async catalog() {
		return { mods: (await sql`select mod_id, author_id, entry, added_at, updated_at from catalog order by mod_id`) };
	},
	async reports() {
		return { reports: await sql`select r.id, r.mod_id, r.reason, r.text, r.created_at, u.name as from_name from reports r join users u on u.install_id = r.install_id where not r.resolved order by r.id` };
	},
	async approve(b) {
		const [s] = await sql`select id, author_id, mod_id, status, meta from submissions where id = ${Number(b.id)}`;
		if (!s || s.status !== 'pending') throw Object.assign(new Error('Only pending uploads can be approved.'), { status: 409 });
		const e = b.entry;
		if (!e || e.id !== s.mod_id || !Array.isArray(e.files) || !e.zip) throw Object.assign(new Error('Bad entry.'), { status: 400 });
		await sql`insert into catalog (mod_id, author_id, entry) values (${s.mod_id}, ${s.author_id}, ${JSON.stringify(e)})
			on conflict (mod_id) do update set entry = excluded.entry, updated_at = now()`;
		await sql`insert into mod_owners (mod_id, author_id) values (${s.mod_id}, ${s.author_id}) on conflict (mod_id) do nothing`;
		await sql`update submissions set status = 'approved', decided_at = now(), seen_by_author = false where id = ${s.id}`;
		await sql`delete from submission_chunks where submission_id = ${s.id}`;
		return { ok: true };
	},
	async reject(b) {
		const reason = String(b.reason || '').trim();
		if (reason.length < 5) throw Object.assign(new Error('Give the author a reason.'), { status: 400 });
		const [s] = await sql`update submissions set status = 'rejected', reason = ${reason}, decided_at = now(), seen_by_author = false
			where id = ${Number(b.id)} and status = 'pending' returning id`;
		if (!s) throw Object.assign(new Error('Only pending uploads can be rejected.'), { status: 409 });
		await sql`delete from submission_chunks where submission_id = ${s.id}`;
		return { ok: true };
	},
	async remove(b) {
		const reason = String(b.reason || '').trim() || 'Removed by the modding hub.';
		const [c] = await sql`delete from catalog where mod_id = ${String(b.modId)} returning author_id, entry`;
		if (!c) throw Object.assign(new Error('Not a live community mod.'), { status: 404 });
		// shown to the author like a decision on an upload
		await sql`insert into submissions (author_id, mod_id, kind, status, meta, reason, decided_at, seen_by_author)
			values (${c.author_id}, ${String(b.modId)}, 'removal', 'removed', ${JSON.stringify({ name: c.entry.name, version: c.entry.version })}, ${reason}, now(), false)`;
		await notify(`Community mod removed: ${c.entry.name} (${reason})`);
		return { ok: true };
	},
	async resolve(b) {
		await sql`update reports set resolved = true where id = ${Number(b.id)}`;
		return { ok: true };
	},
	async claim(b) {
		const [a] = await sql`select author_id from authors where lower(display_name) = ${String(b.authorName || '').toLowerCase()}`;
		if (!a) throw Object.assign(new Error('No mod dev account with that name.'), { status: 404 });
		for (const id of Array.isArray(b.modIds) ? b.modIds : [])
			await sql`insert into mod_owners (mod_id, author_id) values (${String(id)}, ${a.author_id}) on conflict (mod_id) do update set author_id = excluded.author_id`;
		return { ok: true };
	},
	// The author name already in mods.json ("Tonnie") is reserved; this lets its owner take it.
	async rename(b) {
		const [a] = await sql`update authors set display_name = ${String(b.to)} where lower(display_name) = ${String(b.from || '').toLowerCase()} returning author_id`;
		if (!a) throw Object.assign(new Error('No mod dev account with that name.'), { status: 404 });
		return { ok: true };
	}
};

export default async function handler(req, res) {
	if (!sql) return res.status(503).json({ error: 'No database.' });
	if (!allowed(req)) return res.status(401).json({ error: 'Not allowed.' });
	const q = req.query || {}, fn = actions[String(q.a || '')];
	if (!fn) return res.status(404).json({ error: 'Unknown action.' });
	try {
		await ensureSchema();
		res.setHeader('Cache-Control', 'no-store');
		res.status(200).json(await fn(req.method === 'POST' ? body(req) : q));
	} catch (err) {
		if (err.status) return res.status(err.status).json({ error: err.message });
		console.error('admin', q.a, err.message);
		res.status(500).json({ error: err.message });
	}
}

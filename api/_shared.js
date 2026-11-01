// Shared by the launcher API (Vercel functions; files starting with _ aren't routes).
// Database: Neon Postgres via DATABASE_URL (Vercel Storage). Names and emails live only there, never in Git.

import { neon } from '@neondatabase/serverless';
import { createHash, randomBytes } from 'node:crypto';

export const REPO = 'TonnieGit/rescueopsmodding';
export const CHUNK = 2 * 1024 * 1024;              // Mod Dev upload piece size (base64 in JSON stays under Vercel's 4.5 MB body limit)
const MANIFEST = process.env.MODS_MANIFEST_URL || `https://github.com/${REPO}/releases/latest/download/mods.json`;

// globalThis.__romodSql lets the local test server swap in an in-memory Postgres (PGlite). Never set on Vercel.
export const sql = globalThis.__romodSql ?? (process.env.DATABASE_URL ? neon(process.env.DATABASE_URL) : null);

let schemaReady = null;
export function ensureSchema() {
	if (!sql) throw new Error('no database');
	schemaReady ??= (async () => {
		await sql`create table if not exists users (
			install_id text primary key, name text not null, email text not null, ip_hash text,
			created_at timestamptz not null default now(), updated_at timestamptz not null default now())`;
		await sql`create table if not exists ratings (
			install_id text not null references users(install_id) on delete cascade, mod_id text not null,
			stars smallint not null check (stars between 1 and 5), updated_at timestamptz not null default now(),
			primary key (install_id, mod_id))`;
		// Mod Dev (2.0). An author is a launcher install that chose a public author name. The author key stays on
		// that PC; the one-time recovery code moves it to another PC. Only hashes of both are kept here.
		await sql`create table if not exists authors (
			author_id text primary key, display_name text not null, key_hash text not null unique, recovery_hash text not null,
			install_id text references users(install_id) on delete set null, ip_hash text,
			created_at timestamptz not null default now())`;
		await sql`create unique index if not exists authors_name on authors (lower(display_name))`;
		// Who may update a mod and who gets its feedback. Covers community mods and claimed mods from mods.json.
		await sql`create table if not exists mod_owners (mod_id text primary key, author_id text not null references authors(author_id) on delete cascade)`;
		// An upload waiting for (or past) the safety check. Files arrive in chunks (base64 text, deleted once decided).
		await sql`create table if not exists submissions (
			id bigserial primary key, author_id text not null references authors(author_id) on delete cascade,
			mod_id text not null, kind text not null, status text not null default 'uploading',
			meta jsonb not null, reason text, total_bytes bigint not null default 0,
			created_at timestamptz not null default now(), decided_at timestamptz, seen_by_author boolean not null default true)`;
		await sql`create table if not exists submission_chunks (
			submission_id bigint not null references submissions(id) on delete cascade, file_index int not null, n int not null,
			data text not null, primary key (submission_id, file_index, n))`;
		// Live community mods: the mods.json-style entry the launcher merges into its mod list.
		await sql`create table if not exists catalog (
			mod_id text primary key, author_id text not null references authors(author_id) on delete cascade,
			entry jsonb not null, added_at timestamptz not null default now(), updated_at timestamptz not null default now())`;
		await sql`create table if not exists feedback (
			id bigserial primary key, mod_id text not null, install_id text not null references users(install_id) on delete cascade,
			kind text not null, text text not null, read boolean not null default false, created_at timestamptz not null default now())`;
		await sql`create table if not exists reports (
			id bigserial primary key, mod_id text not null, install_id text not null references users(install_id) on delete cascade,
			reason text not null, text text not null default '', resolved boolean not null default false, created_at timestamptz not null default now())`;
	})().catch((e) => { schemaReady = null; throw e; });
	return schemaReady;
}

// The live mods.json, cached for 10 minutes.
let modsCache = { at: 0, mods: null };
export async function manifestMods() {
	if (modsCache.mods && Date.now() - modsCache.at < 600_000) return modsCache.mods;
	const r = await fetch(MANIFEST, { headers: { 'User-Agent': 'rescueopsmodding-api' } });
	if (!r.ok) throw new Error('mods.json ' + r.status);
	const m = await r.json();
	modsCache = { at: Date.now(), mods: m.mods };
	return modsCache.mods;
}

// Every mod id players can see: mods.json plus live community mods.
export async function modIds() {
	const ids = new Set((await manifestMods()).map((x) => x.id));
	if (sql) {
		await ensureSchema();
		for (const r of await sql`select mod_id from catalog`) ids.add(r.mod_id);
	}
	return ids;
}

export function ipHash(req) {
	const ip = String(req.headers['x-forwarded-for'] || '').split(',')[0].trim() || 'unknown';
	return createHash('sha256').update((process.env.IP_SALT || 'rescueops') + ip).digest('hex').slice(0, 32);
}

export const validInstall = (id) => typeof id === 'string' && /^[a-f0-9]{32}$/.test(id);
export const sha256 = (s) => createHash('sha256').update(s).digest('hex');
export const randomHex = (bytes) => randomBytes(bytes).toString('hex');

export function body(req) {
	if (req.body && typeof req.body === 'object') return req.body;
	try { return JSON.parse(req.body || '{}'); } catch { return {}; }
}

export async function ratingOf(modId) {
	const [row] = await sql`select coalesce(avg(stars), 0)::float as avg, count(*)::int as n from ratings where mod_id = ${modId}`;
	return { avg: Math.round(row.avg * 10) / 10, n: row.n };
}

// The author behind an author key, or null.
export async function authorOf(key) {
	if (typeof key !== 'string' || !/^[a-f0-9]{64}$/.test(key)) return null;
	const [a] = await sql`select author_id, display_name from authors where key_hash = ${sha256(key)}`;
	return a || null;
}

// Optional: posts a line to a Discord channel (DISCORD_WEBHOOK_URL) so Tonnie hears about uploads and reports.
export async function notify(text) {
	const url = process.env.DISCORD_WEBHOOK_URL;
	if (!url) return;
	try { await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ content: text.slice(0, 1900) }) }); }
	catch (e) { console.error('notify', e.message); }
}

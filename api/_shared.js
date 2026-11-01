// Shared by the launcher API (Vercel functions; files starting with _ aren't routes).
// Database: Neon Postgres via DATABASE_URL (Vercel Storage). Names and emails live only there, never in Git.

import { neon } from '@neondatabase/serverless';
import { createHash } from 'node:crypto';

export const REPO = 'TonnieGit/rescueopsmodding';
const MANIFEST = `https://github.com/${REPO}/releases/latest/download/mods.json`;

export const sql = process.env.DATABASE_URL ? neon(process.env.DATABASE_URL) : null;

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
	})().catch((e) => { schemaReady = null; throw e; });
	return schemaReady;
}

// Mod ids from the live mods.json, cached for 10 minutes.
let modsCache = { at: 0, ids: null };
export async function modIds() {
	if (modsCache.ids && Date.now() - modsCache.at < 600_000) return modsCache.ids;
	const r = await fetch(MANIFEST, { headers: { 'User-Agent': 'rescueopsmodding-api' } });
	if (!r.ok) throw new Error('mods.json ' + r.status);
	const m = await r.json();
	modsCache = { at: Date.now(), ids: new Set(m.mods.map((x) => x.id)) };
	return modsCache.ids;
}

export function ipHash(req) {
	const ip = String(req.headers['x-forwarded-for'] || '').split(',')[0].trim() || 'unknown';
	return createHash('sha256').update((process.env.IP_SALT || 'rescueops') + ip).digest('hex').slice(0, 32);
}

export const validInstall = (id) => typeof id === 'string' && /^[a-f0-9]{32}$/.test(id);

export function body(req) {
	if (req.body && typeof req.body === 'object') return req.body;
	try { return JSON.parse(req.body || '{}'); } catch { return {}; }
}

export async function ratingOf(modId) {
	const [row] = await sql`select coalesce(avg(stars), 0)::float as avg, count(*)::int as n from ratings where mod_id = ${modId}`;
	return { avg: Math.round(row.avg * 10) / 10, n: row.n };
}

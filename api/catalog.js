// GET /api/catalog -> { updated, mods: [ mods.json-style entries ] }
// Live community mods (Mod Dev, launcher 2.0+). The launcher adds these to the mods from mods.json; an entry with the
// same id as a mods.json mod replaces it (only possible for a mod its owner updated through Mod Dev).
// Public, cached for a minute at Vercel's edge.

import { sql, ensureSchema } from './_shared.js';

export default async function handler(req, res) {
	if (!sql) return res.status(200).json({ updated: new Date().toISOString(), mods: [] });
	try {
		await ensureSchema();
		const rows = await sql`select entry, added_at, updated_at from catalog order by added_at`;
		res.setHeader('Cache-Control', 'public, s-maxage=60, stale-while-revalidate=600');
		res.status(200).json({ updated: new Date().toISOString(), mods: rows.map((r) => ({ ...r.entry, added: r.added_at, updatedAt: r.updated_at })) });
	} catch (err) {
		console.error('catalog', err.message);
		res.status(503).json({ error: 'The community mod list is unavailable right now.' });
	}
}

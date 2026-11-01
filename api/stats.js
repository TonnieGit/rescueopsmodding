// GET /api/stats -> { updated, mods: { <id>: { downloads, rating, ratings } } }
// Downloads = GitHub's own counter for <id>.zip, added up over every release (launcher and hub downloads both count).
// Public and cached for 5 minutes at Vercel's edge.

import { REPO, sql, ensureSchema } from './_shared.js';

async function downloads() {
	const headers = { 'User-Agent': 'rescueopsmodding-api', Accept: 'application/vnd.github+json' };
	if (process.env.GITHUB_TOKEN) headers.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
	const counts = {};
	for (let page = 1; page <= 10; page++) {
		const r = await fetch(`https://api.github.com/repos/${REPO}/releases?per_page=100&page=${page}`, { headers });
		if (!r.ok) throw new Error('GitHub ' + r.status);
		const releases = await r.json();
		for (const rel of releases)
			for (const a of rel.assets || [])
				if (a.name.toLowerCase().endsWith('.zip')) {
					const id = a.name.slice(0, -4);
					counts[id] = (counts[id] || 0) + a.download_count;
				}
		if (releases.length < 100) break;
	}
	return counts;
}

export default async function handler(req, res) {
	const mods = {};
	const add = (id) => (mods[id] ??= { downloads: 0, rating: 0, ratings: 0 });
	let ok = false;
	try {
		for (const [id, n] of Object.entries(await downloads())) add(id).downloads = n;
		ok = true;
	} catch (e) { console.error('downloads', e.message); }
	if (sql) {
		try {
			await ensureSchema();
			const rows = await sql`select mod_id, avg(stars)::float as avg, count(*)::int as n from ratings group by mod_id`;
			for (const r of rows) Object.assign(add(r.mod_id), { rating: Math.round(r.avg * 10) / 10, ratings: r.n });
			ok = true;
		} catch (e) { console.error('ratings', e.message); }
	}
	if (!ok) return res.status(503).json({ error: 'Stats are unavailable right now.' });
	res.setHeader('Cache-Control', 'public, s-maxage=300, stale-while-revalidate=3600');
	res.status(200).json({ updated: new Date().toISOString(), mods });
}

// POST /api/rate { installId, modId, stars }   stars 1-5 sets or changes the rating, 0 removes it.
// One rating per mod per launcher install; the install must be registered first.
// Returns the mod's new { rating, ratings } so the launcher can show it straight away.

import { sql, ensureSchema, modIds, validInstall, body, ratingOf } from './_shared.js';

export default async function handler(req, res) {
	if (req.method !== 'POST') return res.status(405).json({ error: 'POST only.' });
	if (!sql) return res.status(503).json({ error: 'Ratings aren\'t switched on yet.' });
	const { installId, modId, stars } = body(req);
	if (!validInstall(installId)) return res.status(400).json({ error: 'Bad install id.' });
	if (!Number.isInteger(stars) || stars < 0 || stars > 5) return res.status(400).json({ error: 'Stars must be 0 to 5.' });
	try {
		if (typeof modId !== 'string' || !(await modIds()).has(modId)) return res.status(404).json({ error: 'That mod isn\'t in the mod list.' });
		await ensureSchema();
		const [user] = await sql`select 1 as ok from users where install_id = ${installId}`;
		if (!user) return res.status(403).json({ error: 'register' });
		const [recent] = await sql`select count(*)::int as n from ratings where install_id = ${installId} and updated_at > now() - interval '10 minutes'`;
		if (recent.n >= 30) return res.status(429).json({ error: 'Too many ratings in a short time. Try again in a few minutes.' });
		if (stars === 0) await sql`delete from ratings where install_id = ${installId} and mod_id = ${modId}`;
		else await sql`insert into ratings (install_id, mod_id, stars) values (${installId}, ${modId}, ${stars})
			on conflict (install_id, mod_id) do update set stars = excluded.stars, updated_at = now()`;
		const r = await ratingOf(modId);
		res.status(200).json({ ok: true, modId, rating: r.avg, ratings: r.n });
	} catch (err) {
		console.error('rate', err.message);
		res.status(500).json({ error: 'Couldn\'t save your rating. Try again later.' });
	}
}

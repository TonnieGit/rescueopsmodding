// POST /api/register { installId, name, email, consent: true }
// Stores who's behind a launcher install, once per PC. Needed before rating (and, later, Mod Dev).
// Nothing here is public. See /privacy/.

import { sql, ensureSchema, ipHash, validInstall, body } from './_shared.js';

export default async function handler(req, res) {
	if (req.method !== 'POST') return res.status(405).json({ error: 'POST only.' });
	if (!sql) return res.status(503).json({ error: 'Ratings aren\'t switched on yet.' });
	const { installId, name, email, consent } = body(req);
	const n = typeof name === 'string' ? name.trim() : '', e = typeof email === 'string' ? email.trim().toLowerCase() : '';
	if (!validInstall(installId)) return res.status(400).json({ error: 'Bad install id.' });
	if (n.length < 2 || n.length > 40) return res.status(400).json({ error: 'Name must be 2 to 40 characters.' });
	if (e.length > 120 || !/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(e)) return res.status(400).json({ error: 'That email address doesn\'t look right.' });
	if (consent !== true) return res.status(400).json({ error: 'Please accept the privacy note.' });
	try {
		await ensureSchema();
		const ip = ipHash(req);
		const [recent] = await sql`select count(*)::int as n from users where ip_hash = ${ip} and created_at > now() - interval '1 day' and install_id <> ${installId}`;
		if (recent.n >= 10) return res.status(429).json({ error: 'Too many sign-ups from this connection today. Try again tomorrow.' });
		await sql`insert into users (install_id, name, email, ip_hash) values (${installId}, ${n}, ${e}, ${ip})
			on conflict (install_id) do update set name = excluded.name, email = excluded.email, updated_at = now()`;
		res.status(200).json({ ok: true });
	} catch (err) {
		console.error('register', err.message);
		res.status(500).json({ error: 'Couldn\'t save your details. Try again later.' });
	}
}

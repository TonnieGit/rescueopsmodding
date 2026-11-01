// POST /api/delete-details { email }
// Deletes every launcher install registered with that email, and their ratings, feedback and reports (they go with the
// user row). A mod dev account loses its link to them; its public author name and live mods stay (see /privacy/).
// Always gives the same answer, so the form can't be used to find out whether someone is registered.
// Keeps a note of the request with the email hashed, never the email itself.

import { createHash } from 'node:crypto';
import { sql, ensureSchema, ipHash, body } from './_shared.js';

const THANKS = 'Thank you. If your details are saved in our system, they will be deleted as soon as possible, generally within 24 hours.';

export default async function handler(req, res) {
	if (req.method !== 'POST') return res.status(405).json({ error: 'POST only.' });
	const raw = body(req).email;
	const email = typeof raw === 'string' ? raw.trim().toLowerCase() : '';
	if (email.length > 120 || !/^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/.test(email)) return res.status(400).json({ error: 'That email address doesn\'t look right.' });
	if (!sql) return res.status(200).json({ ok: true, message: THANKS });
	try {
		await ensureSchema();
		await sql`create table if not exists deletion_requests (
			id bigserial primary key, email_hash text not null, ip_hash text, removed int not null default 0,
			requested_at timestamptz not null default now())`;
		const ip = ipHash(req);
		const [recent] = await sql`select count(*)::int as n from deletion_requests where ip_hash = ${ip} and requested_at > now() - interval '1 hour'`;
		if (recent.n >= 10) return res.status(429).json({ error: 'Too many requests from this connection. Try again in an hour.' });
		const removed = await sql`delete from users where email = ${email} returning install_id`;
		const emailHash = createHash('sha256').update(email).digest('hex');
		await sql`insert into deletion_requests (email_hash, ip_hash, removed) values (${emailHash}, ${ip}, ${removed.length})`;
		res.status(200).json({ ok: true, message: THANKS });
	} catch (err) {
		console.error('delete-details', err.message);
		res.status(500).json({ error: 'Something went wrong. Please try again later, or message Tonnie on Patreon.' });
	}
}

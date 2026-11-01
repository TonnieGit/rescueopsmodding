import type { APIRoute } from 'astro';
import { getNews } from '../news';

// Read by the Rescue Ops Mod Launcher. Built on every deploy, so a pushed news page goes live with the site.
export const GET: APIRoute = async ({ site }) => {
	const base = (site?.toString() ?? 'https://rescueopsmodding.vercel.app/').replace(/\/$/, '');
	const items = (await getNews()).map((n) => ({
		id: n.id,
		title: n.title,
		summary: n.summary,
		date: n.date.toISOString().slice(0, 10),
		kind: n.kind,
		url: base + n.url,
	}));
	return new Response(JSON.stringify({ schema: 1, source: 'rescueopsmodding', items }, null, 2), {
		headers: { 'Content-Type': 'application/json; charset=utf-8' },
	});
};

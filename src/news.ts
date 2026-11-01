import { getCollection } from 'astro:content';

export interface NewsItem { id: string; title: string; summary: string; date: Date; kind: 'hub' | 'mods' | 'event'; url: string }

/** Every page marked `news: true` with a date, newest first. */
export async function getNews(): Promise<NewsItem[]> {
	const pages = await getCollection('docs', (e) => e.data.news === true && e.data.date !== undefined);
	return pages
		.map((e) => ({
			id: e.id,
			title: e.data.title,
			summary: e.data.summary ?? e.data.description ?? '',
			date: e.data.date as Date,
			kind: e.data.kind ?? 'hub',
			url: '/' + e.id.replace(/\/index$/, '') + '/',
		}))
		.sort((a, b) => b.date.getTime() - a.date.getTime());
}

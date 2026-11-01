import { defineCollection } from 'astro:content';
import { docsLoader } from '@astrojs/starlight/loaders';
import { docsSchema } from '@astrojs/starlight/schema';
import { z } from 'astro/zod';

export const collections = {
	docs: defineCollection({
		loader: docsLoader(),
		schema: docsSchema({
			// News: any page with `news: true` and a `date` appears on /news/ and in /news.json (read by the Mod Launcher).
			extend: z.object({
				news: z.boolean().optional(),
				date: z.coerce.date().optional(),
				summary: z.string().optional(),
				kind: z.enum(['hub', 'mods', 'event']).optional(),
			}),
		}),
	}),
};

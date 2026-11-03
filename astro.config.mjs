// @ts-check
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

// https://astro.build/config
export default defineConfig({
	site: 'https://rescueopsmodding.vercel.app',
	redirects: {
		'/start/introduction': '/',
	},
	integrations: [
		starlight({
			title: 'Rescue Ops Modding',
			description:
				'Tutorials, mods and reference for modding Rescue Ops: Wildfire (Unreal Engine 5.8).',
			logo: { src: './src/assets/logo.svg' },
			favicon: '/favicon.svg',
			head: [
				// Vercel Web Analytics (enable it in the Vercel project's Analytics tab)
				{
					tag: 'script',
					content:
						'window.va = window.va || function () { (window.vaq = window.vaq || []).push(arguments); };',
				},
				{ tag: 'script', attrs: { defer: true, src: '/_vercel/insights/script.js' } },
			],
			customCss: [
				'@fontsource/bebas-neue/400.css',
				'@fontsource-variable/rubik',
				'./src/styles/custom.css',
			],
			social: [
				{ icon: 'github', label: 'GitHub', href: 'https://github.com/TonnieGit/rescueopsmodding' },
				{ icon: 'patreon', label: 'Patreon', href: 'https://patreon.com/Tonnie001' },
			],
			editLink: {
				baseUrl: 'https://github.com/TonnieGit/rescueopsmodding/edit/main/',
			},
			lastUpdated: true,
			sidebar: [
				{ label: 'News', link: '/news/' },
				{
					label: 'Getting started',
					items: [
						{ label: 'Introduction', link: '/' },
						{ label: 'Tools you need', slug: 'start/tools' },
						{ label: 'Set up FModel', slug: 'start/fmodel' },
						{ label: 'Get the .usmap', slug: 'start/usmap' },
						{ label: 'Set up the mod project', slug: 'start/mod-project' },
						{ label: 'Package and install your mod', slug: 'start/packaging' },
					],
				},
				{
					label: 'Tutorials',
					items: [
						{ label: 'Retexturing', slug: 'tutorials/retexturing' },
						{ label: 'Replacing sounds', slug: 'tutorials/sounds' },
						{ label: 'Replacing 3D models', slug: 'tutorials/models' },
						{ label: 'Emergency lights', slug: 'tutorials/emergency-lights' },
						{ label: 'Red and blue beacons', slug: 'tutorials/red-blue-beacons' },
					],
				},
				{
					label: 'Help',
					items: [
						{ label: 'Troubleshooting', slug: 'start/troubleshooting' },
						{ label: 'Privacy', slug: 'privacy' },
					],
				},
				{
					label: 'Mods',
					items: [
						{ label: 'All mods', slug: 'mods' },
						{ label: 'Installing mods', slug: 'mods/installing' },
						{ label: 'Community mods', slug: 'mods/community' },
						{ label: 'QLD RFS: Uniforms', slug: 'mods/qld-rfs-uniforms' },
						{ label: 'QLD RFS: BR9 Helmet Yellow', slug: 'mods/br9-bushfire-helmet-yellow' },
						{ label: 'QLD RFS: Fire Truck', slug: 'mods/qld-rfs-truck-reskin' },
						{ label: 'Beacons: Blue', slug: 'mods/brighter-beacons' },
						{ label: 'Beacons: Red', slug: 'mods/red-beacons' },
						{ label: 'Beacons: Red & Blue', slug: 'mods/red-blue-beacons' },
						{ label: 'Lights: HQ Real Light', slug: 'mods/lights-hq' },
						{ label: 'ELS: Sirens', slug: 'mods/els' },
						{ label: 'ELS: Light Bar AUS', slug: 'mods/els-light-bar' },
						{ label: 'ELS: Light Bar France', slug: 'mods/els-france' },
						{ label: 'Free Camera', slug: 'mods/free-camera' },
						{ label: 'Camera: First Person', slug: 'mods/first-person' },
						{ label: 'ROMF and Beacons: Light Engine', slug: 'mods/tonnie-core' },
						{ label: 'Hazard 1000 Tones', slug: 'mods/hazard-1000-tones' },
					],
				},
				{
					label: 'Mod Dev',
					items: [
						{ label: 'Share your mods', slug: 'mod-dev' },
						{ label: 'Upload rules', slug: 'mod-dev/rules' },
					],
				},
				{
					label: 'Reference',
					items: [
						{ label: 'Game facts', slug: 'reference/game' },
						{ label: 'Asset paths', slug: 'reference/asset-paths' },
						{ label: 'Texture formats', slug: 'reference/texture-formats' },
						{ label: 'CCF truck', slug: 'reference/ccf-truck' },
						{ label: 'Chunk ID registry', slug: 'reference/chunk-ids' },
					],
				},
			],
		}),
	],
});

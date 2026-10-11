# Rescue Ops Modding

Tutorials, mods and reference for modding **Rescue Ops: Wildfire** (Unreal Engine 5.8). Built with [Astro Starlight](https://starlight.astro.build) and deployed on Vercel.

## Run locally

```sh
npm install
npm run dev      # http://localhost:4321
npm run build    # static site in ./dist
```

## Adding content

- Pages live in `src/content/docs/` (`.md`, or `.mdx` when using components such as `<Steps>`).
- Add new pages to the `sidebar` in `astro.config.mjs`.
- Screenshots go in `src/assets/screenshots/`. Astro optimises them at build time.

## Mod downloads

Download buttons link to `https://github.com/TonnieGit/rescueopsmodding/releases/latest/download/<zip>`. Publish a GitHub Release with these assets attached:

- `QLD_RFS_Uniforms.zip`
- `QLD_RFS_Truck_Reskin.zip`
- `Hazard_1000_Tones.zip`

## Credits

Mods and guides by **Tonnie**. [Support on Patreon](https://patreon.com/Tonnie001).

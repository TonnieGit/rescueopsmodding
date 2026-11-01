## Project

Astro Starlight docs site for Rescue Ops: Wildfire modding, deployed to Vercel from GitHub (`TonnieGit/rescueopsmodding`). Source notes live in `Documents\RescueOps_Modding` (`spec.md`, `CHANGELOG.md`, phase plans).

- Credit everything to **Tonnie** (Patreon https://patreon.com/Tonnie001). Never use the author's real name or personal file paths.
- Australian English. Only document methods confirmed in-game; mark anything else as planned/unverified.
- New pages must be added to the `sidebar` in `astro.config.mjs` (news items are the exception: they're reached from /news/).
- **News:** every release or notable hub change gets a news page in `src/content/docs/news/<slug>.md` with frontmatter `news: true`, `date: YYYY-MM-DD`, `kind: hub | mods | event` and a one-line `summary`. Any other page can join the feed with the same fields. `/news/` lists them and `/news.json` (built by `src/pages/news.json.ts`) is read by the Rescue Ops Mod Launcher. Don't make news for typo or layout fixes.

## Development

When starting the dev server, use background mode:

```
astro dev --background
```

Manage the background server with `astro dev stop`, `astro dev status`, and `astro dev logs`.

Full documentation: https://docs.astro.build · https://starlight.astro.build

# Rescue Ops Mod Launcher

Downloads, updates and loads the Rescue Ops: Wildfire mods from this site, then starts the game. Download it from the
[latest release](https://github.com/TonnieGit/rescueopsmodding/releases/latest/download/RescueOpsModLauncher.exe).
How to use it: [Installing mods](https://rescueopsmodding.vercel.app/mods/installing/).

It replaces the Mod Manager (`ModManager.cs`, built by `build.bat`), which is still released so 1.1 users get the link to the launcher.

## How it works

1. Reads `mods.json` from the latest release. It lists every mod's files and their SHA-256 hashes,
   plus categories, thumbnails (`public/launcher/thumbs/` on the hub), changelogs and the launcher's own version.
2. Finds the game in every Steam library (registry `SteamPath` + `steamapps\libraryfolders.vdf`) and reads its
   `appmanifest_*.acf` for the app id and build.
3. Loaded mods are in `Wildfire\Content\Paks`; unticked ones move to `Wildfire\ModLibrary` (the game loads Paks and
   its subfolders, so the library sits outside it).
4. Downloads mods' zips from the same release, checks every file's hash, then puts them in place.
5. News comes from the Steam news API (the full game's app) and the hub's `/news.json`.
6. Self-update: when `launcher.version` in `mods.json` is newer, it downloads the exe, checks `launcher.sha256`,
   renames itself to `.old` and moves the new one in.

It only moves or deletes files named in `mods.json` and paks the player added, never the game's own files.

7. Presets (1.3) are saved in `state.json`. Share codes are `ROMOD-` + base64url of `{n: name, m: [mod ids]}`; local paks are left out.
8. Ratings and download counts (1.3) come from the hub API in `/api` (Vercel functions):
   - `GET /api/stats`: per mod `downloads` (GitHub's counter for `<id>.zip`, added up over every release), `rating` and `ratings`. Cached 5 minutes.
   - `POST /api/register` `{installId, name, email, consent}`: once per PC, before the first rating.
   - `POST /api/rate` `{installId, modId, stars}`: 1 to 5 sets the rating, 0 removes it.
   Names, emails and ratings live in a Neon Postgres database (`DATABASE_URL`, added through Vercel Storage), never in Git.
   The tables are created on first use. Optional env: `GITHUB_TOKEN` (higher GitHub API limit), `IP_SALT`. See `/privacy/`.
9. `testedBuild` per mod (Steam build ids, comma-separated, set by `make-manifest.ps1`'s `$testedOn`): loaded mods not checked on the
   player's build get a warning. Bump `$testedOn` after re-testing the mods on a new game patch.

10. Mod Dev (2.0): community mods, uploads, feedback and reports. See below.

## Mod Dev (2.0)

Anyone can upload a mod from the launcher (**MOD DEV**). Guide for players: `/mod-dev/`, rules: `/mod-dev/rules/`.

- **Accounts.** A registered launcher install picks a public author name (`POST /api/dev?a=author`). The launcher makes a random
  author key (kept in `state.json`); the hub stores only its SHA-256 and that of a one-time recovery code
  (`ABCD-EFGH-JKLM-NPQR`), which moves the account to another PC (`recover`, the old key stops working) and can be replaced (`newcode`).
  Author names already in `mods.json` (Tonnie) are reserved.
- **Uploads.** `submit` (details + file list with sizes and SHA-256) -> `chunk` (2 MB pieces, base64 JSON, under Vercel's 4.5 MB body
  limit) -> `finish` (checks every piece arrived, Unreal .utoc header and .pak footer, JPEG headers). Limits: 100 MB per mod,
  250 MB waiting in total, 3 waiting uploads per author, one per mod. File names must not be used by any other mod.
  Waiting uploads live in the `submission_chunks` table and are deleted once decided.
- **The safety check** is Tonnie's: `RescueOps_Modding	ools\modmanagereview.mjs` (`list`, `show`, `fetch`, `approve`, `reject`,
  `reports`, `resolve`, `remove`, `claim`, `rename`), through `/api/admin` with `Authorization: Bearer <ADMIN_TOKEN>`.
  `approve` zips the files (`Wildfire/Content/Paks/...`), uploads `<id>-v<version>.zip` and the pictures to the GitHub release
  **community** (created on first use, never "latest"), and writes the mods.json-style entry to the `catalog` table.
  Updates list the previous version's file names in `replaces`, so the launcher removes them.
- **Community mods** come from `GET /api/catalog` (cached a minute) and are merged into `mods.json`'s list (same id = the catalog
  entry wins). The launcher keeps a copy in `%LOCALAPPDATA%\RescueOpsModLauncher\catalog.json` for when the hub is unreachable.
  `/api/stats` counts `<id>-v<version>.zip` downloads into `<id>`. Ratings work for community mods too.
- **Feedback** (`feedback`) goes to the mod's owner (`mod_owners`) and shows in their Inbox; **reports** (`report`) go to Tonnie
  (`review.mjs reports`). Feedback for mods.json mods reaches an author once they're claimed: `review.mjs claim <name> <modId...>`.
- Environment (Vercel): `DATABASE_URL` (already set), **`ADMIN_TOKEN`** (24+ random characters, needed for review.mjs),
  optional `DISCORD_WEBHOOK_URL` (a message per upload and report).

Testing without the live site: `--api http://127.0.0.1:8787/api/` points the launcher at a local copy of the API.

## Code

- `Launcher.cs`: entry point; loads the embedded WebView2 DLLs and extracts the UI.
- `LauncherForm.cs`: the window and the commands from the page.
- `ModDev.cs`: community mods, Mod Dev accounts and uploads, feedback and reports, drag and drop.
- `Core.cs`: game discovery, mod list, library (load, unload, install files, local paks).
- `Services.cs`: settings (`%LOCALAPPDATA%\RescueOpsModLauncher\state.json`), downloads, news, self-update.
- `ui/`: the page (`index.html`, fonts, images), embedded in the exe.
- `lib/webview2/`: Microsoft WebView2 SDK (BSD licence).

## Build

Run `build_launcher.bat`. It uses the C# compiler that comes with Windows (.NET Framework 4.8), so there's
nothing to install. It needs the WebView2 Runtime, which comes with Windows 10 and 11.

Test switches: `--manifest <url or path to mods.json>`, `--game <game folder>`, `--ui <folder>` (use the UI files
from disk), `--api <url>` (a local copy of the hub API) and `--devtools` (right-click menu, DevTools and remote debugging on
port 9222; also lets the page pass file paths instead of opening file dialogs, for scripted tests).

## Releasing

Copy `RescueOpsModLauncher.exe` into `RescueOps_Modding\Release\`, run `tools\modmanager\make-manifest.ps1`, then make
a release containing all the mod zips, `Wildfire.usmap`, `mods.json`, `RescueOpsModManager.exe` and
`RescueOpsModLauncher.exe`, because the site and both apps link to `releases/latest/download/<file>`.

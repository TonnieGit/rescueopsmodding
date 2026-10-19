# Rescue Ops Mod Manager

Installs, updates and uninstalls the Rescue Ops: Wildfire mods from this site. Download it from the
[latest release](https://github.com/TonnieGit/rescueopsmodding/releases/latest/download/RescueOpsModManager.exe).
How to use it: [Installing mods](https://rescueopsmodding.vercel.app/mods/installing/).

## How it works

1. Reads `mods.json` from the latest release. It lists every mod's files and their SHA-256 hashes.
2. Finds the game in every Steam library (registry `SteamPath` + `steamapps\libraryfolders.vdf`).
3. Hashes the files in `Wildfire\Content\Paks` to show what's installed and what's out of date.
4. Downloads the ticked mods' zips from the same release, checks every file's hash, then copies them in.

It only adds or deletes files named in `mods.json`, never the game's own files.

## Build

Run `build.bat`. It uses the C# compiler that comes with Windows (.NET Framework 4.8), so there's
nothing to install.

Test switches: `--manifest <url or path to mods.json>` and `--game <game folder or Paks folder>`.

## Releasing

Every release must contain all the mod zips, `mods.json` and `RescueOpsModManager.exe`, because the
site and the manager link to `releases/latest/download/<file>`.

---
title: Set up the mod project
description: Create the blank Unreal 5.8 project that every mod is built from, and set its packaging options.
---

:::note[In this guide you will]
- Create the Unreal project you'll build every mod in
- Set the packaging options so your mods come out as separate files
- Copy the game's rendering settings, so new materials work in-game
:::

You only do this once. Every mod (textures, sounds, models) is built from this one project.

## Step 1: Create the project

1. Open the **Epic Games Launcher** → **Unreal Engine** → **Library** and launch **5.8**.
2. In the **Unreal Project Browser**, choose **Games** → **Blank**.
3. On the right, set **Blueprint** (not C++) and untick **Starter Content**.
4. At the bottom, choose a location outside the game folder and set the **Project Name** to exactly:

   ```text
   Wildfire
   ```

5. Click **Create**.

:::caution[Gotcha: the project must be called Wildfire]
The game's content lives under the project name `Wildfire`. A project with any other name packs your files under a different path, and the game silently ignores them. If you already made one with another name, create a new project. Renaming isn't enough.
:::

## Step 2: Set the packaging options

1. Open **Edit → Project Settings**.
2. In the left list, under **Project**, click **Packaging**.
3. Set these. The search box at the top of Project Settings finds each one quickly:

   | Setting | Value |
   |---|---|
   | **Build Configuration** | Shipping |
   | **Use Pak File** | ✔ |
   | **Use Io Store** | ✔ |
   | **Generate Chunks** | ✔ |
   | **Additional Asset Directories to Cook** | click **+**, then pick or type `/Game/Wildfire` |

   *Additional Asset Directories to Cook* is in the **advanced** part of the Packaging section. Click the small arrow at the bottom of the section to show it.

4. Close Project Settings. It saves automatically.

:::danger[Gotcha: your files are in the project but missing from the mod]
**Cause:** in Unreal 5.8, PrimaryAssetLabels no longer force files into the build. A texture that isn't used by any map in your blank project is **silently left out**, even with the label set up correctly.

**Fix:** the **Additional Asset Directories to Cook = `/Game/Wildfire`** setting above.

**How we found it:** we tracked it down to the engine's `Engine\Config\BaseGame.ini`, which makes labels editor-only by default in 5.8.
:::

## Step 3: Create the base folder

In the **Content Browser**, right-click `Content` → **New Folder** → name it `Wildfire`. Every mod's files go inside it, in the same folders as the game uses.

## Step 4: Match the game's rendering settings

Your project must compile materials for the **same rendering settings as the game**. Texture and sound mods work without this step, but any mod with its **own material** (e.g. a [new 3D model](/tutorials/models/) with its own textures) renders black in-game without it. Do it once, now, so you never hit the problem.

1. **Close Unreal completely.** It may overwrite the file below when it exits.

2. Extract the game's settings file. In PowerShell (change the game path if your Steam library is elsewhere):

   ```powershell
   & "C:\Program Files\Epic Games\UE_5.8\Engine\Binaries\Win64\UnrealPak.exe" "C:\Program Files (x86)\Steam\steamapps\common\Rescue Ops Demo\Wildfire\Content\Paks\Wildfire-Windows.pak" -Extract "$env:USERPROFILE\Documents\WildfireGameConfig" -Filter="*Wildfire/Config/*"
   ```

   This only reads the game's pak. It writes the game's config files to `Documents\WildfireGameConfig\Wildfire\Config\`.

3. Make a backup copy of your project's `Config\DefaultEngine.ini` (e.g. `DefaultEngine.ini.bak`).

4. Open the game's `DefaultEngine.ini` (from step 2) in a text editor and copy its whole **`[/Script/Engine.RendererSettings]`** section: from that line down to the line before the next `[...]` heading.

5. Open your project's `Config\DefaultEngine.ini`, find its `[/Script/Engine.RendererSettings]` section and **replace the whole section** with the one you copied. Leave everything else in the file alone. Save.

6. Open the project. Unreal recompiles its shaders, and the next **package takes a long time** (much longer than usual). Later packages are quick again.

:::danger[Gotcha: my new material is black with coloured speckles in-game]
**Cause:** a new Unreal 5.8 project has different rendering settings from the game (e.g. `r.VelocityOutputPass`, `r.Substrate.ProjectGBufferFormat`, `r.VirtualTextures`, `r.Nanite.Tessellation`). Materials are compiled for those settings, so in-game they write garbage, even though they look right in the editor.

**Fix:** this step. After changing the settings, open your material, click **Apply**, save and package again.

**How we found it:** turning texture streaming off made no difference, so we extracted the game's `DefaultEngine.ini` from its pak and compared it with the mod project's.
:::

## Good habits

- **Never save a level into a mod folder.** Everything in a labelled folder ends up in that mod. Leave the default level alone or save it outside `Content/Wildfire`.
- Use **File → Save All** before every package.
- Give each mod its own **Chunk ID** so mods can be installed and removed separately.

Next: pick a tutorial: [Retexturing](/tutorials/retexturing/), [Replacing sounds](/tutorials/sounds/) or [Replacing 3D models](/tutorials/models/).

---
title: Set up the mod project
description: Create the blank Unreal 5.8 project that every mod is built from, and set its packaging options.
---

:::note[In this guide you will]
- Create the Unreal project you'll build every mod in
- Set the packaging options so your mods come out as separate files
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

## Good habits

- **Never save a level into a mod folder.** Everything in a labelled folder ends up in that mod. Leave the default level alone or save it outside `Content/Wildfire`.
- Use **File → Save All** before every package.
- Give each mod its own **Chunk ID** so mods can be installed and removed separately.

Next: pick a tutorial: [Retexturing](/tutorials/retexturing/), [Replacing sounds](/tutorials/sounds/) or [Replacing 3D models](/tutorials/models/).

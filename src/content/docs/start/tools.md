---
title: Tools you need
description: 'Software for modding Rescue Ops: Wildfire, and how to set each one up.'
---

| Tool | Used for | Notes |
|---|---|---|
| **Unreal Engine 5.8** (Epic Games Launcher) | Building mods | 5.8.3 builds load fine in the 5.8.1 game (textures and audio confirmed). Includes `UnrealPak.exe` for checking builds. |
| **FModel** | Browsing and exporting game assets | Set the engine to `GAME_UE5_8` and load the `.usmap`. |
| **`.usmap` mapping file** | Lets FModel read the game's unversioned properties | You have to generate it once. See [Generate the .usmap](/start/usmap/). |
| **UE4SS** (experimental build) | Only for generating the `.usmap` | Remove it afterwards. It crashes the game on map load. |
| Image editor (Substance Painter, Photoshop, GIMP…) | Painting textures | Work on copies, never on the exports. |
| **Blender** | Viewing meshes, UV overlays, scripted image work | FModel exports meshes as `.glb`. |
| Audio editor (Audacity etc.) | Preparing sounds | WAV, 16-bit PCM, 44.1 kHz. |

## Game folder

Steam → Library → right-click the game → **Manage → Browse local files**. The folder you need is:

```text
<game>\Wildfire\Content\Paks\
```

It contains `Wildfire-Windows.pak/.utoc/.ucas` and `global.utoc/.ucas`. Mods go here too.

## FModel setup

1. **Settings → General**: set the game directory to the folder above's parent (the one containing `Wildfire`), and set **UE Versions** to `GAME_UE5_8`.
2. **Settings → General → Local mapping file**: tick it and point it at your `.usmap`.
3. **Settings → Models → Nanite mesh export format**: set it to the fallback/normal LODs option, **not "Nanite only"**. Otherwise Nanite meshes such as the trucks export as empty 160-byte `.glb` files.
4. No AES key is needed. The game isn't encrypted.

:::caution[FModel output folder]
In our testing FModel ignored its output-folder setting and wrote exports to `FModel\Output\Exports\…`. Check there if your exports seem to vanish, then copy them into your own read-only "originals" folder.
:::

## Hardware we tested on

The mods were built on an i7-6700K with a GTX 1080 (8 GB). Packaging a small mod project takes a few minutes, so you don't need a high-end PC.

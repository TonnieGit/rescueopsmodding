---
title: Tools you need
description: 'Software for modding Rescue Ops: Wildfire, and how to set each one up.'
---

| Tool | Used for | Download | Notes |
|---|---|---|---|
| **Unreal Engine 5.8** | Building mods | [Epic Games Launcher](https://store.epicgames.com/download), then **Unreal Engine → Library → +** and pick 5.8 | Free. 5.8.3 builds load fine in the 5.8.1 game (textures and audio confirmed). Includes `UnrealPak.exe` for checking builds. |
| **FModel** | Browsing and exporting game assets | [GitHub releases](https://github.com/4sval/FModel/releases) ([fmodel.app](https://fmodel.app)) | Free. Needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0). See [Set up FModel](/start/fmodel/). |
| **`.usmap` mapping file** | Lets FModel read the game's unversioned properties | [Wildfire.usmap](https://github.com/TonnieGit/rescueopsmodding/releases/latest/download/Wildfire.usmap) (2.8 MB) | See [Get the .usmap](/start/usmap/) for which game version it matches. |
| **Blender** | Viewing meshes, UV overlays, building replacement models | [blender.org](https://www.blender.org/download/) | Free. Opens FModel's `.glb` exports with no add-ons. |
| **Image editor** | Painting textures | [GIMP](https://www.gimp.org/downloads/) (free), [Substance 3D Painter](https://www.adobe.com/products/substance3d/apps/painter.html) or [Photoshop](https://www.adobe.com/products/photoshop.html) | Any editor that saves PNG works. Work on copies, never on the exports. |
| **Audio editor** | Preparing sounds | [Audacity](https://www.audacityteam.org/download/) | Free. Export WAV, 16-bit PCM, 44.1 kHz. |
| **UE4SS** (experimental build) | Only for making a new `.usmap` after a game update | [experimental-latest](https://github.com/UE4SS-RE/RE-UE4SS/releases/tag/experimental-latest) | Most people won't need it. We used build `5e627997`. Remove it afterwards: it crashes the game on map load. |

## Game folder

Steam → Library → right-click the game → **Manage → Browse local files**. The folder you need is:

```text
<game>\Wildfire\Content\Paks\
```

It contains `Wildfire-Windows.pak/.utoc/.ucas` and `global.utoc/.ucas`. Mods go here too.

## FModel setup

See [Set up FModel](/start/fmodel/) for a step-by-step guide: installing it, adding the game, loading the `.usmap` and the export settings to change.

## Hardware we tested on

The mods were built on an i7-6700K with a GTX 1080 (8 GB). Packaging a small mod project takes a few minutes, so you don't need a high-end PC.

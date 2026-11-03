---
title: Chunk ID registry
description: Chunk IDs and container names already used by published mods, so new mods don't clash.
---

Every mod is packaged into its own chunk (set by the PrimaryAssetLabel's **Chunk ID**). Two mods with the same container name overwrite each other's files. Pick a free ID and a unique `z_<Name>_P` name (or `pakchunk<ID>-<Name>-Windows_P` for mods with their own material).

## In use

| Chunk ID | Container | Mod | Author |
|---|---|---|---|
| 1000 | `z_FirefighterTex_P` | [QLD RFS: Uniforms](/mods/qld-rfs-uniforms/) | Tonnie |
| 1001 | `z_Hazard_1000_Sounds_P` | [Hazard 1000 Tones](/mods/hazard-1000-tones/) | Tonnie |
| 1002 | `z_QLD_Truck_P` | [QLD RFS: Fire Truck](/mods/qld-rfs-truck-reskin/) | Tonnie |
| 1003 | `pakchunk1003-BR9_Bushfire_Helmet_Yellow-Windows_P` | [QLD RFS: BR9 Helmet Yellow](/mods/br9-bushfire-helmet-yellow/) (also in QLD RFS: Uniforms) | Tonnie |
| 1004 | `pakchunk1004-Beacon_Base-Windows_P` | [Beacons: Light Engine](/mods/tonnie-core/) (shared by the beacon colours). Replaces the v1 files `pakchunk1004-Red_Beacons` / `pakchunk1004-Red_Blue_Beacons` | Tonnie |
| 1005 | `pakchunk1005-Tonnie_Core-Windows_P` | [ROMF](/mods/tonnie-core/) (shared hook) | Tonnie |
| 1006 | `pakchunk1006-Free_Camera-Windows_P` | [Free Camera](/mods/free-camera/) | Tonnie |
| 1007 | `pakchunk1007-Red_Blue_Beacons-Windows_P` | [Beacons: Red & Blue](/mods/red-blue-beacons/) colour (also in QLD RFS: Fire Truck) | Tonnie |
| 1008 | `pakchunk1008-Red_Beacons-Windows_P` | [Beacons: Red](/mods/red-beacons/) colour | Tonnie |
| 1009 | `pakchunk1009-Brighter_Beacons-Windows_P` | [Beacons: Blue](/mods/brighter-beacons/) colour (also in ELS: Light Bar France) | Tonnie |
| 1011 | `pakchunk1011-ELS-Windows_P` | [ELS: Sirens](/mods/els/) (also in both ELS light bars and QLD RFS: Fire Truck) | Tonnie |
| 1012 | `pakchunk1012-ELS_LightBar-Windows_P` | ELS: Light Bar Engine, the light bar shared by [ELS: Light Bar AUS](/mods/els-light-bar/) and [France](/mods/els-france/) | Tonnie |
| 1013 | `pakchunk1013-Lights_HQ-Windows_P` | [Lights: HQ Real Light](/mods/lights-hq/) | Tonnie |
| 1014 | `pakchunk1014-ELS_France_Override-Windows_P` | [ELS: Light Bar France](/mods/els-france/) (blue bar and French sirens) | Tonnie |
| 1015 | `pakchunk1015-ELS_LightBar_AUS-Windows_P` | [ELS: Light Bar AUS](/mods/els-light-bar/) (marker file, also in QLD RFS: Fire Truck) | Tonnie |
| 1016 | `pakchunk1016-First_Person-Windows_P` | [Camera: First Person](/mods/first-person/) | Tonnie |

## Conventions

- Use IDs **1000 and above**. Chunk 0 is the project's base content and is never shipped.
- For texture and sound mods, the chunk number only affects the build output name (`pakchunk<ID>-Windows.*`). Once you rename the files it's the **container name** that must be unique.
- Mods with their **own material** keep `pakchunk<ID>` at the start of the file name, because the game finds their shaders by that number ([details](/tutorials/models/#step-9-name-and-install)). Their **Chunk ID** must be unique too.
- Two mods that replace the **same asset** conflict whatever their names. The one that loads last wins.

## Add your mod

Open a pull request on [GitHub](https://github.com/TonnieGit/rescueopsmodding) adding a row to this table.

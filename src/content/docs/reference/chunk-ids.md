---
title: Chunk ID registry
description: Chunk IDs and container names already used by published mods, so new mods don't clash.
---

Every mod is packaged into its own chunk (set by the PrimaryAssetLabel's **Chunk ID**). Two mods with the same container name overwrite each other's files. Pick a free ID and a unique `z_<Name>_P` name.

## In use

| Chunk ID | Container | Mod | Author |
|---|---|---|---|
| 1000 | `z_FirefighterTex_P` | [QLD RFS Uniforms](/mods/qld-rfs-uniforms/) | Tonnie |
| 1001 | `z_Hazard_1000_Sounds_P` | [Hazard 1000 Tones](/mods/hazard-1000-tones/) (also in Truck Reskin) | Tonnie |
| 1002 | `z_QLD_Truck_P` | [QLD RFS Truck Reskin](/mods/qld-rfs-truck-reskin/) | Tonnie |
| 1003 | `z_QLD_Helmet_P` | *Reserved: helmet model swap (planned)* | Tonnie |

## Conventions

- Use IDs **1000 and above**. Chunk 0 is the project's base content and is never shipped.
- The chunk number only affects the build output name (`pakchunk<ID>-Windows.*`). Once you rename the files it's the **container name** that must be unique.
- Two mods that replace the **same asset** conflict whatever their names. The one that loads last wins.

## Add your mod

Open a pull request on [GitHub](https://github.com/TonnieGit/rescueopsmodding) adding a row to this table.

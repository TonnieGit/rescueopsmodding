---
title: Chunk ID registry
description: Chunk IDs and container names already used by published mods, so new mods don't clash.
---

Every mod is packaged into its own chunk (set by the PrimaryAssetLabel's **Chunk ID**). Two mods with the same container name overwrite each other's files. Pick a free ID and a unique `z_<Name>_P` name (or `pakchunk<ID>-<Name>-Windows_P` for mods with their own material).

## In use

| Chunk ID | Container | Mod | Author |
|---|---|---|---|
| 1000 | `z_FirefighterTex_P` | [QLD RFS Uniforms](/mods/qld-rfs-uniforms/) | Tonnie |
| 1001 | `z_Hazard_1000_Sounds_P` | [Hazard 1000 Tones](/mods/hazard-1000-tones/) (also in Truck Reskin) | Tonnie |
| 1002 | `z_QLD_Truck_P` | [QLD RFS Truck Reskin](/mods/qld-rfs-truck-reskin/) | Tonnie |
| 1003 | `pakchunk1003-BR9_Bushfire_Helmet_Yellow-Windows_P` | [BR9 – Bushfire Helmet Yellow](/mods/br9-bushfire-helmet-yellow/) (also in QLD RFS Uniforms) | Tonnie |

## Conventions

- Use IDs **1000 and above**. Chunk 0 is the project's base content and is never shipped.
- For texture and sound mods, the chunk number only affects the build output name (`pakchunk<ID>-Windows.*`). Once you rename the files it's the **container name** that must be unique.
- Mods with their **own material** keep `pakchunk<ID>` at the start of the file name, because the game finds their shaders by that number ([details](/tutorials/models/#step-9-name-and-install)). Their **Chunk ID** must be unique too.
- Two mods that replace the **same asset** conflict whatever their names. The one that loads last wins.

## Add your mod

Open a pull request on [GitHub](https://github.com/TonnieGit/rescueopsmodding) adding a row to this table.

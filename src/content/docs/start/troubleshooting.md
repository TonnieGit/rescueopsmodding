---
title: Troubleshooting & gotchas
description: 'Common problems when modding Rescue Ops: Wildfire, and how to fix them.'
---

## My mod doesn't show up in-game

| Check | Fix |
|---|---|
| Is the asset in the chunk manifest (`pakchunk<ID>.txt`)? | If not: set **Additional Asset Directories to Cook = `/Game/Wildfire`**. UE 5.8 labels are editor-only and don't pull assets into the cook. |
| Is the path identical, capitalisation included? | Copy it from the FModel `.json` `Package` field. `TrimSheet` ≠ `Trimsheet`, and it's `Vehicules`. |
| Is the project named `Wildfire`? | It must be, or the mount point is wrong. |
| Mount point in `UnrealPak -List` starts with `../../../Wildfire/Content/`? | Same cause as above. |
| Are all three files there and named `z_…_P`? | The `.pak`, `.utoc` and `.ucas` must share a name ending in `_P`. |
| Did the game update? | Rebuild against the new version. |

## It shows, but looks wrong

| Symptom | Likely cause |
|---|---|
| Blurry | Game Texture Quality below Epic (Medium = quarter resolution). Set it to Epic. |
| Washed out or too dark | sRGB wrong: `_D` on, `_N` / `_RM` / `_ERM` off |
| Inverted or odd lighting | `_N` imported without Normalmap compression, or the green channel flipped |
| Shiny or matte in the wrong places | Roughness/metallic in the wrong channels. G = roughness, B = metallic, R = 0 |
| Old detail still visible after repainting | Roughness or normal still differ there. See [Painting tips](#painting-tips) below. |
| Something else changed colour too | That texture or colour block is shared by other meshes. |

## Sound problems

| Symptom | Likely cause |
|---|---|
| Click on every loop | MetaSound **Loop Start** isn't 0. Add a matching pre-roll ([details](/tutorials/sounds/#step-2-export-the-originals-and-read-their-settings)). |
| Pop on switch-on | Add a short fade-in on the pre-roll |
| Volume slider or distance no longer affect it | You shipped a MetaSound, SoundClass or Attenuation. Remove it from the chunk. |
| Two sounds in one mod | A duplicated PrimaryAssetLabel kept the other's Chunk ID |

## Tools

| Problem | Fix |
|---|---|
| FModel shows files but won't preview or export | Load the `.usmap`, engine `GAME_UE5_8` |
| Nanite meshes export as 160-byte `.glb` | FModel → Settings → Models → Nanite export format: not "Nanite only" |
| Exports go to the wrong folder | FModel writes to `Output\Exports\` regardless. Copy them from there. |
| UE4SS crashes about 15 s in | Expected on lobby load. Dump the `.usmap` on the splash screen ([how](/start/usmap/)), then remove UE4SS. |
| Messy AO bake in Substance on the truck | Overlapping or tiling UVs (mirrored doors, shared lights, tiling sets). This is expected, not a broken mesh. |

## Things that don't work (yet)

- **retoc** and **UE4-DDS-Tools** (direct cooked-asset editing) didn't support UE 5.8 at the time of writing, so build in the editor instead.
- **UE4SS Lua mods**: the game crashes on lobby load with UE4SS installed.

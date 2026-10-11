---
title: CCF truck
description: 'Meshes, material slots and texture sets of the CCF fire truck in Rescue Ops: Wildfire.'
---

The CCF is the main fire truck: a French forest tanker (*camion-citerne feux de forêts*). Its siren MetaSound is `MSS_Siren_CCFM`.

## Meshes and material slots

| Mesh | Type | Material slots |
|---|---|---|
| `S_CCF` | StaticMesh, **Nanite** (main body) | MetalTread, RibbingYellowAndRed, TrimSheet, VariousTiling, LightPrecise, FirehosePipeCable, RegionalPlate |
| `SK_CCF` | SkeletalMesh (chassis, wheels, tyres) | LightPrecise, VariousTiling |
| `Doors/*` (8) | StaticMesh, Nanite | LightPrecise, TrimSheet, VariousTiling (+ MetalTread / Ribbing on some) |

41 meshes in total, including the command panel, glass, light meshes and joints. Exporting them needs FModel's Nanite export setting changed (see [Tools](/start/tools/#fmodel-setup)).

## Materials

- CCF material instances have **no colour parameters**. All colour comes from textures.
- `MI_CCF_LightPrecise` / `MI_CCF_LightPreciseFlash` share the Precise textures. Flash turns on the `MustFlash` switch.
- `MI_CCF_RegionalPlate` reuses VariousTiling with UOffset 0.5 / VOffset 0.25.
- `MI_CCF_Glass` uses colour parameters only.

## Texture sets

| Set | Role |
|---|---|
| **Precise** | Unique details: side lettering, number plates ("M-30", "13-CCF"), the "18/112" sign, Rescue Ops logo, stickers, lights, tyres |
| **VariousTiling** | **Paint colour blocks.** The two bottom red blocks are the main body colour. Mustard sits middle-left; greys, navy and pale blue are on the top row. |
| **TrimSheet** | Horizontal stripes (yellow, red, black, metal, blue), an orange block and yellow ribbing |
| **StripedMarking** | Reflective rear chevrons (red and fluoro yellow in the original) |
| **PumpCommand** | Pump control panel |
| **MetalTread** | Tiling chequer plate |

## Things to know before painting

- **The side lettering is geometry.** Only pixels inside the letter shapes are visible. You can recolour the letters but not paint new text there.
- **Full rectangles** you can repaint freely: both plates, the 18/112 sign, the door logo, the "ANGLES MORTS" sticker and the "WIDE" panel.
- **Colour blocks are shared.** Recolouring a VariousTiling block changes every part mapped to it.
- **Overlapping UVs** (mirrored doors, shared lights and wheels) and tiling sets outside 0–1 make AO bakes look messy. That's expected.
- Hiding a painted-over detail means matching **roughness and normal** as well as colour. See [Painting tips](#painting-tips) below.

## Painting tips

What worked for the [QLD RFS Truck Reskin](/mods/qld-rfs-truck-reskin/):

| To change… | Edit |
|---|---|
| Body colour | `T_CCF_VariousTiling_D`: recolour the **two bottom red blocks** |
| Logos, number plates, signs | `T_CCF_Precise_D`: these are full rectangles, so paint anything |
| Stripes and trim | `T_CCF_TrimSheet_D` |
| Rear chevrons | `T_CCF_StripedMarking_D` |

**Hiding the French side lettering.** The letters are part of the mesh, so new text can't go there, but the old letters can be hidden. Inside the letter pixels, set:

| Texture | Value |
|---|---|
| `T_CCF_Precise_D` | your body colour |
| `T_CCF_Precise_ERM` | R 0, G (roughness) **56**, B 0, matching the glossy body paint |
| `T_CCF_Precise_N` | flat **(128, 128, 255)** |

Change only the colour and the letters stay readable: the originals are matte (roughness ≈ 92) and slightly raised.

**Colour.** A golden yellow that reads correctly in the game's lighting is **`#DCA305`** (220, 163, 5). Pure lemon yellows look too bright in-game. Recolour with a soft falloff around the target colour rather than a hard replace, so edges and the baked-in dirt survive.

**Chevrons.** Australian rear chevrons are red and reflective white. Recolour the fluoro yellow stripes to off-white (≈ 238, 238, 232) and keep the honeycomb shading.

**Painting in Substance.** After importing all the CCF `.glb` files into one Blender scene, merge the duplicate `.001`/`.002` materials before exporting, or each copy becomes its own texture set. Expect messy AO bakes: the doors, lights and wheels share mirrored UVs.

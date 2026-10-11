---
title: Texture formats
description: 'Compression, sRGB, texture group and channel layouts used by Rescue Ops: Wildfire textures.'
---

## By suffix

| Suffix | Content | Pixel format | Compression setting | sRGB | Texture group |
|---|---|---|---|---|---|
| `_D` | Base colour | DXT1 / BC1 | `TC_Default` | **On** | World |
| `_N` | Tangent-space normal | BC5 | `TC_Normalmap` | Off | `WorldNormalMap` |
| `_RM` | Packed masks | DXT1 / BC1 | `TC_Masks` | Off | World |
| `_ERM` | Packed masks | DXT1 / BC1 | `TC_Masks` | Off | World |

Settings marked as defaults are simply missing from the FModel `.json`. A missing `SRGB` means true, and a missing `CompressionSettings` means `TC_Default`.

## Channels

| Texture | R | G | B | A |
|---|---|---|---|---|
| `_D` | colour | colour | colour | unused (BC1 has no alpha) |
| `_N` | normal X | normal Y | rebuilt Z | unused (BC5 stores only R+G) |
| `_RM` | ≈ 0, unused | **roughness** | **metallic** | unused |
| `_ERM` | "emissive": ≈ 0 in every CCF set | **roughness** | **metallic** | unused |

Keep R at 0 when you repack masks. Light glow on the truck comes from a material switch (`MustFlash`), not this channel.

## Sizes

| Texture | Cooked | Source |
|---|---|---|
| `T_FirefighterBody_*` | 2048² | 4096² |
| `T_CCF_Precise_*`, `T_CCF_VariousTiling_*` | 2048² | 2048² |
| `T_CCF_TrimSheet_*` | 1024² | 4096² |
| `T_CCF_PumpCommand_*` | 1024² | 1024² |
| `T_CCF_StripedMarking_D` / `_ERM` / `_N` | 512² / 256² / 64² | 2048² |
| `T_CCF_MetalTread_D` / `_ERM` / `_N` | 64² / 64² / 512² | 2048² |

Replacements can be larger than the cooked size: 4096² loads fine and looks sharper.

## Streaming

Large mips (e.g. 2048 down to 128) are stored in a separate bulk file (`.ubulk`) and the small mips inline in the package. That's why a replacement must be a properly cooked Texture2D from the editor, not a raw image dropped into a container.

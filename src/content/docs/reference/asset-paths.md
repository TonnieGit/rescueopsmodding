---
title: Asset paths
description: 'Game asset paths in Rescue Ops: Wildfire that we have mapped, for textures, meshes, materials and audio.'
---

All paths are under `/Game/Wildfire/` (on disk in your mod project: `Content/Wildfire/`). Copy them exactly. Capitalisation and spelling (`Vehicules`) matter.

## Firefighter

| Asset | Path | Notes |
|---|---|---|
| Body diffuse | `Textures/Characters/Firefighter/Body/T_FirefighterBody_D` | Also used by the helmet |
| Body normal | `Textures/Characters/Firefighter/Body/T_FirefighterBody_N` | |
| Body rough/metal | `Textures/Characters/Firefighter/Body/T_FirefighterBody_RM` | G = roughness, B = metallic |
| Head | `Textures/Characters/Firefighter/Head/T_FirefighterHead_D/_N/_R` | 2048² |
| Eyes | `Textures/Characters/Common/Eyes/T_Eye_D/_N/_Mask` | 512² |
| Body material | `Materials/Characters/Firefighter/MI_FirefighterCloth` | No texture parameters |
| Helmet mesh | `StaticMeshes/Props/FirefighterEquipment/S_FirefighterHelmet` | Slot 0 `MI_FirefighterCloth`, slot 1 `MI_FS_Glass` (visor) |
| Visor glass | `Materials/Environment/Buildings/FireStation/MI_FS_Glass` | Parameters only (tint, opacity 0.5) |

## CCF fire truck

| Asset | Path |
|---|---|
| Body mesh (Nanite) | `Vehicles/CCF/S_CCF` |
| Chassis (skeletal) | `Vehicles/CCF/SK_CCF` |
| Doors | `Vehicles/CCF/Doors/*` |
| Unique detail textures | `Textures/Vehicles/CCF/Precise/T_CCF_Precise_D/_N/_ERM` |
| Paint colour blocks | `Textures/Vehicles/CCF/VariousTiling/T_CCF_VariousTiling_D/_N/_ERM` |
| Stripes | `Textures/Vehicles/CCF/TrimSheet/T_CCF_TrimSheet_*` |
| Chevrons | `Textures/Vehicles/CCF/RibbingYellowAndRed/T_CCF_StripedMarking_*` |
| Pump panel | `Textures/Vehicles/CCF/PumpPanel/T_CCF_PumpCommand_*` |
| Chequer plate | `Textures/Vehicles/CCF/MetalTread/T_CCF_MetalTread_*` |
| Materials | `Materials/Vehicles/CCF/MI_CCF_*`, parent `Materials/Vehicles/M_Vehicle_Body` |

There's also a US engine, **CalFire34**, which we haven't mapped yet.

## Audio

| Asset | Path | Notes |
|---|---|---|
| Siren wave | `Audio/Vehicules/Siren/A_Firetruck_siren_loop_mono` | Stereo despite the name, looping |
| Siren MetaSound | `Audio/Vehicules/Siren/MSS_Siren_CCFM` | Loop Start 0.05 s, don't replace |
| Horn wave | `Audio/Vehicules/Horn/A_Truck_Horn_Loop` | Mono, looping |
| Horn MetaSound | `Audio/Vehicules/Horn/MSS_Horn_CCFM` | Loop Start 0, don't replace |
| Attenuation | `Audio/Attenuations/ATT_CCF` | ~44 m max distance |
| Sound classes | `Audio/SoundClasses/Master`, `SFX`, `Ambient`, `Music` | |
| Siren toggle ability | `Blueprints/Core/GAS/GameplayAbilities/Actions/Vehicles/BP_ToggleSiren_GA` | Input `IA_Ability_ToggleSiren` |

## Maps

| Map | Notes |
|---|---|
| `Maps/L_TitleScreen` | |
| `Maps/L_Lobby` | Fire station; loaded automatically as `L_Lobby?listen` (where UE4SS crashes) |

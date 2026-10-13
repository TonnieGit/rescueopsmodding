---
title: Game facts
description: 'Technical facts about Rescue Ops: Wildfire relevant to modding: engine, containers, audio and online services.'
---

Checked on the Steam demo, October 2026.

| | |
|---|---|
| Engine | **Unreal Engine 5.8.1**, Shipping build |
| Project name | `Wildfire` |
| Executable | `Wildfire\Binaries\Win64\WildfireDemo-Win64-Shipping.exe` |
| Content | `Wildfire\Content\Paks\`: `Wildfire-Windows.pak/.utoc/.ucas` + `global.utoc/.ucas` |
| Container format | **IoStore** (`.utoc`/`.ucas`, with a stub `.pak`) |
| Encryption | **None** (no AES key needed) |
| Pak signing | **None** (no `.sig` files) |
| Anti-cheat | None found (no EasyAntiCheat) |
| Properties | **Unversioned**, so you need a `.usmap` ([download it](/start/usmap/)) |
| Audio | Native Unreal audio (**MetaSounds**). No Wwise or FMOD. Waves are Bink Audio (`BINKA`). |
| Online | Redpoint EOS + Steam |
| Game files | About 9,800 |

## Settings that matter for modders

- The game's default **Texture Quality is Medium** (`sg.TextureQuality=1`), which sets `r.Streaming.MipBias=2` and shows textures at a quarter of their size. Set it to Epic for testing.
- Engine World / WorldNormalMap texture groups allow up to 16384², so 4K replacements aren't capped.

## Editor compatibility

| Built with | Game | Result |
|---|---|---|
| UE 5.8.3 | 5.8.1 | ✅ Textures and audio load correctly |

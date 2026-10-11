---
title: Generate the .usmap
description: 'Dump a mapping file with UE4SS so FModel can read Rescue Ops: Wildfire assets.'
---


The game is cooked with **unversioned properties**, so FModel can't preview or export most assets without a **`.usmap` mapping file**. You make it once per game version with UE4SS, and then remove UE4SS again.

## What you need

- The **experimental UE4SS build** (v4.0.0-rc1 line; we used `5e627997`). The stable v3.0.1 release doesn't work with this UE 5.8 game.

## Steps

1. Install UE4SS into `Wildfire\Binaries\Win64\` following its own instructions.
2. In `UE4SS-settings.ini`, set `GuiConsoleEnabled = 0`.
3. In `Mods\mods.txt`, set `BPML_GenericFunctions : 0`.
4. Launch the game and **press `Ctrl` + `Numpad 6` on the splash screen**. Don't wait.
5. The UE4SS log shows `Mappings Generation Completed Successfully!` and the `.usmap` appears next to `UE4SS.dll` (about 2.8 MB).
6. The game will crash a few seconds later, when it loads the fire station. That's expected. Copy the `.usmap` somewhere safe.

:::note[Why the splash screen?]
With UE4SS installed, the game crashes about 15 seconds in when it loads the lobby map. Pressing the hotkey on the splash screen finishes the dump before that happens.
:::

## Remove UE4SS

You don't need UE4SS for any mod on this site, and the game crashes with it installed. **Move** every UE4SS file out of `Win64` into a backup folder, including `dwmapi.dll` and the `ue4ss\` folder. Then launch the game to check it boots normally.

Steam's **Verify integrity** won't do this for you. It doesn't delete extra files.

---
title: 'Mod Dev: share your mods'
description: 'Upload your own Rescue Ops: Wildfire mods from the Mod Launcher. Players get them in one click, rate them and send you feedback.'
---

Made a mod for Rescue Ops: Wildfire? **Mod Dev** in the [Mod Launcher](/mods/installing/) (2.0 and later) puts it in front of every player who uses the launcher, with your name on it. They install it in one click, rate it, and send you bugs, suggestions and thanks. You don't need a website or a download link, and it's free.

:::note[In this guide you will learn]
- How to set up a mod dev account (two minutes, no password)
- What to upload and how to fill in the details
- What happens after you upload, and how to update your mod
- How to keep your account safe and move it to another PC
:::

## Before you start

You need a finished mod: the `.pak` file, plus its `.utoc` and `.ucas` if it has them (Unreal 5 IoStore mods do). If you're new to modding, start with [Set up the mod project](/start/mod-project/) and [Package and install your mod](/start/packaging/).

Give your files **their own names**. Two mods can't share a file name, so the launcher refuses files that another mod already uses. Use `pakchunk<ID>-<YourMod>-Windows_P` or `z_<YourMod>_P`, with a chunk ID that isn't taken: see the [chunk ID registry](/reference/chunk-ids/).

Read the [upload rules](/mod-dev/rules/). The short version: only upload your own work.

## Step 1: Become a mod dev

1. Open the Mod Launcher and click **MOD DEV** on the left.
2. Click **Become a mod dev**.
3. If you haven't rated a mod yet, the launcher asks for a name and email first. They stay private (see [Privacy](/privacy/)).
4. Type the **author name** players will see on your mods, then click **OK**. It's 2 to 32 characters and can't be changed later.
5. The launcher shows your **recovery code**, for example `ABCD-EFGH-JKLM-NPQR`. Save it somewhere safe (a password manager, or a photo on your phone), then click **I've saved it**.

:::caution[Keep your recovery code]
There are no passwords. Your account lives on your PC as a secret key, and the recovery code is the only way to use it on another PC or after reinstalling Windows. It's shown once. Anyone who has it can upload as you, so don't share it.
:::

## Step 2: Upload your mod

1. In **MOD DEV**, click **Upload a mod**.
2. **Mod name**: what players see, 3 to 60 characters. It can't be the name of a mod that's already in the launcher.
3. **Version**: start with `1`. Each update needs a new number (`1.1`, `2`, ...).
4. **Category**: where it shows in the launcher's mod list.
5. **Description**: what it changes and **how to use it**: keys, menus, anything a player needs to know. 20 to 2000 characters.
6. **Mod files**: click **Choose files**, or drop them on the box. You can pick the `.pak`, `.utoc` and `.ucas` files or a `.zip` that holds them. Up to 100 MB.
7. **Picture**: shown on the mod's card. Any JPG or PNG; the launcher crops it to 16:9.
8. **Screenshots** (optional): up to 6. Players click the picture to see them full size.
9. **Needs and clashes** (optional): tick the mods yours needs (they're installed with it, for example ROMF) and the mods it can't run alongside (the launcher lets only one load).
10. Tick the box to confirm the mod is yours to share, then click **Upload**.

The status bar shows the upload. Once it's done, the mod appears under **My mods > Uploads** as **Waiting for its safety check**.

## Step 3: The safety check

We run a quick safety check on every upload, then it goes live, usually within 24 hours. It's a check that the files are what they say they are and follow the [upload rules](/mod-dev/rules/), not a judgement of how good the mod is. Players decide that with their ratings.

When it's done, the launcher tells you on the dashboard:

- **Live**: the mod is in every player's launcher, on the [Community mods](/mods/community/) page, and in the launcher's news.
- **Didn't pass the safety check**: the reason is shown under **My mods > Uploads**. Fix it and upload again.

While an upload is waiting you can **Withdraw** it, for example to send a corrected version.

## Feedback from players

Players can click **Feedback** on your mod's details to send you a bug report, a suggestion or thanks. It arrives in **MOD DEV > Inbox**, with their name (never their email). The dashboard tells you when something new comes in. Your mod's downloads and star rating are shown under **My mods**.

## Updating your mod

1. In **MOD DEV > My mods**, click **Upload update** next to the mod.
2. The details are filled in. Give it a **new version** number and say **What's new**, one change per line.
3. Add the new files and a picture, then click **Upload**.

After its safety check, everyone who has your mod sees the update in their launcher. Only you can update your mods. If the new version's files have different names, the old files are removed when players update.

## On another PC

1. Install the Mod Launcher, open **MOD DEV** and click **I have a recovery code**.
2. Type the code and click **OK**.

Your account moves to that PC. The old PC stops being able to upload (it shows a message saying the account was restored elsewhere). Lost your code? On a PC where your account still works, open **MOD DEV > My mods > Account** and click **Make a new code**. The old code stops working.

## Questions

Message **Tonnie** on [Patreon](https://patreon.com/Tonnie001) or ask in the game's Discord.

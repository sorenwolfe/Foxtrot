# Foxtrot

**Hear an orchestrion roll before you go and earn it.**

Be able to hear orchestrion rolls before you own them. Whether it be in a duty before rolling on it, or from the Marketboard. Right click and preview a track from the item,
or use the database to look up tracks.

Type `/foxtrot` to open it.

---

## Installing it

You need XIVLauncher with Dalamud. If you already use any plugins, you have it.

Foxtrot isn't in the official plugin list yet, so you add it by hand the first time. It takes about
a minute, and you only ever do it once.

1. In game, type `/xlplugins` and press Enter. The plugin installer window opens.
2. Click **Settings** at the bottom-left of that window.
3. Click the **Experimental** tab along the top.
4. Scroll to **Custom Plugin Repositories**. There's an empty text box at the bottom of the list. 
   Click the **+** button next to the box at the bottom.
   Paste this into it:

   ```
   https://raw.githubusercontent.com/sorenwolfe/XIVPlugins/main/repo.json
   ```
6. Click **Save and Close**.
7. You're back at the plugin list. Type `foxtrot` in the search box. Click **Install**.

---

## What it does

**Right-click a roll, hear the roll.** Any orchestrion roll in your bags gets a **Preview** entry
on its right-click menu. Click it and a small player opens with the track playing.

**Browse everything.** `/foxtrot` opens a searchable list of every orchestrion track in the game.
Search by name or by what the description says, filter by category, and play any of them — whether
you own the roll or not. That's the point: the tracks worth hearing before you commit to hunting
one are exactly the ones you don't have yet.

**A small player.** Play, stop, a volume slider that's separate from your game volume, and a
running time. Star the ones you like and the browser will filter down to just those.

**Your zone music gets out of the way.** While a preview plays, the game's music fades down, and
it fades back the moment you stop — including if you unload the plugin halfway through a track.
Two tracks at once makes a preview impossible to judge. You can turn this off, or change how far
down it goes, in settings.

---

## Commands

| Type this | What happens |
|---|---|
| `/foxtrot` | Opens and closes the browser (`/orch` works too) |
| `/foxtrot player` | Opens and closes the small player |
| `/foxtrot stop` | Stops whatever is playing |
| `/foxtrot config` | Opens the settings |


---

## If something isn't working

**The Preview entry doesn't appear.** It only shows on actual orchestrion rolls. Check the
right-click options in settings are on.

**The browser is empty.** Foxtrot reads the track list from the game's own data files at startup.
If it comes up empty, something went wrong reading them — the count is shown at the bottom of the
settings window, and there'll be a line in the Dalamud log.

**My music is quiet after using it.** It shouldn't ever be — the music is restored when a preview
stops, when the track ends, and when the plugin unloads. If you do manage it, please open an issue
and say what you were doing, because that's the bug I most want to hear about.

---

## Licence

AGPL-3.0-or-later. Full text in `LICENSE`.

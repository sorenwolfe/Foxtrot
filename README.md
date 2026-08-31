# Foxtrot

**Hear an orchestrion roll before you go and earn it.**

The game will happily sell you a roll for a few hundred thousand gil and tell you absolutely
nothing about what it sounds like. Foxtrot fixes that: right-click a roll and hear it, or open the
browser and listen to any track in the game — including the ones you don't own yet.

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
   Paste this into it:

   ```
   https://raw.githubusercontent.com/sorenwolfe/Foxtrot/main/repo.json
   ```

5. Click the **+** button next to the box.
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

**It uses the game's own player.** Previews go through the same call the in-game orchestrion makes,
on the game's own orchestrion audio channel. So it sounds exactly like owning the roll would, and
it respects your audio setup rather than talking over it.

---

## Commands

| Type this | What happens |
|---|---|
| `/foxtrot` | Opens and closes the browser (`/orch` works too) |
| `/foxtrot player` | Opens and closes the small player |
| `/foxtrot stop` | Stops whatever is playing |
| `/foxtrot config` | Opens the settings |

---

## Known limits

**No seek bar.** The game gives us elapsed time but no way to jump to a point in the track, so you
can see how far in you are but not scrub.

**"Hold" isn't a real pause.** The game has no pause for this kind of sound. Hold slows the track
to a standstill, which is the closest thing available — if it misbehaves, Stop and play it again.

**No right-click in the orchestrion list yet.** Rolls in your *bags* work. Rows in the in-game
orchestrion list don't, because working out which row is selected needs a detail that can only be
found with the game running, and guessing at it would crash rather than misbehave. Use the browser
in the meantime — it plays more anyway.

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

## The icon

The current icon is a placeholder I drew as vector art — a stand-in until the real thing exists.
Replacing it is one file: drop a square PNG at `images/icon.png`.

---

## Licence

AGPL-3.0-or-later. Full text in `LICENSE`.

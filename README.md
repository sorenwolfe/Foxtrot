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
   https://raw.githubusercontent.com/sorenwolfe/XIVPlugins/main/repo.json
   ```

   That link carries all of my plugins, so you only ever add one. If you'd rather have just this
   one, `https://raw.githubusercontent.com/sorenwolfe/Foxtrot/main/repo.json` works too.

5. Click the **+** button next to the box.
6. Click **Save and Close**.
7. You're back at the plugin list. Type `foxtrot` in the search box. Click **Install**.

---

## What it does

**Right-click a roll, hear the roll.** Any orchestrion roll gets a **Preview** entry on its
right-click menu. Click it and a small player opens with the track playing.

**Including rolls you don't own.** The market board, a need/greed roll, a roll someone linked in
chat — anywhere the game will give you a right-click menu. That's the point: "is this worth
400,000 gil" and "do I need this over the tank" are questions you ask *before* the thing is yours,
and previewing only your own bags answered them a purchase too late.

**Browse everything.** `/foxtrot` opens a searchable list of every orchestrion track in the game.
Search by name or by what the description says, filter by category, and play any of them — whether
you own the roll or not. That's the point: the tracks worth hearing before you commit to hunting
one are exactly the ones you don't have yet.

**It knows what you've already got.** Rolls you've learned are dimmed, and there's a
**Not learned** filter that narrows the list to exactly the ones still out there — so the browser
answers "what should I go and find?" rather than just "what exists?".

**A small player.** One button that plays or stops depending on what's happening, a volume
slider separate from your game volume, and a star. It reads the game's own playback state, so if
someone's orchestrion is going it says so rather than offering to stop music that isn't yours. The
volume slider drives the game's orchestrion channel and hands it back exactly as it found it, so it
moves the preview and nothing else.

**Your zone music gets out of the way.** While a preview plays, the game's music fades down, and
it fades back the moment you stop — including if you unload the plugin halfway through a track.
Two tracks at once makes a preview impossible to judge. You can turn this off, or change how far
down it goes, in settings.

**It uses the game's own sampler.** Previews go through the same call the orchestrion in a house
makes when you audition a roll. So it sounds exactly like owning the roll would, respects your
audio setup, and the buttons reflect what the game is actually doing rather than what the plugin
last asked for.

**And it follows you.** That sampler is built for furniture, so the game plays it from a fixed spot
on the floor and fades it out as you walk away — correct for an orchestrion in a room, useless for
a preview. The sound is kept on top of your character instead, so it plays at full volume wherever
you go.

**It looks like RaidPlan.** Same dark panels, same accent colour, same soft shadows. One switch in
settings turns it off if you'd rather it matched your other plugins.

---

## Commands

| Type this | What happens |
|---|---|
| `/foxtrot` | Opens and closes the browser (`/orch` works too) |
| `/foxtrot player` | Opens and closes the small player |
| `/foxtrot stop` | Stops whatever is playing |
| `/foxtrot config` | Opens the settings |
| `/foxtrot diag` | Prints what it managed to read, for when something looks wrong |

---

## Known limits

**No seek bar, and no pause.** The game's sampler starts and stops; it offers no way to jump to a
point in a track or to hold one. Stop and play it again is the whole vocabulary.

**No right-click in the orchestrion list.** Rolls in your bags, on the market board, in a loot
roll and in chat links all work, because all of those are *items* and the game reports what the
cursor is over. Rows in the orchestrion list are songs rather than items, so nothing reports them,
and reading the selected row out of that window is not written. The browser plays anything
regardless — including rolls you don't own.

**Matching rolls to tracks is currently English-only.** A roll should be tied to its track through
the item's action data, which is exact and language-independent. On a live client that path
matches *nothing*, so every roll is currently found by stripping "Orchestrion Roll" off the end of
its name — which means no previews at all on a non-English client, and 86 rolls unmatched even on
an English one. `/foxtrot diag` now prints what that sheet actually holds so this can be fixed
from a reading rather than another guess.

---

## If something isn't working

**The Preview entry doesn't appear.** Right-click wherever it didn't appear, then run
`/foxtrot diag`. The last line says which of the three quite different things happened:

- *nothing hovered* — the game never reported an item there, so there was nothing to offer. That
  window doesn't show item tooltips, or the hover didn't register.
- *hovered item N, which is not a roll this can play* — the item was seen and isn't a roll, or is
  one the plugin failed to map to a track.
- *offered X* — it did appear, and the entry is there.

If the menu isn't mentioned at all, the game didn't raise one this plugin can see. The rest of
`diag` prints how many rolls were mapped and a few examples; nothing mapped is the bug worth
reporting.

**"Failed to update plugin Foxtrot (Load failed)."** Open `/xllog` and read the actual error
first — the wording matters:

- *"Distributed plugin version does not match repo version"* means `repo.json` was bumped but no
  release was tagged, so the download still holds the old build. Tag one:
  `git tag v0.4.2` then `git push --tags`. The **Release consistency** workflow catches this on push,
  before anyone sees it.
- Anything else is the plugin itself failing to start. Look for lines beginning `Foxtrot:` — it
  logs each stage of startup, so the last one printed says how far it got, and the exception after
  it is the cause. Restarting the game once clears anything a failed load left behind.

**The browser is empty.** Foxtrot reads the track list from the game's own data files at startup.
If it comes up empty, something went wrong reading them — the count is shown at the bottom of the
settings window, and there'll be a line in the Dalamud log.

**It crashed the game outright.** Twice, and both were mine. The second one, fixed in 0.4.2, had
nothing to do with the market board despite looking like it did: keeping the preview on top of you
read the camera through `CameraManager.Instance()->CurrentCamera`, the one place in this plugin
that followed a game pointer without checking it first — on a path that runs every frame a preview
is playing. It reads fine until it doesn't, so it crashed on whatever you happened to be doing at
the time. It now uses your character's position, which Dalamud hands over as a plain value and
which is simply absent during the loading screens where the camera was unsafe to touch. The test
suite now refuses any unchecked dereference anywhere in the plugin.

The first, in 0.4.0, was on the market board and was the same class of mistake.
Working out which item a right-click was aimed at read the game's own memory — the loot window's
agent, the market board's, the context menu's title string — and a bad read there raises an access
violation, which .NET does not hand to a `catch` block. The process ends with no exception and no
log line, which is why there was nothing to read afterwards. The guards I had written around those
reads were worth nothing. 0.4.1 takes the item from Dalamud's own managed tracking of what the
cursor is over, so that path now contains no pointers at all.

**My music is quiet after using it.** It shouldn't be. This did happen up to 0.3.1: the volume was
read back through the game's *effective* volume, which folds in your master slider, so with master
at 50% a full music bus read as half — and restoring wrote that half back. Every preview quietly
halved your music again. It now reads and writes the channel's own setting, which round-trips. If
you still manage it, please open an issue and say what you were doing.

---

## Working on it

The icon lives only in this repository, at `images/icon.png`. It is excluded from `git archive`
via `.gitattributes`, so a source archive can never carry a copy that overwrites it — that went
wrong once already.

Two things the build checks before it will pass:

- The icon is a square PNG no larger than 512×512. Dalamud rejects anything bigger and shows a
  "?" in the plugin list, with nothing anywhere to say why.
- The version in `Foxtrot.csproj` matches the one in `repo.json`. If they disagree, nobody is
  offered the update and nothing looks broken.

Separately, **Release consistency** compares what `repo.json` advertises against what the
published release actually contains. Bumping the version without tagging a release is the one
mistake that breaks installs for everyone, and it is invisible from this side — the list looks
right, the download link works, and only the player sees it fail.

It runs on push, after a release, and hourly, but only the last two can fail. Between the version
bump and the tag the repository is *meant* to look inconsistent, and going red there put a cross on
every correct release until the check was worth nothing to read.

### Releasing

1. Bump `<Version>` in `Foxtrot.csproj`.
2. Bump `AssemblyVersion` in `repo.json` to match.
3. Commit and push.
4. **Tag it**, or nothing is built: `git tag v0.4.2` then `git push --tags`.

---

## Licence

AGPL-3.0-or-later. Full text in `LICENSE`.

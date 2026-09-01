using System;
using System.Collections.Generic;
using System.Linq;
using Lumina.Excel.Sheets;

namespace Foxtrot.Services;

/// <summary>
/// The whole orchestrion list, read once from the game's own sheets.
/// </summary>
/// <remarks>
/// Read once at load rather than per frame: it is a few hundred rows that never change during a
/// session, and the browser filters over it on every keystroke.
/// </remarks>
public sealed class SongLibrary
{
    /// <summary>
    /// The Orchestrion Roll item category. Looked up by name, with this as the fallback.
    /// </summary>
    /// <remarks>
    /// The id has been stable for years, but a name lookup survives a renumbering and costs one
    /// pass over a small sheet at load.
    /// </remarks>
    public const uint FallbackRollCategory = 94;

    private readonly Dictionary<uint, Song> byId = new();
    private readonly Dictionary<uint, uint> songByItem = new();

    /// <summary>Track name to track, for right-clicks that arrive with a name and nothing else.</summary>
    private readonly Dictionary<string, uint> songByName = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>What the last load actually managed, so it can be reported rather than guessed at.</summary>
    public uint RollCategoryId { get; private set; }

    public int RollsMatchedByAction { get; private set; }

    /// <summary>Roll items that carry any item action at all, matched or not.</summary>
    /// <remarks>
    /// The count that decides whether tying a roll to its track through the action sheet is a
    /// fixable mistake or a dead end. If no roll has an action row, there is nothing there to read
    /// and the name is the only route the game offers.
    /// </remarks>
    public int RollsWithActionRow { get; private set; }

    public int RollsMatchedByName { get; private set; }

    /// <summary>Items that look like rolls but could not be tied to a track. Should be zero.</summary>
    public int RollsUnmatched { get; private set; }

    public int RollItemCount => songByItem.Count;

    public IReadOnlyCollection<Song> All => byId.Values;

    public IReadOnlyList<string> Categories { get; private set; } = Array.Empty<string>();

    public int Count => byId.Count;

    public bool TryGet(uint songId, out Song song) => byId.TryGetValue(songId, out song);

    /// <summary>The track a roll item plays, if that item is a roll at all.</summary>
    public bool TryGetByItem(uint itemId, out Song song)
    {
        song = default;
        return songByItem.TryGetValue(itemId, out var songId) && byId.TryGetValue(songId, out song);
    }

    public void Load()
    {
        byId.Clear();
        songByItem.Clear();
        songByName.Clear();

        try
        {
            LoadSongs();
            LoadRollItems();
            Categories = SongSearch.Categories(byId.Values);

            Plugin.Log.Information(
                $"Foxtrot: {byId.Count} track(s); {songByItem.Count} roll item(s) " +
                $"({RollsMatchedByAction} by action, {RollsMatchedByName} by name, " +
                $"{RollsUnmatched} unmatched); roll category {RollCategoryId}.");
        }
        catch (Exception ex)
        {
            // A missing sheet should leave an empty browser, not a plugin that will not load.
            Plugin.Log.Error(ex, "Could not read the orchestrion sheets.");
        }
    }

    private void LoadSongs()
    {
        var songs = Plugin.Data.GetExcelSheet<Orchestrion>();
        var paths = Plugin.Data.GetExcelSheet<OrchestrionPath>();
        var uiparams = Plugin.Data.GetExcelSheet<OrchestrionUiparam>();

        if (songs == null || paths == null)
            return;

        foreach (var row in songs)
        {
            var name = row.Name.ExtractText().Trim();
            if (name.Length == 0)
                continue;

            var file = string.Empty;
            if (paths.TryGetRow(row.RowId, out var path))
                file = path.File.ExtractText().Trim();

            // Row 0 is a blank placeholder and plenty of rows have no audio behind them.
            if (file.Length == 0)
                continue;

            var category = string.Empty;
            ushort order = 0;

            if (uiparams != null && uiparams.TryGetRow(row.RowId, out var ui))
            {
                order = ui.Order;
                if (ui.OrchestrionCategory.ValueNullable is { } cat)
                    category = cat.Name.ExtractText().Trim();
            }

            byId[row.RowId] = new Song(
                row.RowId,
                name,
                row.Description.ExtractText().Trim(),
                file,
                category,
                order);
        }
    }

    /// <summary>
    /// Maps roll items to the track they teach, so a right-click in a bag can find the music.
    /// </summary>
    /// <remarks>
    /// Two independent ways, because the first cannot be verified outside a running game. The
    /// item's action carries a track id in its first data slot — the right answer where it works.
    /// Failing that, roll items are named "&lt;Track&gt; Orchestrion Roll", and that is checkable.
    ///
    /// Nothing is examined unless it is a roll, by category or by name. That slot means different
    /// things for different kinds of item, so without the guard a consumable whose data happened
    /// to be 42 would offer to play track 42.
    /// </remarks>
    private void LoadRollItems()
    {
        var items = Plugin.Data.GetExcelSheet<Item>();
        if (items == null)
            return;

        RollCategoryId = FindRollCategory();
        RollsMatchedByAction = 0;
        RollsWithActionRow = 0;
        RollsMatchedByName = 0;
        RollsUnmatched = 0;

        foreach (var song in byId.Values)
            songByName.TryAdd(song.Name, song.Id);

        foreach (var item in items)
        {
            var name = item.Name.ExtractText().Trim();
            var inCategory = item.ItemUICategory.RowId == RollCategoryId;
            var namedLikeRoll = RollNames.LooksLikeRoll(name);

            if (!inCategory && !namedLikeRoll)
                continue;

            uint songId = 0;
            if (item.ItemAction.ValueNullable is { } action && action.Data.Count > 0)
            {
                if (action.RowId != 0)
                    RollsWithActionRow++;

                var candidate = (uint)action.Data[0];
                if (candidate != 0 && byId.ContainsKey(candidate))
                {
                    songId = candidate;
                    RollsMatchedByAction++;
                }
            }

            // Only when the action gave nothing usable. This is what covers the case where the
            // assumptions about that sheet turn out to be wrong.
            if (songId == 0 && namedLikeRoll)
            {
                var stem = RollNames.Stem(name);
                if (stem.Length > 0 && songByName.TryGetValue(stem, out var found))
                {
                    songId = found;
                    RollsMatchedByName++;
                }
            }

            if (songId != 0)
                songByItem[item.RowId] = songId;
            else
                RollsUnmatched++;
        }
    }

    /// <summary>
    /// What the item action sheet actually holds for a few rolls, for the diagnostics command.
    /// </summary>
    /// <remarks>
    /// Added because a live run reported 875 rolls matched by name and <em>none</em> by action —
    /// so the preferred path, the one that is supposed to be exact and language-independent, is
    /// matching nothing at all. Everything currently works through an English name suffix, which
    /// means a client in any other language gets no previews whatsoever, and the 86 rolls that
    /// matched neither way have no way back.
    ///
    /// Guessing at which field is really the track id from outside the game is how the wrong
    /// assumption got here in the first place. This prints what is actually in the sheet so the
    /// next attempt is based on a reading rather than another guess.
    /// </remarks>
    public IEnumerable<string> SampleRollActions(int count)
    {
        var items = Plugin.Data.GetExcelSheet<Item>();
        if (items == null)
            yield break;

        var shown = 0;

        // Rolls that were actually tied to a track. The first pass at this walked the sheet in
        // order and printed whatever looked like a roll by name, which turned out to be "Blank
        // Grade 1 Orchestrion Roll" and its siblings — crafting stock with no action and no track,
        // so every line said "unknown" and answered nothing. The interesting rows are the ones
        // that do have a track, because those are the ones the action data ought to have found.
        foreach (var itemId in songByItem.Keys)
        {
            if (shown >= count)
                yield break;

            if (!items.TryGetRow(itemId, out var item))
                continue;

            var name = item.Name.ExtractText().Trim();

            if (item.ItemAction.ValueNullable is not { } action || action.RowId == 0)
            {
                yield return $"{name}: no item action at all; matched by name to {songByItem[itemId]}.";
                shown++;
                continue;
            }

            var data = string.Join(", ", action.Data);
            yield return $"{name}: action row {action.RowId}, data [{data}]; " +
                         $"track is actually {songByItem[itemId]}.";
            shown++;
        }
    }

    /// <summary>A few real item-to-track pairs, for the diagnostics command.</summary>
    public IEnumerable<string> SampleMappings(int count)
    {
        var items = Plugin.Data.GetExcelSheet<Item>();

        foreach (var pair in songByItem.Take(count))
        {
            var itemName = items != null && items.TryGetRow(pair.Key, out var row)
                ? row.Name.ExtractText()
                : $"item {pair.Key}";

            var track = byId.TryGetValue(pair.Value, out var song) ? song.Name : $"track {pair.Value}";
            yield return $"{itemName} -> {track}";
        }
    }

    private static uint FindRollCategory()
    {
        try
        {
            var categories = Plugin.Data.GetExcelSheet<ItemUICategory>();
            if (categories == null)
                return FallbackRollCategory;

            foreach (var category in categories)
            {
                if (category.Name.ExtractText().Contains("Orchestrion", StringComparison.OrdinalIgnoreCase))
                    return category.RowId;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not find the orchestrion roll category; using the known id.");
        }

        return FallbackRollCategory;
    }
}

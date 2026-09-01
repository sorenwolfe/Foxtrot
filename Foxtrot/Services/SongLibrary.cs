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

    /// <summary>What the last load actually managed, so it can be reported rather than guessed at.</summary>
    public uint RollCategoryId { get; private set; }

    public int RollsMatchedByAction { get; private set; }

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
        RollsMatchedByName = 0;
        RollsUnmatched = 0;

        var byName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var song in byId.Values)
            byName.TryAdd(song.Name, song.Id);

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
                if (stem.Length > 0 && byName.TryGetValue(stem, out var found))
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

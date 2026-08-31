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

            Plugin.Log.Information($"Foxtrot: {byId.Count} orchestrion track(s), {songByItem.Count} roll item(s).");
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
    /// The item's action carries the track id in its first data slot. That slot means different
    /// things for different kinds of item, so this only trusts it for items in the Orchestrion
    /// Roll category — otherwise a random consumable whose data happens to be 42 would offer to
    /// play track 42.
    /// </remarks>
    private void LoadRollItems()
    {
        var items = Plugin.Data.GetExcelSheet<Item>();
        if (items == null)
            return;

        var rollCategory = FindRollCategory();

        foreach (var item in items)
        {
            if (item.ItemUICategory.RowId != rollCategory)
                continue;

            if (item.ItemAction.ValueNullable is not { } action)
                continue;

            if (action.Data.Count == 0)
                continue;

            var songId = (uint)action.Data[0];
            if (songId != 0 && byId.ContainsKey(songId))
                songByItem[item.RowId] = songId;
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

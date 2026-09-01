using System;
using Dalamud.Game.Gui.ContextMenu;
using Dalamud.Game.Text.SeStringHandling;

namespace Foxtrot.Services;

/// <summary>
/// Puts "Preview" on the right-click menu of anything that is an orchestrion roll.
/// </summary>
/// <remarks>
/// Two places matter: a roll sitting in your bags, and a row in the in-game orchestrion list.
/// Dalamud reports both through the same event, so this is one handler that asks two questions —
/// what did they right-click, and is it a track we can play.
/// </remarks>
public sealed class RollContextMenu : IDisposable
{
    /// <summary>The in-game orchestrion list. Its rows are songs, not items.</summary>
    private const string OrchestrionAddon = "MJIMinionNoteBook";

    /// <summary>The window the orchestrion list actually uses.</summary>
    private const string OrchestrionListAddon = "OrchestrionPlayList";

    private readonly SongLibrary library;
    private readonly Action<Song> onPreview;

    public RollContextMenu(SongLibrary library, Action<Song> onPreview)
    {
        this.library = library;
        this.onPreview = onPreview;

        Plugin.ContextMenu.OnMenuOpened += OnMenuOpened;
    }

    private void OnMenuOpened(IMenuOpenedArgs args)
    {
        try
        {
            if (!TryResolve(args, out var song))
                return;

            args.AddMenuItem(new MenuItem
            {
                Name = "Preview",
                PrefixChar = 'F',
                PrefixColor = 539,
                Priority = -1,
                OnClicked = _ => onPreview(song),
            });
        }
        catch (Exception ex)
        {
            // A throw here would take the game's context menu with it.
            Plugin.Log.Error(ex, "Could not add the preview menu item.");
        }
    }

    /// <summary>Works out which track, if any, the right-click was aimed at.</summary>
    private bool TryResolve(IMenuOpenedArgs args, out Song song)
    {
        song = default;

        if (args.Target is MenuTargetInventory inventory)
        {
            if (!Plugin.Config.ContextMenuOnItems)
                return false;

            if (inventory.TargetItem is not { } item)
                return false;

            // BaseItemId is the game's own answer with the high-quality and collectable offsets
            // already taken off, which is what the sheets are keyed by.
            if (library.TryGetByItem(item.BaseItemId, out song) && song.Playable)
                return true;

            // Worth a line when it declines: "no Preview appeared" is otherwise indistinguishable
            // from the plugin not running at all.
            Plugin.Log.Verbose($"Foxtrot: no track for item {item.BaseItemId}.");
            return false;
        }

        if (!Plugin.Config.ContextMenuOnOrchestrionList)
            return false;

        if (args.AddonName is not (OrchestrionListAddon or OrchestrionAddon))
            return false;

        // The list's rows are not items, so the selected track has to be read from the addon
        // itself. Left to the caller that knows how, so this stays testable.
        return OrchestrionListReader.TryGetSelected(args, library, out song) && song.Playable;
    }

    public void Dispose() => Plugin.ContextMenu.OnMenuOpened -= OnMenuOpened;
}

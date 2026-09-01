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
    private readonly HoveredItem hovered;

    public RollContextMenu(SongLibrary library, HoveredItem hovered, Action<Song> onPreview)
    {
        this.library = library;
        this.hovered = hovered;
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

        if (args.AddonName is OrchestrionListAddon or OrchestrionAddon)
        {
            if (!Plugin.Config.ContextMenuOnOrchestrionList)
                return false;

            // The list's rows are songs rather than items, so nothing hovered an item and the
            // selected row has to be read from the addon. Left to the caller that knows how.
            return OrchestrionListReader.TryGetSelected(args, library, out song) && song.Playable;
        }

        return TryResolveElsewhere(out song);
    }

    /// <summary>
    /// A roll right-clicked anywhere that is not your bags: the market board, a loot roll, a link
    /// in chat.
    /// </summary>
    /// <remarks>
    /// The point of previewing is to decide whether you want something, which means the moment
    /// that matters most is before you own it — looking at the market board price, or staring at
    /// a need/greed timer. Restricting this to items already in your bags answered the question
    /// only after it had stopped mattering.
    ///
    /// What was right-clicked is whatever the cursor was last over, which Dalamud tracks for us
    /// from the game's own tooltip code. That is one number and no pointers, and it covers every
    /// window that shows an item tooltip rather than the handful anybody thought to enumerate.
    ///
    /// The answer is still checked against the library, so a window this plugin has never heard of
    /// either resolves to a real track or offers nothing at all.
    /// </remarks>
    private bool TryResolveElsewhere(out Song song)
    {
        song = default;

        if (!Plugin.Config.ContextMenuAnywhere)
            return false;

        var itemId = hovered.Recent;
        if (itemId == 0)
            return false;

        return library.TryGetByItem(itemId, out song) && song.Playable;
    }

    public void Dispose() => Plugin.ContextMenu.OnMenuOpened -= OnMenuOpened;
}

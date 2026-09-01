using System;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Foxtrot.Services;

/// <summary>
/// Works out which item a right-click was aimed at, outside your bags.
/// </summary>
/// <remarks>
/// A roll in your bags arrives through a documented Dalamud API. Everywhere else — the market
/// board, a loot roll, an item linked in chat — the menu says nothing about what it is attached
/// to, so the item has to be read from the window that opened it.
///
/// Two routes, tried in that order. The precise one asks the window's own agent for the item id;
/// those fields are named in the game's mapped structures rather than guessed at, which is what
/// makes it safe to read them from a menu handler on the main thread. The general one falls back
/// to the title the game puts at the top of the menu, which for an item is its name.
///
/// The general route is what makes this work in windows nobody has thought about yet. The precise
/// route exists because a name is ambiguous in principle and an id never is.
/// </remarks>
public static class ContextItemReader
{
    /// <summary>The need/greed window that appears when a duty drops something.</summary>
    public const string LootAddon = "NeedGreed";

    /// <summary>The market board. Two windows, either of which can raise the menu.</summary>
    public const string MarketBoardAddon = "ItemSearch";

    /// <summary>The market board's results list.</summary>
    public const string MarketBoardResultAddon = "ItemSearchResult";

    /// <summary>
    /// Asks the window that opened the menu which item it is about. Zero means it could not say.
    /// </summary>
    public static uint ItemIdFrom(string? addonName)
    {
        try
        {
            unsafe
            {
                switch (addonName)
                {
                    case LootAddon:
                    {
                        var loot = AgentLoot.Instance();

                        // Hovered rather than selected: the right-click is on the row under the
                        // cursor, which is not necessarily the row that happens to be selected.
                        return loot == null ? 0 : loot->HoveredItemId;
                    }

                    case MarketBoardAddon or MarketBoardResultAddon:
                    {
                        var search = AgentItemSearch.Instance();
                        return search == null ? 0 : search->ResultItemId;
                    }

                    default:
                        return 0;
                }
            }
        }
        catch (Exception ex)
        {
            // Never worth taking the game's context menu down over.
            Plugin.Log.Warning(ex, $"Could not read the item behind the {addonName} menu.");
            return 0;
        }
    }

    /// <summary>
    /// The title the game put on the menu, which for an item context menu is the item's name.
    /// </summary>
    /// <remarks>
    /// Deliberately the last resort. It is the only thing available in windows this plugin knows
    /// nothing about, and it is how previewing works in places nobody has enumerated — but a name
    /// is a weaker claim than an id, so it is only consulted when no id could be had.
    /// </remarks>
    public static string Title()
    {
        try
        {
            unsafe
            {
                // Dalamud keeps its own handle on this agent private, so it is read from the
                // game's singleton instead. Same object, and there is only ever one menu open.
                var context = AgentContext.Instance();
                return context == null ? string.Empty : context->ContextMenuTitle.ToString();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not read the context menu title.");
            return string.Empty;
        }
    }
}

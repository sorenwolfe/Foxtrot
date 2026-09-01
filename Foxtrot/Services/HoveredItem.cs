using System;

namespace Foxtrot.Services;

/// <summary>
/// Remembers the last item the cursor was over, so a right-click knows what it was aimed at.
/// </summary>
/// <remarks>
/// The previous version read this out of the game's own agents — the loot window's, the market
/// board's, the context menu's own title string. That crashed the game outright on the market
/// board, and the guards around it were worth nothing: a bad pointer read raises an access
/// violation, which .NET does not deliver to a catch block. It takes the process down with no
/// exception, no log line, and nothing to read afterwards. Wrapping unsafe reads in try/catch buys
/// exactly nothing, which is the part I had wrong.
///
/// Dalamud already tracks this, from its own hook on the game's tooltip code, and hands it over as
/// a plain number. No pointers, so there is nothing left that can fault. It is also more general
/// than the per-window reads were: it is set for the market board, a need/greed roll, an item
/// linked in chat, a recipe ingredient — anywhere the game will show an item tooltip.
///
/// The value is remembered rather than read at the moment of the click, because opening the menu
/// dismisses the tooltip and the game clears it. What is kept is the last non-zero one, and only
/// for a few seconds, so a menu opened somewhere else entirely does not inherit a roll that was
/// hovered a minute ago.
/// </remarks>
public sealed class HoveredItem : IDisposable
{
    /// <summary>
    /// How long a hovered item stays relevant, in milliseconds.
    /// </summary>
    /// <remarks>
    /// Long enough to cover hover, right-click and the menu opening; short enough that an
    /// unrelated menu later does not still think a roll is in play.
    /// </remarks>
    private const long RelevantForMilliseconds = 5_000;

    private ulong lastItemId;
    private long lastSeenAt;

    public HoveredItem() => Plugin.GameGui.HoveredItemChanged += OnHoveredItemChanged;

    /// <summary>The item most recently hovered, or zero if that was too long ago.</summary>
    public uint Recent => Relevant(lastItemId, Environment.TickCount64 - lastSeenAt);

    /// <summary>
    /// Whether a remembered item still describes what was just right-clicked.
    /// </summary>
    /// <remarks>
    /// Separated from the clock so the rules can be tested. Zero in is zero out: a menu that
    /// followed no hover at all must resolve to nothing rather than to whatever was last seen.
    /// </remarks>
    public static uint Relevant(ulong itemId, long ageMilliseconds)
    {
        if (itemId == 0 || ageMilliseconds > RelevantForMilliseconds || ageMilliseconds < 0)
            return 0;

        // High-quality items are the same item offset by a million. Rolls are never HQ, but
        // taking it off costs nothing and makes this correct for anything else that asks.
        return (uint)(itemId % 1_000_000);
    }

    /// <summary>The raw value and its age, for the diagnostics command.</summary>
    public string Describe() =>
        lastItemId == 0
            ? "nothing hovered yet"
            : $"item {lastItemId}, {(Environment.TickCount64 - lastSeenAt) / 1000}s ago";

    private void OnHoveredItemChanged(object? sender, ulong itemId)
    {
        // Zero means the tooltip went away, which is exactly what opening a context menu does. If
        // that cleared what we remembered, the right-click would arrive with nothing to go on.
        if (itemId == 0)
            return;

        lastItemId = itemId;
        lastSeenAt = Environment.TickCount64;
    }

    public void Dispose() => Plugin.GameGui.HoveredItemChanged -= OnHoveredItemChanged;
}

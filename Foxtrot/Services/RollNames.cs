using System;
using System.Collections.Generic;

namespace Foxtrot.Services;

/// <summary>
/// Recognises a roll item by its name.
/// </summary>
/// <remarks>
/// A second, independent way of matching a roll to its track. The first way reads the item's
/// action data, which is the right answer when it works but rests on assumptions about a sheet
/// this plugin cannot check without the game. Roll items are named "&lt;Track&gt; Orchestrion
/// Roll", and that is checkable here, so when the action data comes up empty the name is asked
/// instead.
///
/// English only, deliberately. A wrong guess in another language would silently map a roll to the
/// wrong track; the action-data path still covers those, and what it misses is counted and
/// reported rather than papered over.
/// </remarks>
public static class RollNames
{
    private const string Suffix = "Orchestrion Roll";

    /// <summary>Whether this item name looks like an orchestrion roll at all.</summary>
    public static bool LooksLikeRoll(string? itemName) =>
        itemName != null && itemName.TrimEnd().EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The track name inside a roll's item name, or empty if it does not look like one.
    /// </summary>
    /// <remarks>
    /// "Sunrise Orchestrion Roll" gives "Sunrise". The suffix on its own gives nothing rather than
    /// an empty-named match against whatever track happens to have a blank name.
    /// </remarks>
    public static string Stem(string? itemName)
    {
        if (!LooksLikeRoll(itemName))
            return string.Empty;

        var trimmed = itemName!.TrimEnd();
        return trimmed[..^Suffix.Length].Trim();
    }

    /// <summary>
    /// The track names to try for a piece of text, best first.
    /// </summary>
    /// <remarks>
    /// A right-click outside your bags arrives with a name and nothing else, and the name can be
    /// either shape: "A Cold Wind Orchestrion Roll" is the item, "A Cold Wind" is the track. The
    /// stem goes first because an item name *contains* a track name — trying the whole string
    /// first happens to work for most rolls and quietly picks the wrong track for any whose full
    /// item name is also a real track name.
    ///
    /// Nothing is yielded for blank text. A window that named nothing must match nothing, or the
    /// plugin ends up offering to preview a menu heading.
    /// </remarks>
    public static IEnumerable<string> Candidates(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var trimmed = text.Trim();

        var stem = Stem(trimmed);
        if (stem.Length > 0)
            yield return stem;

        if (!string.Equals(stem, trimmed, StringComparison.Ordinal))
            yield return trimmed;
    }
}

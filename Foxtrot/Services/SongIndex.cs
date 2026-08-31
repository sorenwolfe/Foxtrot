using System;
using System.Collections.Generic;
using System.Linq;

namespace Foxtrot.Services;

/// <summary>One orchestrion track, as far as this plugin cares.</summary>
public readonly record struct Song(
    uint Id,
    string Name,
    string Description,
    string FilePath,
    string Category,
    ushort Order)
{
    /// <summary>A track with no audio file cannot be previewed, whatever else the sheets say.</summary>
    public bool Playable => FilePath.Length > 0;
}

/// <summary>Whether the list is narrowed to what you have, or what you have not.</summary>
public enum OwnedFilter
{
    Any,
    Owned,
    NotOwned,
}

/// <summary>
/// Searching and ordering the track list.
/// </summary>
/// <remarks>
/// Kept apart from the sheet loading so it can be exercised without the game's data files. The
/// searching is the part with rules in it; reading rows is not.
/// </remarks>
public static class SongSearch
{
    /// <summary>
    /// Tracks matching a typed query, best first.
    /// </summary>
    /// <remarks>
    /// Matches on name first and description second, because someone typing "shadowbringers" wants
    /// the song called that above the twenty whose blurb mentions it. Within a rank the game's own
    /// ordering is kept, so a browse with an empty box looks like the in-game list rather than a
    /// shuffle.
    /// </remarks>
    /// <param name="owns">
    /// Whether this character has learned a track. Null means unknown, in which case the ownership
    /// filter is ignored rather than guessed at — an empty list because the game could not be read
    /// looks exactly like owning nothing.
    /// </param>
    public static List<Song> Filter(
        IEnumerable<Song> songs,
        string? query,
        string? category,
        OwnedFilter owned = OwnedFilter.Any,
        Func<uint, bool>? owns = null)
    {
        var text = (query ?? string.Empty).Trim();
        var wanted = (category ?? string.Empty).Trim();

        var pool = songs.Where(s => s.Playable);

        if (wanted.Length > 0)
            pool = pool.Where(s => s.Category.Equals(wanted, StringComparison.OrdinalIgnoreCase));

        if (owned != OwnedFilter.Any && owns != null)
            pool = pool.Where(s => owns(s.Id) == (owned == OwnedFilter.Owned));

        if (text.Length == 0)
            return pool.OrderBy(s => s.Order).ThenBy(s => s.Id).ToList();

        return pool
            .Select(s => (Song: s, Rank: Rank(s, text)))
            .Where(x => x.Rank < int.MaxValue)
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.Song.Order)
            .ThenBy(x => x.Song.Id)
            .Select(x => x.Song)
            .ToList();
    }

    /// <summary>Lower is a better match. MaxValue means it does not match at all.</summary>
    private static int Rank(Song song, string query)
    {
        var name = song.Name;

        if (name.Equals(query, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 1;

        if (name.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 2;

        if (song.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 3;

        return int.MaxValue;
    }

    /// <summary>Category names in the game's own order, for the filter dropdown.</summary>
    public static List<string> Categories(IEnumerable<Song> songs) =>
        songs.Where(s => s.Playable && s.Category.Length > 0)
            .GroupBy(s => s.Category)
            .OrderBy(g => g.Min(s => s.Order))
            .Select(g => g.Key)
            .ToList();
}

/// <summary>Turns elapsed seconds into something readable next to a play button.</summary>
public static class TrackTime
{
    public static string Format(float seconds)
    {
        if (float.IsNaN(seconds) || seconds < 0f)
            seconds = 0f;

        var total = (int)seconds;
        return $"{total / 60}:{total % 60:00}";
    }
}

using System;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Foxtrot.Services;

/// <summary>Which rolls this character has actually learned.</summary>
/// <remarks>
/// The whole reason to browse tracks you cannot play from your bags is to decide what to go and
/// earn, and that question is unanswerable without knowing what you already have. A read-only bit
/// test against the character's unlock state — the same thing the game's own orchestrion list
/// greys out with.
/// </remarks>
public sealed class RollOwnership
{
    /// <summary>
    /// How long an answer is reused.
    /// </summary>
    /// <remarks>
    /// Unlocks change when you use a roll, which is rare, but the browser asks this once per
    /// visible row per frame. A short cache turns a few hundred game reads a second into a few.
    /// </remarks>
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(2);

    private readonly System.Collections.Generic.Dictionary<uint, bool> cache = new();
    private DateTime freshUntil = DateTime.MinValue;

    /// <summary>False when the unlock state cannot be read at all, so nothing is claimed.</summary>
    public bool Available { get; private set; } = true;

    /// <summary>Throws the cache away. Called when a roll is used, or on demand.</summary>
    public void Forget()
    {
        cache.Clear();
        freshUntil = DateTime.MinValue;
    }

    public bool Owns(uint songId)
    {
        if (songId == 0)
            return false;

        var now = DateTime.UtcNow;
        if (now > freshUntil)
        {
            cache.Clear();
            freshUntil = now + Lifetime;
        }

        if (cache.TryGetValue(songId, out var known))
            return known;

        var owned = Read(songId);
        cache[songId] = owned;
        return owned;
    }

    private bool Read(uint songId)
    {
        try
        {
            unsafe
            {
                var state = PlayerState.Instance();
                if (state == null)
                {
                    // Not logged in yet. Not a failure, just nothing to say.
                    return false;
                }

                return state->IsOrchestrionRollUnlocked(songId);
            }
        }
        catch (Exception ex)
        {
            // Game memory. If this ever stops working the browser should lose a column, not crash.
            Plugin.Log.Warning(ex, "Could not read orchestrion unlock state.");
            Available = false;
            return false;
        }
    }
}

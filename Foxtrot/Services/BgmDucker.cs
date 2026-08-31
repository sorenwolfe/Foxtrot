using System;

namespace Foxtrot.Services;

/// <summary>
/// The volume knob the ducker turns, kept behind an interface so its rules can be tested.
/// </summary>
/// <remarks>
/// The real one writes to the game's own music bus. That cannot be reached outside the game, and
/// the rules around it — restore exactly what was there, never stack two ducks, never leave the
/// music down — are exactly the part worth being sure of.
/// </remarks>
public interface IMusicBus
{
    float Volume { get; set; }
}

/// <summary>
/// Fades the game's music down while a preview plays, and puts it back afterwards.
/// </summary>
/// <remarks>
/// The one thing this must never do is leave someone's music turned down. A player whose game has
/// gone quiet has no reason to suspect a preview plugin, will not find the setting, and will
/// reasonably conclude the game is broken. So the original volume is captured once on the way
/// down, restoring is safe to call at any time from any state, and unload restores unconditionally
/// — including when the game is mid-preview.
/// </remarks>
public sealed class BgmDucker : IDisposable
{
    private readonly IMusicBus bus;

    private bool ducked;
    private float restoreTo;

    public BgmDucker(IMusicBus bus) => this.bus = bus;

    /// <summary>True while the game's music is being held down for a preview.</summary>
    public bool IsDucked => ducked;

    /// <summary>The volume that will be put back. Only meaningful while ducked.</summary>
    public float RestoreVolume => restoreTo;

    /// <summary>
    /// Takes the music down to <paramref name="target"/>, remembering where it was.
    /// </summary>
    /// <remarks>
    /// Calling this twice does not capture twice. Without that guard the second call would record
    /// the already-ducked volume as the original and restoring would put the music back to the
    /// ducked level — quietly, permanently, and looking exactly like the plugin working.
    /// </remarks>
    public void Duck(float target)
    {
        if (ducked)
        {
            bus.Volume = Math.Clamp(target, 0f, 1f);
            return;
        }

        restoreTo = Math.Clamp(bus.Volume, 0f, 1f);
        ducked = true;
        bus.Volume = Math.Clamp(target, 0f, 1f);
    }

    /// <summary>Puts the music back. Safe to call when nothing was ducked.</summary>
    public void Restore()
    {
        if (!ducked)
            return;

        ducked = false;
        bus.Volume = restoreTo;
    }

    /// <summary>
    /// Notes that the player changed the music volume themselves while a preview was running.
    /// </summary>
    /// <remarks>
    /// Otherwise restoring would undo their change and put the music back where the plugin found
    /// it ten minutes ago. Their choice wins: the duck is abandoned rather than fought.
    /// </remarks>
    public void Forget() => ducked = false;

    public void Dispose() => Restore();
}

using System;

namespace Foxtrot.Services;

/// <summary>
/// The thing the buttons drive: what is playing, how loud, and stopping it.
/// </summary>
/// <remarks>
/// Every path out of playing — the button, a new track, the track ending by itself, the plugin
/// unloading — goes through <see cref="Stop"/>, so there is exactly one place that has to remember
/// to give the player their music back. Spreading that over four call sites is how someone ends up
/// with a permanently quiet game.
/// </remarks>
public sealed class PreviewPlayer : IDisposable
{
    private readonly BgmDucker ducker;

    private PlayingSound? sound;

    public PreviewPlayer(BgmDucker ducker) => this.ducker = ducker;

    /// <summary>The track loaded into the player, playing or not.</summary>
    public Song? Current { get; private set; }

    public bool IsPlaying => sound?.IsPlaying == true;

    /// <summary>Seconds into the track.</summary>
    public float Elapsed => sound?.Elapsed ?? 0f;

    /// <summary>Set when the game refused to start a track, so the window can say so.</summary>
    public string LastError { get; private set; } = string.Empty;

    public void Play(Song song)
    {
        // Whatever was playing goes first, including its duck. Starting a second track over the
        // top of the first would leave the first one's handle unreachable and unstoppable.
        Stop();

        if (!song.Playable)
        {
            LastError = "There is no music behind that roll.";
            return;
        }

        Current = song;
        LastError = string.Empty;

        if (Plugin.Config.DuckGameMusic)
            ducker.Duck(Plugin.Config.DuckedMusicVolume);

        sound = OrchestrionAudio.Play(song.FilePath, Plugin.Config.PreviewVolume);

        if (sound != null)
            return;

        // Nothing started, so nothing should stay ducked.
        LastError = "The game would not start that track.";
        ducker.Restore();
    }

    public void SetVolume(float volume) => sound?.SetVolume(volume);

    /// <summary>
    /// The closest the game gives us to a pause. Unverified, and stopping is the honest fallback.
    /// </summary>
    public void TryHold(bool held) => sound?.SetSpeed(held ? 0f : 1f);

    public void Stop()
    {
        sound?.Stop();
        sound = null;
        ducker.Restore();
    }

    /// <summary>
    /// Notices a track that finished on its own, so the music comes back without a button press.
    /// </summary>
    /// <remarks>
    /// Called every frame the window is up. Without it, a track that runs to its end leaves the
    /// game's music ducked until somebody happens to press stop — the exact failure this whole
    /// class is arranged to prevent, arriving by the one route that needs no mistake to reach.
    /// </remarks>
    public void Poll()
    {
        if (sound == null)
            return;

        if (!sound.IsPlaying)
            Stop();
    }

    public void Dispose() => Stop();
}

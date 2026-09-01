using System;

namespace Foxtrot.Services;

/// <summary>
/// The thing the buttons drive: what is loaded, whether it is sounding, and stopping it.
/// </summary>
/// <remarks>
/// Whether something is playing is asked of the game rather than remembered here. The previous
/// version kept its own flag, so a preview that never started still lit the Stop button and a
/// track that ended on its own still looked like it was playing — the buttons described this
/// plugin's intentions rather than the game's behaviour.
///
/// Every path out of playing still goes through <see cref="Stop"/>, so there is exactly one place
/// that has to remember to give the player their music back.
/// </remarks>
public sealed class PreviewPlayer : IDisposable
{
    private readonly BgmDucker ducker;

    private bool ours;

    public PreviewPlayer(BgmDucker ducker) => this.ducker = ducker;

    /// <summary>The track loaded into the player, sounding or not.</summary>
    public Song? Current { get; private set; }

    /// <summary>
    /// True while this plugin's own preview is sounding.
    /// </summary>
    /// <remarks>
    /// Someone else's orchestrion playing is deliberately not "playing" here: the Stop button
    /// must not offer to stop music this plugin did not start.
    /// </remarks>
    public bool IsPlaying => ours && OrchestrionSampler.State == SamplerState.Sampling;

    /// <summary>Set when the game would not start a track, so the window can say so.</summary>
    public string LastError { get; private set; } = string.Empty;

    public void Play(Song song)
    {
        // Whatever was sounding goes first, including its duck.
        Stop();

        Current = song;
        LastError = string.Empty;

        if (Plugin.Config.DuckGameMusic)
            ducker.Duck(Plugin.Config.DuckedMusicVolume);

        if (!OrchestrionSampler.Play(song.Id))
        {
            LastError = "The game would not start that track.";
            ducker.Restore();
            return;
        }

        ours = true;
    }

    public void Stop()
    {
        if (ours)
            OrchestrionSampler.Stop();

        ours = false;
        ducker.Restore();
    }

    /// <summary>
    /// Notices a track that stopped without us, so the music comes back without a button press.
    /// </summary>
    /// <remarks>
    /// Called every frame a window is up. Without it, a preview that ends on its own leaves the
    /// game's music ducked until somebody happens to press stop — the exact failure this is all
    /// arranged to prevent, arriving by the one route that needs no mistake to reach.
    /// </remarks>
    public void Poll()
    {
        if (!ours)
            return;

        // Unknown means the game could not be asked this frame, which is not the same as silent.
        // Treating it as stopped would cut a preview short on a hiccup.
        if (OrchestrionSampler.State == SamplerState.Silent)
            Stop();
    }

    public void Dispose() => Stop();
}

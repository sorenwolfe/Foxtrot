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
    private readonly BgmDucker volume;

    private bool ours;

    public PreviewPlayer(BgmDucker ducker, BgmDucker volume)
    {
        this.ducker = ducker;
        this.volume = volume;
    }

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

        // The orchestrion bus is held at the preview volume for as long as the preview runs, and
        // handed back untouched afterwards. Holding rather than setting is what stops the plugin
        // from permanently rewriting a slider the player never asked it to touch.
        volume.Duck(Plugin.Config.PreviewVolume);

        if (!OrchestrionSampler.Play(song.Id))
        {
            LastError = "The game would not start that track.";
            volume.Restore();
            ducker.Restore();
            return;
        }

        ours = true;

        // Before the first frame, or the track begins at whatever spot the game had left in the
        // sampler and audibly slides into place.
        OrchestrionEmitter.PinToListener();
    }

    /// <summary>
    /// Moves the preview volume while it is playing, without disturbing what gets restored.
    /// </summary>
    /// <remarks>
    /// The slider used to write to the settings and stop there. Nothing read it back, so it was a
    /// number in a file that happened to be drawn on screen.
    /// </remarks>
    public void SetVolume(float level)
    {
        Plugin.Config.PreviewVolume = Math.Clamp(level, 0f, 1f);

        if (volume.IsDucked)
            volume.Duck(Plugin.Config.PreviewVolume);
    }

    public void Stop()
    {
        if (ours)
            OrchestrionSampler.Stop();

        ours = false;
        volume.Restore();
        ducker.Restore();
    }

    /// <summary>
    /// Keeps the preview where the listener is, and notices a track that stopped without us.
    /// </summary>
    /// <remarks>
    /// Called every frame, from the game's update rather than from drawing — a preview outlives
    /// the window it was started from, and tying this to drawing meant closing the window left the
    /// music ducked and the emitter stranded until somebody happened to open it again.
    /// </remarks>
    public void Poll()
    {
        if (!ours)
            return;

        // Unknown means the game could not be asked this frame, which is not the same as silent.
        // Treating it as stopped would cut a preview short on a hiccup.
        if (OrchestrionSampler.State == SamplerState.Silent)
        {
            Stop();
            return;
        }

        OrchestrionEmitter.PinToListener();
    }

    public void Dispose() => Stop();
}

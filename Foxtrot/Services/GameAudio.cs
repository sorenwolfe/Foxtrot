using System;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Sound;

namespace Foxtrot.Services;

/// <summary>The game's own music bus.</summary>
public sealed class GameMusicBus : IMusicBus
{
    public float Volume
    {
        get
        {
            try
            {
                unsafe
                {
                    var manager = SoundManager.Instance();
                    return manager == null ? 1f : manager->GetEffectiveVolume(SoundBus.Music);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not read the music volume.");
                return 1f;
            }
        }

        set
        {
            try
            {
                unsafe
                {
                    var manager = SoundManager.Instance();
                    if (manager == null)
                        return;

                    // The last argument is a fade in milliseconds. A short one stops the duck
                    // sounding like the music was cut off.
                    manager->SetVolume(SoundBus.Music, Math.Clamp(value, 0f, 1f), 250);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not set the music volume.");
            }
        }
    }
}

/// <summary>What the game says it is doing with the orchestrion right now.</summary>
public enum SamplerState
{
    /// <summary>The game could not be asked. Not the same as silent.</summary>
    Unknown,

    Silent,

    /// <summary>Previewing a track, which is what this plugin asks for.</summary>
    Sampling,

    /// <summary>Playing properly, which is somebody's actual orchestrion rather than us.</summary>
    Playing,
}

/// <summary>
/// Previews a track through the game's own orchestrion sampler.
/// </summary>
/// <remarks>
/// This is the call the orchestrion in a house makes when you audition a roll, and it takes a
/// track id rather than a file path. Driving sound through the file path directly did not play
/// anything at all, which is unsurprising in hindsight: it skipped whatever setup the game does
/// around a sample, and there was no way to tell from the outside whether it had worked.
///
/// The state comes back from the game too, so the buttons reflect what is actually happening
/// rather than what this plugin last asked for.
/// </remarks>
public static class OrchestrionSampler
{
    public static SamplerState State
    {
        get
        {
            try
            {
                unsafe
                {
                    var manager = OrchestrionManager.Instance();
                    if (manager == null)
                        return SamplerState.Unknown;

                    return manager->Mode switch
                    {
                        OrchestrionMode.Sampling => SamplerState.Sampling,
                        OrchestrionMode.Playing or OrchestrionMode.Looping => SamplerState.Playing,
                        _ => SamplerState.Silent,
                    };
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not read the orchestrion state.");
                return SamplerState.Unknown;
            }
        }
    }

    /// <summary>The track the game currently has loaded, or zero.</summary>
    public static ushort CurrentTrack
    {
        get
        {
            try
            {
                unsafe
                {
                    var manager = OrchestrionManager.Instance();
                    return manager == null ? (ushort)0 : manager->TrackId;
                }
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>Starts a preview. False means the game would not take the request at all.</summary>
    public static bool Play(uint songId)
    {
        if (songId is 0 or > ushort.MaxValue)
            return false;

        try
        {
            // Static on the game's side: sampling needs no instance, only a track id.
            OrchestrionManager.PlaySample((ushort)songId);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not start the preview.");
            return false;
        }
    }

    public static void Stop()
    {
        try
        {
            OrchestrionManager.StopSample();

            // Only ours. A real orchestrion playing is somebody's furniture doing its job, and
            // stopping it would be reaching well past what this plugin was asked to do.
            if (State == SamplerState.Sampling)
                OrchestrionManager.StopCurrentTrack();
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not stop the preview.");
        }
    }
}

using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Sound;

namespace Foxtrot.Services;

/// <summary>One of the game's volume sliders, read and written the way the game stores it.</summary>
/// <remarks>
/// Reading had been going through <c>GetEffectiveVolume</c>, which folds in the master volume:
/// with master at 50% a bus sitting at 1.0 reads back as 0.5, and restoring it writes 0.5 into the
/// bus. Every preview halved the player's music again, permanently, and the plugin looked like it
/// was putting things back exactly. The raw per-bus array is what a restore has to round-trip.
/// </remarks>
public sealed class SoundBusVolume(SoundBus bus) : IMusicBus
{
    /// <summary>How long the game takes to move the slider, in milliseconds.</summary>
    /// <remarks>Instant changes sound like the audio glitched rather than faded.</remarks>
    private const int FadeMilliseconds = 250;

    /// <summary>
    /// Picks the value a restore has to round-trip: the bus's own setting, not the audible one.
    /// </summary>
    /// <remarks>
    /// Separated out because the array is the part that could shift under a game patch, and
    /// reading one slot past the end of it inside the sound manager is not a mistake that
    /// announces itself. The fallback is audibly wrong to restore, and still much better than
    /// whatever happens to sit in the next few bytes of memory.
    /// </remarks>
    public static float Select(ReadOnlySpan<float> volumes, int slot, float fallback) =>
        slot >= 0 && slot < volumes.Length
            ? Math.Clamp(volumes[slot], 0f, 1f)
            : Math.Clamp(fallback, 0f, 1f);

    public float Volume
    {
        get
        {
            try
            {
                unsafe
                {
                    var manager = SoundManager.Instance();
                    if (manager == null)
                        return 1f;

                    return Select(manager->Volume, (int)bus, manager->GetEffectiveVolume(bus));
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Could not read the {bus} volume.");
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

                    manager->SetVolume(bus, Math.Clamp(value, 0f, 1f), FadeMilliseconds);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, $"Could not set the {bus} volume.");
            }
        }
    }
}

/// <summary>The game's own music bus, which is what gets ducked under a preview.</summary>
public sealed class GameMusicBus : IMusicBus
{
    private readonly SoundBusVolume inner = new(SoundBus.Music);

    public float Volume
    {
        get => inner.Volume;
        set => inner.Volume = value;
    }
}

/// <summary>
/// The orchestrion's own bus, which is the one a preview actually comes out of.
/// </summary>
/// <remarks>
/// The volume slider had nothing behind it. It stored a number in the settings file and no code
/// ever read that number back, so moving it did exactly nothing — correctly saved, correctly
/// reloaded, and inaudible. The game keeps a separate bus for orchestrion audio, which is
/// precisely the right knob: it moves the preview without touching anything else.
/// </remarks>
public sealed class GameOrchestrionBus : IMusicBus
{
    private readonly SoundBusVolume inner = new(SoundBus.Orchestrion);

    public float Volume
    {
        get => inner.Volume;
        set => inner.Volume = value;
    }
}

/// <summary>
/// Keeps the preview from fading out as the player walks away from where they started it.
/// </summary>
/// <remarks>
/// The sampler is built for furniture. A real orchestrion sits in a room and is supposed to get
/// quieter as you leave it, so the game emits the sample from a fixed world position and pans and
/// attenuates it from there. For a preview that behaviour is simply wrong — the track is coming
/// from a window on your screen, not from a spot on the floor.
///
/// Rather than fight the 3D audio, the emitter is moved onto the listener every frame, so the
/// distance is always zero and there is nothing to attenuate. The listener in this game is the
/// camera, which is why sound thins out when you zoom out.
/// </remarks>
public static class OrchestrionEmitter
{
    /// <summary>Puts the emitter on the listener. False means the game could not be asked.</summary>
    public static bool PinToListener()
    {
        try
        {
            unsafe
            {
                var camera = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CameraManager
                    .Instance()->CurrentCamera;

                if (camera == null)
                    return false;

                return MoveTo(camera->Position);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not follow the listener with the preview.");
            return false;
        }
    }

    /// <summary>
    /// Writes the emitter position everywhere the game reads it from.
    /// </summary>
    /// <remarks>
    /// Two copies exist: the sampler's own state, and the sound manager's working position. Which
    /// one wins depends on when the mixer next looks, so both are set rather than guessing.
    /// </remarks>
    private static unsafe bool MoveTo(Vector3 position)
    {
        var moved = false;

        var sample = OrchestrionSampleState.Instance();
        if (sample != null)
        {
            sample->Position = position;
            moved = true;
        }

        var sound = SoundManager.Instance();
        if (sound != null)
        {
            sound->OrchestrionPosX = position.X;
            sound->OrchestrionPosY = position.Y;
            sound->OrchestrionPosZ = position.Z;
            moved = true;
        }

        return moved;
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

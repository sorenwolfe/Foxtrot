using System;
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

/// <summary>
/// One playing preview. Wraps the game's own sound handle so nothing else has to touch a pointer.
/// </summary>
/// <remarks>
/// The handle belongs to the game and can be recycled once the track finishes, so nothing here
/// holds onto it past <see cref="IsPlaying"/> going false, and every read is guarded. A stale
/// pointer read in a UI draw is a crash in the middle of someone's raid.
/// </remarks>
public sealed unsafe class PlayingSound
{
    private SoundData* handle;

    internal PlayingSound(SoundData* handle) => this.handle = handle;

    public bool IsPlaying
    {
        get
        {
            try
            {
                return handle != null && handle->IsPlaying();
            }
            catch
            {
                handle = null;
                return false;
            }
        }
    }

    /// <summary>Seconds since the track started. There is no way to set it, so no seeking.</summary>
    public float Elapsed
    {
        get
        {
            try
            {
                return handle == null ? 0f : handle->GetElapsedTime();
            }
            catch
            {
                return 0f;
            }
        }
    }

    public void SetVolume(float volume)
    {
        try
        {
            if (handle != null)
                handle->SetVolume(Math.Clamp(volume, 0f, 1f), 0);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not set the preview volume.");
        }
    }

    /// <summary>
    /// Speed, which is also the closest thing the game gives us to a pause.
    /// </summary>
    /// <remarks>
    /// There is no Pause on the game's sound handle. Setting the speed to zero is the only
    /// candidate and it is not what the field was built for, so whether it holds the track still
    /// or does something stranger is not something we can promise. The player treats it as a
    /// nicety and falls back to stopping.
    /// </remarks>
    public void SetSpeed(float speed)
    {
        try
        {
            if (handle != null)
                handle->SetSpeed(Math.Clamp(speed, 0f, 4f), 0);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not set the playback speed.");
        }
    }

    public void Stop(uint fadeMs = 200)
    {
        try
        {
            if (handle != null)
                handle->Stop(fadeMs);
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Could not stop the preview.");
        }
        finally
        {
            handle = null;
        }
    }
}

/// <summary>Starts orchestrion tracks through the game's own orchestrion playback.</summary>
/// <remarks>
/// Not a bespoke audio pipeline: this is the same call the in-game orchestrion makes, so a preview
/// goes through the player's own audio setup, on the game's own orchestrion channel, and sounds
/// exactly like owning the roll would.
/// </remarks>
public static class OrchestrionAudio
{
    public static PlayingSound? Play(string filePath, float volume)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        try
        {
            unsafe
            {
                var manager = SoundManager.Instance();
                if (manager == null)
                    return null;

                var sound = manager->PlayOrchestrionSound(
                    filePath,
                    Math.Clamp(volume, 0f, 1f),
                    0f,
                    0f,
                    false);

                return sound == null ? null : new PlayingSound(sound);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Could not start the preview.");
            return null;
        }
    }
}

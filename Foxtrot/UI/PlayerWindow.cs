using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Foxtrot.Services;

namespace Foxtrot.UI;

/// <summary>
/// The little player that appears when you preview something.
/// </summary>
/// <remarks>
/// Small on purpose. It exists to answer one question — what is this track — so it carries the
/// name, a stop button, a volume slider and a running time, and nothing else. Anything more and it
/// stops being something you can leave open in a corner.
/// </remarks>
public sealed class PlayerWindow : Window, IDisposable
{
    private readonly PreviewPlayer player;

    private bool held;

    public PlayerWindow(PreviewPlayer player)
        : base("Foxtrot###foxtrot-player",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.player = player;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 0),
            MaximumSize = new Vector2(640, 400),
        };
    }

    public override void Draw()
    {
        // The track may have ended since the last frame; noticing here is what gives the game's
        // music back without anyone pressing anything.
        player.Poll();

        var song = player.Current;
        if (song is not { } current)
        {
            ImGui.TextDisabled("Nothing loaded.");
            ImGui.TextWrapped("Right-click an orchestrion roll in your bags and choose Preview, or open the browser.");

            if (ImGui.Button("Browse all tracks", Vector2.Zero))
                Plugin.Browser.IsOpen = true;

            return;
        }

        ImGui.TextUnformatted(current.Name);

        if (current.Category.Length > 0)
        {
            ImGui.SameLine();
            ImGui.TextDisabled("(" + current.Category + ")");
        }

        if (current.Description.Length > 0 && ImGui.IsItemHovered())
            ImGui.SetTooltip(current.Description);

        ImGui.Separator();

        var playing = player.IsPlaying;

        if (playing)
        {
            if (ImGui.Button(held ? "Resume" : "Hold", new Vector2(72, 0)))
            {
                held = !held;
                player.TryHold(held);
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(
                    "The game has no real pause for this, so Hold slows the track to a standstill.\n" +
                    "If it misbehaves, use Stop and play it again.");
            }
        }
        else
        {
            if (ImGui.Button("Play", new Vector2(72, 0)))
            {
                held = false;
                player.Play(current);
            }
        }

        ImGui.SameLine();

        ImGui.BeginDisabled(!playing);
        if (ImGui.Button("Stop", new Vector2(72, 0)))
        {
            held = false;
            player.Stop();
        }

        ImGui.EndDisabled();

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextDisabled(playing ? TrackTime.Format(player.Elapsed) : "--:--");

        ImGui.SameLine();
        var favourite = Plugin.Config.IsFavourite(current.Id);
        if (ImGui.Button(favourite ? "Unstar" : "Star", new Vector2(72, 0)))
        {
            Plugin.Config.ToggleFavourite(current.Id);
            Plugin.SaveConfig();
        }

        ImGui.SetNextItemWidth(-1);
        var volume = Plugin.Config.PreviewVolume;
        if (ImGui.SliderFloat("##volume", ref volume, 0f, 1f, "Volume %.0f%%", ImGuiSliderFlags.None))
        {
            Plugin.Config.PreviewVolume = volume;
            player.SetVolume(volume);
            Plugin.SaveConfig();
        }

        if (player.LastError.Length > 0)
            ImGui.TextColored(new Vector4(0.95f, 0.5f, 0.45f, 1f), player.LastError);
    }

    public void Dispose()
    {
    }
}

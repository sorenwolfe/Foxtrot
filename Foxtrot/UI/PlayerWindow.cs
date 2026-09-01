using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Foxtrot.Services;
using Foxtrot.UI.Theme;

namespace Foxtrot.UI;

/// <summary>
/// The small player that appears when you preview something.
/// </summary>
/// <remarks>
/// It answers one question — what is this track — so it carries the name, the transport, a volume
/// and a running time, and nothing else. Anything more and it stops being something you can leave
/// open in a corner.
/// </remarks>
public sealed class PlayerWindow : Window, IDisposable
{
    private readonly PreviewPlayer player;

    private ThemeScope theme;

    public PlayerWindow(PreviewPlayer player)
        : base("Foxtrot###foxtrot-player",
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.player = player;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(340, 0),
            MaximumSize = new Vector2(720, 400),
        };
    }

    public override void PreDraw() => theme = ThemeScope.Push();

    public override void PostDraw() => theme.Dispose();

    public override void Draw()
    {
        if (Plugin.Config.ThemeEnabled && Plugin.Config.ThemeShadows)
        {
            var pos = ImGui.GetWindowPos();
            Sprites.Shadow(ImGui.GetBackgroundDrawList(), pos, pos + ImGui.GetWindowSize(),
                18f * UiHelpers.Scale, 0.45f);
        }

        if (player.Current is not { } current)
        {
            DrawEmpty();
            return;
        }

        DrawTitle(current);
        ImGui.Separator();
        ImGui.Spacing();

        DrawTransport(current);
        ImGui.Spacing();
        DrawVolume();

        if (player.LastError.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Palette.Vec(Palette.Danger), player.LastError);
        }
    }

    private void DrawEmpty()
    {
        ImGui.TextDisabled("Nothing loaded.");
        ImGui.Spacing();
        ImGui.TextWrapped("Right-click an orchestrion roll in your bags and choose Preview, or open the browser.");
        ImGui.Spacing();

        if (ImGui.Button("Browse all tracks", new Vector2(-1, ImGui.GetFrameHeight() * 1.2f)))
            Plugin.Browser.IsOpen = true;
    }

    private static void DrawTitle(Song current)
    {
        ImGui.TextUnformatted(current.Name);

        if (current.Description.Length > 0 && ImGui.IsItemHovered())
            UiHelpers.Tooltip(current.Description);

        var note = current.Category.Length > 0 ? current.Category : string.Empty;

        if (Plugin.Ownership.Available && !Plugin.Ownership.Owns(current.Id))
            note = note.Length > 0 ? note + "  ·  not learned yet" : "not learned yet";

        if (note.Length > 0)
            ImGui.TextDisabled(note);
    }

    private void DrawTransport(Song current)
    {
        var playing = player.IsPlaying;
        var wide = new Vector2(ImGui.GetFrameHeight() * 3.4f, ImGui.GetFrameHeight() * 1.2f);

        // One button that does the obvious thing, rather than a Play and a Stop where only one is
        // ever meaningful and the other sits there looking broken.
        if (UiHelpers.AccentButton(playing ? "Stop" : "Play", wide))
        {
            if (playing)
                player.Stop();
            else
                player.Play(current);
        }

        ImGui.SameLine();

        var star = Plugin.Config.IsFavourite(current.Id);
        if (ImGui.Button(star ? "Starred" : "Star", wide))
        {
            Plugin.Config.ToggleFavourite(current.Id);
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();

        // Someone else's orchestrion is worth saying out loud, or Stop looks broken when it is
        // simply not ours to stop.
        var elsewhere = !playing && OrchestrionSampler.State == SamplerState.Playing;
        if (elsewhere)
            ImGui.TextDisabled("an orchestrion is playing");
        else
            ImGui.TextDisabled(playing ? "playing" : "stopped");
    }

    private void DrawVolume()
    {
        // Stored 0-1, shown 0-100. Formatting the stored value with a percent sign was the bug:
        // the whole range rendered as 0% or 1%, so the slider looked broken while working.
        var percent = Plugin.Config.PreviewVolume * 100f;

        ImGui.SetNextItemWidth(-1);
        if (!ImGui.SliderFloat("##volume", ref percent, 0f, 100f, "Volume  %.0f%%", ImGuiSliderFlags.None))
            return;

        // Through the player, so a slider moved mid-track is heard immediately rather than
        // only applying to whatever gets played next.
        Plugin.Preview.SetVolume(percent / 100f);
        Plugin.SaveConfig();
    }

    public void Dispose()
    {
    }
}

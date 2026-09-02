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
///
/// The layout leans on space and weight rather than on lines and boxes: the track name is the
/// largest thing in the window, everything else recedes, and the only saturated colour is the
/// equalizer and the play button. A panel with a border around every element reads as a form, and
/// this is meant to read as a player.
/// </remarks>
public sealed class PlayerWindow : Window, IDisposable
{
    private const float TitleScale = 1.35f;

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

        ImGui.Dummy(UiHelpers.Scaled(0, 2));

        DrawTitle(current);

        ImGui.Dummy(UiHelpers.Scaled(0, 8));
        Equalizer.Draw("##levels", new Vector2(-1, 34f * UiHelpers.Scale), player.IsPlaying);
        ImGui.Dummy(UiHelpers.Scaled(0, 6));

        DrawTransport(current);
        ImGui.Dummy(UiHelpers.Scaled(0, 4));
        DrawVolume();

        if (player.LastError.Length > 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Palette.Vec(Palette.Danger), player.LastError);
        }
    }

    private void DrawEmpty()
    {
        ImGui.Dummy(UiHelpers.Scaled(0, 4));
        Equalizer.Draw("##levels", new Vector2(-1, 34f * UiHelpers.Scale), false);
        ImGui.Dummy(UiHelpers.Scaled(0, 6));

        ImGui.TextDisabled("Nothing loaded.");
        ImGui.Spacing();
        ImGui.TextWrapped("Right-click an orchestrion roll and choose Preview, or open the browser.");
        ImGui.Spacing();

        if (UiHelpers.AccentButton("Browse all tracks", new Vector2(-1, ImGui.GetFrameHeight() * 1.2f)))
            Plugin.Browser.IsOpen = true;
    }

    /// <summary>The track name, at the size of the thing you actually opened the window to read.</summary>
    private static void DrawTitle(Song current)
    {
        ImGui.SetWindowFontScale(TitleScale);
        ImGui.TextUnformatted(current.Name);
        ImGui.SetWindowFontScale(1f);

        if (current.Description.Length > 0 && ImGui.IsItemHovered())
            UiHelpers.Tooltip(current.Description);

        var note = current.Category.Length > 0 ? current.Category : string.Empty;

        if (Plugin.Ownership.Available && !Plugin.Ownership.Owns(current.Id))
            note = note.Length > 0 ? note + "  ·  not learned yet" : "not learned yet";

        // Always drawn, even when empty, so the window does not change height when a track with no
        // category follows one that has one.
        ImGui.TextColored(Palette.Vec(Palette.TextDim), note.Length > 0 ? note : " ");
    }

    private void DrawTransport(Song current)
    {
        var playing = player.IsPlaying;
        var height = ImGui.GetFrameHeight() * 1.5f;
        var square = new Vector2(height, height);

        // One button that does the obvious thing, rather than a Play and a Stop where only one is
        // ever meaningful and the other sits there looking broken.
        ImGui.PushStyleColor(ImGuiCol.Button, Palette.Vec(Palette.Accent, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Palette.Vec(Palette.Accent));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Palette.Vec(Palette.Accent, 0.8f));

        var transport = Controls.TransportButton("transport", square, playing);

        ImGui.PopStyleColor(3);

        if (ImGui.IsItemHovered())
            UiHelpers.Tooltip(playing ? "Stop" : "Play");

        if (transport)
        {
            if (playing)
                player.Stop();
            else
                player.Play(current);
        }

        ImGui.SameLine();

        // No fill of its own: a star sitting on a button-shaped slab reads as two controls.
        ImGui.PushStyleColor(ImGuiCol.Button, Palette.Vec(Palette.Text, 0f));

        var star = Plugin.Config.IsFavourite(current.Id);
        var toggled = Controls.StarButton("star", square, star);

        ImGui.PopStyleColor();

        if (ImGui.IsItemHovered())
            UiHelpers.Tooltip(star ? "Remove from starred" : "Add to starred");

        if (toggled)
        {
            Plugin.Config.ToggleFavourite(current.Id);
            Plugin.SaveConfig();
        }

        // The running time and the state share the right-hand end, because only one of them is
        // ever interesting: a time while it plays, a reason while it does not.
        var elsewhere = !playing && OrchestrionSampler.State == SamplerState.Playing;
        var trailing = playing ? TrackTime.Format(player.Elapsed)
            : elsewhere ? "an orchestrion is playing"
            : "stopped";

        var width = UiHelpers.TextSize(trailing).X;
        ImGui.SameLine(ImGui.GetContentRegionMax().X - width);
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((height - ImGui.GetTextLineHeight()) * 0.5f));
        ImGui.TextColored(Palette.Vec(playing ? Palette.Text : Palette.TextDim), trailing);
    }

    /// <summary>
    /// A speaker and a track with a handle, the way volume is dragged everywhere else.
    /// </summary>
    /// <remarks>
    /// The stored value is 0 to 1 throughout now, so there is no percentage conversion left to get
    /// wrong. Formatting a 0-1 value with a percent sign was the original bug, and the surest fix
    /// for a conversion is not having one.
    /// </remarks>
    private static void DrawVolume()
    {
        var level = Plugin.Config.PreviewVolume;

        if (!Controls.VolumeSlider("volume", ref level, ImGui.GetContentRegionAvail().X))
            return;

        // Through the player, so a slider moved mid-track is heard immediately rather than
        // only applying to whatever gets played next.
        Plugin.Preview.SetVolume(level);
        Plugin.SaveConfig();
    }

    public void Dispose()
    {
    }
}

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Foxtrot.UI.Theme;

namespace Foxtrot.UI;

public sealed class ConfigWindow : Window, IDisposable
{
    private ThemeScope theme;

    public ConfigWindow()
        : base("Foxtrot settings###foxtrot-config", ImGuiWindowFlags.AlwaysAutoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(430, 0),
            MaximumSize = new Vector2(760, 960),
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

        var config = Plugin.Config;

        Heading("Playback");

        if (Percent("Preview volume", config.PreviewVolume, out var volume))
        {
            Plugin.Preview.SetVolume(volume);
            Plugin.SaveConfig();
        }

        var duck = config.DuckGameMusic;
        if (ImGui.Checkbox("Quieten the zone music while previewing", ref duck))
        {
            config.DuckGameMusic = duck;

            // Turning it off mid-preview should give the music back at once, not on the next stop.
            if (!duck)
                Plugin.Preview.Stop();

            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.Tooltip(
            "Two tracks at once makes a preview impossible to judge. The zone music fades back in " +
            "as soon as the preview stops, including if you unload the plugin mid-track.");

        if (config.DuckGameMusic && Percent("Zone music drops to", config.DuckedMusicVolume, out var ducked))
        {
            config.DuckedMusicVolume = ducked;
            Plugin.SaveConfig();
        }

        Heading("Right-click menu");

        var onItems = config.ContextMenuOnItems;
        if (ImGui.Checkbox("On orchestrion rolls in my bags", ref onItems))
        {
            config.ContextMenuOnItems = onItems;
            Plugin.SaveConfig();
        }

        var onList = config.ContextMenuOnOrchestrionList;
        if (ImGui.Checkbox("In the orchestrion list", ref onList))
        {
            config.ContextMenuOnOrchestrionList = onList;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.Tooltip(
            "Not working yet — reading which row is selected in that window needs a detail that " +
            "can only be found with the game running. The browser plays anything in the meantime, " +
            "including rolls you do not own.");

        var openPlayer = config.OpenPlayerOnPreview;
        if (ImGui.Checkbox("Open the player when a preview starts", ref openPlayer))
        {
            config.OpenPlayerOnPreview = openPlayer;
            Plugin.SaveConfig();
        }

        Heading("Appearance");

        var themed = config.ThemeEnabled;
        if (ImGui.Checkbox("Use the Foxtrot look", ref themed))
        {
            config.ThemeEnabled = themed;
            Plugin.SaveConfig();
        }

        ImGui.SameLine();
        UiHelpers.Tooltip("Off falls back to whatever style your other plugins use.");

        if (config.ThemeEnabled)
        {
            var shadows = config.ThemeShadows;
            if (ImGui.Checkbox("Soft shadows", ref shadows))
            {
                config.ThemeShadows = shadows;
                Plugin.SaveConfig();
            }

            var full = Palette.Vec(Palette.Accent);
            var accent = new Vector3(full.X, full.Y, full.Z);
            if (ImGui.ColorEdit3("Accent colour", ref accent, ImGuiColorEditFlags.NoInputs))
            {
                config.ThemeAccent = Palette.FromVec(new Vector4(accent, 1f));
                Plugin.SaveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextDisabled(
            $"{Plugin.Library.Count} track(s), {Plugin.Library.RollItemCount} roll item(s), " +
            $"{config.Favourites.Count} starred.  /foxtrot diag says more.");
    }

    private static void Heading(string text)
    {
        ImGui.Spacing();
        ImGui.TextDisabled(text);
        ImGui.Separator();
        ImGui.Spacing();
    }

    /// <summary>
    /// A 0-1 setting shown as a percentage.
    /// </summary>
    /// <remarks>
    /// The conversion is here rather than at each call site because getting it wrong is invisible
    /// in the code and obvious on screen: formatting a 0-1 value with a percent sign renders the
    /// entire range as 0% or 1%, so the slider looks broken while working perfectly.
    /// </remarks>
    private static bool Percent(string label, float stored, out float updated)
    {
        var shown = Math.Clamp(stored, 0f, 1f) * 100f;

        ImGui.SetNextItemWidth(220 * UiHelpers.Scale);
        if (!ImGui.SliderFloat(label, ref shown, 0f, 100f, "%.0f%%", ImGuiSliderFlags.None))
        {
            updated = stored;
            return false;
        }

        updated = Math.Clamp(shown / 100f, 0f, 1f);
        return true;
    }

    public void Dispose()
    {
    }
}

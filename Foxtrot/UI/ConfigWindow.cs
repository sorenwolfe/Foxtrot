using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace Foxtrot.UI;

public sealed class ConfigWindow : Window, IDisposable
{
    public ConfigWindow()
        : base("Foxtrot settings###foxtrot-config", ImGuiWindowFlags.AlwaysAutoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 0),
            MaximumSize = new Vector2(700, 900),
        };
    }

    public override void Draw()
    {
        var config = Plugin.Config;

        ImGui.TextDisabled("Playback");
        ImGui.Separator();

        ImGui.SetNextItemWidth(240);
        var volume = config.PreviewVolume;
        if (ImGui.SliderFloat("Preview volume", ref volume, 0f, 1f, "%.0f%%", ImGuiSliderFlags.None))
        {
            config.PreviewVolume = volume;
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
        Help("Two tracks at once makes a preview impossible to judge. The zone music fades back " +
             "in as soon as the preview stops, including if you unload the plugin mid-track.");

        if (config.DuckGameMusic)
        {
            ImGui.SetNextItemWidth(240);
            var ducked = config.DuckedMusicVolume;
            if (ImGui.SliderFloat("Zone music drops to", ref ducked, 0f, 1f, "%.0f%%", ImGuiSliderFlags.None))
            {
                config.DuckedMusicVolume = ducked;
                Plugin.SaveConfig();
            }
        }

        ImGui.Spacing();
        ImGui.TextDisabled("Right-click menu");
        ImGui.Separator();

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
        Help("Not working yet — reading which row is selected in that window needs a detail that " +
             "can only be found with the game running. The browser plays anything in the meantime, " +
             "including rolls you do not own.");

        var openPlayer = config.OpenPlayerOnPreview;
        if (ImGui.Checkbox("Open the player when a preview starts", ref openPlayer))
        {
            config.OpenPlayerOnPreview = openPlayer;
            Plugin.SaveConfig();
        }

        ImGui.Spacing();
        ImGui.TextDisabled($"{Plugin.Library.Count} track(s) loaded, {config.Favourites.Count} starred.");
    }

    private static void Help(string text)
    {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(360);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public void Dispose()
    {
    }
}

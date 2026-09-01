using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace Foxtrot.UI.Theme;

/// <summary>
/// Pushes the dark glass look for the duration of one window's draw, and pops exactly what it
/// pushed.
/// </summary>
/// <remarks>
/// The counts are tracked rather than written as constants on purpose. An ImGui style stack that
/// is popped the wrong number of times does not throw — it quietly bleeds colours into every
/// other plugin's windows, or trips an assert deep inside ImGui frames later, and either way the
/// cause is nowhere near the symptom.
/// </remarks>
/// <summary>Numbers the theme and its tests both need to agree on.</summary>
public static class ThemeMetrics
{
    public static readonly Vector2 FramePadding = new(9f, 5f);

    /// <summary>
    /// How far off centre a glyph ends up inside a button, in pixels. Positive is to the right.
    /// </summary>
    /// <remarks>
    /// ImGui centres a label between the frame padding, not inside the button, and it only
    /// centres when there is slack to centre into — with none it pins the text against the
    /// padding edge and carries on silently. That is why an icon on a square button drifts as
    /// soon as the horizontal padding grows, and why icon buttons draw with no padding at all.
    /// </remarks>
    public static float GlyphOffsetFromCentre(float buttonSide, float padX, float glyphWidth)
    {
        var slack = buttonSide - (padX * 2f) - glyphWidth;
        var start = padX + (slack > 0f ? slack * 0.5f : 0f);

        return start + (glyphWidth * 0.5f) - (buttonSide * 0.5f);
    }
}

public struct ThemeScope : IDisposable
{
    private int colours;
    private int vars;
    private bool active;

    public static ThemeScope Push()
    {
        var scope = new ThemeScope();

        if (!Plugin.Config.ThemeEnabled)
            return scope;

        scope.active = true;
        var accent = Palette.Accent;

        // ---- surfaces
        scope.Colour(ImGuiCol.WindowBg, Palette.Vec(Palette.Window, 0.96f));
        scope.Colour(ImGuiCol.ChildBg, Palette.Vec(Palette.Panel, 0.55f));
        scope.Colour(ImGuiCol.PopupBg, Palette.Vec(Palette.Panel, 0.98f));
        scope.Colour(ImGuiCol.MenuBarBg, Palette.Vec(Palette.Panel, 1f));
        scope.Colour(ImGuiCol.Border, Palette.Vec(0xFFFFFF, 0.09f));
        scope.Colour(ImGuiCol.BorderShadow, Palette.Vec(0x000000, 0f));

        // ---- text
        scope.Colour(ImGuiCol.Text, Palette.Vec(Palette.Text));
        scope.Colour(ImGuiCol.TextDisabled, Palette.Vec(Palette.TextDim));
        scope.Colour(ImGuiCol.TextSelectedBg, Palette.Vec(accent, 0.32f));

        // ---- inputs
        scope.Colour(ImGuiCol.FrameBg, Palette.Vec(Palette.PanelRaised));
        scope.Colour(ImGuiCol.FrameBgHovered, Palette.Vec(Palette.Hover));
        scope.Colour(ImGuiCol.FrameBgActive, Palette.Vec(Palette.Pressed));
        scope.Colour(ImGuiCol.CheckMark, Palette.Vec(accent));
        scope.Colour(ImGuiCol.SliderGrab, Palette.Vec(accent, 0.9f));
        scope.Colour(ImGuiCol.SliderGrabActive, Palette.Vec(Palette.Text));

        // ---- buttons
        scope.Colour(ImGuiCol.Button, Palette.Vec(Palette.PanelRaised));
        scope.Colour(ImGuiCol.ButtonHovered, Palette.Vec(Palette.Hover));
        scope.Colour(ImGuiCol.ButtonActive, Palette.Vec(Palette.Pressed));

        // ---- selectables, tree nodes, headers
        scope.Colour(ImGuiCol.Header, Palette.Vec(accent, 0.16f));
        scope.Colour(ImGuiCol.HeaderHovered, Palette.Vec(accent, 0.26f));
        scope.Colour(ImGuiCol.HeaderActive, Palette.Vec(accent, 0.36f));

        // ---- tabs
        scope.Colour(ImGuiCol.Tab, Palette.Vec(Palette.Window, 0f));
        scope.Colour(ImGuiCol.TabHovered, Palette.Vec(0xFFFFFF, 0.06f));
        scope.Colour(ImGuiCol.TabActive, Palette.Vec(accent, 0.14f));
        scope.Colour(ImGuiCol.TabUnfocused, Palette.Vec(Palette.Window, 0f));
        scope.Colour(ImGuiCol.TabUnfocusedActive, Palette.Vec(accent, 0.08f));

        // ---- title bar
        scope.Colour(ImGuiCol.TitleBg, Palette.Vec(Palette.Panel));
        scope.Colour(ImGuiCol.TitleBgActive, Palette.Vec(0x19212F));
        scope.Colour(ImGuiCol.TitleBgCollapsed, Palette.Vec(Palette.Window, 0.8f));

        // ---- separators and grips
        scope.Colour(ImGuiCol.Separator, Palette.Vec(0xFFFFFF, 0.07f));
        scope.Colour(ImGuiCol.SeparatorHovered, Palette.Vec(accent, 0.5f));
        scope.Colour(ImGuiCol.SeparatorActive, Palette.Vec(accent, 0.8f));
        scope.Colour(ImGuiCol.ResizeGrip, Palette.Vec(0xFFFFFF, 0.06f));
        scope.Colour(ImGuiCol.ResizeGripHovered, Palette.Vec(accent, 0.4f));
        scope.Colour(ImGuiCol.ResizeGripActive, Palette.Vec(accent, 0.7f));

        // ---- scrollbars
        scope.Colour(ImGuiCol.ScrollbarBg, Palette.Vec(0x000000, 0.18f));
        scope.Colour(ImGuiCol.ScrollbarGrab, Palette.Vec(0xFFFFFF, 0.10f));
        scope.Colour(ImGuiCol.ScrollbarGrabHovered, Palette.Vec(0xFFFFFF, 0.17f));
        scope.Colour(ImGuiCol.ScrollbarGrabActive, Palette.Vec(accent, 0.6f));

        // ---- tables
        scope.Colour(ImGuiCol.TableHeaderBg, Palette.Vec(Palette.Panel));
        scope.Colour(ImGuiCol.TableBorderStrong, Palette.Vec(0xFFFFFF, 0.11f));
        scope.Colour(ImGuiCol.TableBorderLight, Palette.Vec(0xFFFFFF, 0.05f));
        scope.Colour(ImGuiCol.TableRowBg, Palette.Vec(0x000000, 0f));
        scope.Colour(ImGuiCol.TableRowBgAlt, Palette.Vec(0xFFFFFF, 0.022f));

        scope.Colour(ImGuiCol.NavHighlight, Palette.Vec(accent, 0.7f));
        scope.Colour(ImGuiCol.DragDropTarget, Palette.Vec(accent));

        // ---- shape and rhythm
        scope.Var(ImGuiStyleVar.WindowRounding, 8f);
        scope.Var(ImGuiStyleVar.ChildRounding, 7f);
        scope.Var(ImGuiStyleVar.PopupRounding, 8f);
        scope.Var(ImGuiStyleVar.FrameRounding, 6f);
        scope.Var(ImGuiStyleVar.GrabRounding, 6f);
        scope.Var(ImGuiStyleVar.ScrollbarRounding, 6f);
        scope.Var(ImGuiStyleVar.TabRounding, 5f);

        scope.Var(ImGuiStyleVar.WindowBorderSize, 1f);
        scope.Var(ImGuiStyleVar.ChildBorderSize, 1f);
        scope.Var(ImGuiStyleVar.FrameBorderSize, 1f);
        scope.Var(ImGuiStyleVar.PopupBorderSize, 1f);

        scope.Var(ImGuiStyleVar.WindowPadding, new Vector2(12f, 11f));
        scope.Var(ImGuiStyleVar.FramePadding, ThemeMetrics.FramePadding);
        scope.Var(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 7f));
        scope.Var(ImGuiStyleVar.ItemInnerSpacing, new Vector2(7f, 5f));
        scope.Var(ImGuiStyleVar.CellPadding, new Vector2(7f, 5f));
        scope.Var(ImGuiStyleVar.ScrollbarSize, 11f);
        scope.Var(ImGuiStyleVar.GrabMinSize, 11f);

        return scope;
    }

    private void Colour(ImGuiCol target, Vector4 value)
    {
        ImGui.PushStyleColor(target, value);
        colours++;
    }

    private void Var(ImGuiStyleVar target, float value)
    {
        ImGui.PushStyleVar(target, value);
        vars++;
    }

    private void Var(ImGuiStyleVar target, Vector2 value)
    {
        ImGui.PushStyleVar(target, value);
        vars++;
    }

    public void Dispose()
    {
        if (!active)
            return;

        if (colours > 0)
            ImGui.PopStyleColor(colours);

        if (vars > 0)
            ImGui.PopStyleVar(vars);

        colours = 0;
        vars = 0;
        active = false;
    }
}

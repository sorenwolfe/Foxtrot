using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;

namespace Foxtrot.UI;

/// <summary>Small drawing and layout helpers shared by every window.</summary>
public static class UiHelpers
{
    public static float Scale => ImGuiHelpers.GlobalScale;

    public static Vector2 Scaled(float x, float y) => new(x * Scale, y * Scale);

    /// <summary>Packs a colour the way ImGui's draw list wants it (ABGR).</summary>
    public static uint Pack(Vector4 colour) => ImGui.ColorConvertFloat4ToU32(colour);

    public static Vector4 Unpack(uint colour)
    {
        return new Vector4(
            (colour & 0xFF) / 255f,
            ((colour >> 8) & 0xFF) / 255f,
            ((colour >> 16) & 0xFF) / 255f,
            ((colour >> 24) & 0xFF) / 255f);
    }

    public static uint WithAlpha(uint colour, float alpha)
    {
        var a = (uint)Math.Clamp(alpha * 255f, 0f, 255f);
        return (colour & 0x00FFFFFF) | (a << 24);
    }

    /// <summary>Blends towards black, for drop shadows and inactive states.</summary>
    public static uint Darken(uint colour, float amount)
    {
        var v = Unpack(colour);
        var f = 1f - Math.Clamp(amount, 0f, 1f);
        return Pack(new Vector4(v.X * f, v.Y * f, v.Z * f, v.W));
    }

    /// <summary>Picks black or white text depending on how bright the background is.</summary>
    public static uint ReadableTextOn(uint background)
    {
        var v = Unpack(background);
        var luma = (0.299f * v.X) + (0.587f * v.Y) + (0.114f * v.Z);
        return luma > 0.55f ? 0xFF101010u : 0xFFFFFFFFu;
    }

    // ---------------------------------------------------------------- binding shims
    // The generated ImGui bindings expose one overload per parameter count and take an explicit
    // callback delegate. Wrapping the handful of calls we make keeps the rest of the UI readable
    // and avoids ambiguity between the two callback delegate shapes.

    public static Vector2 TextSize(string text) => ImGui.CalcTextSize(text, false, -1f);

    public static bool InputText(string label, ref string value, int maxLength = 512,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        return ImGui.InputText(label, ref value, maxLength, flags, (ImGui.ImGuiInputTextCallbackDelegate?)null);
    }

    public static bool InputTextHint(string label, string hint, ref string value, int maxLength = 512,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        return ImGui.InputTextWithHint(label, hint, ref value, maxLength, flags, (ImGui.ImGuiInputTextCallbackDelegate?)null);
    }

    public static bool InputMultiline(string label, ref string value, Vector2 size, int maxLength = 8192,
        ImGuiInputTextFlags flags = ImGuiInputTextFlags.None)
    {
        return ImGui.InputTextMultiline(label, ref value, maxLength, size, flags, (ImGui.ImGuiInputTextCallbackDelegate?)null);
    }

    /// <summary>
    /// A tooltip that wraps and can carry a heading. ImGui.SetTooltip lays its text out on one
    /// line, so anything longer than a few words runs off the edge of the screen.
    /// </summary>
    /// <summary>
    /// A button in the accent colour, for the one action a window is mostly about.
    /// </summary>
    /// <remarks>
    /// Exactly one per window. The moment there are two, neither reads as the thing to press.
    /// </remarks>
    public static bool AccentButton(string label, Vector2 size)
    {
        var accent = Theme.Palette.Accent;

        ImGui.PushStyleColor(ImGuiCol.Button, Theme.Palette.Pack(accent, 0.85f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, Theme.Palette.Pack(accent, 1f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, Theme.Palette.Pack(accent, 0.7f));

        var pressed = ImGui.Button(label, size);

        ImGui.PopStyleColor(3);
        return pressed;
    }

    public static void Tooltip(string body) => Tooltip(null, body);

    public static void Tooltip(string? title, string body)
    {
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 22f);

        if (!string.IsNullOrEmpty(title))
        {
            ImGui.TextColored(Pack(new Vector4(0.54f, 0.68f, 1f, 1f)), title);
            ImGui.Separator();
        }

        ImGui.TextUnformatted(body);

        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    public static void HelpMarker(string text)
    {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered())
            return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 30f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    /// <summary>Draws a game icon inline, falling back to nothing if it is not loaded yet.</summary>
    public static bool DrawIcon(uint iconId, Vector2 size)
    {
        if (iconId == 0)
            return false;

        try
        {
            var shared = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId));
            if (!shared.TryGetWrap(out var wrap, out _) || wrap == null)
                return false;

            ImGui.Image(wrap.Handle, size);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Adds a game icon to a draw list, e.g. inside the arena canvas.</summary>
    public static bool DrawIconAt(ImDrawListPtr drawList, uint iconId, Vector2 min, Vector2 max, uint tint)
    {
        if (iconId == 0)
            return false;

        try
        {
            var shared = Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId));
            if (!shared.TryGetWrap(out var wrap, out _) || wrap == null)
                return false;

            drawList.AddImage(wrap.Handle, min, max, Vector2.Zero, Vector2.One, tint);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Draws text centred on a point, with a soft shadow so it survives busy backgrounds.</summary>
    public static void CenteredShadowText(ImDrawListPtr drawList, Vector2 centre, string text, uint colour)
    {
        if (string.IsNullOrEmpty(text))
            return;

        var size = TextSize(text);
        var pos = centre - (size * 0.5f);
        drawList.AddText(pos + new Vector2(1, 1), 0xC0000000, text);
        drawList.AddText(pos, colour, text);
    }

    /// <summary>A small square swatch that opens a colour picker when clicked.</summary>
    public static bool ColorButton(string id, ref uint packed, string tooltip = "")
    {
        var value = Unpack(packed);
        var changed = false;

        if (ImGui.ColorEdit4(id, ref value,
                ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview))
        {
            packed = Pack(value);
            changed = true;
        }

        if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return changed;
    }

    /// <summary>Rotates a point around the origin. Angle in degrees, clockwise.</summary>
    public static Vector2 Rotate(Vector2 point, float degrees)
    {
        var rad = degrees * (MathF.PI / 180f);
        var cos = MathF.Cos(rad);
        var sin = MathF.Sin(rad);
        return new Vector2((point.X * cos) - (point.Y * sin), (point.X * sin) + (point.Y * cos));
    }

    /// <summary>Stay on the current line only if an item of this width still fits.</summary>
    public static void SameLineIfRoom(float nextItemWidth)
    {
        var rightEdge = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        var next = ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X + nextItemWidth;
        if (next < rightEdge)
            ImGui.SameLine();
    }

    public static float ButtonWidth(string label) =>
        TextSize(label).X + (ImGui.GetStyle().FramePadding.X * 2);

    /// <summary>Width for a full-width item that has to leave room for a trailing widget.</summary>
    public static float WidthLeaving(string trailing) =>
        -(TextSize(trailing).X + (ImGui.GetStyle().ItemSpacing.X * 2));
}

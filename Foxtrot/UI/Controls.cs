using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Foxtrot.UI.Theme;

namespace Foxtrot.UI;

/// <summary>
/// The player's controls, drawn as shapes rather than written as words.
/// </summary>
/// <remarks>
/// Drawn rather than set in an icon font. A font would mean a dependency, a fallback for when it
/// fails to load, and glyphs that do not scale with the rest of the window; a triangle and a square
/// are a few lines of geometry that are correct at any size and cannot fail to load.
///
/// The geometry is separated from the drawing throughout. A star that comes out lopsided or a
/// slider whose handle does not sit under the cursor are both arithmetic mistakes, and arithmetic
/// can be checked without a running game.
/// </remarks>
public static class Controls
{
    /// <summary>The points of a five-pointed star, outer and inner alternating, first at the top.</summary>
    /// <remarks>
    /// Ten points, walked in order, trace the outline. Starting at the top matters: a star rotated
    /// by a tenth of a turn reads as a broken cog rather than as a star, and it is the kind of
    /// thing that looks almost right until it is next to something that is.
    /// </remarks>
    public static Vector2[] StarPoints(Vector2 centre, float outerRadius, float innerRadius)
    {
        var points = new Vector2[10];

        for (var i = 0; i < 10; i++)
        {
            // Half a turn per step, starting straight up, which is -90 degrees on screen.
            var angle = (-MathF.PI / 2f) + (i * MathF.PI / 5f);
            var radius = (i % 2 == 0) ? outerRadius : innerRadius;

            points[i] = new Vector2(
                centre.X + (MathF.Cos(angle) * radius),
                centre.Y + (MathF.Sin(angle) * radius));
        }

        return points;
    }

    /// <summary>How many arcs the speaker shows: none when silent, one when quiet, two when loud.</summary>
    public static int SpeakerWaves(float volume)
    {
        if (volume <= 0.001f)
            return 0;

        return volume < 0.5f ? 1 : 2;
    }

    /// <summary>Where along a track a click or drag lands, as 0 to 1.</summary>
    /// <remarks>
    /// Clamped, because a drag that starts on the handle continues to be reported well past both
    /// ends of the track, and a zero-width track would otherwise divide by zero on the first frame
    /// of a window that has not been laid out yet.
    /// </remarks>
    public static float ValueFromPosition(float x, float trackLeft, float trackRight)
    {
        var span = trackRight - trackLeft;
        if (span <= 0f)
            return 0f;

        return Math.Clamp((x - trackLeft) / span, 0f, 1f);
    }

    /// <summary>A square button carrying a play triangle or a stop square.</summary>
    public static bool TransportButton(string id, Vector2 size, bool playing)
    {
        var pressed = ImGui.Button($"##{id}", size);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var centre = (min + max) * 0.5f;
        var draw = ImGui.GetWindowDrawList();

        var glyph = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.34f;
        var ink = Palette.Pack(UiHelpers.ReadableTextOn(Palette.Accent));

        if (playing)
        {
            // A square, very slightly rounded so it does not look like a missing glyph.
            draw.AddRectFilled(
                centre - new Vector2(glyph, glyph),
                centre + new Vector2(glyph, glyph),
                ink,
                2f * UiHelpers.Scale);
        }
        else
        {
            // Nudged right by a fraction: a triangle centred on its bounding box looks
            // left-heavy, because its mass sits towards the flat edge.
            var offset = glyph * 0.15f;

            draw.AddTriangleFilled(
                new Vector2(centre.X - glyph + offset, centre.Y - glyph),
                new Vector2(centre.X - glyph + offset, centre.Y + glyph),
                new Vector2(centre.X + glyph + offset, centre.Y),
                ink);
        }

        return pressed;
    }

    /// <summary>A star that fills in when it is on.</summary>
    public static bool StarButton(string id, Vector2 size, bool starred)
    {
        var pressed = ImGui.Button($"##{id}", size);
        var hovered = ImGui.IsItemHovered();

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var centre = (min + max) * 0.5f;
        var draw = ImGui.GetWindowDrawList();

        var outer = MathF.Min(max.X - min.X, max.Y - min.Y) * 0.32f;
        var points = StarPoints(centre, outer, outer * 0.42f);

        var colour = starred
            ? Palette.Pack(Palette.Attention)
            : Palette.Pack(hovered ? Palette.Text : Palette.TextDim, hovered ? 0.9f : 0.7f);

        if (starred)
        {
            // A star is not convex, so the polygon fill would cut across it. A fan from the centre
            // out to each pair of neighbouring points covers it exactly, points included.
            for (var i = 0; i < points.Length; i++)
                draw.AddTriangleFilled(centre, points[i], points[(i + 1) % points.Length], colour);
        }
        else
        {
            for (var i = 0; i < points.Length; i++)
                draw.AddLine(points[i], points[(i + 1) % points.Length], colour, 1.6f * UiHelpers.Scale);
        }

        return pressed;
    }

    /// <summary>
    /// A speaker and a thin track with a handle, dragged the way volume is dragged everywhere else.
    /// </summary>
    /// <remarks>
    /// Returns true on any frame the value moved, so the caller can push it at the game
    /// immediately rather than only when the drag ends.
    /// </remarks>
    public static bool VolumeSlider(string id, ref float value, float width)
    {
        var scale = UiHelpers.Scale;
        var height = 22f * scale;
        var iconWidth = 22f * scale;

        ImGui.InvisibleButton($"##{id}", new Vector2(MathF.Max(width, iconWidth + (40f * scale)), height));

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();

        var handleRadius = 6f * scale;
        var trackLeft = min.X + iconWidth + (6f * scale) + handleRadius;
        var trackRight = max.X - handleRadius;
        var midY = (min.Y + max.Y) * 0.5f;

        var changed = false;

        if (ImGui.IsItemActive())
        {
            var moved = ValueFromPosition(ImGui.GetIO().MousePos.X, trackLeft, trackRight);
            if (MathF.Abs(moved - value) > 0.0001f)
            {
                value = moved;
                changed = true;
            }
        }

        var filled = Math.Clamp(value, 0f, 1f);
        var handleX = trackLeft + ((trackRight - trackLeft) * filled);
        var hot = ImGui.IsItemHovered() || ImGui.IsItemActive();

        DrawSpeaker(draw, new Vector2(min.X + (iconWidth * 0.5f), midY), iconWidth * 0.5f, filled);

        var thickness = 3f * scale;
        draw.AddRectFilled(
            new Vector2(trackLeft, midY - (thickness * 0.5f)),
            new Vector2(trackRight, midY + (thickness * 0.5f)),
            Palette.Pack(Palette.Text, 0.16f),
            thickness);

        draw.AddRectFilled(
            new Vector2(trackLeft, midY - (thickness * 0.5f)),
            new Vector2(handleX, midY + (thickness * 0.5f)),
            Palette.Pack(Palette.Accent, 0.95f),
            thickness);

        draw.AddCircleFilled(new Vector2(handleX, midY), handleRadius + (hot ? 1f * scale : 0f),
            Palette.Pack(Palette.Accent));
        draw.AddCircleFilled(new Vector2(handleX, midY), handleRadius * 0.45f,
            Palette.Pack(0xFFFFFF, 0.85f));

        if (hot)
        {
            var label = $"{filled * 100f:0}%";
            var size = UiHelpers.TextSize(label);
            draw.AddText(new Vector2(handleX - (size.X * 0.5f), min.Y - size.Y - (2f * scale)),
                Palette.Pack(Palette.Text, 0.9f), label);
        }

        return changed;
    }

    /// <summary>The speaker glyph: a box, a cone, and however many arcs the level calls for.</summary>
    private static void DrawSpeaker(ImDrawListPtr draw, Vector2 centre, float radius, float volume)
    {
        var colour = Palette.Pack(Palette.TextMuted, 0.95f);
        var body = radius * 0.42f;

        draw.AddRectFilled(
            new Vector2(centre.X - (radius * 0.75f), centre.Y - (body * 0.55f)),
            new Vector2(centre.X - (radius * 0.2f), centre.Y + (body * 0.55f)),
            colour, 1f);

        draw.AddTriangleFilled(
            new Vector2(centre.X - (radius * 0.2f), centre.Y - body),
            new Vector2(centre.X - (radius * 0.2f), centre.Y + body),
            new Vector2(centre.X + (radius * 0.25f), centre.Y),
            colour);

        var waves = SpeakerWaves(volume);

        if (waves == 0)
        {
            // Muted reads as a cross rather than as an absence, or a silent speaker just looks
            // like a speaker that failed to finish drawing.
            var arm = radius * 0.3f;
            var at = new Vector2(centre.X + (radius * 0.55f), centre.Y);

            draw.AddLine(at - new Vector2(arm, arm), at + new Vector2(arm, arm), colour, 1.6f * UiHelpers.Scale);
            draw.AddLine(at - new Vector2(arm, -arm), at + new Vector2(arm, -arm), colour, 1.6f * UiHelpers.Scale);
            return;
        }

        // Arcs as short polylines: the drawing API's own arc helpers are not worth the risk of a
        // binding that turns out to want a pointer.
        for (var wave = 1; wave <= waves; wave++)
        {
            var arcRadius = radius * (0.35f + (0.28f * wave));
            var previous = Vector2.Zero;

            for (var step = 0; step <= 8; step++)
            {
                var angle = (-MathF.PI / 3.4f) + (step / 8f * (2f * MathF.PI / 3.4f));
                var point = new Vector2(
                    centre.X + (MathF.Cos(angle) * arcRadius) - (radius * 0.1f),
                    centre.Y + (MathF.Sin(angle) * arcRadius));

                if (step > 0)
                    draw.AddLine(previous, point, colour, 1.5f * UiHelpers.Scale);

                previous = point;
            }
        }
    }
}

using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Foxtrot.UI.Theme;

namespace Foxtrot.UI;

/// <summary>
/// The bars that move while a preview plays.
/// </summary>
/// <remarks>
/// They are ornament, and it is worth being straight about that: the game's sampler hands out no
/// amplitude and no spectrum, so there is nothing to read. Nothing here follows the music. What it
/// does honestly report is whether something is playing at all — the bars move when it is and lie
/// flat when it is not — which is the one thing a level meter is actually used for at a glance.
///
/// The motion is a sum of two slow waves per bar rather than random noise. Random would need state
/// and a clock to look smooth, and it could not be tested; this is a pure function of time and bar
/// index, so the shape can be checked without a running game and looks identical every session.
/// </remarks>
public static class Equalizer
{
    /// <summary>How flat the bars sit when nothing is playing.</summary>
    /// <remarks>Not zero: a row of nothing reads as broken rather than as stopped.</remarks>
    public const float RestingHeight = 0.06f;

    /// <summary>
    /// How tall one bar stands, from 0 to 1.
    /// </summary>
    /// <param name="seconds">Any steadily increasing clock.</param>
    /// <param name="index">Which bar, from the left.</param>
    /// <param name="count">How many bars there are.</param>
    /// <param name="playing">False lies the whole row flat.</param>
    public static float BarHeight(float seconds, int index, int count, bool playing)
    {
        if (!playing)
            return RestingHeight;

        if (count <= 0 || index < 0 || index >= count)
            return RestingHeight;

        // A stalled or nonsense clock should freeze the bars, not throw or spike them.
        if (float.IsNaN(seconds) || float.IsInfinity(seconds))
            return RestingHeight;

        // Two waves per bar, at rates that share no common multiple, so the row never falls into
        // a visible marching pattern the way a single wave with a phase offset does.
        var fast = MathF.Sin(seconds * (2.1f + 0.37f * index) + index * 1.7f);
        var slow = MathF.Sin(seconds * (1.3f + 0.21f * index) + index * 0.9f);

        // The two amplitudes sum to exactly 0.5, so this lands in 0..1 without needing a clamp.
        var wave = 0.5f + (0.28f * fast) + (0.22f * slow);

        // Real spectra lean on the left, so the row is shaped to fall away towards the right.
        // Without it the bars read as a decoration rather than as a meter.
        var lean = count == 1 ? 1f : 1f - (index / (float)(count - 1));
        var shape = 0.55f + (0.45f * MathF.Pow(lean, 0.8f));

        // Scaled into the space above the resting line rather than clamped against it. Clamping
        // parked the quietest bars flat on the floor for seconds at a time, which does not read as
        // a quiet band — it reads as a broken one.
        return RestingHeight + ((1f - RestingHeight) * wave * shape);
    }

    /// <summary>Draws a row of bars into the current window and consumes the space.</summary>
    public static void Draw(string id, Vector2 size, bool playing, int bars = 24)
    {
        if (bars <= 0 || size.X <= 0f || size.Y <= 0f)
            return;

        ImGui.InvisibleButton(id, size);

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();

        var seconds = (float)ImGui.GetTime();
        var accent = Palette.Accent;

        var slot = (max.X - min.X) / bars;
        var width = MathF.Max(1f, slot * 0.55f);
        var rounding = MathF.Min(width * 0.5f, 2f * UiHelpers.Scale);

        for (var i = 0; i < bars; i++)
        {
            var height = BarHeight(seconds, i, bars, playing) * (max.Y - min.Y);

            var left = min.X + (slot * i) + ((slot - width) * 0.5f);
            var top = max.Y - height;

            // Taller reads as louder, so the tall ones are also the brightest. A row at one alpha
            // looks like a picture of an equalizer; this looks like one that is working.
            var alpha = playing ? 0.35f + (0.55f * (height / (max.Y - min.Y))) : 0.25f;

            draw.AddRectFilled(
                new Vector2(left, top),
                new Vector2(left + width, max.Y),
                Palette.Pack(accent, alpha),
                rounding);
        }
    }
}

using System;
using System.Numerics;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;

namespace Foxtrot.UI.Theme;

/// <summary>
/// The two things ImGui cannot draw for itself: a blurred shadow and a soft radial glow. Both are
/// generated PNGs embedded in the assembly, so there is no file beside the DLL to go missing on
/// somebody's install.
/// </summary>
/// <remarks>
/// These come back as shared textures that Dalamud owns, so there is nothing here to dispose —
/// which is the reason for using the shared lookup rather than creating our own wraps.
/// </remarks>
public static class Sprites
{
    private const string ShadowResource = "Foxtrot.Resources.shadow.png";
    private const string GlowResource = "Foxtrot.Resources.glow.png";

    /// <summary>How much of the shadow sprite is the soft edge, in its own pixels.</summary>
    private const float ShadowInset = 28f;
    private const float ShadowSize = 96f;

    private static ISharedImmediateTexture? shadow;
    private static ISharedImmediateTexture? glow;

    private static ISharedImmediateTexture? Load(ref ISharedImmediateTexture? cache, string name)
    {
        if (cache != null)
            return cache;

        try
        {
            cache = Plugin.TextureProvider.GetFromManifestResource(Assembly.GetExecutingAssembly(), name);
        }
        catch (Exception ex)
        {
            // A missing sprite is a cosmetic loss, never a reason to take the window down.
            Plugin.Log.Warning(ex, "RaidPlan could not load the sprite {Name}.", name);
        }

        return cache;
    }

    private static bool TryHandle(ISharedImmediateTexture? texture, out ImTextureID id)
    {
        id = default;
        if (texture == null)
            return false;

        if (!texture.TryGetWrap(out var wrap, out _) || wrap == null)
            return false;

        id = wrap.Handle;
        return true;
    }

    /// <summary>
    /// Draws a soft shadow behind a rounded rectangle, nine-sliced so the corners stay round at
    /// any window size.
    /// </summary>
    public static void Shadow(ImDrawListPtr drawList, Vector2 min, Vector2 max, float spread, float alpha)
    {
        if (!TryHandle(Load(ref shadow, ShadowResource), out var id))
            return;

        var tint = Palette.Pack(0x000000, alpha);
        var outer = spread;

        // The sprite is one rounded blob; slicing it at the inset keeps the blur width constant
        // instead of stretching a corner across a whole edge.
        var u = ShadowInset / ShadowSize;
        var l = min.X - outer;
        var t = min.Y - outer;
        var r = max.X + outer;
        var b = max.Y + outer;
        var ix0 = min.X + outer;
        var iy0 = min.Y + outer;
        var ix1 = max.X - outer;
        var iy1 = max.Y - outer;

        if (ix1 <= ix0 || iy1 <= iy0)
            return;

        // corners
        drawList.AddImage(id, new Vector2(l, t), new Vector2(ix0, iy0), new Vector2(0, 0), new Vector2(u, u), tint);
        drawList.AddImage(id, new Vector2(ix1, t), new Vector2(r, iy0), new Vector2(1 - u, 0), new Vector2(1, u), tint);
        drawList.AddImage(id, new Vector2(l, iy1), new Vector2(ix0, b), new Vector2(0, 1 - u), new Vector2(u, 1), tint);
        drawList.AddImage(id, new Vector2(ix1, iy1), new Vector2(r, b), new Vector2(1 - u, 1 - u), new Vector2(1, 1), tint);

        // edges
        drawList.AddImage(id, new Vector2(ix0, t), new Vector2(ix1, iy0), new Vector2(u, 0), new Vector2(1 - u, u), tint);
        drawList.AddImage(id, new Vector2(ix0, iy1), new Vector2(ix1, b), new Vector2(u, 1 - u), new Vector2(1 - u, 1), tint);
        drawList.AddImage(id, new Vector2(l, iy0), new Vector2(ix0, iy1), new Vector2(0, u), new Vector2(u, 1 - u), tint);
        drawList.AddImage(id, new Vector2(ix1, iy0), new Vector2(r, iy1), new Vector2(1 - u, u), new Vector2(1, 1 - u), tint);

        // middle
        drawList.AddImage(id, new Vector2(ix0, iy0), new Vector2(ix1, iy1), new Vector2(u, u), new Vector2(1 - u, 1 - u), tint);
    }

    /// <summary>A soft circular glow centred on a point, tinted to whatever colour is passed.</summary>
    public static bool Glow(ImDrawListPtr drawList, Vector2 centre, float radius, uint tint)
    {
        if (!TryHandle(Load(ref glow, GlowResource), out var id))
            return false;

        var half = new Vector2(radius, radius);
        drawList.AddImage(id, centre - half, centre + half, Vector2.Zero, Vector2.One, tint);
        return true;
    }

    /// <summary>Dropped on unload so a reload does not hold a handle from the old load context.</summary>
    public static void Forget()
    {
        shadow = null;
        glow = null;
    }
}

using Dalamud.Configuration;

namespace Foxtrot;

public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    /// <summary>How loud previews are, independent of the game's own music volume.</summary>
    public float PreviewVolume { get; set; } = 0.8f;

    /// <summary>Fade the zone music down while a preview plays, and put it back after.</summary>
    public bool DuckGameMusic { get; set; } = true;

    /// <summary>What the zone music is taken down to. Zero is silence.</summary>
    public float DuckedMusicVolume { get; set; }

    /// <summary>Open the player automatically when a preview starts.</summary>
    public bool OpenPlayerOnPreview { get; set; } = true;

    /// <summary>Show the "Preview" entry on orchestrion rolls in your bags.</summary>
    public bool ContextMenuOnItems { get; set; } = true;

    /// <summary>Show it in the in-game orchestrion list too.</summary>
    public bool ContextMenuOnOrchestrionList { get; set; } = true;

    // ---------------------------------------------------------------- theme

    /// <summary>The dark glass look. Off falls back to whatever Dalamud's style is.</summary>
    public bool ThemeEnabled { get; set; } = true;

    /// <summary>Accent colour as 0xRRGGBB. Zero means the default indigo.</summary>
    public uint ThemeAccent { get; set; }

    /// <summary>Soft shadow behind the plugin's windows.</summary>
    public bool ThemeShadows { get; set; } = true;

    /// <summary>Track ids the player has starred, newest last.</summary>
    public System.Collections.Generic.List<uint> Favourites { get; set; } = new();

    public bool IsFavourite(uint songId) => Favourites.Contains(songId);

    public void ToggleFavourite(uint songId)
    {
        if (!Favourites.Remove(songId))
            Favourites.Add(songId);
    }
}

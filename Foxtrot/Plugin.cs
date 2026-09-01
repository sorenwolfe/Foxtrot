using System;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Foxtrot.Services;
using Foxtrot.UI;

namespace Foxtrot;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/foxtrot";
    private const string CommandAlias = "/orch";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IContextMenu ContextMenu { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;

    internal static Configuration Config { get; private set; } = null!;
    internal static SongLibrary Library { get; private set; } = null!;
    internal static PreviewPlayer Preview { get; private set; } = null!;
    internal static RollOwnership Ownership { get; private set; } = null!;
    internal static PlayerWindow Player { get; private set; } = null!;
    internal static BrowserWindow Browser { get; private set; } = null!;

    private static BgmDucker ducker = null!;
    private static BgmDucker previewVolume = null!;
    private static RollContextMenu? contextMenu;
    private static HoveredItem? hovered;
    private static ConfigWindow configWindow = null!;

    public readonly WindowSystem WindowSystem = new("Foxtrot");

    public Plugin()
    {
        try
        {
            // Everything here is ours. Nothing below this block has touched the game yet, so a
            // throw in any of it leaves nothing attached to clean up.
            Log.Debug("Foxtrot: reading settings.");
            Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

            Log.Debug("Foxtrot: reading the orchestrion sheets.");
            Library = new SongLibrary();
            Library.Load();

            Log.Debug("Foxtrot: building services.");
            Ownership = new RollOwnership();
            ducker = new BgmDucker(new GameMusicBus());
            previewVolume = new BgmDucker(new GameOrchestrionBus());
            Preview = new PreviewPlayer(ducker, previewVolume);

            Log.Debug("Foxtrot: building windows.");
            Player = new PlayerWindow(Preview);
            Browser = new BrowserWindow(Library, Preview);
            configWindow = new ConfigWindow();

            WindowSystem.AddWindow(Player);
            WindowSystem.AddWindow(Browser);
            WindowSystem.AddWindow(configWindow);

            Log.Debug("Foxtrot: attaching to the game.");
            Attach();

            Log.Debug("Foxtrot: loaded.");
        }
        catch (Exception ex)
        {
            // Dalamud does not call Dispose when a constructor throws. Anything already attached
            // would stay attached to an assembly that is about to be dropped, and the next load
            // would fail on a command that is still registered — reported only as "Load failed",
            // which is what sends someone hunting through the wrong version.
            Log.Error(ex, "Foxtrot failed to load; detaching what it had attached.");
            Detach();
            throw;
        }
    }

    /// <summary>
    /// Everything that reaches into the game. Kept together and done last, so there is exactly one
    /// place that has to be undone and exactly one moment when it becomes necessary.
    /// </summary>
    private void Attach()
    {
        // Before the menu, which reads from it.
        hovered = new HoveredItem();
        contextMenu = new RollContextMenu(Library, hovered, OnPreviewRequested);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the orchestrion browser. /foxtrot player opens the player, " +
                          "/foxtrot stop stops whatever is playing, /foxtrot diag reports what it read.",
        });

        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Short form of /foxtrot.",
        });

        Framework.Update += OnFrameworkUpdate;

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi += ToggleBrowser;
    }

    /// <summary>
    /// Undoes <see cref="Attach"/>. Safe to call twice, and safe on a half-built plugin.
    /// </summary>
    /// <remarks>
    /// Every step is null-tolerant and individually guarded. Removing a command that was never
    /// added is harmless; failing to remove one that was is what makes the next load impossible.
    /// </remarks>
    private void Detach()
    {
        Safely(() => Framework.Update -= OnFrameworkUpdate, "detach the frame hook");

        Safely(() => CommandManager?.RemoveHandler(CommandName), "remove " + CommandName);
        Safely(() => CommandManager?.RemoveHandler(CommandAlias), "remove " + CommandAlias);

        if (PluginInterface?.UiBuilder is { } ui)
        {
            Safely(() => ui.Draw -= WindowSystem.Draw, "detach the draw hook");
            Safely(() => ui.OpenConfigUi -= ToggleConfig, "detach the config button");
            Safely(() => ui.OpenMainUi -= ToggleBrowser, "detach the main button");
        }

        Safely(() => contextMenu?.Dispose(), "detach the context menu");
        contextMenu = null;

        Safely(() => hovered?.Dispose(), "stop watching the hovered item");
        hovered = null;
    }

    /// <summary>
    /// Services the preview once a frame.
    /// </summary>
    /// <remarks>
    /// This used to hang off drawing, so it only ran while a window happened to be open.
    /// Closing the player mid-track left the zone music ducked and the preview stuck at the
    /// spot it started from, and nothing brought either back until the window reappeared.
    /// </remarks>
    private static void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            Preview?.Poll();
        }
        catch (Exception ex)
        {
            // Once a frame is often enough that a log line per failure would bury the game.
            Log.Error(ex, "Foxtrot: the preview could not be serviced this frame.");
        }
    }

    internal static void SaveConfig() => PluginInterface.SavePluginConfig(Config);

    private static void OnPreviewRequested(Song song)
    {
        Preview.Play(song);

        if (Config.OpenPlayerOnPreview)
            Player.IsOpen = true;
    }

    private void OnCommand(string command, string arguments)
    {
        switch (arguments.Trim().ToLowerInvariant())
        {
            case "player":
                Player.IsOpen = !Player.IsOpen;
                break;

            case "stop":
                Preview.Stop();
                break;

            case "config" or "settings":
                ToggleConfig();
                break;

            case "diag" or "debug":
                ReportDiagnostics();
                break;

            default:
                ToggleBrowser();
                break;
        }
    }

    /// <summary>
    /// Prints what the plugin actually managed to read, into chat.
    /// </summary>
    /// <remarks>
    /// Whether a roll maps to a track depends on how the game's item sheets are laid out, which
    /// cannot be checked anywhere but a running game. Without this, "no Preview appeared" is a
    /// dead end — it looks identical whether the plugin is not running, the item is not a roll, or
    /// the mapping came up empty. This says which.
    /// </remarks>
    private static void ReportDiagnostics()
    {
        var library = Library;

        void Say(string line) => ChatGui.Print(line, "Foxtrot", null);

        Say($"{library.Count} track(s) readable, {library.RollItemCount} roll item(s) mapped.");
        Say($"Matched {library.RollsMatchedByAction} by item action, " +
            $"{library.RollsMatchedByName} by name, {library.RollsUnmatched} not matched.");
        Say($"Roll item category resolved to {library.RollCategoryId} " +
            $"(the long-standing value is {SongLibrary.FallbackRollCategory}).");

        if (library.RollItemCount == 0)
        {
            Say("Nothing mapped, so no roll will offer a preview. That is the bug to report.");
            return;
        }

        // A couple of real examples say more than any count: they show the mapping is not just
        // non-empty but pointing at the right music.
        foreach (var line in library.SampleMappings(3))
            Say("  e.g. " + line);

        Say($"Hovered item watch: {hovered?.Describe() ?? "not running"}.");
        Say($"Last right-click menu: {contextMenu?.LastMenu ?? "not attached"}.");

        // Only when the exact path is matching nothing, which is a real fault rather than noise:
        // name matching is English-only, so a client in any other language has no previews at all.
        if (library.RollsMatchedByAction == 0 && library.RollItemCount > 0)
        {
            Say("No roll matched by item action, so everything is riding on English names.");
            foreach (var line in library.SampleRollActions(3))
                Say("  raw: " + line);
        }
    }

    private void ToggleBrowser() => Browser.IsOpen = !Browser.IsOpen;

    private static void ToggleConfig() => configWindow.IsOpen = !configWindow.IsOpen;

    /// <summary>
    /// Runs a teardown step and swallows what it throws.
    /// </summary>
    /// <remarks>
    /// One failing step must not abandon the rest. A half-unloaded plugin leaves its commands
    /// registered, and the next version then refuses to load with nothing but "Load failed" to go
    /// on — which the player blames on the update.
    /// </remarks>
    private static void Safely(Action step, string what)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Foxtrot could not " + what + " while unloading.");
        }
    }

    public void Dispose()
    {
        // Detach from the game first. Everything after this is ours and can be leaked without
        // consequence; these are Dalamud's and must come off no matter what happens below.
        Detach();

        // Before anything else of ours: this is what gives the player their music back, and it is
        // the one step whose failure they would actually notice.
        Safely(() => Preview?.Dispose(), "stop the preview");
        Safely(() => previewVolume?.Dispose(), "restore the orchestrion volume");
        Safely(() => ducker?.Dispose(), "restore the game music");

        Safely(() => WindowSystem?.RemoveAllWindows(), "remove the windows");
        Safely(() => Player?.Dispose(), "dispose the player window");
        Safely(() => Browser?.Dispose(), "dispose the browser window");
        Safely(() => configWindow?.Dispose(), "dispose the settings window");

        Safely(() => PluginInterface?.SavePluginConfig(Config), "save the settings");
    }
}

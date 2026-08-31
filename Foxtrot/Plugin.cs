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
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal static Configuration Config { get; private set; } = null!;
    internal static SongLibrary Library { get; private set; } = null!;
    internal static PreviewPlayer Preview { get; private set; } = null!;
    internal static RollOwnership Ownership { get; private set; } = null!;
    internal static PlayerWindow Player { get; private set; } = null!;
    internal static BrowserWindow Browser { get; private set; } = null!;

    private static BgmDucker ducker = null!;
    private static RollContextMenu contextMenu = null!;
    private static ConfigWindow configWindow = null!;

    public readonly WindowSystem WindowSystem = new("Foxtrot");

    public Plugin()
    {
        Config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Library = new SongLibrary();
        Library.Load();

        Ownership = new RollOwnership();
        ducker = new BgmDucker(new GameMusicBus());
        Preview = new PreviewPlayer(ducker);

        Player = new PlayerWindow(Preview);
        Browser = new BrowserWindow(Library, Preview);
        configWindow = new ConfigWindow();

        WindowSystem.AddWindow(Player);
        WindowSystem.AddWindow(Browser);
        WindowSystem.AddWindow(configWindow);

        contextMenu = new RollContextMenu(Library, OnPreviewRequested);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the orchestrion browser. /foxtrot player opens the player, " +
                          "/foxtrot stop stops whatever is playing.",
        });

        CommandManager.AddHandler(CommandAlias, new CommandInfo(OnCommand)
        {
            HelpMessage = "Short form of /foxtrot.",
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        PluginInterface.UiBuilder.OpenMainUi += ToggleBrowser;
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

            default:
                ToggleBrowser();
                break;
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
        Safely(() => CommandManager.RemoveHandler(CommandName), "remove " + CommandName);
        Safely(() => CommandManager.RemoveHandler(CommandAlias), "remove " + CommandAlias);
        Safely(() => PluginInterface.UiBuilder.Draw -= WindowSystem.Draw, "detach the draw hook");
        Safely(() => PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig, "detach the config button");
        Safely(() => PluginInterface.UiBuilder.OpenMainUi -= ToggleBrowser, "detach the main button");
        Safely(contextMenu.Dispose, "detach the context menu");

        // Before anything else of ours: this is what gives the player their music back, and it is
        // the one step whose failure they would actually notice.
        Safely(Preview.Dispose, "stop the preview");
        Safely(ducker.Dispose, "restore the game music");

        Safely(WindowSystem.RemoveAllWindows, "remove the windows");
        Safely(Player.Dispose, "dispose the player window");
        Safely(Browser.Dispose, "dispose the browser window");
        Safely(configWindow.Dispose, "dispose the settings window");

        Safely(() => PluginInterface.SavePluginConfig(Config), "save the settings");
    }
}

using Dalamud.Game.Gui.ContextMenu;

namespace Foxtrot.Services;

/// <summary>
/// Works out which row is selected in the in-game orchestrion list.
/// </summary>
/// <remarks>
/// Not implemented, deliberately, and it returns false rather than guessing.
///
/// A roll in your bags is an item, and Dalamud hands us its id through a documented API — that
/// path is solid. A row in the orchestrion list is not an item; the selected index lives inside
/// the window's own agent, at an offset nobody can read off a reference assembly. Reaching in
/// there on a guessed offset is a pointer read into a structure we have not seen, in a menu
/// handler, on the game's main thread. That is a crash, not a bug.
///
/// Finding the offset takes one session with the game open and a debugger, and then this method
/// becomes a few lines. Until somebody has done that, the browser window is the way to preview a
/// track that is not in your bags — and it covers more, because it can play the ones you do not
/// own yet.
/// </remarks>
public static class OrchestrionListReader
{
    public static bool TryGetSelected(IMenuOpenedArgs args, SongLibrary library, out Song song)
    {
        song = default;
        return false;
    }
}

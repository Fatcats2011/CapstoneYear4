/// <summary>
/// What the pause menu does online, where pausing doesn't stop the match: the game goes on for everyone, and the pause
/// menu is this machine's own overlay (PlayerInstantiate). It has no "End match" row (its rows are hand-lettered art):
/// the host's Main Menu ends the match for everyone, and a client's leaves it. See docs/online.md
/// </summary>
public static class PausePolicy
{
    /// <summary>What pausing says on an online host</summary>
    public const string HOST_HINT = "Online: the match goes on while you're paused. Main Menu ends it for everyone.";

    /// <summary>What pausing says on an online client</summary>
    public const string CLIENT_HINT = "Online: the match goes on while you're paused. Main Menu leaves it.";

    /// <summary>
    /// Whether a pause menu stays open when the game switches to this state: only while players drive (the tutorial, the
    /// waves, the golden round). Anything else (a cutscene, the results, loading, the menus) closes it
    /// </summary>
    public static bool KeepsPause(GameState state)
    {
        return state == GameState.Tutorial || state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
    }

    /// <summary>
    /// What pausing says: nothing offline (the game stops), else what Main Menu does on this machine
    /// </summary>
    public static string HintFor(NetworkRole role)
    {
        switch (role)
        {
            case NetworkRole.Host:
                return HOST_HINT;
            case NetworkRole.Client:
                return CLIENT_HINT;
            default:
                return null;
        }
    }
}

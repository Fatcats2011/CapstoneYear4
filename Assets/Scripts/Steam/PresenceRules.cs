/// <summary>
/// What friends see in Steam for each game state: a token of the localization file uploaded in Steamworks
/// (docs/steam/rich-presence-english.vdf, which must hold every token here), and the player count it shows. See
/// docs/steam/in-game-features.md
/// </summary>
public static class PresenceRules
{
    /// <summary>The key Steam shows, as a token of the localization file</summary>
    public const string DISPLAY = "steam_display";

    /// <summary>The player count, which the tokens show as %players%</summary>
    public const string PLAYERS = "players";

    /// <summary>
    /// The token for a game state with this many players (one or fewer is "solo"), or null when the state changes
    /// nothing (a load, a pause), so the last one stays
    /// </summary>
    public static string For(GameState state, int players)
    {
        bool solo = players <= 1;
        switch (state)
        {
            case GameState.Menu:
            case GameState.Options:
            case GameState.Credits:
                return "#Menus";
            case GameState.PlayerSelect:
                return "#GettingReady";
            case GameState.Tutorial:
                return "#Tutorial";
            case GameState.StartingCutscene:
            case GameState.Begin:
            case GameState.MainLoop:
                return solo ? "#DeliveringSolo" : "#Delivering";
            case GameState.GoldenCutscene:
            case GameState.FinalPackage:
                return solo ? "#GoldenSolo" : "#Golden";
            case GameState.Results:
                return "#Results";
            default:
                return null;
        }
    }
}

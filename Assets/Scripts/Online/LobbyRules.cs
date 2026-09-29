/// <summary>
/// Steam lobbies for online play: their tags, whether this game joins one, the lobby Steam starts the game to join
/// (+connect_lobby), and whether this machine can answer an invite right now. See docs/online.md
/// </summary>
public static class LobbyRules
{
    public const string GAME_KEY = "game";
    public const string GAME = "doa";
    public const string BUILD_KEY = "build";
    public const string CONNECT_LOBBY = "+connect_lobby";

    public const string NOT_THIS_GAME = "That lobby isn't a Dead on Arrival match.";
    public const string BUSY = "Finish your match before joining a friend's.";
    public const string ALREADY_ONLINE = "Leave your online match before joining another.";
    public const string ONE_PLAYER = "Online play is one player per machine: the other players here need to leave first.";

    /// <summary>
    /// Why this machine won't join a lobby, or null to join it
    /// </summary>
    /// <param name="lobbyGame">The lobby's game tag (Steam's test app is shared by every game made with it)</param>
    /// <param name="lobbyBuild">The lobby's build tag: its host's version</param>
    /// <param name="myBuild">This machine's version</param>
    public static string Refusal(string lobbyGame, string lobbyBuild, string myBuild)
    {
        if (lobbyGame != GAME)
            return NOT_THIS_GAME;

        // Worded as the host turns a direct join away
        return JoinRules.Refusal(lobbyBuild, myBuild, 0, GameState.Menu);
    }

    /// <summary>
    /// The lobby Steam started the game to join (a friend's invite starts it with "+connect_lobby id"), or 0
    /// </summary>
    public static ulong LobbyToJoin(string[] args)
    {
        if (args == null)
            return 0;

        for (int i = 0; i < args.Length - 1; i++)
        {
            ulong lobby;
            if (args[i] == CONNECT_LOBBY && ulong.TryParse(args[i + 1], out lobby))
                return lobby;
        }
        return 0;
    }

    /// <summary>
    /// Whether the game is in its menus: the title screen, options, credits or player select
    /// </summary>
    public static bool InTheMenus(GameState state)
    {
        switch (state)
        {
            case GameState.Default:
            case GameState.Menu:
            case GameState.Options:
            case GameState.Credits:
            case GameState.PlayerSelect:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Why this machine can't answer an invite now, or null when it can. Being online comes first, then a match, then
    /// the players here
    /// </summary>
    /// <param name="state">What the game is doing</param>
    /// <param name="localPlayers">Players on this machine</param>
    /// <param name="online">Whether this machine is online already</param>
    public static string Busy(GameState state, int localPlayers, bool online)
    {
        if (online)
            return ALREADY_ONLINE;

        if (!InTheMenus(state))
            return BUSY;

        if (localPlayers > 1)
            return ONE_PLAYER;

        return null;
    }
}

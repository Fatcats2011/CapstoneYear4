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
    /// A build's tag: its version, then its Netcode setup (OnlineSession.NetcodeSetup), the hash Netcode compares when a
    /// player joins. Two builds of one version can differ there, and Netcode then drops the joiner without a reason
    /// </summary>
    public static string BuildTag(string version, ulong netcodeSetup)
    {
        return version + "/" + netcodeSetup.ToString("x16");
    }

    /// <summary>
    /// The version in a build tag: the part before the last "/", or the whole tag when it has no setup
    /// </summary>
    public static string VersionOf(string buildTag)
    {
        int slash = buildTag.LastIndexOf('/');
        return slash < 0 ? buildTag : buildTag.Substring(0, slash);
    }

    /// <summary>
    /// Why this machine won't join a lobby on its own version but another build
    /// </summary>
    public static string OtherBuild(string version)
    {
        return "That match is on another build of version " + Shown(version) + ". You both need the same build.";
    }

    /// <summary>The most of another machine's version string ever shown or logged</summary>
    public const int SHOWN_VERSION = 32;

    /// <summary>
    /// Another machine's version as shown in a hint or the log: no control characters (a new line could forge a log
    /// line), and at most SHOWN_VERSION characters
    /// </summary>
    public static string Shown(string version)
    {
        if (string.IsNullOrEmpty(version))
            return "";

        System.Text.StringBuilder shown = new System.Text.StringBuilder(SHOWN_VERSION);
        foreach (char c in version)
        {
            if (char.IsControl(c))
                continue;
            if (shown.Length == SHOWN_VERSION)
                break;
            shown.Append(c);
        }
        return shown.ToString();
    }

    /// <summary>
    /// Why this machine won't join a lobby, or null to join it
    /// </summary>
    /// <param name="lobbyGame">The lobby's game tag (Steam's test app is shared by every game made with it)</param>
    /// <param name="lobbyBuild">The lobby's build tag (BuildTag): its host's version and Netcode setup</param>
    /// <param name="myBuild">This machine's build tag</param>
    public static string Refusal(string lobbyGame, string lobbyBuild, string myBuild)
    {
        if (lobbyGame != GAME)
            return NOT_THIS_GAME;

        if (lobbyBuild == myBuild)
            return null;

        // Another version is worded as the host turns a direct join away
        string lobbyVersion = VersionOf(lobbyBuild);
        string myVersion = VersionOf(myBuild);
        if (lobbyVersion != myVersion)
            return JoinRules.Refusal(lobbyVersion, myVersion, 0, GameState.Menu);

        return OtherBuild(myVersion);
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

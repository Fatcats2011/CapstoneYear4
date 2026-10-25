/// <summary>
/// Who the host lets into an online match: a player on the same build, while a seat is free and the host is still in
/// the menus. Nobody joins a match that has started
/// </summary>
public static class JoinRules
{
    public static string FULL => Loc.Get("online.full");
    public static string STARTED => Loc.Get("online.started");
    public static string NOT_THIS_GAME => Loc.Get("online.notthisgame");

    /// <summary>
    /// Why the host turns a joining player away, or null to let them in
    /// </summary>
    /// <param name="hostVersion">The host's build version</param>
    /// <param name="joinerVersion">The build version the player sent (empty if they sent none)</param>
    /// <param name="seatsTaken">Players already let in, the host included</param>
    /// <param name="hostState">What the host's game is doing</param>
    public static string Refusal(string hostVersion, string joinerVersion, int seatsTaken, GameState hostState)
    {
        return HostNotice.Shown(RefusalNotice(hostVersion, joinerVersion, seatsTaken, hostState));
    }

    /// <summary>
    /// The refusal as the host sends it (HostNotice): a key and its arguments, read in the player's own language. The
    /// joiner's version, unknown, is the key for "an unknown version" itself, put into words on their machine
    /// </summary>
    public static string RefusalNotice(string hostVersion, string joinerVersion, int seatsTaken, GameState hostState)
    {
        // Both versions come from another machine: what's shown (and logged) is cleaned and cut (LobbyRules.Shown)
        if (joinerVersion != hostVersion)
            return HostNotice.Encode("online.version", LobbyRules.Shown(hostVersion),
                string.IsNullOrEmpty(joinerVersion) ? Loc.Get("online.unknownversion") : LobbyRules.Shown(joinerVersion));

        if (seatsTaken >= Constants.MAX_PLAYERS)
            return HostNotice.Encode("online.full");

        if (HasStarted(hostState))
            return HostNotice.Encode("online.started");

        return null;
    }

    // Players may join while the host is on the title screen, in options or credits, or in player select
    static bool HasStarted(GameState hostState)
    {
        switch (hostState)
        {
            case GameState.Default:
            case GameState.Menu:
            case GameState.Options:
            case GameState.Credits:
            case GameState.PlayerSelect:
                return false;
            default:
                return true;
        }
    }
}

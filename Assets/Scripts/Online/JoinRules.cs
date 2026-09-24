/// <summary>
/// Who the host lets into an online match: a player on the same build, while a seat is free and the host is still in
/// the menus. Nobody joins a match that has started
/// </summary>
public static class JoinRules
{
    public const string FULL = "That match is full.";
    public const string STARTED = "That match has already started.";

    /// <summary>
    /// Why the host turns a joining player away, or null to let them in
    /// </summary>
    /// <param name="hostVersion">The host's build version</param>
    /// <param name="joinerVersion">The build version the player sent (empty if they sent none)</param>
    /// <param name="seatsTaken">Players already let in, the host included</param>
    /// <param name="hostState">What the host's game is doing</param>
    public static string Refusal(string hostVersion, string joinerVersion, int seatsTaken, GameState hostState)
    {
        if (joinerVersion != hostVersion)
            return "That match is on version " + hostVersion + " and you have "
                + (string.IsNullOrEmpty(joinerVersion) ? "an unknown version" : joinerVersion) + ". You both need the same version.";

        if (seatsTaken >= Constants.MAX_PLAYERS)
            return FULL;

        if (HasStarted(hostState))
            return STARTED;

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

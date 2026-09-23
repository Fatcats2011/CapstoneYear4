/// <summary>
/// Steam App ID and launch rules
/// </summary>
public static class SteamStartup
{
    /// <summary>
    /// Valve's public test app (Spacewar), used until the Dead on Arrival App ID is approved
    /// </summary>
    public const uint TEST_APP_ID = 480;

    /// <summary>
    /// The game's Steam App ID. Replace with the real one once Steamworks approves the app (and update steam_appid.txt)
    /// </summary>
    public const uint APP_ID = TEST_APP_ID;

    ///<summary>
    /// Only a real App ID in a player build relaunches the game through the Steam client
    ///</summary>
    public static bool ShouldRestartThroughSteam(uint appId, bool isEditor)
    {
        return !isEditor && appId != TEST_APP_ID;
    }
}

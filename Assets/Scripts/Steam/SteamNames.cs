#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// This player's Steam name (Steamworks.NET). Without Steam nothing calls Steamworks, which throws without Steam
/// </summary>
public static class SteamNames
{
    /// <summary>The persona name of whoever is signed in to Steam, or "" without Steam</summary>
    public static string Mine()
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized)
            return SteamFriends.GetPersonaName();
#endif
        return "";
    }

    /// <summary>The language the player runs Steam in ("french", "latam"…), or null without Steam</summary>
    public static string Language()
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized)
            return SteamApps.GetCurrentGameLanguage();
#endif
        return null;
    }

    /// <summary>The Steam ID of whoever is signed in to Steam, or 0 without Steam</summary>
    public static ulong MyId()
    {
#if !DISABLESTEAMWORKS
        if (SteamManager.Initialized)
            return SteamUser.GetSteamID().m_SteamID;
#endif
        return 0;
    }
}

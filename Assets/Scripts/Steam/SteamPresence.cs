#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Steam's Rich Presence (Steamworks.NET). Steam clears it when the game shuts down. Without Steam nothing calls
/// Steamworks, which throws without Steam
/// </summary>
public class SteamPresence : IPresence
{
    /// <summary>Whether Steam is running</summary>
    public bool Available { get { return SteamManager.Initialized; } }

    public void Set(string key, string value)
    {
        if (!Available)
            return;

#if !DISABLESTEAMWORKS
        SteamFriends.SetRichPresence(key, value);
#endif
    }
}

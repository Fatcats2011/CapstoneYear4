#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System.Collections.Generic;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Steam's achievements (Steamworks.NET) for this PC's Steam user. Steam loads the user's stats by itself, so an unlock
/// is SetAchievement, then StoreStats. Without Steam nothing calls Steamworks, which throws without Steam. An app
/// without an achievement of that name (the test app, 480, has neither of the game's) gets one warning per name
/// </summary>
public class SteamAchievementStore : IAchievementStore
{
    readonly HashSet<string> warned = new HashSet<string>();

    /// <summary>Whether Steam is running</summary>
    public bool Available { get { return SteamManager.Initialized; } }

    public void Unlock(Achievement achievement)
    {
        if (!Available)
            return;

#if !DISABLESTEAMWORKS
        string name = AchievementNames.ApiName(achievement);
        if (name == null)
            return;
        if (SteamUserStats.GetAchievement(name, out bool achieved) && achieved)
            return;

        if (!SteamUserStats.SetAchievement(name))
        {
            if (warned.Add(name))
                Debug.LogWarning("Steam: this app has no achievement " + name);
            return;
        }
        SteamUserStats.StoreStats();
#endif
    }
}

/// <summary>
/// Where this PC's Steam user's achievements unlock: Steam's in the game (SteamAchievementStore), a fake in tests.
/// SteamFeatures unlocks offline, OnlineAchievements online. See docs/steam/in-game-features.md
/// </summary>
public interface IAchievementStore
{
    /// <summary>Whether achievements work here (Steam is running)</summary>
    bool Available { get; }

    /// <summary>Unlocks an achievement. One that's unlocked already stays as it is</summary>
    void Unlock(Achievement achievement);
}

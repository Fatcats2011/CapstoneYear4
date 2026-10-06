/// <summary>
/// What this PC's Steam user's friends see them doing (Rich Presence): Steam's in the game (SteamPresence), a fake in
/// tests. SteamFeatures sets it from the game state (PresenceRules). See docs/steam/in-game-features.md
/// </summary>
public interface IPresence
{
    /// <summary>Whether Rich Presence works here (Steam is running)</summary>
    bool Available { get; }

    /// <summary>Sets one Rich Presence key</summary>
    void Set(string key, string value);
}

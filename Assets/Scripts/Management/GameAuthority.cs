using UnityEngine;

/// <summary>
/// This machine's part in a match: a local (split-screen) match, or the host or a client of an online one (Phase 3)
/// </summary>
public enum NetworkRole { Offline, Host, Client }

/// <summary>
/// Who decides a match's rules: game states, waves and order spawns, pickups and deliveries, steals and clashes, scores.
/// In a local match this machine does. Online (Phase 3) the host does, and clients show what the host decided
/// </summary>
public static class GameAuthority
{
    /// <summary>
    /// This machine's part in the match. Offline until online play starts a session, and again after it ends
    /// </summary>
    public static NetworkRole Role { get; set; } = NetworkRole.Offline;

    /// <summary>
    /// Whether this machine decides the rules
    /// </summary>
    public static bool IsAuthority { get { return Role != NetworkRole.Client; } }

    /// <summary>
    /// Whether the match is played over the network
    /// </summary>
    public static bool IsOnline { get { return Role != NetworkRole.Offline; } }

    /// <summary>
    /// Pauses or slows the game clock in a local match. Online, everyone shares one clock, so this does nothing
    /// </summary>
    public static void SetTimeScale(float scale)
    {
        if (IsOnline)
            return;

        Time.timeScale = scale;
    }
}

using System;

/// <summary>
/// A machine's steal requests on their way out of the game code (online): its player, boosting, hit another machine's
/// player. The host decides whether that's a steal, a clash or nothing (StealRules), for everyone. OnlineSteals sends a
/// client's requests to the host, and judges the host's own. Offline nothing listens: a local match hits at once.
/// See docs/online.md
/// </summary>
public static class StealSync
{
    /// <summary>Online: this machine's player (the attacker's seat), boosting, hit another player (the victim's seat)</summary>
    public static event Action<int, int> Asked;

    /// <summary>
    /// Online: this machine's player, boosting, hit another player: the host decides what happens
    /// </summary>
    public static void Ask(int attacker, int victim)
    {
        Asked?.Invoke(attacker, victim);
    }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        Asked = null;
    }
}

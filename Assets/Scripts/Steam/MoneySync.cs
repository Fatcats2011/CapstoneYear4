using System;
using UnityEngine;

/// <summary>
/// Money earned, on its way out of the game code, for the results cards' breakdown. As FeatSync: the game says (OrderHandler,
/// beside every change to a score, on the machine that runs the rules), MatchAwards counts (MoneyTally).
/// </summary>
public static class MoneySync
{
    /// <summary>A player (their seat) earned some money, and from which source</summary>
    public static event Action<int, MoneySource, int> Earned;

    /// <summary>A player (their seat) earned this much from this source. Nothing is said for no money</summary>
    public static void Earn(int seat, MoneySource source, int amount)
    {
        if (amount <= 0)
            return;
        Earned?.Invoke(seat, source, amount);
    }

    /// <summary>Forgets every listener (a new Play Mode session)</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        Earned = null;
    }
}

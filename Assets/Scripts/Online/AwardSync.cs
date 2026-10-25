using System;

/// <summary>
/// The awards on their way between the game code and the network, and the last ones, kept for a results screen that
/// opens after they arrive. A new match forgets them (Clear)
/// </summary>
public static class AwardSync
{
    /// <summary>The rules' machine picked the awards: the host shares them (OnlineAwards)</summary>
    public static event Action<AwardBoard> Shared;

    /// <summary>Client: the host's awards arrived</summary>
    public static event Action<AwardBoard> Arrived;

    /// <summary>The last awards picked or heard this match</summary>
    public static AwardBoard Last { get; private set; }

    /// <summary>Whether there are any this match</summary>
    public static bool HasLast { get; private set; }

    public static void Decide(AwardBoard board)
    {
        Keep(board);
        Shared?.Invoke(board);
    }

    public static void Arrive(AwardBoard board)
    {
        Keep(board);
        Arrived?.Invoke(board);
    }

    /// <summary>A new match: no awards yet</summary>
    public static void Clear()
    {
        Last = default;
        HasLast = false;
    }

    /// <summary>
    /// Forgets every listener and the last awards (tests, a new Play Mode session)
    /// </summary>
    public static void Reset()
    {
        Shared = null;
        Arrived = null;
        Clear();
    }

    static void Keep(AwardBoard board)
    {
        Last = board;
        HasLast = true;
    }
}

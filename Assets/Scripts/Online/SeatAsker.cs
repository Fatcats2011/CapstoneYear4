using System;

/// <summary>
/// How many seats this machine asks the host for (Phase 3J): one for each of its players without a seat, and not again
/// while those asks are on their way. A seat that comes, or a refusal, answers one ask. OnlineGame uses it
/// </summary>
public class SeatAsker
{
    /// <summary>How many asks are on their way</summary>
    public int Asking { get; private set; }

    /// <summary>
    /// How many new asks to send for this many players without a seat. Counted as asking before they go: the host
    /// answers its own at once
    /// </summary>
    public int ToAsk(int unseated)
    {
        int more = Math.Max(0, unseated - Asking);
        Asking += more;
        return more;
    }

    /// <summary>One ask was answered (a seat came, or a refusal)</summary>
    public void Answered()
    {
        Asking = Math.Max(0, Asking - 1);
    }

    /// <summary>Forgets every ask (the session ended)</summary>
    public void Reset()
    {
        Asking = 0;
    }
}

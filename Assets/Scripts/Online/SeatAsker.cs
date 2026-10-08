using System;

/// <summary>
/// How many seats this machine asks the host for (Phase 3J): one for each of its players without a seat, and not again
/// while those asks are on their way. A seat that comes, or a refusal, answers one ask. Asks with no answer for
/// ANSWER_WAIT seconds count as lost (the host's rate limit drops the excess) and go again. OnlineGame uses it
/// </summary>
public class SeatAsker
{
    /// <summary>How many asks are on their way</summary>
    public int Asking { get; private set; }

    /// <summary>How long an ask may go unanswered before it counts as lost, in seconds (a round trip is well under 1 s)</summary>
    public const float ANSWER_WAIT = 3f;

    float lastAsked;

    /// <summary>
    /// How many new asks to send for this many players without a seat, now (real time, in seconds). Counted as asking
    /// before they go: the host answers its own at once
    /// </summary>
    public int ToAsk(int unseated, float now)
    {
        if (Asking > 0 && now - lastAsked > ANSWER_WAIT)
            Asking = 0; // lost on the way

        int more = Math.Max(0, unseated - Asking);
        Asking += more;
        if (more > 0)
            lastAsked = now;
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

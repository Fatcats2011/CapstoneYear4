using System;
using UnityEngine;

/// <summary>
/// Tells this machine that a player earned money and why, or that first place changed hands, so each player's callout
/// can show it (StealCallout). Every machine announces a delivery as it replays it (OrderHandler), and a lead change from
/// the scoreboard's own ranking (Scoreboard), so all of them tell the same story without a new message. Players go by their seat
/// </summary>
public static class ScoreFeedback
{
    /// <summary>A player's money went up: seat, why, how much, and the stamp on the order ("" for none)</summary>
    public static event Action<int, GainReason, int, string> Gained;

    /// <summary>First place changed hands: the new leader's seat, then the old leader's</summary>
    public static event Action<int, int> LeadTaken;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        Gained = null;
        LeadTaken = null;
    }

    /// <summary>A player earned money. An amount of nothing or less (a bad value), or no seat, is dropped; no stamp reads as ""</summary>
    public static void AnnounceGain(int seat, GainReason reason, int amount, string stamp)
    {
        if (seat < 0 || amount <= 0)
            return;
        Gained?.Invoke(seat, reason, amount, stamp ?? "");
    }

    /// <summary>First place changed hands</summary>
    public static void AnnounceLead(int newLeader, int oldLeader)
    {
        LeadTaken?.Invoke(newLeader, oldLeader);
    }
}

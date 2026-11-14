using System;
using UnityEngine;

/// <summary>One player boosted into another, and what it did (StealFeedbackRules.Outcome). Players go by their seat</summary>
public readonly struct BumpNotice
{
    /// <summary>The player who boosted in: a steal's thief</summary>
    public readonly int Attacker;
    /// <summary>The player they hit</summary>
    public readonly int Victim;
    public readonly BumpOutcome Outcome;
    /// <summary>What the thief was paid for the steal: 0 when nothing was</summary>
    public readonly int Paid;

    public BumpNotice(int attacker, int victim, BumpOutcome outcome, int paid)
    {
        Attacker = attacker;
        Victim = victim;
        Outcome = outcome;
        Paid = paid;
    }
}

/// <summary>
/// Tells this machine that a bump happened and what it did, so each player's callout, rumble, camera kick and sounds can
/// show it (StealCallout). A local match announces a bump as it happens (OrderHandler); online every machine announces
/// the host's (OnlineSteals), so all of them tell the same story
/// </summary>
public static class StealFeedback
{
    public static event Action<BumpNotice> Happened;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        Happened = null;
    }

    /// <summary>A bump happened. An outcome this build doesn't know (a newer host's) is dropped</summary>
    public static void Announce(BumpNotice notice)
    {
        if (StealFeedbackRules.Known((byte)notice.Outcome))
            Happened?.Invoke(notice);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Why a player's money went up on a delivery (GainFeedbackRules.ReasonFor). Only whole deliveries have a reason</summary>
public enum GainReason : byte { Delivered, Golden, Lode }

/// <summary>
/// What players are told when money comes in, and when first place changes hands. Every number and word choice is here,
/// to tune in playtests:
/// - ReasonFor, Key: which line a delivery reads (a strings.csv key).
/// - BoardShowsGains, LeadCalled: which modes show a "+$" on the scoreboard, and which call a change of lead.
/// - CountUp, PopAlpha: how the scoreboard's number counts up to its new total, and how its "+$" fades.
/// - NewLeader, LeadChanged: who leads a ranking, and when a new leader is worth calling.
/// OrderHandler announces gains and the scoreboard announces leads (ScoreFeedback); StealCallout and Scoreboard show them.
/// </summary>
public static class GainFeedbackRules
{
    /// <summary>Two gains this close together are one line, with the sum</summary>
    public const float MERGE_SECONDS = 0.5f;

    /// <summary>How long the scoreboard's "+$" is up, its fade included, and how long that fade is</summary>
    public const float BOARD_POP_SECONDS = 1.2f;
    public const float BOARD_POP_FADE = 0.3f;

    /// <summary>How long the scoreboard's number takes to count up to a new total</summary>
    public const float COUNT_UP_SECONDS = 0.4f;

    /// <summary>How long a gain line is up before a lead line may replace it</summary>
    public const float GAIN_HOLD = 0.8f;

    /// <summary>Why a delivery paid: the Mother Lode first, then the golden order, otherwise a plain delivery</summary>
    public static GainReason ReasonFor(Constants.OrderValue tier, bool isLode)
    {
        if (isLode)
            return GainReason.Lode;
        return tier == Constants.OrderValue.Golden ? GainReason.Golden : GainReason.Delivered;
    }

    /// <summary>The line a gain reads (a strings.csv key). A plain delivery with a stamp ("×2") has its own, which shows it</summary>
    public static string Key(GainReason reason, string stamp)
    {
        switch (reason)
        {
            case GainReason.Golden: return "gain.golden";
            case GainReason.Lode: return "gain.lode";
            default: return string.IsNullOrEmpty(stamp) ? "gain.delivered" : "gain.delivered_stolen";
        }
    }

    /// <summary>Whether the scoreboard's rows show money in this mode, so a "+$" can sit beside it</summary>
    public static bool BoardShowsGains(GameMode mode)
    {
        return mode == GameMode.Standard || mode == GameMode.GoldRush;
    }

    /// <summary>Whether this mode's scoreboard ranks by something a lead can change on, and so calls it</summary>
    public static bool LeadCalled(GameMode mode)
    {
        return mode == GameMode.Standard || mode == GameMode.GoldRush || mode == GameMode.KingsCrown;
    }

    /// <summary>The number the scoreboard shows at a time since a gain landed: from, then straight up to "to" over COUNT_UP_SECONDS</summary>
    public static int CountUp(int from, int to, float age)
    {
        if (age <= 0f)
            return from;
        if (age >= COUNT_UP_SECONDS)
            return to;
        return Mathf.RoundToInt(Mathf.Lerp(from, to, age / COUNT_UP_SECONDS));
    }

    /// <summary>How solid the "+$" is at a time since the gain: solid, then fading over the last BOARD_POP_FADE, gone by BOARD_POP_SECONDS</summary>
    public static float PopAlpha(float age)
    {
        if (age < 0f || age >= BOARD_POP_SECONDS)
            return 0f;
        float left = BOARD_POP_SECONDS - age;
        return left >= BOARD_POP_FADE ? 1f : left / BOARD_POP_FADE;
    }

    /// <summary>
    /// Who leads. The first seat of the ranking, if it is strictly ahead of the second (or alone with more than nothing).
    /// Otherwise a tie, or no points yet, changes nothing: the last leader keeps it while they are still ranked, else nobody leads (-1)
    /// </summary>
    /// <param name="ranking">Seats, best first</param>
    /// <param name="points">What a seat has, in the unit the ranking is in</param>
    public static int NewLeader(int lastLeader, IReadOnlyList<int> ranking, Func<int, int> points)
    {
        if (ranking == null || ranking.Count == 0)
            return -1;

        int top = ranking[0];
        int topPoints = points(top);
        bool strictlyAhead = ranking.Count > 1 ? topPoints > points(ranking[1]) : topPoints > 0;
        if (strictlyAhead)
            return top;

        for (int i = 0; i < ranking.Count; i++)
            if (ranking[i] == lastLeader)
                return lastLeader;
        return -1;
    }

    /// <summary>Whether first place moved from one player to another. Not for the first leader of a match, nor when nobody leads now</summary>
    public static bool LeadChanged(int lastLeader, int newLeader)
    {
        return lastLeader >= 0 && newLeader >= 0 && newLeader != lastLeader;
    }
}

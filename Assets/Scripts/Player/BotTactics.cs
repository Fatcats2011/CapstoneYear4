using System.Collections.Generic;
using UnityEngine;

/// <summary>What a CPU driver can go for: a free order, or a rival carrying one (a steal)</summary>
public enum BotOptionKind
{
    Order,
    Steal
}

/// <summary>One thing a CPU driver could go for, as BotTactics sees it</summary>
public struct BotOption
{
    public BotOptionKind Kind;
    /// <summary>The order's Worth (a steal: the victim's best order's)</summary>
    public int Worth;
    public bool Golden;
    /// <summary>How far away it is, in metres</summary>
    public float Distance;
    /// <summary>A steal: whether the victim leads the scoreboard</summary>
    public bool VictimLeads;
    /// <summary>A steal: whether the victim is boosting (a hit would be a clash, not a steal)</summary>
    public bool VictimBoosting;
}

/// <summary>A CPU driver's own state, as BotTactics sees it</summary>
public struct BotSituation
{
    public BotProfile Profile;
    public int MyScore;
    /// <summary>The highest score of everyone seated, the bot's own included</summary>
    public int LeaderScore;
    /// <summary>How many orders it holds (0-2)</summary>
    public int Held;
    /// <summary>The match's Steals rule</summary>
    public bool StealsOn;
}

/// <summary>
/// A CPU driver's tactics (docs/superpowers/specs/2026-10-28-bot-personalities-design.md): it scores each free order and
/// each steal by its personality's weights, shifted by the scoreboard. Behind the leader it gets desperate (values more,
/// minds distance less, steals more); leading it plays safe. Steals prefer the leader. BotDriver feeds it the scene
/// </summary>
public static class BotTactics
{
    /// <summary>This far behind the leader, in points, a bot is fully desperate</summary>
    public const int FULL_GAP = 200;
    /// <summary>A bot carrying an order detours only for something this close, in metres</summary>
    public const float DETOUR = 25f;
    /// <summary>What it's going for already scores this much more (so it doesn't flick between near equals)</summary>
    public const float STICKY = 1.25f;
    /// <summary>A steal from the leader scores this much more</summary>
    public const float LEADER_BONUS = 1.5f;
    /// <summary>A steal from a boosting victim scores this much (a clash, not a steal)</summary>
    public const float BOOSTING_VICTIM = 0.2f;

    /// <summary>How desperate a bot is: 0 level or leading, 1 at FULL_GAP behind the leader or more</summary>
    public static float Desperation(int myScore, int leaderScore)
    {
        return Mathf.Clamp01((leaderScore - myScore) / (float)FULL_GAP);
    }

    /// <summary>Whether a bot leads (a tie for the top counts)</summary>
    public static bool Leads(int myScore, int leaderScore)
    {
        return myScore >= leaderScore;
    }

    /// <summary>What an option is worth to the bot; negative infinity when it can't go for it</summary>
    public static float Score(BotSituation s, BotOption o)
    {
        if (s.Held >= 2 || (s.Held == 1 && o.Distance > DETOUR))
            return float.NegativeInfinity;

        BotProfile p = s.Profile;
        float d = Desperation(s.MyScore, s.LeaderScore);
        float value = o.Worth * p.ValuePerPoint * (1f + d) + (o.Golden ? p.GoldenBonus : 0f);
        float distance = o.Distance * p.DistancePerMetre * (1f - 0.5f * d) * (Leads(s.MyScore, s.LeaderScore) ? 1.5f : 1f);
        if (o.Kind == BotOptionKind.Order)
            return value - distance;

        float appetite = p.StealAppetite + p.DesperateSteal * d;
        // A golden order is big whatever its Worth (50, below an easy one's)
        if (!s.StealsOn || appetite <= 0f || (o.Worth < p.MinStealWorth && !o.Golden))
            return float.NegativeInfinity;
        return value * appetite * (o.VictimLeads ? LEADER_BONUS : 1f) * (o.VictimBoosting ? BOOSTING_VICTIM : 1f) - distance;
    }

    /// <summary>
    /// The option to go for (current: the one it went for last, or -1), or -1: deliver what it holds, or idle holding
    /// nothing with nothing to go for. Holding something, it only turns aside for a close option that scores above 0
    /// </summary>
    public static int Choose(BotSituation s, IReadOnlyList<BotOption> options, int current)
    {
        int best = -1;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < options.Count; i++)
        {
            float score = Score(s, options[i]);
            if (float.IsNegativeInfinity(score))
                continue;
            if (i == current && score > 0f)
                score *= STICKY;
            if (best < 0 || score > bestScore)
            {
                best = i;
                bestScore = score;
            }
        }
        if (s.Held >= 1 && bestScore <= 0f)
            return -1;
        return best;
    }

    /// <summary>Boost-to-hit: within this many degrees of the victim...</summary>
    public const float HIT_ANGLE = 20f;
    /// <summary>...and this close, in metres, the boost carries it into them</summary>
    public const float HIT_DISTANCE = 12f;
    /// <summary>The courier swerves from boosting rivals this close, in metres</summary>
    public const float DANGER_RADIUS = 15f;

    /// <summary>
    /// Whether a chasing bot boosts into its victim now: lined up (bearing in degrees), close (metres) and not boosting
    /// already (a press mid-boost does nothing, and would count as another steal try)
    /// </summary>
    public static bool BoostToHit(float bearing, float distance, bool boosting)
    {
        return !boosting && Mathf.Abs(bearing) <= HIT_ANGLE && distance <= HIT_DISTANCE;
    }

    /// <summary>
    /// The courier's swerve: its aim pushed away from each threat (a boosting rival) within DANGER_RADIUS on the ground,
    /// more the closer the threat (DANGER_RADIUS metres at contact). Height ignored
    /// </summary>
    public static Vector3 Dodge(Vector3 position, Vector3 aim, IReadOnlyList<Vector3> threats)
    {
        Vector3 push = Vector3.zero;
        for (int i = 0; i < threats.Count; i++)
        {
            Vector3 away = position - threats[i];
            away.y = 0f;
            float distance = away.magnitude;
            if (distance >= DANGER_RADIUS || distance < 1e-4f)
                continue;
            push += away / distance * (DANGER_RADIUS - distance);
        }
        return aim + push;
    }

    /// <summary>How long it chases one victim, in seconds: longer desperate, half as long leading</summary>
    public static float ChaseSeconds(BotSituation s)
    {
        float seconds = s.Profile.ChaseSeconds + s.Profile.DesperateChase * Desperation(s.MyScore, s.LeaderScore);
        return Leads(s.MyScore, s.LeaderScore) ? seconds * 0.5f : seconds;
    }
}

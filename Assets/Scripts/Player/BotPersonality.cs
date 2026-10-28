using System.Collections.Generic;

/// <summary>A CPU driver's personality (docs/superpowers/specs/2026-10-28-bot-personalities-design.md)</summary>
public enum BotKind
{
    /// <summary>Goes after players to steal, the leader most of all</summary>
    Thief,
    /// <summary>Drives far for big packages (Hard, Golden); steals big ones only when behind</summary>
    Hunter,
    /// <summary>Near, quick orders, boosts a lot, and swerves away from boosting rivals while it carries</summary>
    Courier
}

/// <summary>
/// How a personality weighs its options (BotTactics). Desperation (how far behind the leader it is) adds the Desperate
/// weights on top
/// </summary>
public readonly struct BotProfile
{
    /// <summary>How much a steal is worth to it over an order of the same value; 0 never steals (until desperate)</summary>
    public readonly float StealAppetite;
    /// <summary>Steal appetite added when fully desperate</summary>
    public readonly float DesperateSteal;
    /// <summary>Score per point an order is worth</summary>
    public readonly float ValuePerPoint;
    /// <summary>Score added for a golden order (its Worth is below an easy one's)</summary>
    public readonly float GoldenBonus;
    /// <summary>Score lost per metre away</summary>
    public readonly float DistancePerMetre;
    /// <summary>How long it chases one victim, in seconds, before giving up</summary>
    public readonly float ChaseSeconds;
    /// <summary>Chase seconds added when fully desperate</summary>
    public readonly float DesperateChase;
    /// <summary>The least time between ordinary boosts, in seconds</summary>
    public readonly float BoostEvery;
    /// <summary>It only steals orders worth at least this</summary>
    public readonly int MinStealWorth;
    /// <summary>Whether it swerves away from boosting rivals while it carries something</summary>
    public readonly bool Dodges;

    public BotProfile(float stealAppetite, float desperateSteal, float valuePerPoint, float goldenBonus, float distancePerMetre,
        float chaseSeconds, float desperateChase, float boostEvery, int minStealWorth, bool dodges)
    {
        StealAppetite = stealAppetite;
        DesperateSteal = desperateSteal;
        ValuePerPoint = valuePerPoint;
        GoldenBonus = goldenBonus;
        DistancePerMetre = distancePerMetre;
        ChaseSeconds = chaseSeconds;
        DesperateChase = desperateChase;
        BoostEvery = boostEvery;
        MinStealWorth = minStealWorth;
        Dodges = dodges;
    }
}

/// <summary>
/// Which personality each bot gets, and their weights. The weights are deliberately far apart, so players can tell the
/// bots apart by how they drive; tune them here
/// </summary>
public static class BotPersonality
{
    static readonly BotProfile thief = new BotProfile(3f, 2f, 1f, 0f, 0.6f, 8f, 6f, 2.5f, 0, false);
    static readonly BotProfile hunter = new BotProfile(0f, 1.5f, 3f, 120f, 0.2f, 5f, 5f, 2.5f, StealValueRules.HARD, false);
    static readonly BotProfile courier = new BotProfile(0f, 0f, 1f, 0f, 1.2f, 0f, 0f, 1.2f, int.MaxValue, true);

    /// <summary>The personality of the bot seated rank-th (0 first): one thief, then a hunter, then couriers</summary>
    public static BotKind ForRank(int rank)
    {
        switch (rank)
        {
            case 0: return BotKind.Thief;
            case 1: return BotKind.Hunter;
            default: return BotKind.Courier;
        }
    }

    /// <summary>
    /// A bot's rank among the bots: how many other bot seats are lower than its own (bots take seats in the order they're
    /// added). An unknown seat (-1) ranks as a courier, never a thief
    /// </summary>
    public static int RankOf(int seat, IEnumerable<int> botSeats)
    {
        if (seat < 0)
            return 2;
        HashSet<int> lower = new HashSet<int>();
        foreach (int other in botSeats)
        {
            if (other >= 0 && other < seat)
                lower.Add(other);
        }
        return lower.Count;
    }

    /// <summary>A personality's weights</summary>
    public static BotProfile For(BotKind kind)
    {
        switch (kind)
        {
            case BotKind.Thief: return thief;
            case BotKind.Hunter: return hunter;
            default: return courier;
        }
    }
}

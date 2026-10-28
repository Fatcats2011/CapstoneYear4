using System;
using System.Collections.Generic;

/// <summary>
/// What an order is worth and what a steal pays. Every number is here, to tune in playtests:
/// - Price: what a tier pays, set by the time an order takes to score (the simulation behind docs/superpowers/specs/
///   2026-10-28-steal-value-design.md). Constants.OrderValue's numbers only name the tier (scenes store them): money
///   comes from here.
/// - Worth: the price, doubled by each steal. The golden order never doubles: it grows with the time it is held.
///   Counting stops at MAX_COUNTED_STEALS so a long run of steals can't overflow a score.
/// - Pay: the thief's payment for taking a package, once per order per thief.
/// - Swell: how much larger a stolen order looks.
/// Order and OrderHandler apply these.
/// </summary>
public static class StealValueRules
{
    public const int EASY = 55;
    public const int MEDIUM = 65;
    public const int HARD = 80;
    public const int GOLDEN = 50;

    /// <summary>What a thief is paid for taking a package, once per order</summary>
    public const int STEAL_PAY = 7;

    /// <summary>The most steals counted: a guard against overflow (80 x 2^16 fits an int), not a limit the game meets</summary>
    public const int MAX_COUNTED_STEALS = 16;

    const float SWELL_PER_STEAL = 0.15f;
    const int MAX_SWELLING_STEALS = 4;

    /// <summary>What a tier pays, before any steal</summary>
    public static int Price(Constants.OrderValue tier)
    {
        switch (tier)
        {
            case Constants.OrderValue.Easy: return EASY;
            case Constants.OrderValue.Medium: return MEDIUM;
            case Constants.OrderValue.Hard: return HARD;
            default: return GOLDEN;
        }
    }

    /// <summary>What an order of a tier is worth after some steals: its price doubled for each (not the golden order's)</summary>
    public static int Worth(Constants.OrderValue tier, int steals)
    {
        if (tier == Constants.OrderValue.Golden)
            return GOLDEN;

        return Price(tier) << Math.Min(Math.Max(steals, 0), MAX_COUNTED_STEALS);
    }

    /// <summary>
    /// What a thief is paid for taking a package: STEAL_PAY the first time this seat takes this order, then nothing (the
    /// seats already paid for it are in alreadyPaid). Nothing for no seat
    /// </summary>
    public static int Pay(HashSet<int> alreadyPaid, int thiefSeat)
    {
        if (thiefSeat < 0 || !alreadyPaid.Add(thiefSeat))
            return 0;

        return STEAL_PAY;
    }

    /// <summary>
    /// Whether a steal in this game state doubles an order and pays the thief. Not in the tutorial: its points carry into
    /// the match (scores reset only for a new match), so a bump there would hand a player a head start
    /// </summary>
    public static bool CountsIn(GameState state)
    {
        return state != GameState.Tutorial;
    }

    /// <summary>How many times larger than usual an order that has been stolen this many times looks</summary>
    public static float Swell(int steals)
    {
        return 1f + SWELL_PER_STEAL * Math.Min(Math.Max(steals, 0), MAX_SWELLING_STEALS);
    }
}

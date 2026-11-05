using UnityEngine;

/// <summary>
/// Gold Rush (docs/gold-rush-brainstorm.md): many more packages, a Mother Lode for everyone to fight over every 30 to 40
/// seconds (MotherLode), and steals that count double in the last 30 seconds (rush hour). Every number is here, to tune in
/// playtests. The host (or this PC offline) runs it: OrderManager spawns, MotherLode pays; scores reach clients as always
/// </summary>
public static class GoldRushRules
{
    /// <summary>How many times the scene's authored order quotas a wave allows at once</summary>
    public const int QUOTA_MULTIPLIER = 3;
    /// <summary>Seconds between spawns (the scene's cooldownTime otherwise)</summary>
    public const float SPAWN_COOLDOWN = 1f;
    /// <summary>The last seconds of the waves, when each steal counts twice (the order doubles twice, the thief is paid twice)</summary>
    public const float RUSH_HOUR_SECONDS = 30f;

    /// <summary>The wait for the next Mother Lode, from when the last one is gone, in seconds</summary>
    public const float LODE_WAIT_MIN = 30f, LODE_WAIT_MAX = 40f;
    /// <summary>What delivering it pays, however often it was stolen (a steal still pays the thief as usual)</summary>
    public const int LODE_WORTH = 250;
    /// <summary>How much larger than a golden package it looks</summary>
    public const float LODE_SCALE = 2.5f;
    /// <summary>The share of its engine a scooter keeps while carrying it</summary>
    public const float LODE_CARRY_FORCE = 0.8f;
    /// <summary>Nobody's got it for this long, in seconds (it may have landed out of reach): it goes, and another comes in its time</summary>
    public const float LODE_UNTOUCHED_LIMIT = 60f;
    /// <summary>The carrier spills a gold nugget each time they've driven this far, in metres</summary>
    public const float NUGGET_SPACING = 12f;
    /// <summary>What a nugget pays whoever drives through it</summary>
    public const int NUGGET_WORTH = 10;
    /// <summary>How close to a nugget takes it, in metres</summary>
    public const float NUGGET_REACH = 4f;
    /// <summary>How long a nugget stays on the road, in seconds</summary>
    public const float NUGGET_LIFE = 20f;

    public static bool Playing => HouseRules.Playing.Mode == GameMode.GoldRush;

    public static int Quota(int authored)
    {
        return Playing ? authored * QUOTA_MULTIPLIER : authored;
    }

    public static float Cooldown(float authored)
    {
        return Playing ? Mathf.Min(authored, SPAWN_COOLDOWN) : authored;
    }

    /// <summary>Whether it's rush hour: Gold Rush, waves still running, and at most RUSH_HOUR_SECONDS left</summary>
    public static bool IsRushHour(GameMode mode, float timeLeft, bool wavesOver)
    {
        return mode == GameMode.GoldRush && !wavesOver && timeLeft > 0f && timeLeft <= RUSH_HOUR_SECONDS;
    }

    /// <summary>How many times a steal counts now (doublings of the order, and the thief's pay): 2 in rush hour, else 1</summary>
    public static int StealCounts()
    {
        OrderManager orders = OrderManager.Instance;
        bool rush = orders != null && IsRushHour(HouseRules.Playing.Mode, orders.GameTimer, orders.FinalOrderActive);
        return rush ? 2 : 1;
    }

    /// <summary>The wait for the next Mother Lode, for a roll from 0 to 1</summary>
    public static float LodeWait(float roll)
    {
        return Mathf.Lerp(LODE_WAIT_MIN, LODE_WAIT_MAX, roll);
    }

    /// <summary>Whether a seat takes a nugget it's this close to: anyone near it but the player who spilled it</summary>
    public static bool TakesNugget(int seat, int spilledBy, float distance)
    {
        return seat != spilledBy && distance <= NUGGET_REACH;
    }
}

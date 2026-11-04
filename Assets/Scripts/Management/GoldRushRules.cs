using UnityEngine;

/// <summary>
/// Gold Rush (docs/game-modes-ideas.md): many more packages, a score that drains while a player isn't delivering, and
/// steals that count double in the last 30 seconds (rush hour). Every number is here, to tune in playtests. The host
/// (or this PC offline) runs it: OrderManager spawns and drains, OrderHandler pays; scores reach clients as always
/// </summary>
public static class GoldRushRules
{
    /// <summary>How many times the scene's authored order quotas a wave allows at once</summary>
    public const int QUOTA_MULTIPLIER = 3;
    /// <summary>Seconds between spawns (the scene's cooldownTime otherwise)</summary>
    public const float SPAWN_COOLDOWN = 1f;
    /// <summary>Points lost per second while not delivering</summary>
    public const float DRAIN_PER_SECOND = 2f;
    /// <summary>Seconds after a delivery (or the match's start) before the drain starts</summary>
    public const float GRACE_SECONDS = 5f;
    /// <summary>The last seconds of the waves, when each steal counts twice (the order doubles twice, the thief is paid twice)</summary>
    public const float RUSH_HOUR_SECONDS = 30f;

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

    /// <summary>
    /// The points drained this frame from a player who last delivered sinceDelivery seconds ago. carry keeps the part of a
    /// point not yet taken between frames. A score never goes below zero
    /// </summary>
    public static int Drain(float sinceDelivery, float deltaTime, ref float carry, int score)
    {
        if (sinceDelivery < GRACE_SECONDS || score <= 0)
        {
            carry = 0f;
            return 0;
        }
        carry += DRAIN_PER_SECOND * deltaTime;
        int points = Mathf.FloorToInt(carry);
        carry -= points;
        return Mathf.Min(points, score);
    }
}

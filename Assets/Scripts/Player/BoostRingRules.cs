using System;

/// <summary>
/// The boost-charge ring under a scooter: how full it is, when it glows, and when a player is told what it means.
/// Every number is here, to tune in playtests:
/// - Fill, Full, Slowed: how far the boost has recharged, and whether a slowed recharge is showing.
/// - Pulse: the gentle glow while the boost is ready.
/// - ShowPrompt, OfferIdleHint: when the boost button prompt and the idle hint are up (PROMPT_*, IDLE_HINT_SECONDS).
/// - BurstScale, BurstAlpha: the ring flaring out and fading when the boost is spent (BURST_SECONDS).
/// </summary>
public static class BoostRingRules
{
    /// <summary>An online player sees the boost prompt at once for this many matches, then only after a pause</summary>
    public const int PROMPT_MATCHES = 3;
    /// <summary>How long a ready boost waits before the prompt shows, once the first PROMPT_MATCHES online matches are over</summary>
    public const float PROMPT_DELAY = 3f;
    /// <summary>How long a boost sits ready and unused before the idle hint is offered</summary>
    public const float IDLE_HINT_SECONDS = 15f;
    /// <summary>How long the burst ring takes to grow and fade</summary>
    public const float BURST_SECONDS = 0.4f;

    const float PULSE_SPEED = 4f;
    const float BURST_END_SCALE = 1.6f;

    /// <summary>
    /// Whether the prompt and the match count follow the online rule: online with another machine's human in the match.
    /// Solo with CPU drivers is hosted online too, but plays like local (local players and bots only)
    /// </summary>
    public static bool WithPeopleOnline(bool online, System.Collections.Generic.IEnumerable<PlayerSlot> players)
    {
        if (!online || players == null)
            return false;
        foreach (PlayerSlot slot in players)
            if (slot != null && !slot.IsLocal && !slot.IsBot)
                return true;
        return false;
    }

    /// <summary>How much of the recharge is done, 0 to 1: a max of zero or less, or a value that is not a number, counts as full</summary>
    public static float Fill(float elapsed, float max)
    {
        if (max <= 0f || float.IsNaN(max) || float.IsNaN(elapsed))
            return 1f;
        float fill = elapsed / max;
        if (float.IsNaN(fill))
            return 1f;
        return Math.Max(0f, Math.Min(1f, fill));
    }

    public static bool Full(float fill)
    {
        return fill >= 1f;
    }

    /// <summary>Whether the ring shows a slowed recharge: only while it is still charging</summary>
    public static bool Slowed(bool rechargeSlowed, float fill)
    {
        return rechargeSlowed && fill < 1f;
    }

    /// <summary>The glow of a ready boost, 0 to 1 and smooth, at a time since it filled; none before it is full</summary>
    public static float Pulse(float timeFull)
    {
        if (timeFull <= 0f)
            return 0f;
        return 0.5f - 0.5f * (float)Math.Cos(timeFull * PULSE_SPEED);
    }

    /// <summary>Whether the boost button prompt shows: never for a bot or before the boost is full; offline always;
    /// online at once for the first PROMPT_MATCHES matches, then after PROMPT_DELAY</summary>
    public static bool ShowPrompt(bool online, int onlineMatches, bool isBot, float timeFull)
    {
        if (isBot || timeFull <= 0f)
            return false;
        if (!online)
            return true;
        return onlineMatches < PROMPT_MATCHES || timeFull >= PROMPT_DELAY;
    }

    public static bool OfferIdleHint(float timeFull)
    {
        return timeFull >= IDLE_HINT_SECONDS;
    }

    /// <summary>How big the burst ring is at a time since the boost was spent: 1 grows to 1.6 over BURST_SECONDS, held at either end outside it</summary>
    public static float BurstScale(float age)
    {
        float t = Math.Max(0f, Math.Min(1f, age / BURST_SECONDS));
        return 1f + (BURST_END_SCALE - 1f) * t;
    }

    /// <summary>How solid the burst ring is: 1 fading to 0 over BURST_SECONDS, and 0 outside [0, BURST_SECONDS)</summary>
    public static float BurstAlpha(float age)
    {
        if (age < 0f || age >= BURST_SECONDS)
            return 0f;
        return 1f - age / BURST_SECONDS;
    }
}

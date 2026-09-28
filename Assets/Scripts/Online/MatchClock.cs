using System;
using UnityEngine;

/// <summary>
/// Online, the match clock travels as the moment it runs out, in Netcode's server time (the host's clock, on every
/// machine). Clients work out the time left. The host only sends the end again when its clock moved from it (a new
/// wave, the clock stopped). See docs/online.md
/// </summary>
public static class MatchClock
{
    /// <summary>How far the host's clock may move from the end it sent before it sends it again, in seconds</summary>
    public const float REPUBLISH_TOLERANCE = 0.25f;

    /// <summary>When a clock with this much time left runs out</summary>
    public static double EndTime(double now, float timeLeft)
    {
        return now + timeLeft;
    }

    /// <summary>The time left until the end, never below zero</summary>
    public static float Remaining(double end, double now)
    {
        return Mathf.Max(0f, (float)(end - now));
    }

    /// <summary>Host: whether its clock moved from the end it sent</summary>
    public static bool NeedsRepublish(double publishedEnd, double now, float timeLeft)
    {
        return Math.Abs(EndTime(now, timeLeft) - publishedEnd) > REPUBLISH_TOLERANCE;
    }
}

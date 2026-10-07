using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bumps between machines' scooters online (Phase 5A). Another machine's scooter is kinematic here, so bumping it only
/// pushes this machine's player. So the machine whose player bumps another machine's reports it, and the host passes it on:
/// - How hard: Push, from the closing speed, never more than a clash.
/// - The host's checks (Counts): two players, both here, close enough to have touched, neither respawning; and a closing
///   speed no faster than the two are going (Credible).
/// - How often: a pair bumps at most once per PAIR_COOLDOWN, either way round (TooSoon), on the reporting machine and
///   the host.
/// Boosting into someone is a steal or a clash (StealRules), not a bump. OnlineBumps applies these. See docs/online.md
/// </summary>
public class BumpRules
{
    /// <summary>How long a pair's bump counts for them both, in seconds (one touch can report several contacts)</summary>
    public const float PAIR_COOLDOWN = 0.5f;

    /// <summary>
    /// The closing speed of a bump as hard as a clash, in m/s (above a scooter's top speed); a slower bump pushes that
    /// much less, and a modified game's bigger number counts as this. Tune by hand on two PCs
    /// </summary>
    public const float MAX_SPEED = 40f;

    /// <summary>
    /// How far apart the two balls may be on the host for a bump, in metres: touching (about 2 m), plus how far two
    /// scooters at top speed drift apart on the host over a 150 ms connection. Tighter than a steal's reach: a bump
    /// claims the two touched
    /// </summary>
    public const float REACH = 10f;

    /// <summary>How much faster than the two players are going on the host a bump may claim, in m/s (lag, smoothing)</summary>
    public const float SPEED_SLACK = 5f;

    /// <summary>
    /// The closing speed the host believes: the one reported, but never faster than the two players' speeds together as
    /// the host sees them (plus SPEED_SLACK). A modified game can't claim a hard bump from a standing start
    /// </summary>
    public static float Credible(float reported, float bumperSpeed, float victimSpeed)
    {
        // Not a number: no bump (Mathf.Min would quietly turn NaN into the cap)
        if (!NetChecks.Finite(reported))
            return 0f;

        return Mathf.Min(reported, Mathf.Max(0f, bumperSpeed) + Mathf.Max(0f, victimSpeed) + SPEED_SLACK);
    }

    readonly Dictionary<int, float> lastBump = new Dictionary<int, float>(); // each pair's last bump, whichever way round

    /// <summary>
    /// The push a bump at a closing speed gives the bumped player: a share of a clash's (maxPush), MAX_SPEED's bump the
    /// whole of it. Nothing for a speed that isn't a positive number
    /// </summary>
    public static float Push(float speed, float maxPush)
    {
        if (!NetChecks.Finite(speed) || speed <= 0f)
            return 0f;

        return maxPush * Mathf.Min(speed, MAX_SPEED) / MAX_SPEED;
    }

    /// <summary>
    /// Whether the host lets a bump through: two different seats, within reach on the host, neither respawning
    /// </summary>
    public static bool Counts(int bumper, int victim, float distance, bool eitherRespawning)
    {
        return bumper != victim && IsSeat(bumper) && IsSeat(victim) && !eitherRespawning && distance <= REACH;
    }

    /// <summary>
    /// Whether this pair (either way round) bumped within PAIR_COOLDOWN; if not, their bump counts from now
    /// </summary>
    public bool TooSoon(int a, int b, float now)
    {
        int pair = Math.Min(a, b) * Constants.MAX_PLAYERS + Math.Max(a, b);
        if (lastBump.TryGetValue(pair, out float last) && now - last < PAIR_COOLDOWN)
            return true;

        lastBump[pair] = now;
        return false;
    }

    static bool IsSeat(int seat)
    {
        return seat >= 0 && seat < Constants.MAX_PLAYERS;
    }
}

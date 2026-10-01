using System;
using System.Collections.Generic;

/// <summary>
/// What the host makes of one player boosting into another (StealRules)
/// </summary>
public enum HitVerdict { None, Steal, Clash }

/// <summary>
/// The host's rules when one player boosts into another, online. The machine that drives the boosting scooter sees the
/// hit and asks, and the host judges it with its own view:
/// - Both players are there, and neither is respawning.
/// - They're within reach, as the host sees them.
/// - That pair hasn't been hit lately, either way round. So when both machines ask about one bump, the first request the
///   host judges wins, and the other does nothing.
/// Then it's a steal from a player who isn't boosting, and a clash with one who is, as in a local match. The asker's own
/// boost isn't checked: its machine asks only while it boosts, and its flags can arrive after its request.
/// OnlineSteals asks. See docs/online.md
/// </summary>
public class StealRules
{
    /// <summary>How long a pair's hit counts for them both, in seconds (a boost lasts 1.6 s; a robbed player spins out for 1 s)</summary>
    public const float PAIR_COOLDOWN = 2f;

    /// <summary>
    /// How far apart the two balls may be on the host, in metres. The trigger reaches about 3.2 m, but over a 150 ms
    /// connection two scooters meeting head-on at top speed can be 35 m apart on the host when the request lands
    /// </summary>
    public const float REACH = 50f;

    readonly Dictionary<int, float> lastHit = new Dictionary<int, float>(); // each pair's last hit, whichever way round

    /// <summary>
    /// What happens when a boosting player (the attacker) hits another (the victim) at a time, as the host sees them. A
    /// steal or clash starts that pair's cooldown; a hit that counts for nothing doesn't
    /// </summary>
    /// <param name="distance">How far apart their balls are on the host</param>
    /// <param name="victimBoosting">Whether the host sees the victim boosting</param>
    /// <param name="eitherRespawning">Whether either of them is respawning (or hidden for a respawn)</param>
    public HitVerdict Judge(int attacker, int victim, float now, float distance, bool victimBoosting, bool eitherRespawning)
    {
        if (attacker == victim || !IsSeat(attacker) || !IsSeat(victim) || eitherRespawning || distance > REACH)
            return HitVerdict.None;

        int pair = Math.Min(attacker, victim) * Constants.MAX_PLAYERS + Math.Max(attacker, victim);
        float last;
        if (lastHit.TryGetValue(pair, out last) && now - last < PAIR_COOLDOWN)
            return HitVerdict.None;

        lastHit[pair] = now;
        return victimBoosting ? HitVerdict.Clash : HitVerdict.Steal;
    }

    /// <summary>
    /// Whether hits count in a game state: only while players drive (the tutorial, the waves and the golden round)
    /// </summary>
    public static bool Counts(GameState state)
    {
        return state == GameState.Tutorial || state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
    }

    static bool IsSeat(int seat)
    {
        return seat >= 0 && seat < Constants.MAX_PLAYERS;
    }
}

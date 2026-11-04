using System;
using UnityEngine;

/// <summary>Where a bot goes in a timed mode: chasing a seat (and boosting into it), or a place (the crown, or away)</summary>
public readonly struct ModeGoal
{
    public static readonly ModeGoal None = new ModeGoal(ModeMatch.NONE, null);

    /// <summary>The seat to chase and boost into, or NONE</summary>
    public readonly int Chase;
    /// <summary>Where to drive when not chasing (the crown on the road, or away from a threat), or null</summary>
    public readonly Vector3? Place;

    public ModeGoal(int chase, Vector3? place)
    {
        Chase = chase;
        Place = place;
    }
}

/// <summary>
/// The bots' plan in Kings Crown, Infection and Hot Potato (BotDriver uses it instead of orders): take the crown from its
/// holder or run with it; hunt the nearest thief or run from the nearest hunter; pass the potato to the nearest player or
/// run from its holder. Running means a point FLEE metres straight away from the threat. Pure rules
/// </summary>
public static class BotModeTactics
{
    public const float FLEE = 35f;

    public static ModeGoal Choose(ModeMatch match, int me, Vector3 at, Func<int, Vector3?> ballOf)
    {
        if (match == null || me < 0 || match.IsOut(me))
            return ModeGoal.None;

        switch (match.Mode)
        {
            case GameMode.KingsCrown:
                if (match.CrownSeat == ModeMatch.NONE)
                    return new ModeGoal(ModeMatch.NONE, match.CrownSpot);
                if (match.CrownSeat == me)
                    return Flee(at, Nearest(match, me, at, ballOf, seat => true), ballOf);
                return new ModeGoal(match.CrownSeat, null);

            case GameMode.Infection:
                if (match.IsInfected(me))
                    return new ModeGoal(Nearest(match, me, at, ballOf, seat => !match.IsInfected(seat)), null);
                return Flee(at, Nearest(match, me, at, ballOf, seat => match.IsInfected(seat)), ballOf);

            case GameMode.HotPotato:
                if (match.PotatoSeat == me)
                    return new ModeGoal(Nearest(match, me, at, ballOf, seat => true), null);
                int threat = match.PotatoSeat != ModeMatch.NONE ? match.PotatoSeat : Nearest(match, me, at, ballOf, seat => true);
                return Flee(at, threat, ballOf);
        }
        return ModeGoal.None;
    }

    // The nearest other player still in who passes the test, or NONE
    static int Nearest(ModeMatch match, int me, Vector3 at, Func<int, Vector3?> ballOf, Func<int, bool> wanted)
    {
        int best = ModeMatch.NONE;
        float bestDistance = float.MaxValue;
        foreach (int seat in TimedModeRules.Seats(match.Players & ~match.Out))
        {
            if (seat == me || !wanted(seat))
                continue;
            Vector3? ball = ballOf(seat);
            if (ball == null)
                continue;
            float distance = (ball.Value - at).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = seat;
            }
        }
        return best;
    }

    static ModeGoal Flee(Vector3 at, int threat, Func<int, Vector3?> ballOf)
    {
        Vector3? from = threat != ModeMatch.NONE ? ballOf(threat) : null;
        if (from == null)
            return ModeGoal.None;
        Vector3 away = at - from.Value;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
            away = Vector3.forward;
        return new ModeGoal(ModeMatch.NONE, at + away.normalized * FLEE);
    }
}

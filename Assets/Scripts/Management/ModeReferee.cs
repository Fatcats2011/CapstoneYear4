using UnityEngine;

/// <summary>
/// Runs a timed mode's ModeMatch inside the game on the authority (this PC offline, the host online): starts it as the
/// match begins (OrderManager.InitGame), hands it boosting hits (OrderHandler.StealFrom), ticks it, pays the points, and
/// ends the round early once the mode is decided. The round itself is one "wave" as long as the Round Timer
/// (HouseRules.WavesPlayed / OrderManager.WaveLength), so its end, the highlight reel and the results come as usual
/// </summary>
public static class ModeReferee
{
    /// <summary>Authority, as the match begins: a fresh ModeMatch for a timed mode, or none</summary>
    public static void Begin(GameMode mode)
    {
        int players = 0;
        if (PlayerInstantiate.Instance != null)
        {
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
                if (slot.Player != null)
                    players |= 1 << slot.Index;
        }
        ModeMatch.Happened -= PayCatch;
        ModeMatch.Happened += PayCatch;
        ModeMatch.Begin(mode, players, CrownSpot(), Time.time);
    }

    /// <summary>Where the crown lies at the start: a respawn point (they're all on the road), picked at random</summary>
    static Vector3 CrownSpot()
    {
        RespawnPoint[] points = RespawnManager.Instance != null ? RespawnManager.Instance.Points : null;
        if (points == null || points.Length == 0)
            return Vector3.zero;
        return points[Random.Range(0, points.Length)].transform.position + Vector3.up;
    }

    /// <summary>Authority: a boosting player hit another. True when the mode used the hit (no orders change hands)</summary>
    public static bool Strike(OrderHandler attacker, OrderHandler victim)
    {
        ModeMatch match = ModeMatch.Current;
        if (match == null)
            return false;
        return match.Strike(OrderSync.SeatOf(attacker), OrderSync.SeatOf(victim), Time.time);
    }

    /// <summary>Authority, every frame the round runs. True when the mode is decided and the round should end now</summary>
    public static bool Tick()
    {
        ModeMatch match = ModeMatch.Current;
        if (match == null)
            return false;
        match.Tick(Time.time, Time.deltaTime, BallOf, AddScore);
        return match.IsOver;
    }

    static Vector3? BallOf(int seat)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
        if (ball == null || ball.Sphere == null)
            return null;
        return ball.Sphere.transform.position;
    }

    static void AddScore(int seat, int points)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        if (handler == null)
            return;
        handler.AddModePoints(points);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.UpdatePlacement();
    }

    static void PayCatch(ModeMoment moment)
    {
        int points = ModeMatch.CatchPoints(moment);
        if (points > 0 && GameAuthority.IsAuthority)
            AddScore(moment.Seat, points);
    }
}

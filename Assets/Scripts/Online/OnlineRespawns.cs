using UnityEngine;

/// <summary>
/// The session's respawn points on this machine (online):
/// - Client: its player who fell in the water asks the host where to rise (RespawnSync, OnlineMatch.AskRespawn), and
///   rises where the host says (Show; OnlineGame queues the answer with the host's other messages).
/// - Host: picks another machine's player's point as it picks its own player's (the nearest nobody is rising from),
///   holds it while they rise, drops their orders on it for everyone, and answers. A machine only asks for its own seat.
/// OnlineGame adds it next to OnlineSteals. See docs/online.md
/// </summary>
public class OnlineRespawns : MonoBehaviour
{
    /// <summary>
    /// How much longer than a respawn the host holds another machine's player's point, in seconds: the answer's trip to
    /// them, and then some. The host never hears when their respawn ends
    /// </summary>
    public const float HOLD_MARGIN = 1f;

    OnlineSession session;
    OnlineMatch listening; // the match whose clients' requests the host hears

    /// <summary>
    /// Starts sharing this session's respawn points
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        RespawnSync.Asked += AskHost;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // RespawnSync's event is static: an ended session never keeps listening
    void OnDestroy()
    {
        RespawnSync.Asked -= AskHost;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.RespawnAsked += PickFor;
    }

    void StopListening()
    {
        if (listening != null)
            listening.RespawnAsked -= PickFor;
        listening = null;
    }

    // Client: this machine's player fell in the water: the host picks where they rise
    void AskHost(int seat, Vector3 lastGrounded)
    {
        OnlineMatch match = session.Match;
        if (match != null && !match.IsServer)
            match.AskRespawn(seat, lastGrounded);
    }

    // Host: another machine's player fell in the water. Only their own machine can say so. Their point is held while
    // they rise, and their orders drop on it before the answer goes out
    void PickFor(ulong machine, int seat, Vector3 lastGrounded)
    {
        OnlineMatch match = session.Match;
        OrderHandler handler = OrderSync.HandlerIn(seat);
        if (match == null || !session.Owns(machine, seat) || handler == null || RespawnManager.Instance == null
            || !NetChecks.InWorld(lastGrounded))
            return;

        RespawnPoint point = RespawnManager.Instance.GetRespawnPoint(lastGrounded);
        if (point == null)
            return;

        Respawn respawn = RespawnOf(handler);
        point.HoldFor((respawn != null ? respawn.Duration : 0f) + HOLD_MARGIN);
        handler.DropEverything(point.Order1Spawn, point.Order2Spawn, false);
        match.SendRespawn(seat, RespawnManager.Instance.IndexOf(point));
    }

    /// <summary>
    /// Client: where the host says the player in a seat rises (a respawn point's index). This machine's player's respawn
    /// goes on there; another machine's player's is theirs to show
    /// </summary>
    public void Show(int seat, int point)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        if (handler == null || RemoteAvatar.IsRemote(handler) || RespawnManager.Instance == null)
            return;

        Respawn respawn = RespawnOf(handler);
        RespawnPoint rise = RespawnManager.Instance.PointAt(point);
        if (respawn != null && rise != null)
            respawn.RiseAt(rise);
    }

    // A player's respawn: on their ball
    static Respawn RespawnOf(OrderHandler player)
    {
        return player.GetComponent<BallDriving>().Sphere.GetComponent<Respawn>();
    }
}

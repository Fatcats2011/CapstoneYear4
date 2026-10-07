using UnityEngine;

/// <summary>
/// The session's bumps on this machine (online, Phase 5A). Another machine's scooter is kinematic here, so a bump would
/// only push this machine's player:
/// - This machine's players' balls report the bumps they make (BumpReporter, BumpSync). A client asks the host
///   (OnlineMatch.AskBump); the host judges its own players' at once.
/// - Host: checks each bump with its own view (BumpRules), and takes a machine's request only for that machine's own
///   seat. Then every machine hears it (PlayerBump).
/// - Every machine: pushes its own bumped player away from the bumper (Show). A pair's bumps show no closer together
///   than BumpRules.PAIR_COOLDOWN.
/// Bumps don't wait with the host's other messages: like one-shots, a bump is about the moment. OnlineGame adds it next to
/// OnlineSteals. See docs/online.md
/// </summary>
public class OnlineBumps : MonoBehaviour
{
    readonly BumpRules reported = new BumpRules(); // this machine's players' bumps, before they go out
    readonly BumpRules judged = new BumpRules();   // host: the bumps it let through
    readonly BumpRules shown = new BumpRules();    // client: the host's bumps shown here
    readonly System.Collections.Generic.HashSet<GameObject> reporting = new System.Collections.Generic.HashSet<GameObject>(); // players whose ball reports already
    OnlineSession session;
    OnlineMatch listening;

    /// <summary>
    /// Starts sharing this session's bumps
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        BumpSync.Reported += OnReported;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // BumpSync's event is static: an ended session never keeps listening
    void OnDestroy()
    {
        BumpSync.Reported -= OnReported;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.BumpAsked += JudgeFor;
        match.BumpReceived += OnReceived;
    }

    void StopListening()
    {
        if (listening != null)
        {
            listening.BumpAsked -= JudgeFor;
            listening.BumpReceived -= OnReceived;
        }
        listening = null;
    }

    // This machine's players' balls report their bumps (players join and leave seats: checked every frame)
    void Update()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        foreach (PlayerSlot slot in players.Roster.LocalPlayers)
        {
            if (reporting.Contains(slot.Player))
                continue;

            BallDriving driving = slot.Player.GetComponentInChildren<BallDriving>(true);
            if (driving == null || driving.Sphere == null)
                continue;

            if (driving.Sphere.GetComponent<BumpReporter>() == null)
                driving.Sphere.AddComponent<BumpReporter>().Begin(driving, slot.Player.GetComponentInChildren<OrderHandler>(true));
            reporting.RemoveWhere(IsGone); // players who left: their objects are gone
            reporting.Add(slot.Player);
        }
    }

    // This machine's player bumped another machine's: a client asks the host, and the host judges it at once
    void OnReported(int bumper, int victim, float speed)
    {
        OnlineMatch match = session.Match;
        if (match == null || reported.TooSoon(bumper, victim, Time.realtimeSinceStartup))
            return;

        if (match.IsServer)
            Judge(bumper, victim, speed);
        else
            match.AskBump(bumper, victim, speed);
    }

    // Host: another machine's player bumped someone. Only that player's own machine can say so
    void JudgeFor(ulong machine, int bumper, int victim, float speed)
    {
        if (session.SeatOf(machine) == bumper)
            Judge(bumper, victim, speed);
    }

    // Host: lets a bump through, as it sees the two players now, then every machine hears it
    void Judge(int bumper, int victim, float speed)
    {
        OnlineMatch match = session.Match;
        OrderHandler from = OrderSync.HandlerIn(bumper);
        OrderHandler to = OrderSync.HandlerIn(victim);
        if (match == null || from == null || to == null || GameManager.Instance == null
            || !StealRules.Counts(GameManager.Instance.MainState))
            return;

        BallDriving fromBall = from.GetComponent<BallDriving>();
        BallDriving toBall = to.GetComponent<BallDriving>();
        float push = BumpRules.Push(BumpRules.Credible(speed, fromBall.CurrentVelocity, toBall.CurrentVelocity), toBall.ClashForce);
        float distance = Vector3.Distance(BallOf(from), BallOf(to));
        if (push <= 0f || !BumpRules.Counts(bumper, victim, distance, Respawning(from) || Respawning(to))
            || judged.TooSoon(bumper, victim, Time.realtimeSinceStartup))
            return;

        PlayerBump bump = new PlayerBump { Bumper = bumper, Victim = victim, Push = push };
        Show(bump);
        match.SendBump(bump);
    }

    // Client: the host passed a bump on
    void OnReceived(PlayerBump bump)
    {
        if (!shown.TooSoon(bump.Bumper, bump.Victim, Time.realtimeSinceStartup))
            Show(bump);
    }

    /// <summary>
    /// A bump on this machine: the bumped player is pushed away from the bumper, if this machine drives them
    /// </summary>
    public void Show(PlayerBump bump)
    {
        OrderHandler from = OrderSync.HandlerIn(bump.Bumper);
        OrderHandler to = OrderSync.HandlerIn(bump.Victim);
        if (from == null || to == null || RemoteAvatar.IsRemote(to))
            return;

        to.GetComponent<BallDriving>().PushFrom(BallOf(from), bump.Push);
    }

    static bool IsGone(GameObject player)
    {
        return player == null;
    }

    // Where a player's ball is, as this machine shows it
    static Vector3 BallOf(OrderHandler player)
    {
        return player.GetComponent<BallDriving>().Sphere.transform.position;
    }

    // A player respawning: this machine's own for the whole respawn, another machine's while its rider is hidden
    static bool Respawning(OrderHandler player)
    {
        Respawn respawn = player.GetComponent<BallDriving>().Sphere.GetComponent<Respawn>();
        return respawn != null && (respawn.IsRespawning || respawn.RiderHidden);
    }
}

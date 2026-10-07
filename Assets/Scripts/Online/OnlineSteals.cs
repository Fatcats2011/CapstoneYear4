using UnityEngine;

/// <summary>
/// The session's steals and clashes on this machine (online):
/// - This machine's player, boosting, hit another machine's (StealSync). A client asks the host (OnlineMatch.AskSteal);
///   the host judges its own player's at once.
/// - Host: judges each hit with its own view (StealRules), and takes a machine's request only for that machine's own
///   seat. A steal moves the victim's best order to the thief and drops the rest: order changes every client replays
///   (OnlineOrders). Then every machine hears the hit (PlayerHit).
/// - Every machine: for each hit, the players it drives bounce (Show): a robbed player away from the thief, both
///   players in a clash. On a client the host's hits wait with its other messages (OnlineGame).
/// OnlineGame adds it next to OnlineTutorial. See docs/online.md
/// </summary>
public class OnlineSteals : MonoBehaviour
{
    readonly StealRules rules = new StealRules();
    readonly System.Collections.Generic.Dictionary<int, float> shownHits = new System.Collections.Generic.Dictionary<int, float>(); // client: each pair's last hit shown
    OnlineSession session;
    OnlineMatch listening; // the match whose clients' requests the host hears

    /// <summary>
    /// Starts sharing this session's steals and clashes
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        StealSync.Asked += OnAsked;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // StealSync's event is static: an ended session never keeps listening
    void OnDestroy()
    {
        StealSync.Asked -= OnAsked;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.StealAsked += JudgeFor;
    }

    void StopListening()
    {
        if (listening != null)
            listening.StealAsked -= JudgeFor;
        listening = null;
    }

    // This machine's player hit another machine's: a client asks the host, and the host judges it at once
    void OnAsked(int attacker, int victim)
    {
        OnlineMatch match = session.Match;
        if (match == null)
            return;

        if (match.IsServer)
            Judge(attacker, victim);
        else
            match.AskSteal(attacker, victim);
    }

    // Host: another machine's player hit someone. Only that player's own machine can say so
    void JudgeFor(ulong machine, int attacker, int victim)
    {
        if (session.SeatOf(machine) == attacker)
            Judge(attacker, victim);
    }

    // Host: a steal, a clash or nothing, as the host sees the two players now
    void Judge(int attacker, int victim)
    {
        OnlineMatch match = session.Match;
        OrderHandler thief = OrderSync.HandlerIn(attacker);
        OrderHandler target = OrderSync.HandlerIn(victim);
        if (match == null || thief == null || target == null || GameManager.Instance == null
            || !StealRules.Counts(GameManager.Instance.MainState))
            return;

        float distance = Vector3.Distance(BallOf(thief), BallOf(target));
        HitVerdict verdict = rules.Judge(attacker, victim, Time.time, distance, target.IsBoosting, Respawning(thief) || Respawning(target));
        if (verdict == HitVerdict.None)
            return;

        PlayerHit hit;
        if (verdict == HitVerdict.Steal)
        {
            // The orders first: every client replays the steal and the victim's drop, in order (OnlineOrders)
            thief.StealFrom(target);
            hit = PlayerHit.Steal(attacker, victim);
        }
        else
            hit = PlayerHit.Clash(attacker, victim);

        Show(hit);
        match.SendHit(hit);
    }

    /// <summary>
    /// A steal or clash the host decided, on this machine: each player this machine drives bounces. A robbed player
    /// bounces away from the thief; in a clash each bounces off the other, unless phasing. A player who isn't here is
    /// skipped
    /// </summary>
    public void Show(PlayerHit hit)
    {
        OrderHandler attacker = OrderSync.HandlerIn(hit.Attacker);
        OrderHandler victim = OrderSync.HandlerIn(hit.Victim);
        if (attacker == null || victim == null)
            return;

        // A client shows a pair's hits no closer together than the host could decide them (half its cooldown, for
        // network jitter): another machine's host can't lock this machine's player in bounces
        if (!GameAuthority.IsAuthority && TooSoon(hit.Attacker, hit.Victim, Time.realtimeSinceStartup))
            return;

        if (hit.Kind == HitKind.Steal)
        {
            if (!RemoteAvatar.IsRemote(victim))
                victim.BounceFrom(attacker);
            return;
        }

        if (!RemoteAvatar.IsRemote(attacker))
            attacker.ClashWith(victim);
        if (!RemoteAvatar.IsRemote(victim))
            victim.ClashWith(attacker);
    }

    // Client: whether this pair (either way round) had a hit shown within half the host's pair cooldown; if not, it's now
    bool TooSoon(int seatA, int seatB, float now)
    {
        int pair = Mathf.Min(seatA, seatB) * Constants.MAX_PLAYERS + Mathf.Max(seatA, seatB);
        if (shownHits.TryGetValue(pair, out float last) && now - last < StealRules.PAIR_COOLDOWN / 2f)
            return true;

        shownHits[pair] = now;
        return false;
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

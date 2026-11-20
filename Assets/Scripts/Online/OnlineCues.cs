using UnityEngine;

/// <summary>
/// The session's one-shots on this machine (online): what a scooter does in a moment (ScooterCue).
/// - This machine's player made one (CueSync): the host sends it to every client (OnlineMatch.SendCue), and a client
///   reports it to the host (ReportCue).
/// - Host: takes a client's report only for that client's own seat and of a kind the game knows (not one only the host
///   sends), shows it here and sends it to every client.
/// - Host: a player bumped hard (FeatSync.Bumped, PlayerEmoteRules.HardBump) goes to every client as Dizzy for their seat.
///   The host's own ghosts heard the bump already (PlayerEmotes.HearBump).
/// - Every machine shows another machine's player's one-shots on their scooter (Show): its sound, quieter the farther it
///   is from this machine's player (RemoteSound), a full horn's flash, a rise's gravestone and sparkle at its point, and
///   a d-pad emote on their ghost (PlayerEmotes.ShowPad). Dizzy plays on any player's ghost here, this machine's too.
///   A one-shot is about the moment it happens, so it doesn't wait with the host's states (HostQueue).
/// - The host's match clock rings on every client (ClockCue): a new wave's bells, and time up, when each client's player
///   stops too (OrderManager.FollowHostClockCue).
/// OnlineGame adds it next to OnlineRespawns. See docs/online.md
/// </summary>
public class OnlineCues : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose one-shots this machine hears

    /// <summary>
    /// Starts sharing this session's one-shots
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        CueSync.Played += OnPlayed;
        CueSync.Rang += OnRang;
        FeatSync.Bumped += OnBumped;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // CueSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        CueSync.Played -= OnPlayed;
        CueSync.Rang -= OnRang;
        FeatSync.Bumped -= OnBumped;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.CueReported += OnReported;
        match.CueReceived += Show;
        match.ClockRang += ShowClock;
    }

    void StopListening()
    {
        if (listening != null)
        {
            listening.CueReported -= OnReported;
            listening.CueReceived -= Show;
            listening.ClockRang -= ShowClock;
        }
        listening = null;
    }

    // Host: its match clock rang: every client hears it
    void OnRang(ClockCue cue)
    {
        OnlineMatch match = session.Match;
        if (match != null && match.IsServer)
            match.RingClock(cue);
    }

    /// <summary>
    /// Client: the host's match clock rang: the same here (OrderManager.FollowHostClockCue). A machine without a match's
    /// clock (in the menu, or changing scenes) skips it
    /// </summary>
    public void ShowClock(ClockCue cue)
    {
        if (OrderManager.Instance != null)
            OrderManager.Instance.FollowHostClockCue(cue);
    }

    // This machine's player made a one-shot: the host sends it to every client, and a client reports it to the host
    void OnPlayed(int seat, ScooterCue cue)
    {
        OnlineMatch match = session.Match;
        if (match == null)
            return;

        if (match.IsServer)
            match.SendCue(seat, cue);
        else
            match.ReportCue(seat, cue);
    }

    // Host: a bump it judged (OnlineBumps). A hard one makes its victim see stars on every client too
    void OnBumped(int bumper, int victim, float strength)
    {
        OnlineMatch match = session != null ? session.Match : null;
        if (match == null || !match.IsServer || victim < 0 || !PlayerEmoteRules.HardBump(strength))
            return;

        match.SendCue(victim, ScooterCue.Of(CueKind.Dizzy));
    }

    // Host: a client's player made a one-shot. Only their own machine can say so, and only of a kind the game knows that
    // a client may send (another machine's game could send any number). It shows here, then goes to every client
    void OnReported(ulong machine, int seat, ScooterCue cue)
    {
        OnlineMatch match = session.Match;
        if (match == null || !session.Owns(machine, seat) || !NetChecks.Defined(cue.Kind) || ScooterCue.HostOnly(cue.Kind))
            return;

        Show(seat, cue);
        match.SendCue(seat, cue);
    }

    /// <summary>
    /// A player's one-shot, on this machine: another machine's player's scooter plays it here (its sound, quieter the
    /// farther it is from this machine's player; a full horn's flash; a rise's gravestone and sparkle; a d-pad emote on
    /// their ghost). This machine's own player played it already, and a seat that isn't here, or a point this scene doesn't
    /// have, is skipped. Dizzy is the exception: only the host judges bumps, so it plays on whichever ghost is in the
    /// seat, this machine's own players' too
    /// </summary>
    public void Show(int seat, ScooterCue cue)
    {
        OrderHandler player = OrderSync.HandlerIn(seat);
        if (player == null)
            return;

        if (cue.Kind == CueKind.Dizzy)
        {
            PlayerEmotes dizzy = player.GetComponentInParent<PlayerEmotes>();
            if (dizzy != null)
                dizzy.ShowDizzy();
            return;
        }

        if (!RemoteAvatar.IsRemote(player))
            return;

        // A rise: the gravestone and sparkle at the player's point, when this scene has that point
        if (cue.Kind == CueKind.Rise)
        {
            RespawnPoint point = RespawnManager.Instance != null ? RespawnManager.Instance.PointAt(cue.Point) : null;
            Respawn respawn = player.GetComponent<BallDriving>().Sphere.GetComponent<Respawn>();
            if (point != null && respawn != null)
                respawn.ShowRise(point);
            return;
        }

        SoundPool sounds = player.GetComponent<SoundPool>();
        if (sounds != null)
            sounds.PlayRemote(cue.Kind);

        // A d-pad emote: their ghost plays it too (its sound is the pool's, above)
        int pad = ScooterCue.EmoteIndex(cue.Kind);
        if (pad >= 0)
        {
            PlayerEmotes ghost = player.GetComponentInParent<PlayerEmotes>();
            if (ghost != null)
                ghost.ShowPad(pad);
        }

        if (cue.Kind == CueKind.HornReady)
        {
            PhaseIndicator horns = player.GetComponent<PhaseIndicator>();
            if (horns != null)
                horns.FlashHorns();
        }
    }
}

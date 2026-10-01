using UnityEngine;

/// <summary>
/// The session's one-shots on this machine (online): what a scooter does in a moment (ScooterCue).
/// - This machine's player made one (CueSync): the host sends it to every client (OnlineMatch.SendCue), and a client
///   reports it to the host (ReportCue).
/// - Host: takes a client's report only for that client's own seat, shows it here and sends it to every client.
/// - Every machine shows another machine's player's one-shots on their scooter (Show): its sound, quieter the farther it
///   is from this machine's player (RemoteSound), a full horn's flash, and a rise's gravestone and sparkle at its point.
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
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // CueSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        CueSync.Played -= OnPlayed;
        CueSync.Rang -= OnRang;
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

    // Host: a client's player made a one-shot. Only their own machine can say so. It shows here, then goes to every client
    void OnReported(ulong machine, int seat, ScooterCue cue)
    {
        OnlineMatch match = session.Match;
        if (match == null || session.SeatOf(machine) != seat)
            return;

        Show(seat, cue);
        match.SendCue(seat, cue);
    }

    /// <summary>
    /// A player's one-shot, on this machine: another machine's player's scooter plays it here (its sound, quieter the
    /// farther it is from this machine's player; a full horn's flash; a rise's gravestone and sparkle). This machine's own
    /// player played it already, and a seat that isn't here, or a point this scene doesn't have, is skipped
    /// </summary>
    public void Show(int seat, ScooterCue cue)
    {
        OrderHandler player = OrderSync.HandlerIn(seat);
        if (player == null || !RemoteAvatar.IsRemote(player))
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

        if (cue.Kind == CueKind.HornReady)
        {
            PhaseIndicator horns = player.GetComponent<PhaseIndicator>();
            if (horns != null)
                horns.FlashHorns();
        }
    }
}

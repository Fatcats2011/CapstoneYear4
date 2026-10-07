using UnityEngine;

/// <summary>
/// The session's tutorial on this machine (online):
/// - Client: its player finishing the tutorial, or boosting into their cutout, goes to the host (TutorialSync,
///   OnlineMatch.ReportLearnt and AskCutout).
/// - Host: another machine's player counts once their machine says they finished (TutorialManager.SeatLearnt), and
///   gets their cutout's order when their machine asks (CutoutHandler.StealFor). A machine only speaks for its own seat.
/// OnlineGame adds it next to OnlineOrders. See docs/online.md
/// </summary>
public class OnlineTutorial : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose clients' reports the host hears

    /// <summary>
    /// Starts sharing this session's tutorial
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        TutorialSync.Finished += ReportFinished;
        TutorialSync.CutoutAsked += AskForCutout;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // TutorialSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        TutorialSync.Finished -= ReportFinished;
        TutorialSync.CutoutAsked -= AskForCutout;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.MachineLearnt += CountFinished;
        match.CutoutAsked += StealFromCutout;
    }

    void StopListening()
    {
        if (listening != null)
        {
            listening.MachineLearnt -= CountFinished;
            listening.CutoutAsked -= StealFromCutout;
        }
        listening = null;
    }

    // Client: this machine's player finished the tutorial: the host counts them
    void ReportFinished(int seat)
    {
        OnlineMatch match = session.Match;
        if (match != null && !match.IsServer)
            match.ReportLearnt(seat);
    }

    // Host: another machine's player finished the tutorial. Only that player's own machine can say so
    void CountFinished(ulong machine, int seat)
    {
        if (session.Owns(machine, seat) && TutorialManager.Instance != null)
            TutorialManager.Instance.SeatLearnt(seat);
    }

    // Client: this machine's player boosted into a lane's cutout: the host hands out its order
    void AskForCutout(int seat)
    {
        OnlineMatch match = session.Match;
        if (match != null && !match.IsServer)
            match.AskCutout(seat);
    }

    // Host: another machine's player boosted into a cutout. They get its order only from their own lane's cutout
    void StealFromCutout(ulong machine, int seat)
    {
        if (!session.Owns(machine, seat))
            return;

        CutoutHandler cutout = CutoutHandler.InSeat(seat);
        OrderHandler thief = OrderSync.HandlerIn(seat);
        if (cutout != null && thief != null)
            cutout.StealFor(thief);
    }
}

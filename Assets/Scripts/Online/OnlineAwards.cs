using UnityEngine;

/// <summary>
/// The end-of-match awards between machines (online). The host counts and picks them (MatchAwards):
/// - Host: the awards it picked go to every client (OnlineMatch.ShareAwards).
/// - Client: keeps the host's (AwardSync.Arrive) for its results screen. A client never picks.
/// OnlineGame adds it next to OnlineCutsceneSkip. See docs/online.md
/// </summary>
public class OnlineAwards : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose awards this machine hears

    /// <summary>
    /// Starts carrying this session's awards
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        AwardSync.Shared += Share;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // AwardSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        AwardSync.Shared -= Share;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.AwardsReceived += Keep;
    }

    void StopListening()
    {
        if (listening != null)
            listening.AwardsReceived -= Keep;
        listening = null;
    }

    // Host: the awards every machine shows
    void Share(AwardBoard board)
    {
        OnlineMatch match = session.Match;
        if (match != null && match.IsServer)
            match.ShareAwards(board);
    }

    // Client: the host's awards
    void Keep(AwardBoard board)
    {
        AwardSync.Arrive(board);
    }
}

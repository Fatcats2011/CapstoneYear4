using UnityEngine;

/// <summary>
/// The highlight reel's list between machines (online). The host picks and films the reel (HighlightDirector):
/// - Host: its list goes to every client (OnlineMatch.ShareReel), which each play at the list's start time.
/// - Client: hands the host's list on (HighlightSync.Arrive). A client never shares.
/// OnlineGame adds it next to OnlineAwards. See docs/online.md
/// </summary>
public class OnlineHighlights : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose reel this machine hears

    /// <summary>
    /// Starts carrying this session's reel list
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        HighlightSync.Shared += Share;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // HighlightSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        HighlightSync.Shared -= Share;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.ReelReceived += Keep;
    }

    void StopListening()
    {
        if (listening != null)
            listening.ReelReceived -= Keep;
        listening = null;
    }

    // Host: the reel every machine plays
    void Share(ReelList list)
    {
        OnlineMatch match = session.Match;
        if (match != null && match.IsServer)
            match.ShareReel(list);
    }

    // Client: the host's reel
    void Keep(ReelList list)
    {
        HighlightSync.Arrive(list);
    }
}

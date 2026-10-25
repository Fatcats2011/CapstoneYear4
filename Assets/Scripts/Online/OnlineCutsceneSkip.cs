using UnityEngine;

/// <summary>
/// The cutscene skip vote between machines (online). The host holds the vote (CutsceneSkip):
/// - Client: its players' votes go to the host (OnlineMatch.SendSkipVote); the host's count comes back.
/// - Host: another machine's vote counts only for that machine's own seat; its count goes to every client.
/// OnlineGame adds it next to OnlineRematch. See docs/online.md
/// </summary>
public class OnlineCutsceneSkip : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose messages this machine hears

    /// <summary>
    /// Starts carrying this session's skip votes
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        SkipSync.VoteSent += SendVote;
        SkipSync.BoardShared += ShareBoard;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // SkipSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        SkipSync.VoteSent -= SendVote;
        SkipSync.BoardShared -= ShareBoard;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.SkipVoteReceived += CountVote;
        match.SkipBoardReceived += ShowBoard;
    }

    void StopListening()
    {
        if (listening != null)
        {
            listening.SkipVoteReceived -= CountVote;
            listening.SkipBoardReceived -= ShowBoard;
        }
        listening = null;
    }

    // Client: this machine's player voted
    void SendVote(int seat)
    {
        OnlineMatch match = session.Match;
        if (match != null && !match.IsServer)
            match.SendSkipVote(seat);
    }

    // Host: another machine's player voted. Only for their own seat
    void CountVote(ulong machine, int seat)
    {
        if (session.Owns(machine, seat))
            SkipSync.ArriveVote(seat);
    }

    // Host: the count every machine shows
    void ShareBoard(SkipBoard board)
    {
        OnlineMatch match = session.Match;
        if (match != null && match.IsServer)
            match.ShareSkipBoard(board);
    }

    // Client: the host's count
    void ShowBoard(SkipBoard board)
    {
        SkipSync.ArriveBoard(board);
    }
}

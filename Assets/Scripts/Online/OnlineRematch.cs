using UnityEngine;

/// <summary>
/// The results screen's rematch vote between machines (online). The host holds the vote (ResultsVote):
/// - Client: its player's vote goes to the host (OnlineMatch.SendVote); the host's board comes back to the screen.
/// - Host: another machine's vote counts only for that machine's own seat; its board goes to every client; a seat
///   taken out of the rematch has its machine disconnected, with the reason (not banned: they may join again). The host's own seat is never kicked.
/// OnlineGame adds it next to OnlineTutorial. See docs/online.md
/// </summary>
public class OnlineRematch : MonoBehaviour
{
    public static string TAKEN_OUT => Loc.Get("online.takenout");

    OnlineSession session;
    OnlineMatch listening; // the match whose messages this machine hears

    /// <summary>
    /// Starts carrying this session's rematch votes
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        RematchSync.VoteSent += SendVote;
        RematchSync.BoardShared += ShareBoard;
        RematchSync.TakenOut += TakeOut;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // RematchSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        RematchSync.VoteSent -= SendVote;
        RematchSync.BoardShared -= ShareBoard;
        RematchSync.TakenOut -= TakeOut;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.VoteReceived += CountVote;
        match.BoardReceived += ShowBoard;
    }

    void StopListening()
    {
        if (listening != null)
        {
            listening.VoteReceived -= CountVote;
            listening.BoardReceived -= ShowBoard;
        }
        listening = null;
    }

    // Client: this machine's player voted
    void SendVote(int seat, bool rematch)
    {
        OnlineMatch match = session.Match;
        if (match != null && !match.IsServer)
            match.SendVote(seat, rematch);
    }

    // Host: another machine's player voted. Only for their own seat
    void CountVote(ulong machine, int seat, bool rematch)
    {
        if (session.Owns(machine, seat))
            RematchSync.ArriveVote(seat, rematch);
    }

    // Host: the vote as every machine shows it
    void ShareBoard(RematchBoard board)
    {
        OnlineMatch match = session.Match;
        if (match != null && match.IsServer)
            match.ShareBoard(board);
    }

    // Client: the host's board
    void ShowBoard(RematchBoard board)
    {
        RematchSync.ArriveBoard(board);
    }

    // Host: the rematch goes on without a seat: its machine leaves the session
    void TakeOut(int seat)
    {
        OnlineMatch match = session.Match;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (match == null || !match.IsServer || players == null || seat < 0 || seat >= Constants.MAX_PLAYERS)
            return;

        PlayerSlot slot = players.Roster[seat];
        if (slot == null || slot.IsLocal)
            return;
        session.SendAway(slot.OwnerClientId, HostNotice.Encode("online.takenout")); // not a kick: they may join the host's lobby again
    }
}

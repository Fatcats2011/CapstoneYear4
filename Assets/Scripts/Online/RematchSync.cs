using System;

/// <summary>
/// The results screen's rematch vote on its way in and out of the game code (online). OnlineRematch carries them:
/// - Client: its player's vote goes to the host (SendVote); the host's board comes back (ArriveBoard).
/// - Host: a client's vote, checked to be for its own seat, comes in (ArriveVote); its board goes out (ShareBoard);
///   a seat taken out of the rematch has its machine disconnected (TakeOut).
/// Offline nothing listens: ResultsVote decides at once. See docs/online.md
/// </summary>
public static class RematchSync
{
    /// <summary>Client: its player in a seat voted Rematch (true) or took the vote back (false)</summary>
    public static event Action<int, bool> VoteSent;

    /// <summary>Host: another machine's player in a seat voted (already checked: their own seat)</summary>
    public static event Action<int, bool> VoteArrived;

    /// <summary>Host: the vote as every machine should show it</summary>
    public static event Action<RematchBoard> BoardShared;

    /// <summary>Client: the host's board arrived</summary>
    public static event Action<RematchBoard> BoardArrived;

    /// <summary>Host: a seat is taken out of the rematch</summary>
    public static event Action<int> TakenOut;

    public static void SendVote(int seat, bool rematch) { VoteSent?.Invoke(seat, rematch); }

    public static void ArriveVote(int seat, bool rematch) { VoteArrived?.Invoke(seat, rematch); }

    public static void ShareBoard(RematchBoard board) { BoardShared?.Invoke(board); }

    public static void ArriveBoard(RematchBoard board) { BoardArrived?.Invoke(board); }

    public static void TakeOut(int seat) { TakenOut?.Invoke(seat); }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        VoteSent = null;
        VoteArrived = null;
        BoardShared = null;
        BoardArrived = null;
        TakenOut = null;
    }
}

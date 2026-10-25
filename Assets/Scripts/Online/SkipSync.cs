using System;

/// <summary>
/// The cutscene skip vote on its way in and out of the game code (online). OnlineCutsceneSkip carries them:
/// - Client: its player's vote goes to the host (SendVote); the host's board comes back (ArriveBoard).
/// - Host: a client's vote, checked to be for its own seat, comes in (ArriveVote); its board goes out (ShareBoard).
/// Offline nothing listens: CutsceneSkip decides at once. See docs/online.md
/// </summary>
public static class SkipSync
{
    /// <summary>Client: its player in a seat voted to skip</summary>
    public static event Action<int> VoteSent;

    /// <summary>Host: another machine's player in a seat voted (already checked: their own seat)</summary>
    public static event Action<int> VoteArrived;

    /// <summary>Host: the vote as every machine should show it</summary>
    public static event Action<SkipBoard> BoardShared;

    /// <summary>Client: the host's board arrived</summary>
    public static event Action<SkipBoard> BoardArrived;

    public static void SendVote(int seat) { VoteSent?.Invoke(seat); }

    public static void ArriveVote(int seat) { VoteArrived?.Invoke(seat); }

    public static void ShareBoard(SkipBoard board) { BoardShared?.Invoke(board); }

    public static void ArriveBoard(SkipBoard board) { BoardArrived?.Invoke(board); }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        VoteSent = null;
        VoteArrived = null;
        BoardShared = null;
        BoardArrived = null;
    }
}

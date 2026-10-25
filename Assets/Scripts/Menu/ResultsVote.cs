using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The results screen's rematch vote (ResultsMenu opens it once the scores are up). Each player votes for themselves:
/// A is Rematch, B takes the vote back or leaves (Back to lobby). The rules are RematchVote's.
/// - Locally, and on the host online, it decides: a rematch drops the seats taken out (locally) or disconnects their
///   machines (online, RematchSync.TakeOut), then rematches through the scene flow; otherwise everyone goes back.
/// - An online client sends its player's vote to the host and shows the host's board; its B with no vote leaves the
///   session. The host's scene flow takes it on from there.
/// The view (ResultsVoteView) shows the ticks and the countdown
/// </summary>
public class ResultsVote : MonoBehaviour
{
    /// <summary>The vote taking presses, or null while none is open</summary>
    public static ResultsVote Current
    {
        get { return current != null ? current : null; } // a results screen unloaded with its vote open: none
        private set { current = value; }
    }
    static ResultsVote current;

    // The game's hooks, swapped by tests
    internal Func<double> Clock = RealTime;
    internal Action<int> DropSeat = DropLocalSeat;
    internal Func<int, bool> SeatStillThere = InRoster;
    internal Action LeaveSession = OnlineGame.LeaveOnline;

    internal ResultsVoteView View;

    RematchVote vote;
    bool decides;     // this machine decides the vote (locally, or the host)
    bool finished;    // the outcome is out: presses do nothing
    RematchBoard board;
    bool shared;      // the host shared its board at least once
    double lastShared;

    static double RealTime() { return Time.realtimeSinceStartupAsDouble; }

    static void DropLocalSeat(int seat)
    {
        if (PlayerInstantiate.Instance != null)
            PlayerInstantiate.Instance.DropSeat(seat);
    }

    // Online, a seat whose machine left is gone from the roster
    static bool InRoster(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        return players == null || (seat >= 0 && seat < Constants.MAX_PLAYERS && players.Roster[seat] != null);
    }

    /// <summary>
    /// Opens a fresh vote for these seats. hostSeat is the host's own seat online (-1 locally); decides is whether this
    /// machine decides (GameAuthority.IsAuthority)
    /// </summary>
    public void Open(IEnumerable<int> seats, int hostSeat, bool decides)
    {
        Unhook();
        this.decides = decides;
        vote = new RematchVote(seats, Clock(), hostSeat);
        board = RematchBoard.From(vote, Clock());
        finished = false;
        shared = false;
        Current = this;

        if (decides)
            RematchSync.VoteArrived += OnVote;
        else
            RematchSync.BoardArrived += OnBoard;
        View?.Show(board);
    }

    /// <summary>Whether a seat has voted Rematch, as this machine knows it (the host's board on a client)</summary>
    internal bool Voted(int seat)
    {
        return decides ? vote != null && vote.HasVoted(seat) : board.HasVoted(seat);
    }

    /// <summary>A player's press: south is A (Rematch), otherwise B (take the vote back, or leave)</summary>
    public void Press(int seat, bool south)
    {
        if (vote == null || finished)
            return;

        if (!decides)
        {
            if (south)
            {
                if (!board.HasVoted(seat))
                    RematchSync.SendVote(seat, true);
            }
            else if (board.HasVoted(seat))
                RematchSync.SendVote(seat, false);
            else
            {
                finished = true;
                LeaveSession?.Invoke();
            }
            return;
        }

        if (south)
            vote.Vote(seat);
        else if (vote.HasVoted(seat))
            vote.TakeBack(seat);
        else
            vote.Leave(seat);
    }

    void Update()
    {
        if (Current == this)
            Tick(Clock());
    }

    /// <summary>Decides the vote if it can (the deciding machine) and refreshes the view</summary>
    internal void Tick(double now)
    {
        if (vote == null || finished || !decides)
            return;

        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            if (vote.IsIn(seat) && !SeatStillThere(seat))
                vote.Leave(seat);
        }

        RematchOutcome outcome = vote.Decide(now);
        RematchBoard next = RematchBoard.From(vote, now);
        if (!shared || !next.Equals(board) || now - lastShared >= 1)
        {
            board = next;
            shared = true;
            lastShared = now;
            RematchSync.ShareBoard(board);
            View?.Show(board);
        }

        if (outcome == RematchOutcome.Waiting)
            return;

        finished = true;
        Unhook();
        if (Current == this)
            Current = null;
        if (ResultsMenu.Instance != null)
            ResultsMenu.Instance.ClearResultsView();

        if (outcome == RematchOutcome.Rematch)
        {
            foreach (int seat in vote.Out)
            {
                if (GameAuthority.IsOnline)
                    RematchSync.TakeOut(seat);
                else
                    DropSeat(seat);
            }
            SceneFlow.Current.Rematch();
        }
        else
            SceneFlow.Current.ReturnToMenu();
    }

    // Host: another machine's player voted (OnlineRematch checked it's their own seat)
    void OnVote(int seat, bool rematch)
    {
        if (finished || vote == null)
            return;
        if (rematch)
            vote.Vote(seat);
        else
            vote.TakeBack(seat);
    }

    // Client: the host's board
    void OnBoard(RematchBoard arrived)
    {
        if (finished)
            return;
        board = arrived;
        View?.Show(board);
        if (board.Outcome != RematchOutcome.Waiting)
        {
            finished = true;
            Unhook();
            if (Current == this)
                Current = null;
        }
    }

    void Unhook()
    {
        RematchSync.VoteArrived -= OnVote;
        RematchSync.BoardArrived -= OnBoard;
    }

    void OnDestroy()
    {
        Unhook();
        if (Current == this)
            Current = null;
    }
}

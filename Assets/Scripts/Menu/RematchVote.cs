using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>What the results screen's vote came to: still waiting, a rematch, or everyone back to the lobby</summary>
public enum RematchOutcome { Waiting, Rematch, Lobby }

/// <summary>
/// The results screen's rematch vote, as Mario Kart's: every player still in votes Rematch, or leaves (Back to lobby).
/// - Once everyone still in has voted, the rematch starts at once.
/// - When the countdown runs out, whoever voted plays again and the rest are taken out; nobody voted: the lobby.
/// - Online the host can't leave alone: its leaving, or its countdown running out without its vote, is the lobby for all.
/// It decides once: later presses change nothing. ResultsVote runs it, on the machine that decides (the host online)
/// </summary>
public class RematchVote
{
    public const float SECONDS = 15f;

    readonly SortedSet<int> seats;
    readonly HashSet<int> voted = new HashSet<int>();
    readonly SortedSet<int> left = new SortedSet<int>();
    readonly double deadline;
    readonly int hostSeat;
    RematchOutcome outcome = RematchOutcome.Waiting;
    int[] playing = new int[0];
    int[] taken = new int[0];

    /// <param name="seats">The seats in the vote</param>
    /// <param name="openedAt">When the choices showed (seconds, any clock)</param>
    /// <param name="hostSeat">Online, the host's own seat; -1 locally</param>
    public RematchVote(IEnumerable<int> seats, double openedAt, int hostSeat = -1)
    {
        this.seats = new SortedSet<int>(seats);
        deadline = openedAt + SECONDS;
        this.hostSeat = hostSeat;
    }

    bool Decided { get { return outcome != RematchOutcome.Waiting; } }

    /// <summary>The seats that play on, after a Rematch (ascending)</summary>
    public IReadOnlyList<int> Playing { get { return playing; } }

    /// <summary>The seats taken out, once decided: those who left and those who ran out of time (ascending)</summary>
    public IReadOnlyList<int> Out { get { return taken; } }

    public bool IsIn(int seat) { return seats.Contains(seat); }

    public bool HasVoted(int seat) { return voted.Contains(seat); }

    /// <summary>A seat votes Rematch. False: it isn't in, voted already, or the vote is decided</summary>
    public bool Vote(int seat)
    {
        if (Decided || !seats.Contains(seat))
            return false;
        return voted.Add(seat);
    }

    /// <summary>A seat takes its Rematch vote back. False: it hadn't voted, or the vote is decided</summary>
    public bool TakeBack(int seat)
    {
        return !Decided && voted.Remove(seat);
    }

    /// <summary>A seat leaves the vote: Back to lobby, a disconnect, or a dropped seat</summary>
    public void Leave(int seat)
    {
        if (Decided || !seats.Remove(seat))
            return;
        voted.Remove(seat);
        left.Add(seat);
    }

    /// <summary>The whole seconds left on the countdown (rounded up, never below 0)</summary>
    public int SecondsLeft(double now)
    {
        return Math.Max(0, (int)Math.Ceiling(deadline - now));
    }

    /// <summary>Decides the vote if it can, once: afterwards the same outcome comes back</summary>
    public RematchOutcome Decide(double now)
    {
        if (Decided)
            return outcome;

        bool timeUp = now >= deadline;
        if (hostSeat >= 0 && left.Contains(hostSeat))
            return Finish(RematchOutcome.Lobby);
        if (seats.Count == 0)
            return Finish(RematchOutcome.Lobby);
        if (seats.All(voted.Contains))
            return Finish(RematchOutcome.Rematch);
        if (!timeUp)
            return RematchOutcome.Waiting;
        if (hostSeat >= 0 && seats.Contains(hostSeat) && !voted.Contains(hostSeat))
            return Finish(RematchOutcome.Lobby);
        return Finish(voted.Count > 0 ? RematchOutcome.Rematch : RematchOutcome.Lobby);
    }

    RematchOutcome Finish(RematchOutcome result)
    {
        outcome = result;
        if (result == RematchOutcome.Rematch)
        {
            playing = seats.Where(voted.Contains).ToArray();
            taken = left.Concat(seats.Where(s => !voted.Contains(s))).OrderBy(s => s).ToArray();
        }
        else
        {
            taken = left.Concat(seats).OrderBy(s => s).ToArray();
        }
        return result;
    }
}

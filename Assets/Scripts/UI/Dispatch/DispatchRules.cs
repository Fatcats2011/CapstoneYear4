using System;
using System.Collections.Generic;

/// <summary>One seat's ticket on the Dispatch board. A seat nobody holds has Taken = false</summary>
public struct TicketState : IEquatable<TicketState>
{
    public int Seat;
    public bool Taken;
    public string Name;
    public int Colour;
    public int Hat;
    public bool Ready;
    public bool Mine;

    public bool Equals(TicketState other)
    {
        return Seat == other.Seat && Taken == other.Taken && string.Equals(Name, other.Name, StringComparison.Ordinal)
               && Colour == other.Colour && Hat == other.Hat && Ready == other.Ready && Mine == other.Mine;
    }

    public override bool Equals(object obj)
    {
        return obj is TicketState other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int h = Seat;
            h = h * 31 + (Taken ? 1 : 0);
            h = h * 31 + (Name == null ? 0 : StringComparer.Ordinal.GetHashCode(Name));
            h = h * 31 + Colour;
            h = h * 31 + Hat;
            h = h * 31 + (Ready ? 1 : 0);
            h = h * 31 + (Mine ? 1 : 0);
            return h;
        }
    }

    public static bool operator ==(TicketState a, TicketState b) { return a.Equals(b); }
    public static bool operator !=(TicketState a, TicketState b) { return !a.Equals(b); }
}

/// <summary>The Dispatch lobby's rules: who sits where, the ready counter, when a ticket flashes</summary>
public static class DispatchRules
{
    /// <summary>
    /// One ticket per seat, indexed by seat. Players with a seat outside the table are ignored. If two players claim
    /// the same seat, the last one wins
    /// </summary>
    public static TicketState[] Tickets(IEnumerable<(int seat, string name, int colour, int hat, bool ready, bool mine)> players)
    {
        TicketState[] tickets = new TicketState[Constants.MAX_PLAYERS];
        for (int i = 0; i < tickets.Length; i++) tickets[i] = new TicketState { Seat = i };
        if (players == null) return tickets;

        foreach (var p in players)
        {
            if (p.seat < 0 || p.seat >= tickets.Length) continue;
            tickets[p.seat] = new TicketState
            {
                Seat = p.seat,
                Taken = true,
                Name = PlayerNames.Shown(p.name, p.seat),
                Colour = p.colour,
                Hat = p.hat,
                Ready = p.ready,
                Mine = p.mine,
            };
        }
        return tickets;
    }

    /// <summary>"2 / 3 READY": ready seats out of seated ones</summary>
    public static string ReadyLine(TicketState[] tickets)
    {
        int ready = 0, seated = 0;
        foreach (TicketState t in tickets)
        {
            if (!t.Taken) continue;
            seated++;
            if (t.Ready) ready++;
        }
        return $"{ready} / {seated} READY";
    }

    /// <summary>A ticket flashes when it changed and is still held; a seat emptying doesn't</summary>
    public static bool Flashes(TicketState before, TicketState after)
    {
        return after.Taken && before != after;
    }

    /// <summary>At least one seat is taken and every taken seat is ready (the host alone can start)</summary>
    public static bool EveryoneReady(TicketState[] tickets)
    {
        bool any = false;
        foreach (TicketState t in tickets)
        {
            if (!t.Taken) continue;
            if (!t.Ready) return false;
            any = true;
        }
        return any;
    }
}

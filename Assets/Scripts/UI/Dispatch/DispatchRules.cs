using System;
using System.Collections.Generic;
using UnityEngine;

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
    public ulong SteamId; // their profile picture's owner (0: none, as without Steam)

    public bool Equals(TicketState other)
    {
        return Seat == other.Seat && Taken == other.Taken && string.Equals(Name, other.Name, StringComparison.Ordinal)
               && Colour == other.Colour && Hat == other.Hat && Ready == other.Ready && Mine == other.Mine
               && SteamId == other.SteamId;
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
            h = h * 31 + SteamId.GetHashCode();
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

    /// <summary>
    /// The podium a seat's scooter stands on in the menus: online, the seat in the Dispatch room; offline (Local Play),
    /// the seat's own booth. A room missing any seat isn't used at all, online or not
    /// </summary>
    public static GameObject Podium(int seat, bool online, GameObject[] room, GameObject[] booths)
    {
        return online && HasEverySeat(room) ? room[seat] : booths[seat];
    }

    /// <summary>
    /// The room's seats: the ones wired on PlayerInstantiate when all 4 are, else the loaded room's (DispatchRoom, its
    /// own scene), which may be null
    /// </summary>
    public static GameObject[] RoomSeats(GameObject[] wired, GameObject[] loaded)
    {
        return HasEverySeat(wired) ? wired : loaded;
    }

    /// <summary>
    /// A point in the room camera's viewport (0-1 across and up) as a position on a canvas of that size, from its centre
    /// </summary>
    public static Vector2 ViewportToCanvas(Vector3 viewport, Vector2 canvasSize)
    {
        return new Vector2((viewport.x - 0.5f) * canvasSize.x, (viewport.y - 0.5f) * canvasSize.y);
    }

    /// <summary>Whether a viewport point is in front of the camera</summary>
    public static bool InView(Vector3 viewport)
    {
        return viewport.z > 0;
    }

    static bool HasEverySeat(GameObject[] room)
    {
        if (room == null || room.Length < Constants.MAX_PLAYERS)
            return false;
        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            if (room[i] == null)
                return false;
        }
        return true;
    }

    /// <summary>
    /// The part of a player's preview the customize pop-up shows: the ghost and its colour and hat sliders, without the
    /// empty sides. Players 1 and 3 have their sliders right of the ghost, players 2 and 4 left
    /// (PlayerCameraResizer.RelocateCustomizationMenu), so the crop mirrors for them
    /// </summary>
    public static Rect CustomizeCrop(int seat)
    {
        const float left = 0.27f, width = 0.72f, bottom = 0.03f, height = 0.88f;
        bool mirrored = (seat + 1) % 2 == 0;
        return new Rect(mirrored ? 1 - left - width : left, bottom, width, height);
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

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class DispatchRulesTests
{
    private static TicketState[] Four(params int[] takenSeats)
    {
        TicketState[] t = new TicketState[Constants.MAX_PLAYERS];
        for (int i = 0; i < t.Length; i++) t[i] = new TicketState { Seat = i };
        return t;
    }

    private static TicketState Seat(int seat, bool taken, bool ready)
    {
        return new TicketState { Seat = seat, Taken = taken, Ready = ready, Name = "x" };
    }

    [Test]
    public void Tickets_EmptyInput_FourFreeSeats()
    {
        TicketState[] t = DispatchRules.Tickets(new List<(int, string, int, int, bool, bool)>());
        Assert.AreEqual(Constants.MAX_PLAYERS, t.Length);
        for (int i = 0; i < t.Length; i++)
        {
            Assert.IsFalse(t[i].Taken);
            Assert.AreEqual(i, t[i].Seat);
        }
    }

    [Test]
    public void Tickets_PlacesBySeat_AndFillsFallbackName()
    {
        TicketState[] t = DispatchRules.Tickets(new List<(int, string, int, int, bool, bool)>
        {
            (2, "", 1, 3, false, true),
            (0, "Ryan", 0, 0, true, false),
        });
        Assert.AreEqual("Ryan", t[0].Name);
        Assert.AreEqual("Player 3", t[2].Name);
        Assert.IsTrue(t[2].Mine);
        Assert.IsFalse(t[1].Taken);
        Assert.AreEqual(1, t[2].Colour);
        Assert.AreEqual(3, t[2].Hat);
        Assert.IsTrue(t[0].Ready);
    }

    [Test]
    public void Tickets_SeatOutOfRange_Ignored()
    {
        TicketState[] t = DispatchRules.Tickets(new List<(int, string, int, int, bool, bool)>
        {
            (-1, "A", 0, 0, false, false),
            (4, "B", 0, 0, false, false),
        });
        Assert.AreEqual(Constants.MAX_PLAYERS, t.Length);
        foreach (TicketState s in t) Assert.IsFalse(s.Taken);
    }

    [Test]
    public void Tickets_SameSeatTwice_LastWins()
    {
        TicketState[] t = DispatchRules.Tickets(new List<(int, string, int, int, bool, bool)>
        {
            (1, "First", 0, 0, false, false),
            (1, "Second", 0, 0, false, false),
        });
        Assert.AreEqual("Second", t[1].Name);
    }

    [Test]
    public void ReadyLine_CountsOnlyTakenSeats()
    {
        TicketState[] t = Four();
        t[0] = Seat(0, true, true);
        t[1] = Seat(1, true, true);
        t[2] = Seat(2, true, false);
        t[3] = Seat(3, false, true);
        Assert.AreEqual("2 / 3 READY", DispatchRules.ReadyLine(t));
        Assert.AreEqual("0 / 0 READY", DispatchRules.ReadyLine(Four()));
    }

    [Test]
    public void Flashes_ChangesOnTakenSeat()
    {
        TicketState a = new TicketState { Seat = 0, Taken = true, Name = "A", Colour = 0, Ready = false };
        TicketState b = a; b.Colour = 1;
        Assert.IsTrue(DispatchRules.Flashes(a, b));
        b = a; b.Ready = true;
        Assert.IsTrue(DispatchRules.Flashes(a, b));
        b = a; b.Name = "B";
        Assert.IsTrue(DispatchRules.Flashes(a, b));
        b = a;
        Assert.IsFalse(DispatchRules.Flashes(a, b));
    }

    [Test]
    public void Flashes_EmptyingDoesNot_FillingDoes()
    {
        TicketState taken = new TicketState { Seat = 0, Taken = true, Name = "A" };
        TicketState free = new TicketState { Seat = 0 };
        Assert.IsFalse(DispatchRules.Flashes(taken, free));
        Assert.IsTrue(DispatchRules.Flashes(free, taken));
    }

    [Test]
    public void EveryoneReady_Cases()
    {
        Assert.IsFalse(DispatchRules.EveryoneReady(Four()));
        TicketState[] t = Four();
        t[0] = Seat(0, true, true);
        Assert.IsTrue(DispatchRules.EveryoneReady(t));
        t[1] = Seat(1, true, false);
        Assert.IsFalse(DispatchRules.EveryoneReady(t));
    }

    // Four stand-in podiums; the test destroys them
    private static GameObject[] Podiums(string name)
    {
        GameObject[] p = new GameObject[Constants.MAX_PLAYERS];
        for (int i = 0; i < p.Length; i++) p[i] = new GameObject(name + i);
        return p;
    }

    private static void Destroy(params GameObject[][] sets)
    {
        foreach (GameObject[] set in sets)
            foreach (GameObject o in set)
                if (o != null) Object.DestroyImmediate(o);
    }

    [Test]
    public void Podium_Offline_TheSeatsBooth()
    {
        GameObject[] room = Podiums("Room"), booths = Podiums("Booth");
        try
        {
            Assert.AreSame(booths[2], DispatchRules.Podium(2, false, room, booths), "Local Play stays in its booths");
        }
        finally { Destroy(room, booths); }
    }

    [Test]
    public void Podium_Online_TheRoomsSeat()
    {
        GameObject[] room = Podiums("Room"), booths = Podiums("Booth");
        try
        {
            for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
                Assert.AreSame(room[seat], DispatchRules.Podium(seat, true, room, booths));
        }
        finally { Destroy(room, booths); }
    }

    [Test]
    public void Podium_OnlineWithoutARoom_TheBooths()
    {
        GameObject[] booths = Podiums("Booth");
        try
        {
            Assert.AreSame(booths[1], DispatchRules.Podium(1, true, null, booths), "no room in the scene");
            Assert.AreSame(booths[1], DispatchRules.Podium(1, true, new GameObject[Constants.MAX_PLAYERS], booths), "none wired");
            Assert.AreSame(booths[1], DispatchRules.Podium(1, true, new GameObject[2], booths), "too few");
        }
        finally { Destroy(booths); }
    }

    // A room missing a seat isn't used at all: players split between the room and the booths would see each other's gaps
    [Test]
    public void Podium_OnlineWithARoomSeatMissing_TheBooths()
    {
        GameObject[] room = Podiums("Room"), booths = Podiums("Booth");
        try
        {
            Object.DestroyImmediate(room[3]); // a deleted object: Unity's null
            Assert.AreSame(booths[0], DispatchRules.Podium(0, true, room, booths));
        }
        finally { Destroy(room, booths); }
    }

    [Test]
    public void TicketState_Equality()
    {
        TicketState a = new TicketState { Seat = 1, Taken = true, Name = new string(new[] { 'A', 'b' }) };
        TicketState b = new TicketState { Seat = 1, Taken = true, Name = "Ab" };
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);
        Assert.IsTrue(a.Equals((object)b));
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        b.Hat = 2;
        Assert.IsTrue(a != b);
    }
}

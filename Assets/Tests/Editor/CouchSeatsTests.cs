using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Which seat each of this machine's players moves to online (CouchSeats.Plan, Phase 3J): a player already in one of
    /// this machine's seats stays, the menu player takes the first free one, and a player with no seat left waits unseated
    /// </summary>
    public class CouchSeatsTests
    {
        [Test]
        public void Plan_PlayersAlreadyInHeldSeats_Stay()
        {
            CollectionAssert.AreEqual(new[] { 1, 3 }, CouchSeats.Plan(new[] { 1, 3 }, new[] { 1, 3 }));
        }

        [Test]
        public void Plan_TheMenuPlayerTakesTheFirstFreeSeat()
        {
            // The menu player (listed first) is in slot 2, another player in slot 0; this machine holds seats 1 and 3
            CollectionAssert.AreEqual(new[] { 1, 3 }, CouchSeats.Plan(new[] { 2, 0 }, new[] { 3, 1 }));
        }

        [Test]
        public void Plan_MorePlayersThanSeats_LeavesTheLastUnseated()
        {
            CollectionAssert.AreEqual(new[] { 2, -1 }, CouchSeats.Plan(new[] { 0, 1 }, new[] { 2 }));
        }

        [Test]
        public void Plan_AStayingPlayersSeatIsntGivenToAnother()
        {
            // The second player already sits in seat 2, which this machine holds: the menu player takes 3
            CollectionAssert.AreEqual(new[] { 3, 2 }, CouchSeats.Plan(new[] { 0, 2 }, new[] { 2, 3 }));
        }

        [Test]
        public void Plan_TheMenuPlayer_NeverWaitsWhileAnotherPlayerHasASeat()
        {
            // Two players joined offline (the menu player in slot 0, another in 1); the host gives this machine seat 1. The
            // menu player takes it, and the other waits: a machine's menus need their player
            CollectionAssert.AreEqual(new[] { 1, -1 }, CouchSeats.Plan(new[] { 0, 1 }, new[] { 1 }));
        }

        [Test]
        public void Plan_NoPlayers_OrNoSeats()
        {
            CollectionAssert.IsEmpty(CouchSeats.Plan(new int[0], new[] { 1 }));
            CollectionAssert.AreEqual(new[] { -1 }, CouchSeats.Plan(new[] { 0 }, new int[0]), "online, before a seat came");
        }
    }
}

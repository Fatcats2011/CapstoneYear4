using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Online seats: the host sits in seat 0, each new seat goes to the lowest free one, 4 at most. A machine can hold
    /// several (players sharing its screen, Phase 3J)
    /// </summary>
    public class SeatTableTests
    {
        static bool ZeroAndOneReady(int seat)
        {
            return seat == 0 || seat == 1;
        }

        static bool NoneReady(int seat)
        {
            return false;
        }

        [Test]
        public void UnreadyExtras_KeepsEachMachinesLastSeatAndItsReadyOnes()
        {
            // The match starts: a seat granted that moment has a player who never readied
            SeatTable seats = new SeatTable();
            seats.Take(1);
            seats.Take(2);
            seats.Take(1);
            seats.Take(2);

            CollectionAssert.AreEqual(new[] { (1UL, 2), (2UL, 3) }, seats.UnreadyExtras(ZeroAndOneReady));
        }

        [Test]
        public void UnreadyExtras_NoneReady_EachMachineKeepsItsLowestSeat()
        {
            SeatTable seats = new SeatTable();
            seats.Take(1);
            seats.Take(1);
            seats.Take(2);

            CollectionAssert.AreEqual(new[] { (1UL, 1) }, seats.UnreadyExtras(NoneReady));
        }

        [Test]
        public void Take_TheHost_GetsSeatZero()
        {
            SeatTable seats = new SeatTable();

            Assert.AreEqual(0, seats.Take(0));
            Assert.AreEqual(1, seats.Count);
        }

        [Test]
        public void Take_Joiners_GetTheLowestFreeSeat_EvenAfterSomeoneLeft()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(7);
            seats.Take(8);
            seats.FreeAll(7);

            Assert.AreEqual(1, seats.Take(9), "the freed seat");
            Assert.AreEqual(3, seats.Take(10), "then the next free one");
            Assert.AreEqual(4, seats.Count);
        }

        [Test]
        public void Take_WhenEverySeatIsTaken_ReturnsMinusOne()
        {
            SeatTable seats = new SeatTable();
            for (ulong player = 0; player < Constants.MAX_PLAYERS; player++)
                seats.Take(player);

            Assert.AreEqual(-1, seats.Take(99));
            Assert.AreEqual(Constants.MAX_PLAYERS, seats.Count);
        }

        [Test]
        public void Take_TheSameMachineAgain_GetsAnotherSeat()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);

            Assert.AreEqual(1, seats.Take(5));
            Assert.AreEqual(2, seats.Take(5), "a second player on that machine");
            Assert.AreEqual(3, seats.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, seats.SeatsOf(5));
            Assert.AreEqual(2, seats.CountOf(5));
        }

        [Test]
        public void Owns_OnlyTheMachinesOwnSeats()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(5);
            seats.Take(5);

            Assert.IsTrue(seats.Owns(5, 1));
            Assert.IsTrue(seats.Owns(5, 2));
            Assert.IsFalse(seats.Owns(5, 0), "the host's");
            Assert.IsFalse(seats.Owns(5, -1), "not a seat");
            Assert.IsFalse(seats.Owns(5, 4), "not a seat");
            Assert.IsFalse(seats.Owns(5, 3), "a free seat");
        }

        [Test]
        public void Free_OneSeat_KeepsTheMachinesOthers()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(5);
            seats.Take(5);

            Assert.IsTrue(seats.Free(5, 1));
            Assert.IsTrue(seats.Owns(5, 2), "its other seat stays");
            Assert.AreEqual(1, seats.CountOf(5));
            Assert.IsFalse(seats.Free(7, 2), "not that machine's seat");
            Assert.IsFalse(seats.Free(5, 1), "already free");
            Assert.AreEqual(2, seats.Count);
        }

        [Test]
        public void FreeAll_FreesEveryOneOfAMachinesSeats()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(5);
            seats.Take(5);

            Assert.AreEqual(2, seats.FreeAll(5));
            Assert.AreEqual(1, seats.Count);
            Assert.AreEqual(1, seats.Take(9), "the lowest freed seat");
        }

        [Test]
        public void SeatsAndFree_AMachineWithoutASeat_HaveNone()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);

            Assert.IsEmpty(seats.SeatsOf(3));
            Assert.AreEqual(0, seats.CountOf(3));
            Assert.AreEqual(0, seats.FreeAll(3));
            Assert.AreEqual(1, seats.Count);
        }

        [Test]
        public void Clear_FreesEverySeat()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(1);

            seats.Clear();

            Assert.AreEqual(0, seats.Count);
            Assert.IsFalse(seats.Owns(1, 1));
            Assert.AreEqual(0, seats.Take(4), "seat 0 is free again");
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Online seats: the host sits in seat 0, each joiner in the lowest free seat, 4 at most
    /// </summary>
    public class SeatTableTests
    {
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
            seats.Free(7);

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
        public void Take_ASeatedPlayerAgain_KeepsTheirSeat()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(5);

            Assert.AreEqual(1, seats.Take(5));
            Assert.AreEqual(2, seats.Count);
        }

        [Test]
        public void SeatOfAndFree_APlayerWithoutASeat_AreMinusOne()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);

            Assert.AreEqual(-1, seats.SeatOf(3));
            Assert.AreEqual(-1, seats.Free(3));
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
            Assert.AreEqual(-1, seats.SeatOf(1));
            Assert.AreEqual(0, seats.Take(4), "seat 0 is free again");
        }
    }
}

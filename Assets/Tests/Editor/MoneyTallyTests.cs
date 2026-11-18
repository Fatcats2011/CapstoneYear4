using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Where a seat's money came from (MoneyTally), how a delivery's pay splits into sources (MoneyRules) and how the game
    /// reports it (MoneySync)
    /// </summary>
    public class MoneyTallyTests
    {
        static int heard;
        static int lastSeat;
        static MoneySource lastSource;
        static int lastAmount;

        [SetUp]
        public void SetUp()
        {
            MoneySync.Reset();
            heard = 0;
        }

        [TearDown]
        public void TearDown()
        {
            MoneySync.Reset();
        }

        static void Heard(int seat, MoneySource source, int amount)
        {
            heard++;
            lastSeat = seat;
            lastSource = source;
            lastAmount = amount;
        }

        static void Split(Constants.OrderValue tier, bool lode, int worth, int deliveries, int steals, int golden)
        {
            MoneyRules.SplitDelivery(tier, lode, worth, out int d, out int s, out int g);
            Assert.AreEqual(deliveries, d, "deliveries");
            Assert.AreEqual(steals, s, "steals");
            Assert.AreEqual(golden, g, "golden");
        }

        [Test]
        public void Split_AnEasyOrderNeverStolen_IsAllDeliveries()
        {
            Split(Constants.OrderValue.Easy, false, 55, 55, 0, 0);
        }

        [Test]
        public void Split_AHardOrderStolenOnce_HalfDeliveriesHalfSteals()
        {
            Split(Constants.OrderValue.Hard, false, 160, 80, 80, 0);
        }

        [Test]
        public void Split_TheGoldenOrder_IsAllGolden()
        {
            Split(Constants.OrderValue.Golden, false, 50, 0, 0, 50);
        }

        [Test]
        public void Split_TheMotherLode_IsAllDeliveries()
        {
            Split(Constants.OrderValue.Golden, true, 250, 250, 0, 0);
            Split(Constants.OrderValue.Easy, true, 250, 250, 0, 0);
        }

        [Test]
        public void Split_WorthBelowThePrice_NeverGoesNegative()
        {
            MoneyRules.SplitDelivery(Constants.OrderValue.Hard, false, 10, out int deliveries, out int steals, out int golden);

            Assert.AreEqual(0, steals);
            Assert.AreEqual(10, deliveries + steals + golden, "the parts still add up to the pay");
        }

        [Test]
        public void Tally_Adds_PerSeatAndSource()
        {
            MoneyTally tally = new MoneyTally();

            tally.Add(0, MoneySource.Deliveries, 55);
            tally.Add(0, MoneySource.Deliveries, 65);
            tally.Add(0, MoneySource.Steals, 7);
            tally.Add(2, MoneySource.Mode, 3);

            Assert.AreEqual(120, tally.Of(0, MoneySource.Deliveries));
            Assert.AreEqual(7, tally.Of(0, MoneySource.Steals));
            Assert.AreEqual(0, tally.Of(0, MoneySource.Golden));
            Assert.AreEqual(3, tally.Of(2, MoneySource.Mode));
            Assert.AreEqual(0, tally.Of(1, MoneySource.Deliveries));
        }

        [Test]
        public void Tally_IgnoresBadSeatsAndNonPositiveAmounts()
        {
            MoneyTally tally = new MoneyTally();

            tally.Add(-1, MoneySource.Deliveries, 10);
            tally.Add(Constants.MAX_PLAYERS, MoneySource.Deliveries, 10);
            tally.Add(0, MoneySource.Deliveries, 0);
            tally.Add(0, MoneySource.Deliveries, -5);

            for (int seat = -1; seat <= Constants.MAX_PLAYERS; seat++)
                Assert.AreEqual(0, tally.Total(seat));
        }

        [Test]
        public void Tally_Total_IsTheFourSummed()
        {
            MoneyTally tally = new MoneyTally();
            tally.Add(1, MoneySource.Deliveries, 1);
            tally.Add(1, MoneySource.Steals, 2);
            tally.Add(1, MoneySource.Golden, 4);
            tally.Add(1, MoneySource.Mode, 8);

            Assert.AreEqual(15, tally.Total(1));
        }

        [Test]
        public void Tally_NewMatch_ClearsIt()
        {
            MoneyTally tally = new MoneyTally();
            tally.Add(0, MoneySource.Golden, 50);

            tally.NewMatch();

            Assert.AreEqual(0, tally.Total(0));
        }

        [Test]
        public void Earn_RaisesOnce_WithItsValues()
        {
            MoneySync.Earned += Heard;

            MoneySync.Earn(2, MoneySource.Steals, 7);

            Assert.AreEqual(1, heard);
            Assert.AreEqual(2, lastSeat);
            Assert.AreEqual(MoneySource.Steals, lastSource);
            Assert.AreEqual(7, lastAmount);
        }

        [Test]
        public void Earn_Nothing_RaisesNothing()
        {
            MoneySync.Earned += Heard;

            MoneySync.Earn(0, MoneySource.Mode, 0);
            MoneySync.Earn(0, MoneySource.Mode, -3);

            Assert.AreEqual(0, heard);
        }

        [Test]
        public void Reset_ForgetsListeners()
        {
            MoneySync.Earned += Heard;

            MoneySync.Reset();
            MoneySync.Earn(0, MoneySource.Mode, 5);

            Assert.AreEqual(0, heard);
        }
    }
}

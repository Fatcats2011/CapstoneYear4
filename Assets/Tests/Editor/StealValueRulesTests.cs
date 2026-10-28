using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What an order is worth and what a steal pays: the tier prices, the doubling with each steal (never for the golden
    /// order), the cap that keeps a long run of steals from overflowing a score, the thief's payment (once per order per
    /// thief) and how much a stolen order swells
    /// </summary>
    public class StealValueRulesTests
    {
        [Test]
        public void Prices_AreTheTimeBasedOnes()
        {
            Assert.AreEqual(55, StealValueRules.Price(Constants.OrderValue.Easy));
            Assert.AreEqual(65, StealValueRules.Price(Constants.OrderValue.Medium));
            Assert.AreEqual(80, StealValueRules.Price(Constants.OrderValue.Hard));
            Assert.AreEqual(50, StealValueRules.Price(Constants.OrderValue.Golden));
        }

        [Test]
        public void Worth_DoublesPerSteal()
        {
            Assert.AreEqual(55, StealValueRules.Worth(Constants.OrderValue.Easy, 0));
            Assert.AreEqual(110, StealValueRules.Worth(Constants.OrderValue.Easy, 1));
            Assert.AreEqual(220, StealValueRules.Worth(Constants.OrderValue.Easy, 2));
            Assert.AreEqual(320, StealValueRules.Worth(Constants.OrderValue.Hard, 2));
        }

        [Test]
        public void Worth_GoldenNeverDoubles()
        {
            Assert.AreEqual(50, StealValueRules.Worth(Constants.OrderValue.Golden, 0));
            Assert.AreEqual(50, StealValueRules.Worth(Constants.OrderValue.Golden, 3));
        }

        [Test]
        public void Worth_ManyStealsIsCappedNotOverflowed()
        {
            Assert.AreEqual(80 * 65536, StealValueRules.Worth(Constants.OrderValue.Hard, 100));
            Assert.Greater(StealValueRules.Worth(Constants.OrderValue.Hard, int.MaxValue), 0);
            Assert.AreEqual(80, StealValueRules.Worth(Constants.OrderValue.Hard, -3), "a negative count is none");
        }

        [Test]
        public void Pay_FirstTimePerThiefPays_ThenNothing()
        {
            HashSet<int> paid = new HashSet<int>();

            Assert.AreEqual(7, StealValueRules.Pay(paid, 0));
            Assert.AreEqual(0, StealValueRules.Pay(paid, 0), "the same thief again");
            Assert.AreEqual(7, StealValueRules.Pay(paid, 1), "another thief");
        }

        [Test]
        public void Pay_UnknownSeat_PaysNothing()
        {
            HashSet<int> paid = new HashSet<int>();

            Assert.AreEqual(0, StealValueRules.Pay(paid, -1));
            Assert.AreEqual(0, paid.Count);
        }

        [Test]
        public void Swell_GrowsPerStealThenStops()
        {
            Assert.AreEqual(1f, StealValueRules.Swell(0), 0.001f);
            Assert.AreEqual(1.15f, StealValueRules.Swell(1), 0.001f);
            Assert.AreEqual(1.6f, StealValueRules.Swell(4), 0.001f);
            Assert.AreEqual(1.6f, StealValueRules.Swell(10), 0.001f);
        }
    }
}

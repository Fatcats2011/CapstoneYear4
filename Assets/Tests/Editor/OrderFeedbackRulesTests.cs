using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>What an order's price reads on its beacon and its compass mark: "$" and its worth, nothing for the golden order</summary>
    public class OrderFeedbackRulesTests
    {
        [Test]
        public void PriceText_AnEasyOrder_ReadsItsWorth()
        {
            Assert.AreEqual("$55", OrderFeedbackRules.PriceText(Constants.OrderValue.Easy, false, 55));
        }

        [Test]
        public void PriceText_AStolenOrder_ReadsTheDoubledWorth()
        {
            Assert.AreEqual("$130", OrderFeedbackRules.PriceText(Constants.OrderValue.Medium, false, 130));
        }

        [Test]
        public void PriceText_TheGoldenOrder_ReadsNothing()
        {
            Assert.AreEqual("", OrderFeedbackRules.PriceText(Constants.OrderValue.Golden, false, 50));
        }

        [Test]
        public void PriceText_TheMotherLode_ReadsItsWorth()
        {
            Assert.AreEqual("$250", OrderFeedbackRules.PriceText(Constants.OrderValue.Golden, true, 250));
        }
    }
}

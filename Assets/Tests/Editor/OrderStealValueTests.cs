using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// An order counts the steals it has been through and is worth its price doubled for each; the golden order stays at
    /// its price. A thief is paid once per order per seat, and the best order a player holds is the one worth most.
    /// Edit mode: nothing starts, so these reach the members directly
    /// </summary>
    public class OrderStealValueTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        Order NewOrder(Constants.OrderValue tier)
        {
            Order order = objects.Add<Order>();
            order.value = tier;
            return order;
        }

        [Test]
        public void CountSteal_RaisesWorthByDoubling()
        {
            Order order = NewOrder(Constants.OrderValue.Hard);

            order.CountSteal();
            order.CountSteal();

            Assert.AreEqual(2, order.Steals);
            Assert.AreEqual(320, order.Worth);
        }

        [Test]
        public void ResetSteals_BackToThePriceAndForgetsThieves()
        {
            Order order = NewOrder(Constants.OrderValue.Hard);
            order.CountSteal();
            order.CountSteal();
            order.PaidThieves.Add(0);

            order.ResetSteals();

            Assert.AreEqual(0, order.Steals);
            Assert.AreEqual(80, order.Worth);
            Assert.AreEqual(0, order.PaidThieves.Count);
        }

        [Test]
        public void Golden_WorthStaysAtItsPrice()
        {
            Order order = NewOrder(Constants.OrderValue.Golden);

            order.CountSteal();
            order.CountSteal();
            order.CountSteal();

            Assert.AreEqual(50, order.Worth);
        }

        [Test]
        public void PayForSteal_PaysOncePerOrderPerThief()
        {
            Order order = NewOrder(Constants.OrderValue.Easy);
            OrderHandler thief = objects.Add<OrderHandler>();

            Assert.AreEqual(7, thief.PayForSteal(order, 0));
            Assert.AreEqual(7, thief.Score);
            Assert.AreEqual(0, thief.PayForSteal(order, 0), "the same seat again");
            Assert.AreEqual(7, thief.Score);
            Assert.AreEqual(7, thief.PayForSteal(order, 1), "another seat takes it");
            Assert.AreEqual(14, thief.Score);
        }

        [Test]
        public void PayForSteal_PaysForTheGoldenOrderToo()
        {
            Order golden = NewOrder(Constants.OrderValue.Golden);
            OrderHandler thief = objects.Add<OrderHandler>();

            Assert.AreEqual(7, thief.PayForSteal(golden, 0));
        }

        [Test]
        public void PayForSteal_UnknownSeat_PaysNothing()
        {
            Order order = NewOrder(Constants.OrderValue.Easy);
            OrderHandler thief = objects.Add<OrderHandler>();

            Assert.AreEqual(0, thief.PayForSteal(order, -1));
            Assert.AreEqual(0, thief.Score);
        }

        [Test]
        public void GetBestOrder_ChoosesByWorth()
        {
            OrderHandler player = objects.Add<OrderHandler>();
            Order easy = NewOrder(Constants.OrderValue.Easy);
            Order hard = NewOrder(Constants.OrderValue.Hard);
            player.order1 = easy;
            player.order2 = hard;

            Assert.AreSame(hard, player.GetBestOrder(), "unstolen, the hard one is worth more");

            easy.CountSteal();
            easy.CountSteal();

            Assert.AreSame(easy, player.GetBestOrder(), "an easy order stolen twice is worth 220 against 80");
        }
    }
}

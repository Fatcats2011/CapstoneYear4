using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// A player's order handler freezes their scooter when each match's main game ends, and stops listening when the match
    /// resets: matches after matches don't pile up listeners on the order manager. Edit mode: nothing starts
    /// </summary>
    public class OrderHandlerResetTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void UnhookingTheMatchEnd_RemovesWhatHookingAdded()
        {
            OrderManager orders = objects.Add<OrderManager>();
            OrderHandler player = objects.Add<OrderHandler>();

            for (int match = 0; match < 2; match++)
            {
                player.HookMatchEnd(orders);
                Assert.AreEqual(1, Reflect.HandlerCount(orders, nameof(OrderManager.OnMainGameFinishes), player), "listening during match " + match);
                player.UnhookMatchEnd(orders);
            }

            Assert.AreEqual(0, Reflect.HandlerCount(orders, nameof(OrderManager.OnMainGameFinishes), player), "nothing left after two matches");
        }

        [Test]
        public void Destroyed_LeavesNoMatchEndListener()
        {
            // A machine leaving mid-match takes its players' handlers with it, before the match ends
            OrderManager orders = objects.Add<OrderManager>();
            OrderManager.instance = orders;
            try
            {
                OrderHandler player = objects.Add<OrderHandler>();
                player.HookMatchEnd(orders);

                // Unity calls OnDestroy itself only in Play Mode
                player.OnDestroy();

                Assert.AreEqual(0, Reflect.HandlerCount(orders, nameof(OrderManager.OnMainGameFinishes), player));
            }
            finally
            {
                OrderManager.instance = null;
            }
        }
    }
}

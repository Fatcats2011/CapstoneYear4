using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>Gold Rush's numbers (GoldRushRules): more packages, the Mother Lode, and rush hour's double steals</summary>
    public class GoldRushRulesTests
    {
        GameObject ordersHolder;

        [TearDown]
        public void TearDown()
        {
            HouseRules.Playing = HouseRules.Normal;
            OrderManager.instance = null;
            if (ordersHolder != null)
                Object.DestroyImmediate(ordersHolder);
        }

        // The match's order manager with timeLeft on its clock (inactive: its OnEnable needs the game's managers)
        OrderManager OrdersAt(float timeLeft)
        {
            ordersHolder = new GameObject("Orders");
            ordersHolder.SetActive(false);
            OrderManager orders = ordersHolder.AddComponent<OrderManager>();
            OrderManager.instance = orders;
            typeof(OrderManager).GetField("gameTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(orders, timeLeft);
            return orders;
        }

        static void PlayGoldRush()
        {
            HouseRules.Playing = new HouseRules(MatchLength.Normal, true, true, true, GameMode.GoldRush);
        }

        [Test]
        public void Quota_AndCooldown_OnlyChangeInGoldRush()
        {
            Assert.AreEqual(2, GoldRushRules.Quota(2));
            Assert.AreEqual(4f, GoldRushRules.Cooldown(4f));

            PlayGoldRush();

            Assert.AreEqual(6, GoldRushRules.Quota(2));
            Assert.AreEqual(1f, GoldRushRules.Cooldown(4f));
            Assert.AreEqual(0.5f, GoldRushRules.Cooldown(0.5f), "never slower than the scene");
        }

        [TestCase(GameMode.GoldRush, 30f, false, true)]
        [TestCase(GameMode.GoldRush, 12f, false, true)]
        [TestCase(GameMode.GoldRush, 31f, false, false)]
        [TestCase(GameMode.GoldRush, 0f, false, false)]
        [TestCase(GameMode.GoldRush, 10f, true, false)]
        [TestCase(GameMode.Standard, 10f, false, false)]
        public void IsRushHour(GameMode mode, float timeLeft, bool wavesOver, bool rush)
        {
            Assert.AreEqual(rush, GoldRushRules.IsRushHour(mode, timeLeft, wavesOver));
        }

        [Test]
        public void StealCounts_IsOne_WithNoMatch()
        {
            PlayGoldRush();

            Assert.AreEqual(1, GoldRushRules.StealCounts());
        }

        [Test]
        public void StealCounts_IsTwo_InRushHour_UntilTheWavesEnd()
        {
            PlayGoldRush();
            OrderManager orders = OrdersAt(20f);
            Assert.AreEqual(2, GoldRushRules.StealCounts());

            orders.finalOrderActive = true;
            Assert.AreEqual(1, GoldRushRules.StealCounts(), "the golden round");
        }

        [Test]
        public void StealCounts_IsOne_BeforeRushHour_AndInOtherModes()
        {
            PlayGoldRush();
            OrdersAt(45f);
            Assert.AreEqual(1, GoldRushRules.StealCounts());

            HouseRules.Playing = HouseRules.Normal;
            typeof(OrderManager).GetField("gameTimer", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(OrderManager.Instance, 20f);
            Assert.AreEqual(1, GoldRushRules.StealCounts(), "Standard has no rush hour");
        }

        [TestCase(0f, 30f)]
        [TestCase(0.5f, 35f)]
        [TestCase(1f, 40f)]
        public void LodeWait_ThirtyToFortySeconds(float roll, float wait)
        {
            Assert.AreEqual(wait, GoldRushRules.LodeWait(roll), 0.001f);
        }

        [TestCase(1, 0, 3.9f, true)]
        [TestCase(1, 0, 4.1f, false)]
        [TestCase(0, 0, 1f, false)]
        public void TakesNugget_AnyoneNearButWhoSpilledIt(int seat, int spilledBy, float distance, bool takes)
        {
            Assert.AreEqual(takes, GoldRushRules.TakesNugget(seat, spilledBy, distance));
        }
    }
}

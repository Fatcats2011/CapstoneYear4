using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The match rules run only where GameAuthority says this machine decides them: always in a local match, on the host online
    /// </summary>
    public class AuthorityGateTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            Reflect.SetSingleton<TutorialManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
        }

        /// <summary>
        /// An order manager whose match has one wave and no scene orders
        /// </summary>
        OrderManager OrdersWithOneWave()
        {
            OrderManager orders = objects.Add<OrderManager>();
            Reflect.SetField(orders, "maxEasy", new[] { 1 });
            Reflect.SetField(orders, "maxMedium", new[] { 1 });
            Reflect.SetField(orders, "maxHard", new[] { 1 });
            Reflect.SetField(orders, "normalOrders", new List<Order>());
            return orders;
        }

        /// <summary>
        /// A player as the triggers see them: a root with the orders handler (and its scooter) and the ball as children
        /// </summary>
        OrderHandler NewPlayer(string name, out Collider ball)
        {
            GameObject player = objects.NewGameObject(name);
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            Reflect.SetField(handler, "ball", control.AddComponent<BallDriving>());
            GameObject sphere = new GameObject("Ball Of Fun");
            sphere.transform.SetParent(player.transform);
            ball = sphere.AddComponent<SphereCollider>();
            return handler;
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_InitWave_StartsTheWaveClock_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderManager orders = OrdersWithOneWave();

            orders.InitWave();

            Assert.AreEqual(decides ? 20f : 0f, orders.WaveTimer); // 20 s is the component's default wave length
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_InitGame_StartsTheGame_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderManager orders = OrdersWithOneWave();

            Reflect.Invoke(orders, "InitGame");

            Assert.AreEqual(decides, orders.GameStarted);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_InitTutorial_StartsTheTutorial_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            TutorialManager tutorial = objects.Add<TutorialManager>();
            Reflect.SetSingleton(tutorial);
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>()); // nobody has joined, so no tutorial orders are handed out
            tutorial.ShouldTutorialize = false;
            OrderManager orders = OrdersWithOneWave();

            Reflect.Invoke(orders, "InitTutorial");

            Assert.AreEqual(decides, tutorial.ShouldTutorialize);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_Update_GrowsTheHeldGoldenOrdersValue_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderManager orders = OrdersWithOneWave();
            Order golden = objects.Add<Order>();
            Reflect.SetField(golden, "playerHolding", objects.Add<OrderHandler>());
            Reflect.SetField(orders, "finalOrder", golden);
            Reflect.SetField(orders, "finalOrderActive", true);
            Reflect.SetField(orders, "goldTimer", 1f); // a second has passed since the last raise
            orders.FinalOrderValue = 50;

            Reflect.Invoke(orders, "Update");

            Assert.AreEqual(decides ? 51 : 50, orders.FinalOrderValue);
        }

        [Test]
        public void OrderBeacon_PlayerInTheLight_OnAnOnlineClient_NobodyPicksUpTheOrder()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler player = NewPlayer("Player 1", out Collider ball);
            Reflect.SetField(player, "canTakeOrder", true);
            OrderBeacon beacon = objects.Add<OrderBeacon>();
            Reflect.SetField(beacon, "order", objects.Add<Order>());
            Reflect.SetField(beacon, "canInteract", true);

            // Offline this is a pickup, which goes on to the order's scene objects that a test doesn't build
            Assert.DoesNotThrow(() => Reflect.Invoke(beacon, "OnTriggerStay", ball));
            Assert.IsFalse(player.HasOrder);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderHandler_TouchingAnotherPlayer_IsNoted_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderHandler me = NewPlayer("Player 1", out _);
            OrderHandler other = NewPlayer("Player 2", out Collider otherBall);

            Reflect.Invoke(me, "OnTriggerEnter", otherBall);

            Assert.AreEqual(decides ? me : null, other.PlayerTouching);
        }

        [TestCase(NetworkRole.Offline, 2)]
        [TestCase(NetworkRole.Host, 2)]
        [TestCase(NetworkRole.Client, 0)]
        public void OrderHandler_BoostingIntoABoostingPlayer_Clashes_OnlyWhereTheRulesAreDecided(NetworkRole role, int expectedClashes)
        {
            GameAuthority.Role = role;
            OrderHandler me = NewPlayer("Player 1", out _);
            OrderHandler other = NewPlayer("Player 2", out _);
            Reflect.SetField(other.GetComponent<BallDriving>(), "boosting", true);
            me.PlayerTouching = other;
            int clashes = 0;
            me.Clash += _ => clashes++;
            other.Clash += _ => clashes++;

            me.AttemptSteal();

            Assert.AreEqual(expectedClashes, clashes);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderHandler_AwardGoldenBonus_AddsWhatTheGoldenOrderEarned_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderHandler holder = NewPlayer("Player 1", out _);
            holder.Score = 200;

            holder.AwardGoldenBonus(80); // the golden order grew from 50 to 80 while it was held

            Assert.AreEqual(decides ? 230 : 200, holder.Score);
        }
    }
}

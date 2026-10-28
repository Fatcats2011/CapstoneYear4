using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The game tells the highlight reel who struck and who bumped whom, with both players' seats: a steal and a clash
    /// (once each), and an offline bump (once, though both balls feel it). Online the host reports them instead, so
    /// the offline bump watcher stays quiet
    /// </summary>
    public class HighlightEventsTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<(int attacker, int victim, bool steal, bool golden)> struck = new List<(int, int, bool, bool)>();
        readonly List<(int bumper, int victim, float strength)> bumped = new List<(int, int, float)>();
        float now;

        [SetUp]
        public void SetUp()
        {
            FeatSync.Reset();
            FeatSync.Struck += (attacker, victim, steal, golden) => struck.Add((attacker, victim, steal, golden));
            FeatSync.Bumped += (bumper, victim, strength) => bumped.Add((bumper, victim, strength));
            BumpWatcher.Rules = new BumpRules();
            now = 10f;
            BumpWatcher.Clock = () => now;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            FeatSync.Reset();
            GameAuthority.Role = NetworkRole.Offline;
            HouseRules.Playing = HouseRules.Normal;
            PlayerInstantiate.instance = null;
            BumpWatcher.Rules = new BumpRules();
            BumpWatcher.Clock = () => Time.time;
            struck.Clear();
            bumped.Clear();
            objects.DestroyAll();
        }

        // A player in the next free seat: a root with the orders handler and scooter in a child, and the ball with its collider beside
        (OrderHandler handler, GameObject ball) NewPlayer(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            GameObject sphere = new GameObject("Ball Of Fun");
            sphere.transform.SetParent(player.transform);
            sphere.AddComponent<SphereCollider>();
            return (handler, sphere);
        }

        static void SetBoosting(OrderHandler player, bool boosting)
        {
            typeof(BallDriving).GetField("boosting", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player.ball, boosting);
        }

        BumpWatcher Watch((OrderHandler handler, GameObject ball) player, int seat)
        {
            BumpWatcher watcher = player.ball.AddComponent<BumpWatcher>();
            watcher.Begin(player.handler.ball, seat);
            return watcher;
        }

        [Test]
        public void ReportSteal_RaisesStruckWithBothSeats()
        {
            var thief = NewPlayer("Player 1");
            var victim = NewPlayer("Player 2");

            thief.handler.ReportSteal(victim.handler, true);

            Assert.AreEqual(1, struck.Count);
            Assert.AreEqual((0, 1, true, true), struck[0]);
        }

        [Test]
        public void StealFrom_WithStealsOff_RaisesNoStruck()
        {
            HouseRules.Playing = new HouseRules(MatchLength.Normal, false, true);
            var thief = NewPlayer("Player 1");
            var victim = NewPlayer("Player 2");
            victim.handler.order1 = objects.Add<Order>();

            thief.handler.StealFrom(victim.handler);

            Assert.IsEmpty(struck);
        }

        [Test]
        public void OfflineClash_RaisesStruckOnce()
        {
            var attacker = NewPlayer("Player 1");
            var victim = NewPlayer("Player 2");
            SetBoosting(attacker.handler, true);
            SetBoosting(victim.handler, true);

            Reflect.Invoke(attacker.handler, "Hit", victim.handler);

            Assert.AreEqual(1, struck.Count);
            Assert.AreEqual((0, 1, false, false), struck[0]);
        }

        [Test]
        public void BumpWatcher_BothBallsReport_RaisesBumpedOnce()
        {
            var a = NewPlayer("Player 1");
            var b = NewPlayer("Player 2");
            BumpWatcher watchA = Watch(a, 0), watchB = Watch(b, 1);

            watchA.Hit(b.ball.GetComponent<Collider>(), 20f);
            now += 0.01f;
            watchB.Hit(a.ball.GetComponent<Collider>(), 20f);

            Assert.AreEqual(1, bumped.Count);
            Assert.AreEqual(0, bumped[0].bumper);
            Assert.AreEqual(1, bumped[0].victim);
            Assert.AreEqual(0.5f, bumped[0].strength, 0.001f);
        }

        [Test]
        public void BumpWatcher_BeyondMaxSpeed_ReportsFullStrength()
        {
            var a = NewPlayer("Player 1");
            var b = NewPlayer("Player 2");

            Watch(a, 0).Hit(b.ball.GetComponent<Collider>(), BumpRules.MAX_SPEED * 3f);

            Assert.AreEqual(1f, bumped[0].strength, 0.001f);
        }

        [Test]
        public void BumpWatcher_AfterTheCooldown_RaisesAgain()
        {
            var a = NewPlayer("Player 1");
            var b = NewPlayer("Player 2");
            BumpWatcher watchA = Watch(a, 0);

            watchA.Hit(b.ball.GetComponent<Collider>(), 20f);
            now += BumpRules.PAIR_COOLDOWN + 0.1f;
            watchA.Hit(b.ball.GetComponent<Collider>(), 20f);

            Assert.AreEqual(2, bumped.Count);
        }

        [Test]
        public void BumpWatcher_WhenEitherIsBoosting_RaisesNothing()
        {
            // Boosting into someone is a steal or a clash, not a bump
            var a = NewPlayer("Player 1");
            var b = NewPlayer("Player 2");
            BumpWatcher watchA = Watch(a, 0);

            SetBoosting(b.handler, true);
            watchA.Hit(b.ball.GetComponent<Collider>(), 20f);
            SetBoosting(b.handler, false);
            SetBoosting(a.handler, true);
            watchA.Hit(b.ball.GetComponent<Collider>(), 20f);

            Assert.IsEmpty(bumped);
        }

        [Test]
        public void BumpWatcher_Online_RaisesNothing()
        {
            // Online the host reports bumps (OnlineBumps)
            GameAuthority.Role = NetworkRole.Host;
            var a = NewPlayer("Player 1");
            var b = NewPlayer("Player 2");

            Watch(a, 0).Hit(b.ball.GetComponent<Collider>(), 20f);

            Assert.IsEmpty(bumped);
        }

        [Test]
        public void BumpWatcher_NotAPlayer_RaisesNothing()
        {
            var a = NewPlayer("Player 1");
            Collider wall = objects.Add<BoxCollider>();

            Watch(a, 0).Hit(wall, 20f);

            Assert.IsEmpty(bumped);
        }
    }
}

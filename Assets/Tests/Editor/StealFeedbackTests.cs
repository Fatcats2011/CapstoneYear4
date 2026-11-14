using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A bump is announced with what it did (StealFeedback), for the callouts: once per bump, never for an outcome this
    /// build doesn't know. A player judges their own bump: steals off, nothing to take, no room, or a steal
    /// </summary>
    public class StealFeedbackTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<BumpNotice> heard = new List<BumpNotice>();

        void Hear(BumpNotice notice)
        {
            heard.Add(notice);
        }

        [SetUp]
        public void SetUp()
        {
            StealFeedback.Happened += Hear;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            StealFeedback.Happened -= Hear;
            heard.Clear();
            GameAuthority.Role = NetworkRole.Offline;
            HouseRules.Playing = HouseRules.Normal;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        // A player in the next free seat, as HighlightEventsTests makes them
        OrderHandler NewPlayer(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            return handler;
        }

        [TestCase(BumpOutcome.Stolen, 7)]
        [TestCase(BumpOutcome.SlotsFull, 0)]
        [TestCase(BumpOutcome.NothingToSteal, 0)]
        [TestCase(BumpOutcome.StealsOff, 0)]
        [TestCase(BumpOutcome.ModeHit, 0)]
        public void Announce_IsHeardOnce_AsItWasSaid(BumpOutcome outcome, int paid)
        {
            StealFeedback.Announce(new BumpNotice(2, 3, outcome, paid));

            Assert.AreEqual(1, heard.Count);
            Assert.AreEqual(2, heard[0].Attacker);
            Assert.AreEqual(3, heard[0].Victim);
            Assert.AreEqual(outcome, heard[0].Outcome);
            Assert.AreEqual(paid, heard[0].Paid);
        }

        [Test]
        public void Announce_AnOutcomeThisBuildDoesntKnow_IsNeverHeard()
        {
            StealFeedback.Announce(new BumpNotice(0, 1, (BumpOutcome)200, 0));

            Assert.IsEmpty(heard);
        }

        [Test]
        public void JudgeBump_AVictimHoldingNothing_NothingToSteal()
        {
            OrderHandler thief = NewPlayer("Player 1");
            OrderHandler victim = NewPlayer("Player 2");

            Assert.AreEqual(BumpOutcome.NothingToSteal, thief.JudgeBump(victim, false));
        }

        [Test]
        public void JudgeBump_AThiefWithBothSlotsTaken_SlotsFull()
        {
            OrderHandler thief = NewPlayer("Player 1");
            OrderHandler victim = NewPlayer("Player 2");
            thief.order1 = objects.Add<Order>();
            thief.order2 = objects.Add<Order>();
            victim.order1 = objects.Add<Order>();

            Assert.AreEqual(BumpOutcome.SlotsFull, thief.JudgeBump(victim, false));
        }

        [Test]
        public void JudgeBump_RoomAndSomethingToTake_Stolen()
        {
            OrderHandler thief = NewPlayer("Player 1");
            OrderHandler victim = NewPlayer("Player 2");
            thief.order1 = objects.Add<Order>();
            victim.order2 = objects.Add<Order>();

            Assert.AreEqual(BumpOutcome.Stolen, thief.JudgeBump(victim, false));
        }

        [Test]
        public void JudgeBump_TheModeTookTheHit_ModeHit()
        {
            OrderHandler thief = NewPlayer("Player 1");
            OrderHandler victim = NewPlayer("Player 2");
            victim.order1 = objects.Add<Order>();

            Assert.AreEqual(BumpOutcome.ModeHit, thief.JudgeBump(victim, true));
        }

        [Test]
        public void StealFrom_WithStealsOff_SaysSoAndNamesBothSeats()
        {
            HouseRules.Playing = new HouseRules(MatchLength.Normal, false, true);
            OrderHandler thief = NewPlayer("Player 1");
            OrderHandler victim = NewPlayer("Player 2");
            Order held = objects.Add<Order>();
            victim.order1 = held;

            BumpNotice notice = thief.StealFrom(victim);

            Assert.AreEqual(BumpOutcome.StealsOff, notice.Outcome);
            Assert.AreEqual(0, notice.Attacker);
            Assert.AreEqual(1, notice.Victim);
            Assert.AreEqual(0, notice.Paid);
            Assert.AreSame(held, victim.order1, "nothing changes hands");
        }

        [Test]
        public void StealFrom_OnAMachineThatDoesntDecide_SaysNothing()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler thief = NewPlayer("Player 1");
            OrderHandler victim = NewPlayer("Player 2");
            victim.order1 = objects.Add<Order>();

            Assert.AreEqual(BumpOutcome.ModeHit, thief.StealFrom(victim).Outcome);
        }
    }
}

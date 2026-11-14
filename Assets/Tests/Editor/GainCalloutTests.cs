using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// What a player's callout says about their money (StealCallout): a gain for their seat only, gains close together
    /// as one line with the sum, a lead change for the two players it concerns, and a lead line that waits for a fresh
    /// gain line. Also what a delivery pays on this machine (OrderHandler.DeliveryPay). Edit mode: nothing starts, so the
    /// callouts hear gains and leads directly and a clock the tests move by hand
    /// </summary>
    public class GainCalloutTests
    {
        readonly TestObjects objects = new TestObjects();
        float now;

        [SetUp]
        public void SetUp()
        {
            now = 10f;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        float Now()
        {
            return now;
        }

        // A player in the next free seat with a HUD canvas of their own
        (OrderHandler handler, NumberHandler hud) NewPlayer(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            GameObject canvas = new GameObject("Driving Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(player.transform);
            return (handler, canvas.AddComponent<NumberHandler>());
        }

        StealCallout NewCallout(string name)
        {
            var player = NewPlayer(name);
            StealCallout callout = StealCallout.AddTo(player.handler, player.hud);
            callout.Clock = Now;
            return callout;
        }

        Order NewOrder(Constants.OrderValue tier)
        {
            Order order = objects.Add<Order>();
            order.value = tier;
            return order;
        }

        [Test]
        public void AGain_ShowsForThisSeat()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearGain(0, GainReason.Delivered, 65, "");

            Assert.AreEqual(Loc.Get("gain.delivered", 65, ""), callout.ShownWords);
        }

        [Test]
        public void AStolenGain_ShowsTheStamp()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearGain(0, GainReason.Delivered, 130, "×2");

            Assert.AreEqual(Loc.Get("gain.delivered_stolen", 130, "×2"), callout.ShownWords);
            StringAssert.Contains("×2", callout.ShownWords);
        }

        [Test]
        public void AGainForAnotherSeat_ShowsNothing()
        {
            NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");

            second.HearGain(0, GainReason.Delivered, 65, "");

            Assert.AreEqual("", second.ShownWords);
        }

        [Test]
        public void TwoGainsCloseTogether_AreOneLineWithTheSum()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += 0.3f;
            callout.HearGain(0, GainReason.Delivered, 80, "");

            Assert.AreEqual(Loc.Get("gain.delivered", 145, ""), callout.ShownWords);
        }

        [Test]
        public void TwoGainsOfDifferentReasons_MergeAsAPlainDelivery()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearGain(0, GainReason.Golden, 212, "");
            now += 0.3f;
            callout.HearGain(0, GainReason.Delivered, 130, "×2");

            Assert.AreEqual(Loc.Get("gain.delivered", 342, ""), callout.ShownWords);
        }

        [Test]
        public void TwoGainsFarApart_ShowTheSecondAlone()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += 0.6f;
            callout.HearGain(0, GainReason.Delivered, 80, "");

            Assert.AreEqual(Loc.Get("gain.delivered", 80, ""), callout.ShownWords);
        }

        [Test]
        public void ALeadChange_TellsTheNewLeaderAndTheOldOne_AndNobodyElse()
        {
            StealCallout first = NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");
            StealCallout third = NewCallout("Player 3");

            first.HearLead(0, 1);
            second.HearLead(0, 1);
            third.HearLead(0, 1);

            Assert.AreEqual(Loc.Get("lead.you"), first.ShownWords);
            Assert.AreEqual(Loc.Get("lead.other", StealFeedbackRules.Short(Scoreboard.ShownName(0))), second.ShownWords);
            Assert.AreEqual("", third.ShownWords);
        }

        [Test]
        public void ALeadHeardRightAfterAGain_WaitsForIt()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += 0.1f;
            callout.HearLead(0, 1);
            callout.Tick();

            Assert.AreEqual(Loc.Get("gain.delivered", 65, ""), callout.ShownWords, "the gain is not cut short");

            now += 0.8f;
            callout.Tick();

            Assert.AreEqual(Loc.Get("lead.you"), callout.ShownWords);
        }

        [Test]
        public void ALeadHeardWithNoLineUp_ShowsAtOnce()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearLead(0, 1);

            Assert.AreEqual(Loc.Get("lead.you"), callout.ShownWords);
        }

        [Test]
        public void ASteal_ShowsAtOnce_OverAGain()
        {
            StealCallout callout = NewCallout("Player 1");
            NewCallout("Player 2");

            callout.HearGain(0, GainReason.Delivered, 65, "");
            callout.Hear(new BumpNotice(0, 1, BumpOutcome.SlotsFull, 0));

            Assert.AreEqual(Loc.Get("steal.full"), callout.ShownWords);
        }

        [Test]
        public void DeliveryPay_IsTheWorthOfTheOrder()
        {
            Order stolen = NewOrder(Constants.OrderValue.Easy);
            stolen.CountSteal();

            Assert.AreEqual(110, OrderHandler.DeliveryPay(stolen));
            Assert.AreEqual(55, OrderHandler.DeliveryPay(NewOrder(Constants.OrderValue.Easy)));
        }

        [Test]
        public void DeliveryPay_ForTheGoldenOrderWithNoOrderManager_IsItsWorth()
        {
            Assert.AreEqual(50, OrderHandler.DeliveryPay(NewOrder(Constants.OrderValue.Golden)));
        }
    }
}

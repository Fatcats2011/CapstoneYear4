using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A first-time hint on a player's callout (StealCallout.HearHint): for its own seat only, taken (marked seen) when it
    /// is heard, shown once the line is free, never cutting an event line, replaced by a newer one, and up for HINT_SECONDS.
    /// Edit mode: nothing starts, so the callouts hear hints directly, a clock the tests move by hand and Fade stands in for Update
    /// </summary>
    public class HintCalloutTests
    {
        readonly TestObjects objects = new TestObjects();
        float now;

        static GameState InTutorial()
        {
            return GameState.Tutorial;
        }

        [SetUp]
        public void SetUp()
        {
            now = 10f;
            Hints.Reset();
            Hints.Seen = 0;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            Hints.Reset();
            Hints.Seen = 0;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        float Now()
        {
            return now;
        }

        TextMeshProUGUI lastLine;

        StealCallout NewCallout(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            GameObject canvas = new GameObject("Driving Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(player.transform);
            StealCallout callout = StealCallout.AddTo(handler, canvas.AddComponent<NumberHandler>());
            callout.Clock = Now;
            lastLine = canvas.transform.root.GetComponentInChildren<TextMeshProUGUI>(true);
            return callout;
        }

        [Test]
        public void AHintForAnotherSeat_ShowsNothing_AndIsNotMarkedSeen()
        {
            NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");

            second.HearHint(0, HintKind.BoostToSteal);

            Assert.AreEqual("", second.ShownWords);
            Assert.IsFalse(HintRules.Seen(Hints.Seen, HintKind.BoostToSteal));
        }

        [Test]
        public void AHintForThisSeat_ShowsItsWords_AndIsMarkedSeen()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearHint(0, HintKind.BoostToSteal);

            Assert.AreEqual(Loc.Get("hint.boost_to_steal"), callout.ShownWords);
            Assert.AreEqual(Color.white, lastLine.color, "the plain tone");
            Assert.IsTrue(HintRules.Seen(Hints.Seen, HintKind.BoostToSteal));
        }

        [Test]
        public void TheSameHintAgain_ShowsNothing()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearHint(0, HintKind.HoldGold);
            now += HintRules.HINT_SECONDS + 0.1f;
            callout.Fade(GameState.Begin);
            Assert.AreEqual("", callout.ShownWords);

            callout.HearHint(0, HintKind.HoldGold);

            Assert.AreEqual("", callout.ShownWords);
        }

        [Test]
        public void AHintHeardWhileAnEventLineIsFresh_WaitsForIt_ThenShows()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += 0.1f;

            callout.HearHint(0, HintKind.HoldGold);
            callout.Tick();

            Assert.AreEqual(Loc.Get("gain.delivered", 65, ""), callout.ShownWords, "the line is not cut short");
            Assert.IsFalse(HintRules.Seen(Hints.Seen, HintKind.HoldGold), "a hint that only waits isn't seen yet: it may never get its turn");

            now += GainFeedbackRules.GAIN_HOLD;
            callout.Tick();

            Assert.AreEqual(Loc.Get("hint.hold_gold"), callout.ShownWords);
            Assert.IsTrue(HintRules.Seen(Hints.Seen, HintKind.HoldGold), "seen as it shows");
        }

        [Test]
        public void AHintHeardWhileALeadLineWaits_ShowsAfterTheLeadLine()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += 0.1f;
            callout.HearLead(0, 1);
            callout.HearHint(0, HintKind.HoldGold);

            now += GainFeedbackRules.GAIN_HOLD;
            callout.Tick();
            Assert.AreEqual(Loc.Get("lead.you"), callout.ShownWords, "the lead first");

            callout.Tick();
            Assert.AreEqual(Loc.Get("lead.you"), callout.ShownWords, "the hint waits for the lead line");

            now += GainFeedbackRules.GAIN_HOLD;
            callout.Tick();
            Assert.AreEqual(Loc.Get("hint.hold_gold"), callout.ShownWords);
        }

        [Test]
        public void AHintHeardWithTheLineJustFinished_ShowsAtOnce()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += GainFeedbackRules.GAIN_HOLD + 0.1f;

            callout.HearHint(0, HintKind.BoostToBlock);

            Assert.AreEqual(Loc.Get("hint.boost_to_block"), callout.ShownWords);
        }

        [Test]
        public void ASecondHintWhileOneWaits_StaysUnseen_ForNextTime()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearGain(0, GainReason.Delivered, 65, "");

            callout.HearHint(0, HintKind.HoldGold);
            callout.HearHint(0, HintKind.BoostToSteal);

            now += GainFeedbackRules.GAIN_HOLD;
            callout.Tick();

            Assert.AreEqual(Loc.Get("hint.hold_gold"), callout.ShownWords, "the first one gets its turn");
            Assert.IsTrue(HintRules.Seen(Hints.Seen, HintKind.HoldGold));
            Assert.IsFalse(HintRules.Seen(Hints.Seen, HintKind.BoostToSteal), "not lost: it is offered again");
        }

        [Test]
        public void AnEventLine_ReplacesAHint_AtOnce()
        {
            StealCallout callout = NewCallout("Player 1");
            NewCallout("Player 2");
            callout.HearHint(0, HintKind.BoostToSteal);

            callout.Hear(new BumpNotice(0, 1, BumpOutcome.SlotsFull, 0));

            Assert.AreEqual(Loc.Get("steal.full"), callout.ShownWords);

            now += StealFeedbackRules.CALLOUT_SECONDS + 0.1f;
            callout.Fade(GameState.Begin);
            Assert.AreEqual("", callout.ShownWords, "the event line keeps its own timing");
        }

        [Test]
        public void AHint_StaysUpPastACalloutsSeconds_AndIsGoneAfterTheHintsSeconds()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearHint(0, HintKind.StolenPaysDouble);

            now += StealFeedbackRules.CALLOUT_SECONDS + 0.5f;
            callout.Fade(GameState.Begin);
            Assert.AreEqual(Loc.Get("hint.stolen_double"), callout.ShownWords);
            Assert.Greater(lastLine.alpha, 0f);

            now = 10f + HintRules.HINT_SECONDS + 0.1f;
            callout.Fade(GameState.Begin);
            Assert.AreEqual("", callout.ShownWords);
        }

        [Test]
        public void AnEventLine_StillEndsAtItsOwnSeconds_AfterAHint()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearHint(0, HintKind.HoldGold);
            now += HintRules.HINT_SECONDS + 0.1f;
            callout.Fade(GameState.Begin);

            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += StealFeedbackRules.CALLOUT_SECONDS + 0.1f;
            callout.Fade(GameState.Begin);

            Assert.AreEqual("", callout.ShownWords);
        }

        [Test]
        public void NoHint_IsHeard_InTheTutorial()
        {
            StealCallout callout = NewCallout("Player 1");
            Hints.State = InTutorial;
            staticTarget = callout;
            Hints.Offered += OfferedToCallout;

            Hints.Offer(HintKind.BoostToSteal, 0);

            Assert.AreEqual("", callout.ShownWords);
            Assert.IsFalse(HintRules.Seen(Hints.Seen, HintKind.BoostToSteal));
            staticTarget = null;
        }

        static StealCallout staticTarget;

        static void OfferedToCallout(int seat, HintKind kind)
        {
            staticTarget.HearHint(seat, kind);
        }

        [Test]
        public void AnOfferedHint_ReachesTheCallout_ThroughTheHub()
        {
            StealCallout callout = NewCallout("Player 1");
            staticTarget = callout;
            Hints.Offered += OfferedToCallout;

            Hints.Offer(HintKind.BoostToBlock, 0);

            Assert.AreEqual(Loc.Get("hint.boost_to_block"), callout.ShownWords);
            staticTarget = null;
        }
    }
}

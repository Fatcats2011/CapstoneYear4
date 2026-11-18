using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The respawn countdown on a player's callout (StealCallout.HearFell, HearRising, TickRespawn, HearBack): for its own
    /// seat only, "FELL IN THE RIVER" at once, then "BACK IN n" a new line each second once the rise begins, never 0, held
    /// while paused, yielding to an event line and resuming with the current number, ended and cleared when the respawn is
    /// over. Edit mode: nothing starts, so the tests call the callout directly with a clock they move by hand
    /// </summary>
    public class RespawnCalloutTests
    {
        readonly TestObjects objects = new TestObjects();
        float now;
        TextMeshProUGUI lastLine;

        [SetUp]
        public void SetUp()
        {
            now = 10f;
            RespawnFeedback.Reset();
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            RespawnFeedback.Reset();
            heard = "";
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        float Now()
        {
            return now;
        }

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

        // Time passes: the clock and the game's time move together, as Update feeds them
        void Advance(StealCallout callout, float seconds)
        {
            now += seconds;
            callout.TickRespawn(seconds);
        }

        static string Back(int n)
        {
            return Loc.Get("respawn.back", n);
        }

        [Test]
        public void AnotherSeatsFall_ShowsNothing()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearFell(1);

            Assert.AreEqual("", callout.ShownWords);
        }

        [Test]
        public void ThisSeatsFall_ShowsTheFellLine_AtOnce_InThePlainTone()
        {
            StealCallout callout = NewCallout("Player 1");

            callout.HearFell(0);

            Assert.AreEqual(Loc.Get("respawn.fell"), callout.ShownWords);
            Assert.AreEqual(Color.white, lastLine.color);
        }

        [Test]
        public void TheFellLine_ReplacesALineThatIsStillFresh()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearGain(0, GainReason.Delivered, 65, "");

            callout.HearFell(0);

            Assert.AreEqual(Loc.Get("respawn.fell"), callout.ShownWords);
        }

        [Test]
        public void TheCount_WaitsForTheFellLinesHold_ThenShowsTheNumberOfSecondsLeft()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);
            callout.HearRising(0, 7f);

            Advance(callout, 0.1f);
            Assert.AreEqual(Loc.Get("respawn.fell"), callout.ShownWords, "the fell line has its moment");

            Advance(callout, GainFeedbackRules.GAIN_HOLD);
            Assert.AreEqual(Back(7), callout.ShownWords);
            Assert.AreEqual("BACK IN 7", callout.ShownWords);
            Assert.AreEqual(Color.white, lastLine.color, "the plain tone");
        }

        [Test]
        public void TheCount_ShowsANewNumberEachSecond_AndNotTheSameTwice()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);
            callout.HearRising(0, 7f);
            Advance(callout, 0.1f + GainFeedbackRules.GAIN_HOLD);
            Assert.AreEqual(Back(7), callout.ShownWords);

            Advance(callout, 0.05f);
            Assert.AreEqual(Back(7), callout.ShownWords, "still 7: not shown again");
            Assert.AreEqual(7, callout.RespawnNumber);

            Advance(callout, 1f);
            Assert.AreEqual(Back(6), callout.ShownWords);
            Advance(callout, 1f);
            Assert.AreEqual(Back(5), callout.ShownWords);
        }

        [Test]
        public void AnEventLine_IsNotCutShort_ThenTheCountResumesAtTheRightNumber()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, 7f);
            Advance(callout, GainFeedbackRules.GAIN_HOLD);
            Advance(callout, 0.3f);
            Assert.AreEqual(Back(6), callout.ShownWords);

            callout.HearGain(0, GainReason.Delivered, 65, "");
            Advance(callout, 0.4f);

            Assert.AreEqual(Loc.Get("gain.delivered", 65, ""), callout.ShownWords, "the event line is not cut short");

            Advance(callout, 0.55f);

            Assert.AreEqual(Back(5), callout.ShownWords, "4.95 seconds left");
        }

        [Test]
        public void AfterAnEventLine_TheNumberIsShownAgain_EvenIfItWasShownBefore()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, 7f);
            callout.TickRespawn(0f);
            Assert.AreEqual(Back(7), callout.ShownWords);

            callout.HearGain(0, GainReason.Delivered, 65, "");
            Advance(callout, 0.1f);
            Advance(callout, GainFeedbackRules.GAIN_HOLD);

            Assert.AreEqual(Back(7), callout.ShownWords, "6.1 seconds left: 7 again");
        }

        [Test]
        public void ALeadLineWaiting_ShowsBeforeTheCountResumes()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearGain(0, GainReason.Delivered, 65, "");
            now += 0.1f;
            callout.HearLead(0, 1);
            callout.HearRising(0, 7f);

            now += GainFeedbackRules.GAIN_HOLD;
            callout.Tick();
            callout.TickRespawn(GainFeedbackRules.GAIN_HOLD + 0.1f);

            Assert.AreEqual(Loc.Get("lead.you"), callout.ShownWords, "the lead line shows; the count waits for its moment");

            Advance(callout, 0.9f);

            Assert.AreEqual(Back(6), callout.ShownWords);
        }

        [Test]
        public void WithTwoTenthsLeft_ItShowsOne_ThenNothingNewAfterItRunsOut()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, 3.2f);
            callout.TickRespawn(3f);
            Assert.AreEqual(Back(1), callout.ShownWords);

            Advance(callout, 0.25f);
            Advance(callout, 1f);
            Advance(callout, 1f);

            callout.Fade(GameState.Begin);
            Assert.AreEqual("", callout.ShownWords, "the one fades with its time; no new line follows");
        }

        [Test]
        public void AShortWait_ShowsNoNumbers()
        {
            // The game's respawn takes about 2 seconds (the player prefab's timings): too short to count down
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);
            callout.HearRising(0, DrivingFeedbackRules.MIN_COUNTED_WAIT - 0.1f);

            for (int i = 0; i < 40; i++)
            {
                Advance(callout, 0.1f);
                Assert.AreEqual(0, callout.RespawnNumber);
            }
        }

        [Test]
        public void AWaitJustLongEnough_IsCounted()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, DrivingFeedbackRules.MIN_COUNTED_WAIT);
            callout.TickRespawn(0f);

            Assert.AreEqual(Back(3), callout.ShownWords);
        }

        [Test]
        public void ANumberIsNeverZeroOrNegative()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, 3.5f);

            for (int i = 0; i < 60; i++)
            {
                Advance(callout, 0.1f);
                Assert.AreNotEqual(Loc.Get("respawn.back", 0), callout.ShownWords);
                Assert.AreNotEqual(Loc.Get("respawn.back", -1), callout.ShownWords);
            }
        }

        [Test]
        public void APausedGame_ChangesNothing()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);
            callout.HearRising(0, 7f);
            Advance(callout, 0.1f + GainFeedbackRules.GAIN_HOLD);
            Assert.AreEqual(Back(7), callout.ShownWords);

            for (int i = 0; i < 5; i++)
                callout.TickRespawn(0f);

            Assert.AreEqual(Back(7), callout.ShownWords);

            Advance(callout, 0.15f);
            Assert.AreEqual(Back(6), callout.ShownWords, "the count held where it was");
        }

        [Test]
        public void TickWithNoCount_ShowsNothing()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);

            Advance(callout, 5f);
            callout.Fade(GameState.Begin);

            Assert.AreEqual("", callout.ShownWords, "fell, but not rising yet: no number");
        }

        [Test]
        public void ThisSeatsBack_EndsTheCount_AndClearsTheCountdownLine()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);
            callout.HearRising(0, 7f);
            Advance(callout, 0.1f + GainFeedbackRules.GAIN_HOLD);
            Assert.AreEqual(Back(7), callout.ShownWords);

            callout.HearBack(0);

            Assert.AreEqual("", callout.ShownWords, "it doesn't linger once they drive");
            Advance(callout, 1f);
            Advance(callout, 1f);
            Assert.AreEqual("", callout.ShownWords, "no further number");
        }

        [Test]
        public void Back_ClearsTheFellLine_WhenTheRespawnIsStoppedEarly()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearFell(0);

            callout.HearBack(0);

            Assert.AreEqual("", callout.ShownWords);
        }

        [Test]
        public void Back_LeavesAnEventLineUp()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, 7f);
            callout.HearGain(0, GainReason.Delivered, 65, "");

            callout.HearBack(0);

            Assert.AreEqual(Loc.Get("gain.delivered", 65, ""), callout.ShownWords);
        }

        [Test]
        public void AnotherSeatsRisingAndBack_ChangeNothing()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(1, 7f);
            callout.TickRespawn(0f);
            Assert.AreEqual("", callout.ShownWords, "another seat's count is not this player's");

            callout.HearRising(0, 7f);
            callout.TickRespawn(0f);
            callout.HearBack(1);

            Assert.AreEqual(Back(7), callout.ShownWords);
        }

        [Test]
        public void TheEndOfTheMatch_EndsTheCount()
        {
            StealCallout callout = NewCallout("Player 1");
            callout.HearRising(0, 7f);
            callout.TickRespawn(0f);
            Assert.AreEqual(Back(7), callout.ShownWords);

            callout.EndMatch();
            callout.Fade(GameState.Results);
            Advance(callout, 1f);

            Assert.AreEqual("", callout.ShownWords);
        }

        static string heard = "";

        static void HeardFell(int seat)
        {
            heard += "fell" + seat + ";";
        }

        static void HeardRising(int seat, float left)
        {
            heard += "rising" + seat + "," + left + ";";
        }

        static void HeardBack(int seat)
        {
            heard += "back" + seat + ";";
        }

        [Test]
        public void TheHub_TellsEverySubscriber_AndResetForgetsThem()
        {
            RespawnFeedback.Fell += HeardFell;
            RespawnFeedback.Rising += HeardRising;
            RespawnFeedback.Back += HeardBack;

            RespawnFeedback.AnnounceFell(2);
            RespawnFeedback.AnnounceRising(2, 7f);
            RespawnFeedback.AnnounceBack(2);

            Assert.AreEqual("fell2;rising2,7;back2;", heard);

            RespawnFeedback.Reset();
            heard = "";
            RespawnFeedback.AnnounceFell(2);
            RespawnFeedback.AnnounceRising(2, 7f);
            RespawnFeedback.AnnounceBack(2);

            Assert.AreEqual("", heard);
        }

        [Test]
        public void TheHub_WithNoSubscriber_DoesNothing()
        {
            Assert.DoesNotThrow(() => RespawnFeedback.AnnounceFell(0));
            Assert.DoesNotThrow(() => RespawnFeedback.AnnounceRising(0, 7f));
            Assert.DoesNotThrow(() => RespawnFeedback.AnnounceBack(0));
        }

        [TestCase("respawn.fell")]
        [TestCase("respawn.back")]
        public void EveryLine_IsInEveryLanguage_InUpperCase(string key)
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
            {
                string text = csv.Get(key, locale);
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), key + " " + locale);
                Assert.AreEqual(text.ToUpperInvariant(), text, key + " " + locale + " is upper case");
            }
        }

        [Test]
        public void TheFellLine_FitsTheCallout_InEveryLanguage()
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
                Assert.LessOrEqual(csv.Get("respawn.fell", locale).Length, 24, locale);
        }

        [Test]
        public void TheBackLine_TakesTheNumber_AndFitsTheCallout_InEveryLanguage()
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
            {
                string text = csv.Get("respawn.back", locale);
                int at = text.IndexOf("{0}");
                Assert.GreaterOrEqual(at, 0, locale + " has the number");
                Assert.LessOrEqual(at, 22, locale);
                Assert.LessOrEqual(string.Format(text, 7).Length, 24, locale + " with a one-digit number");
            }
        }

        [Test]
        public void TheLines_InEnglish()
        {
            StringsCsv csv = StringsCsv.Load();

            Assert.AreEqual("FELL IN THE RIVER", csv.Get("respawn.fell", "en"));
            Assert.AreEqual("BACK IN 6", string.Format(csv.Get("respawn.back", "en"), 6));
        }
    }
}

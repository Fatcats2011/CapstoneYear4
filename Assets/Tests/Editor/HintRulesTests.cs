using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// First-time hints (HintRules, Hints, HintWatcher): which hint is which, which states show them, the seen mask, what
    /// is offered to whom, the saved line in settings.cfg, and the five lines in every language
    /// </summary>
    public class HintRulesTests
    {
        static int offers;
        static int offeredSeat;
        static HintKind offeredKind;
        static int changes;
        static int changedMask;

        static void OnOffered(int seat, HintKind kind)
        {
            offers++;
            offeredSeat = seat;
            offeredKind = kind;
        }

        static void OnChanged(int mask)
        {
            changes++;
            changedMask = mask;
        }

        static GameState InTutorial()
        {
            return GameState.Tutorial;
        }

        [SetUp]
        public void SetUp()
        {
            Hints.Reset();
            Hints.Seen = 0;
            offers = 0;
            changes = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Hints.Reset();
            Hints.Seen = 0;
        }

        [TestCase(HintKind.BoostToSteal, "hint.boost_to_steal")]
        [TestCase(HintKind.BoostToBlock, "hint.boost_to_block")]
        [TestCase(HintKind.StolenPaysDouble, "hint.stolen_double")]
        [TestCase(HintKind.HoldGold, "hint.hold_gold")]
        [TestCase(HintKind.BoostReady, "hint.boost_ready")]
        public void EachHint_HasItsKey(HintKind kind, string key)
        {
            Assert.AreEqual(key, HintRules.Key(kind));
        }

        [Test]
        public void AHint_StaysUpFourSeconds()
        {
            Assert.AreEqual(4f, HintRules.HINT_SECONDS);
        }

        [Test]
        public void HintsShow_InEveryStateButTheTutorial()
        {
            foreach (GameState state in System.Enum.GetValues(typeof(GameState)))
                Assert.AreEqual(state != GameState.Tutorial, HintRules.ShowsIn(state), state.ToString());
        }

        [Test]
        public void AMarkedHint_IsSeen_AndTheOthersAreNot()
        {
            int mask = HintRules.Mark(0, HintKind.BoostToBlock);

            Assert.IsTrue(HintRules.Seen(mask, HintKind.BoostToBlock));
            Assert.IsFalse(HintRules.Seen(mask, HintKind.BoostToSteal));
            Assert.IsFalse(HintRules.Seen(mask, HintKind.StolenPaysDouble));
            Assert.IsFalse(HintRules.Seen(mask, HintKind.HoldGold));
            Assert.AreEqual(mask, HintRules.Mark(mask, HintKind.BoostToBlock), "marking again changes nothing");
        }

        [Test]
        public void TheFade_RisesHoldsAndFallsOverTheHintsSeconds()
        {
            Assert.AreEqual(0f, HintRules.Alpha(-1f));
            Assert.AreEqual(0f, HintRules.Alpha(0f), 0.001f);
            Assert.AreEqual(1f, HintRules.Alpha(2f), 0.001f);
            Assert.Greater(HintRules.Alpha(3.9f), 0f);
            Assert.AreEqual(0f, HintRules.Alpha(HintRules.HINT_SECONDS));
            Assert.AreEqual(0f, HintRules.Alpha(9f));
        }

        [Test]
        public void Take_IsTrueOnce_AndRaisesSeenChangedOnce()
        {
            Hints.SeenChanged += OnChanged;

            Assert.IsTrue(Hints.Take(HintKind.HoldGold));
            Assert.IsFalse(Hints.Take(HintKind.HoldGold));

            Assert.AreEqual(1, changes);
            Assert.AreEqual(HintRules.Mark(0, HintKind.HoldGold), changedMask);
            Assert.IsTrue(HintRules.Seen(Hints.Seen, HintKind.HoldGold));
        }

        [Test]
        public void SettingSeen_RaisesNothing()
        {
            Hints.SeenChanged += OnChanged;

            Hints.Seen = 5;

            Assert.AreEqual(0, changes);
            Assert.AreEqual(5, Hints.Seen);
        }

        [Test]
        public void Offer_RaisesOffered_ForAnUnseenHint()
        {
            Hints.Offered += OnOffered;

            Hints.Offer(HintKind.BoostToSteal, 2);

            Assert.AreEqual(1, offers);
            Assert.AreEqual(2, offeredSeat);
            Assert.AreEqual(HintKind.BoostToSteal, offeredKind);
        }

        [Test]
        public void Offer_RaisesNothing_ForASeenHint_ASeatBelowZero_OrTheTutorial()
        {
            Hints.Offered += OnOffered;

            Hints.Seen = HintRules.Mark(0, HintKind.BoostToSteal);
            Hints.Offer(HintKind.BoostToSteal, 0);
            Hints.Offer(HintKind.BoostToBlock, -1);
            Hints.State = InTutorial;
            Hints.Offer(HintKind.HoldGold, 0);

            Assert.AreEqual(0, offers);
        }

        [Test]
        public void ABumpWithoutBoosting_OffersBoostToSteal_ToTheBumper()
        {
            Hints.Offered += OnOffered;

            HintWatcher.OnBumped(1, 0, 0.5f);

            Assert.AreEqual(1, offers);
            Assert.AreEqual(1, offeredSeat);
            Assert.AreEqual(HintKind.BoostToSteal, offeredKind);
        }

        [Test]
        public void ASteal_OffersBoostToBlock_ToTheVictim_AndTheDoubleToTheThief()
        {
            Hints.Offered += OnOffered;

            HintWatcher.OnNotice(new BumpNotice(2, 3, BumpOutcome.Stolen, 7));

            Assert.AreEqual(2, offers);
            Assert.AreEqual(2, offeredSeat, "the last offer is the thief's");
            Assert.AreEqual(HintKind.StolenPaysDouble, offeredKind);
        }

        [Test]
        public void ABumpThatTookNothing_OffersNothing()
        {
            Hints.Offered += OnOffered;

            HintWatcher.OnNotice(new BumpNotice(0, 1, BumpOutcome.SlotsFull, 0));
            HintWatcher.OnNotice(new BumpNotice(0, 1, BumpOutcome.ModeHit, 0));

            Assert.AreEqual(0, offers);
        }

        [Test]
        public void TheSavedLine_ReplacesTheOldOne_AndKeepsTheRest()
        {
            string text = GameSettings.WithHints("bgm=3\nhints=1\nsfx=4\n", 5);

            Assert.AreEqual("bgm=3\nsfx=4\nhints=5\n", text);
        }

        [Test]
        public void TheSavedLine_IsAddedToAFileWithout_OrToNoFile()
        {
            Assert.AreEqual("bgm=3\nhints=2\n", GameSettings.WithHints("bgm=3\n", 2));
            Assert.AreEqual("hints=2\n", GameSettings.WithHints(null, 2));
        }

        [Test]
        public void TheMask_SurvivesTheSettingsFile()
        {
            GameSettings saved = new GameSettings(3, 4, 0);
            saved.hintsSeen = 6;
            GameSettings defaults = new GameSettings(3, 4, 0);

            Assert.AreEqual(6, GameSettings.Parse(saved.ToText(), defaults).hintsSeen);
            Assert.AreEqual(9, GameSettings.Parse(GameSettings.WithHints(saved.ToText(), 9), defaults).hintsSeen);
            Assert.AreEqual(0, GameSettings.Parse("hints=-4\n", defaults).hintsSeen, "a damaged value is ignored");
        }

        [TestCase("hint.boost_to_steal")]
        [TestCase("hint.boost_to_block")]
        [TestCase("hint.stolen_double")]
        [TestCase("hint.hold_gold")]
        [TestCase("hint.boost_ready")]
        public void EveryHint_IsInEveryLanguage_InCapitals_AndFitsTheLine(string key)
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
            {
                string words = csv.Get(key, locale);
                Assert.IsNotNull(words, key + " " + locale);
                Assert.LessOrEqual(words.Length, 30, key + " " + locale);
                Assert.AreEqual(words.ToUpperInvariant(), words, key + " " + locale);
            }
        }

        [Test]
        public void TheHints_InEnglish()
        {
            StringsCsv csv = StringsCsv.Load();

            Assert.AreEqual("BOOST INTO RIVALS TO STEAL", csv.Get("hint.boost_to_steal", "en"));
            Assert.AreEqual("BOOST TO BLOCK A THIEF", csv.Get("hint.boost_to_block", "en"));
            Assert.AreEqual("STOLEN ORDERS PAY DOUBLE", csv.Get("hint.stolen_double", "en"));
            Assert.AreEqual("HOLD GOLD LONGER, EARN MORE", csv.Get("hint.hold_gold", "en"));
            Assert.AreEqual("RING FULL: BOOST!", csv.Get("hint.boost_ready", "en"));
        }
    }
}

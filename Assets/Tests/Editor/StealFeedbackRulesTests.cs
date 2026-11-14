using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What a bump did and what each player is told: the outcome (a mode's hit, steals off, nothing to take, no room, a
    /// steal), the line each side reads, a name cut short, the stamp on a stolen package, and how a callout fades
    /// </summary>
    public class StealFeedbackRulesTests
    {
        [Test]
        public void Outcome_ChecksTheModeThenTheRuleThenTheVictimThenTheRoom()
        {
            Assert.AreEqual(BumpOutcome.ModeHit, StealFeedbackRules.Outcome(true, true, true, true));
            Assert.AreEqual(BumpOutcome.StealsOff, StealFeedbackRules.Outcome(false, false, true, true));
            Assert.AreEqual(BumpOutcome.NothingToSteal, StealFeedbackRules.Outcome(false, true, false, false));
            Assert.AreEqual(BumpOutcome.SlotsFull, StealFeedbackRules.Outcome(false, true, true, false));
            Assert.AreEqual(BumpOutcome.Stolen, StealFeedbackRules.Outcome(false, true, true, true));
        }

        [Test]
        public void TheThief_ReadsWhatTheyWerePaid()
        {
            Assert.AreEqual("steal.stole_paid", StealFeedbackRules.AttackerKey(BumpOutcome.Stolen, 14, out CalloutTone tone));
            Assert.AreEqual(CalloutTone.Thief, tone);
        }

        [Test]
        public void TheThief_AlreadyPaidForTheOrder_ReadsNoMoney()
        {
            Assert.AreEqual("steal.stole", StealFeedbackRules.AttackerKey(BumpOutcome.Stolen, 0, out CalloutTone tone));
            Assert.AreEqual(CalloutTone.Thief, tone);
        }

        [TestCase(BumpOutcome.SlotsFull, "steal.full")]
        [TestCase(BumpOutcome.NothingToSteal, "steal.nothing")]
        [TestCase(BumpOutcome.StealsOff, "steal.off")]
        public void ABumpThatTookNothing_SaysWhy(BumpOutcome outcome, string key)
        {
            Assert.AreEqual(key, StealFeedbackRules.AttackerKey(outcome, 0, out CalloutTone tone));
            Assert.AreEqual(CalloutTone.Plain, tone);
        }

        [Test]
        public void AModesHit_SaysNothing()
        {
            Assert.IsNull(StealFeedbackRules.AttackerKey(BumpOutcome.ModeHit, 0, out CalloutTone tone));
            Assert.AreEqual(CalloutTone.None, tone);
        }

        [Test]
        public void TheVictim_IsToldOnlyOfASteal()
        {
            Assert.AreEqual("steal.robbed", StealFeedbackRules.VictimKey(BumpOutcome.Stolen, out CalloutTone tone));
            Assert.AreEqual(CalloutTone.Victim, tone);

            foreach (BumpOutcome other in new[] { BumpOutcome.SlotsFull, BumpOutcome.NothingToSteal, BumpOutcome.StealsOff, BumpOutcome.ModeHit })
            {
                Assert.IsNull(StealFeedbackRules.VictimKey(other, out tone), other.ToString());
                Assert.AreEqual(CalloutTone.None, tone, other.ToString());
            }
        }

        [Test]
        public void AnOutcomeThisBuildDoesntKnow_SaysNothing()
        {
            Assert.IsFalse(StealFeedbackRules.Known(200));
            Assert.IsTrue(StealFeedbackRules.Known((byte)BumpOutcome.StealsOff));
            Assert.IsNull(StealFeedbackRules.AttackerKey((BumpOutcome)200, 7, out CalloutTone tone));
            Assert.AreEqual(CalloutTone.None, tone);
            Assert.IsNull(StealFeedbackRules.VictimKey((BumpOutcome)200, out tone));
            Assert.AreEqual(CalloutTone.None, tone);
        }

        [Test]
        public void ALongName_IsCutShort()
        {
            Assert.AreEqual("Ryan", StealFeedbackRules.Short("Ryan"));
            Assert.AreEqual("ABCDEFGHIJKL…", StealFeedbackRules.Short("ABCDEFGHIJKLMNOP"));
            Assert.AreEqual("ABCDEFGHIJKL", StealFeedbackRules.Short("ABCDEFGHIJKL"));
            Assert.AreEqual("", StealFeedbackRules.Short(null));
            Assert.AreEqual("", StealFeedbackRules.Short(""));
        }

        [Test]
        public void TheStamp_DoublesWithEachSteal()
        {
            Assert.AreEqual("", StealFeedbackRules.StampText(Constants.OrderValue.Hard, false, 0));
            Assert.AreEqual("×2", StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 1));
            Assert.AreEqual("×4", StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 2));
            Assert.AreEqual("×64", StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 6));
            Assert.AreEqual("", StealFeedbackRules.StampText(Constants.OrderValue.Hard, false, -1));
        }

        [Test]
        public void TheStamp_StopsGrowingAtSixtyFour()
        {
            Assert.AreEqual("×64+", StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 7));
            Assert.AreEqual("×64+", StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 16));
        }

        [Test]
        public void TheGoldenOrderAndTheMotherLode_HaveNoStamp()
        {
            Assert.AreEqual("", StealFeedbackRules.StampText(Constants.OrderValue.Golden, false, 3));
            Assert.AreEqual("", StealFeedbackRules.StampText(Constants.OrderValue.Hard, true, 3));
        }

        [Test]
        public void ACallout_FadesInHoldsAndFadesOut()
        {
            Assert.AreEqual(0f, StealFeedbackRules.CalloutAlpha(-0.1f));
            Assert.AreEqual(0f, StealFeedbackRules.CalloutAlpha(1.5f));
            Assert.AreEqual(1f, StealFeedbackRules.CalloutAlpha(0.75f));
            Assert.AreEqual(0.5f, StealFeedbackRules.CalloutAlpha(0.075f), 0.01f);
            Assert.AreEqual(0.5f, StealFeedbackRules.CalloutAlpha(1.425f), 0.01f);
        }
    }
}

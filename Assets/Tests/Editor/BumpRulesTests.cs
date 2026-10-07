using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Bumps between machines' scooters online (BumpRules): how hard a bump pushes, which bumps the host lets through,
    /// and how often a pair can bump. See docs/online.md
    /// </summary>
    public class BumpRulesTests
    {
        const float CLASH = 30f; // a scooter's clash force

        [Test]
        public void Push_ScalesWithSpeed_CappedAtTheClash()
        {
            Assert.AreEqual(CLASH / 4f, BumpRules.Push(BumpRules.MAX_SPEED / 4f, CLASH), 1e-3f, "a quarter of the hardest bump: a quarter of a clash");
            Assert.AreEqual(CLASH, BumpRules.Push(BumpRules.MAX_SPEED, CLASH), 1e-3f, "the hardest bump: a clash");
            Assert.AreEqual(CLASH, BumpRules.Push(1000f, CLASH), 1e-3f, "never more than a clash, whatever a game sends");
        }

        [Test]
        public void Push_NanOrNegative_IsZero()
        {
            Assert.AreEqual(0f, BumpRules.Push(float.NaN, CLASH));
            Assert.AreEqual(0f, BumpRules.Push(float.PositiveInfinity, CLASH));
            Assert.AreEqual(0f, BumpRules.Push(-5f, CLASH));
        }

        [Test]
        public void Counts_OnlyTwoPlayersHereWithinReach_NeitherRespawning()
        {
            Assert.IsTrue(BumpRules.Counts(1, 0, 3f, false));
            Assert.IsFalse(BumpRules.Counts(1, 1, 3f, false), "a player can't bump themselves");
            Assert.IsFalse(BumpRules.Counts(1, 4, 3f, false), "not a seat");
            Assert.IsFalse(BumpRules.Counts(1, 0, BumpRules.REACH + 1f, false), "too far apart on the host to have touched");
            Assert.IsFalse(BumpRules.Counts(1, 0, 20f, false), "20 m apart: a modified game can't push a player it isn't touching");
            Assert.IsFalse(BumpRules.Counts(1, 0, 3f, true), "one of them is respawning");
        }

        [Test]
        public void Credible_AClosingSpeedNoFasterThanTheTwoAreGoing()
        {
            Assert.AreEqual(10f, BumpRules.Credible(10f, 8f, 4f), 1e-4f, "12 m/s between them on the host: 10 is believable");
            Assert.AreEqual(5f + BumpRules.SPEED_SLACK, BumpRules.Credible(1e9f, 5f, 0f), 1e-4f, "a modified game's huge speed counts as what the host sees");
            Assert.AreEqual(BumpRules.SPEED_SLACK, BumpRules.Credible(30f, 0f, 0f), 1e-4f, "both standing still: only the slack for lag");
            Assert.AreEqual(0f, BumpRules.Credible(float.NaN, 5f, 5f), "not a number: no bump (Mathf.Min would make it one)");
        }

        [Test]
        public void TooSoon_APairWithinHalfASecond_EitherWayRound()
        {
            BumpRules rules = new BumpRules();
            Assert.IsFalse(rules.TooSoon(1, 0, 10f));
            Assert.IsTrue(rules.TooSoon(0, 1, 10.2f), "the same pair, the other way round");
            Assert.IsFalse(rules.TooSoon(2, 0, 10.2f), "another pair");
            Assert.IsFalse(rules.TooSoon(1, 0, 10f + BumpRules.PAIR_COOLDOWN), "half a second later");
        }
    }
}

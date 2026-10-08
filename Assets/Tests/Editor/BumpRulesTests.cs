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
            Assert.IsFalse(rules.TooSoon(1, 0, 10f, 5f));
            Assert.IsTrue(rules.TooSoon(0, 1, 10.2f, 5f), "the same pair, the other way round");
            Assert.IsFalse(rules.TooSoon(2, 0, 10.2f, 5f), "another pair");
            Assert.IsFalse(rules.TooSoon(1, 0, 10f + BumpRules.PAIR_COOLDOWN, 5f), "half a second later");
        }

        [Test]
        public void TooSoon_AHarderBumpWithinTheCooldown_GoesThrough()
        {
            // A parked player's machine reports a weak bump as a scooter rams it; the rammer's real one follows
            BumpRules rules = new BumpRules();
            Assert.IsFalse(rules.TooSoon(1, 0, 0f, 3f));
            Assert.IsFalse(rules.TooSoon(0, 1, 0.1f, 10f));
        }

        [Test]
        public void TooSoon_RisingReportsWithinTheCooldown_OnlyOneHarderGoesThrough()
        {
            // A modified game sending ever so slightly harder bumps can't add pushes up past a clash
            BumpRules rules = new BumpRules();
            int through = 0;
            for (int i = 0; i < 20; i++)
            {
                if (!rules.TooSoon(1, 0, i * 0.02f, 1f + i * 0.01f))
                    through++;
            }

            Assert.AreEqual(2, through, "the first, and one harder");
        }

        [Test]
        public void TooSoon_AHarderOne_DoesntRestartTheCooldown()
        {
            BumpRules rules = new BumpRules();
            Assert.IsFalse(rules.TooSoon(1, 0, 0f, 3f));
            Assert.IsFalse(rules.TooSoon(1, 0, 0.4f, 10f), "harder");
            Assert.IsFalse(rules.TooSoon(1, 0, 0.55f, 1f), "half a second after the first: a new window");
        }

        [Test]
        public void TooSoon_AnEqualOrWeakerOneWithinTheCooldown_IsTooSoon()
        {
            BumpRules rules = new BumpRules();
            Assert.IsFalse(rules.TooSoon(1, 0, 0f, 10f));
            Assert.IsTrue(rules.TooSoon(1, 0, 0.1f, 10f), "as hard");
            Assert.IsTrue(rules.TooSoon(0, 1, 0.2f, 3f), "weaker");
        }

        [Test]
        public void TooSoon_AfterTheCooldown_AnyBumpGoesThrough()
        {
            BumpRules rules = new BumpRules();
            Assert.IsFalse(rules.TooSoon(1, 0, 0f, 10f));
            Assert.IsFalse(rules.TooSoon(1, 0, 0.6f, 1f));
        }

        [Test]
        public void Shown_NeverMoreThanAClash()
        {
            // A modified host could send any push
            Assert.AreEqual(4000f, BumpRules.Shown(1e9f, 4000f));
            Assert.AreEqual(0f, BumpRules.Shown(-5f, 4000f));
            Assert.AreEqual(0f, BumpRules.Shown(float.NaN, 4000f));
            Assert.AreEqual(100f, BumpRules.Shown(100f, 4000f));
        }
    }
}

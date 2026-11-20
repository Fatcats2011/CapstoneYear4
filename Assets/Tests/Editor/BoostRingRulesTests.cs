using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>The boost-charge ring under a scooter (BoostRingRules): fill, slowed recharge, full pulse, the online prompt, the idle hint and the burst</summary>
    public class BoostRingRulesTests
    {
        [Test]
        public void Fill_HalfIsHalf()
        {
            Assert.AreEqual(0.5f, BoostRingRules.Fill(1.5f, 3f), 0.0001f);
        }

        [Test]
        public void Fill_ClampsAboveOne()
        {
            Assert.AreEqual(1f, BoostRingRules.Fill(9f, 3f), 0.0001f);
        }

        [Test]
        public void Fill_TreatsZeroMaxAsFull()
        {
            Assert.AreEqual(1f, BoostRingRules.Fill(0f, 0f), 0.0001f);
            Assert.AreEqual(1f, BoostRingRules.Fill(1f, float.NaN), 0.0001f);
        }

        [Test]
        public void Fill_FollowsRatio_AfterJumpFromFullToZeroAndBack()
        {
            Assert.AreEqual(1f, BoostRingRules.Fill(3f, 3f), 0.0001f);
            Assert.AreEqual(0f, BoostRingRules.Fill(0f, 3f), 0.0001f);
            Assert.AreEqual(0.3333f, BoostRingRules.Fill(1f, 3f), 0.001f);
        }

        [Test]
        public void Slowed_ShowsOnlyWhileCharging()
        {
            Assert.IsTrue(BoostRingRules.Slowed(true, 0.5f));
            Assert.IsFalse(BoostRingRules.Slowed(true, 1f));
            Assert.IsFalse(BoostRingRules.Slowed(false, 0.5f));
        }

        [Test]
        public void Pulse_ZeroWhenNotFull()
        {
            Assert.AreEqual(0f, BoostRingRules.Pulse(0f));
            Assert.AreEqual(0f, BoostRingRules.Pulse(-1f));
        }

        [Test]
        public void Pulse_StaysInZeroToOne()
        {
            for (float t = 0.01f; t < 10f; t += 0.07f)
            {
                float p = BoostRingRules.Pulse(t);
                Assert.GreaterOrEqual(p, 0f);
                Assert.LessOrEqual(p, 1f);
            }
        }

        [Test]
        public void ShowPrompt_Local_AlwaysWhenFull()
        {
            Assert.IsTrue(BoostRingRules.ShowPrompt(false, 99, false, 0.01f));
        }

        [Test]
        public void ShowPrompt_Online_FirstThreeMatches()
        {
            Assert.IsTrue(BoostRingRules.ShowPrompt(true, 2, false, 0.01f));
            Assert.IsFalse(BoostRingRules.ShowPrompt(true, 3, false, 0.01f));
            Assert.IsTrue(BoostRingRules.ShowPrompt(true, 3, false, 3f));
        }

        [Test]
        public void ShowPrompt_NeverForBots()
        {
            Assert.IsFalse(BoostRingRules.ShowPrompt(false, 0, true, 5f));
            Assert.IsFalse(BoostRingRules.ShowPrompt(true, 0, true, 5f));
        }

        [Test]
        public void ShowPrompt_NeverWhenNotFull()
        {
            Assert.IsFalse(BoostRingRules.ShowPrompt(false, 0, false, 0f));
            Assert.IsFalse(BoostRingRules.ShowPrompt(true, 0, false, 0f));
        }

        [Test]
        public void OfferIdleHint_At15s()
        {
            Assert.IsFalse(BoostRingRules.OfferIdleHint(14.9f));
            Assert.IsTrue(BoostRingRules.OfferIdleHint(15f));
        }

        [Test]
        public void Burst_FadesOutBy_BURST_SECONDS()
        {
            Assert.AreEqual(1f, BoostRingRules.BurstAlpha(0f), 0.0001f);
            Assert.AreEqual(0f, BoostRingRules.BurstAlpha(BoostRingRules.BURST_SECONDS));
            Assert.AreEqual(0f, BoostRingRules.BurstAlpha(-0.1f));
            Assert.AreEqual(1f, BoostRingRules.BurstScale(0f), 0.0001f);
            Assert.AreEqual(1.6f, BoostRingRules.BurstScale(BoostRingRules.BURST_SECONDS), 0.0001f);
        }

        [Test]
        public void WithPeopleOnline_OnlyWhenAnotherMachinesHumanPlays()
        {
            PlayerSlot bot = new PlayerSlot(1, null, null, 0, isBot: true);
            PlayerSlot remote = new PlayerSlot(2, null, null, 5);

            Assert.IsFalse(BoostRingRules.WithPeopleOnline(false, new[] { bot, remote }), "offline is local");
            Assert.IsFalse(BoostRingRules.WithPeopleOnline(true, new[] { bot }), "solo with CPU drivers plays like local");
            Assert.IsFalse(BoostRingRules.WithPeopleOnline(true, null), "no roster");
            Assert.IsTrue(BoostRingRules.WithPeopleOnline(true, new[] { bot, remote }), "another machine's player");
        }
    }
}

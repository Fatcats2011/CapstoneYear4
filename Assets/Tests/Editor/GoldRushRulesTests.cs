using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>Gold Rush's numbers (GoldRushRules): more packages, the score drain, and rush hour's double steals</summary>
    public class GoldRushRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            HouseRules.Playing = HouseRules.Normal;
        }

        static void PlayGoldRush()
        {
            HouseRules.Playing = new HouseRules(MatchLength.Normal, true, true, true, GameMode.GoldRush);
        }

        [Test]
        public void Quota_AndCooldown_OnlyChangeInGoldRush()
        {
            Assert.AreEqual(2, GoldRushRules.Quota(2));
            Assert.AreEqual(4f, GoldRushRules.Cooldown(4f));

            PlayGoldRush();

            Assert.AreEqual(6, GoldRushRules.Quota(2));
            Assert.AreEqual(1f, GoldRushRules.Cooldown(4f));
            Assert.AreEqual(0.5f, GoldRushRules.Cooldown(0.5f), "never slower than the scene");
        }

        [TestCase(GameMode.GoldRush, 30f, false, true)]
        [TestCase(GameMode.GoldRush, 12f, false, true)]
        [TestCase(GameMode.GoldRush, 31f, false, false)]
        [TestCase(GameMode.GoldRush, 0f, false, false)]
        [TestCase(GameMode.GoldRush, 10f, true, false)]
        [TestCase(GameMode.Standard, 10f, false, false)]
        public void IsRushHour(GameMode mode, float timeLeft, bool wavesOver, bool rush)
        {
            Assert.AreEqual(rush, GoldRushRules.IsRushHour(mode, timeLeft, wavesOver));
        }

        [Test]
        public void StealCounts_IsOne_WithNoMatch()
        {
            PlayGoldRush();

            Assert.AreEqual(1, GoldRushRules.StealCounts());
        }

        [Test]
        public void Drain_NothingDuringTheGrace()
        {
            float carry = 0f;

            Assert.AreEqual(0, GoldRushRules.Drain(4.9f, 1f, ref carry, 100));
        }

        [Test]
        public void Drain_TwoPointsASecond_AcrossFrames()
        {
            float carry = 0f;
            int drained = 0;
            for (int frame = 0; frame < 60; frame++)
                drained += GoldRushRules.Drain(10f, 1f / 60f, ref carry, 100);

            Assert.AreEqual(2, drained, 1, "about two points in a second of frames");
        }

        [Test]
        public void Drain_NeverBelowZero()
        {
            float carry = 0f;

            Assert.AreEqual(1, GoldRushRules.Drain(10f, 5f, ref carry, 1));
            Assert.AreEqual(0, GoldRushRules.Drain(10f, 5f, ref carry, 0));
        }

        [Test]
        public void Drain_ADeliveryClearsTheCarry()
        {
            float carry = 0.9f;

            GoldRushRules.Drain(0f, 0.1f, ref carry, 100);

            Assert.AreEqual(0f, carry);
        }
    }
}

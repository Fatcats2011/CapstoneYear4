using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What a gain of money is called (GainFeedbackRules): the reason and its line, which modes show a "+$" or call a lead,
    /// how the scoreboard's number counts up and its "+$" fades, who leads and when that changes, and that the six new
    /// lines exist in every language
    /// </summary>
    public class GainFeedbackRulesTests
    {
        // The points NewLeader reads: a field and a method, so no test lambda captures a local
        readonly Dictionary<int, int> points = new Dictionary<int, int>();

        int PointsOf(int seat)
        {
            return points.TryGetValue(seat, out int value) ? value : 0;
        }

        void SetPoints(params int[] perSeat)
        {
            points.Clear();
            for (int seat = 0; seat < perSeat.Length; seat++)
                points[seat] = perSeat[seat];
        }

        [TestCase(Constants.OrderValue.Easy, false, GainReason.Delivered)]
        [TestCase(Constants.OrderValue.Golden, false, GainReason.Golden)]
        [TestCase(Constants.OrderValue.Hard, true, GainReason.Lode)]
        [TestCase(Constants.OrderValue.Golden, true, GainReason.Lode)]
        public void ReasonFor_TheLodeFirstThenTheGoldenOrder(Constants.OrderValue tier, bool isLode, GainReason expected)
        {
            Assert.AreEqual(expected, GainFeedbackRules.ReasonFor(tier, isLode));
        }

        [Test]
        public void Key_ADeliveryWithAStampHasItsOwnLine()
        {
            Assert.AreEqual("gain.delivered", GainFeedbackRules.Key(GainReason.Delivered, ""));
            Assert.AreEqual("gain.delivered_stolen", GainFeedbackRules.Key(GainReason.Delivered, "×2"));
            Assert.AreEqual("gain.golden", GainFeedbackRules.Key(GainReason.Golden, "×2"));
            Assert.AreEqual("gain.lode", GainFeedbackRules.Key(GainReason.Lode, ""));
        }

        [Test]
        public void BoardShowsGains_OnlyWhereTheRowShowsMoney()
        {
            Assert.IsTrue(GainFeedbackRules.BoardShowsGains(GameMode.Standard));
            Assert.IsTrue(GainFeedbackRules.BoardShowsGains(GameMode.GoldRush));
            Assert.IsFalse(GainFeedbackRules.BoardShowsGains(GameMode.KingsCrown));
            Assert.IsFalse(GainFeedbackRules.BoardShowsGains(GameMode.Infection));
            Assert.IsFalse(GainFeedbackRules.BoardShowsGains(GameMode.HotPotato));
        }

        [Test]
        public void LeadCalled_OnlyWhereTheBoardRanksByMoneyOrTime()
        {
            Assert.IsTrue(GainFeedbackRules.LeadCalled(GameMode.Standard));
            Assert.IsTrue(GainFeedbackRules.LeadCalled(GameMode.GoldRush));
            Assert.IsTrue(GainFeedbackRules.LeadCalled(GameMode.KingsCrown));
            Assert.IsFalse(GainFeedbackRules.LeadCalled(GameMode.Infection));
            Assert.IsFalse(GainFeedbackRules.LeadCalled(GameMode.HotPotato));
        }

        [Test]
        public void CountUp_RunsLinearlyFromOldToNew()
        {
            Assert.AreEqual(100, GainFeedbackRules.CountUp(100, 200, -1f));
            Assert.AreEqual(100, GainFeedbackRules.CountUp(100, 200, 0f));
            Assert.AreEqual(150, GainFeedbackRules.CountUp(100, 200, 0.2f));
            Assert.AreEqual(200, GainFeedbackRules.CountUp(100, 200, 0.4f));
            Assert.AreEqual(200, GainFeedbackRules.CountUp(100, 200, 9f));
        }

        [Test]
        public void PopAlpha_HoldsThenFadesThenIsGone()
        {
            Assert.AreEqual(0f, GainFeedbackRules.PopAlpha(-0.1f));
            Assert.AreEqual(1f, GainFeedbackRules.PopAlpha(0.5f));
            Assert.AreEqual(0.5f, GainFeedbackRules.PopAlpha(1.05f), 0.01f);
            Assert.AreEqual(0f, GainFeedbackRules.PopAlpha(1.2f));
        }

        [Test]
        public void NewLeader_AStrictLeadIsTheLeader()
        {
            SetPoints(0, 50);

            Assert.AreEqual(1, GainFeedbackRules.NewLeader(-1, new[] { 1, 0 }, PointsOf));
        }

        [Test]
        public void NewLeader_ATieKeepsTheLastLeader()
        {
            SetPoints(50, 50);

            Assert.AreEqual(0, GainFeedbackRules.NewLeader(0, new[] { 1, 0 }, PointsOf));
        }

        [Test]
        public void NewLeader_ATieWithNoLastLeader_IsNobody()
        {
            SetPoints(50, 50);

            Assert.AreEqual(-1, GainFeedbackRules.NewLeader(-1, new[] { 1, 0 }, PointsOf));
        }

        [Test]
        public void NewLeader_TheLastLeaderLeft_TheNextStrictLeaderTakesOver()
        {
            SetPoints(0, 30, 60);

            Assert.AreEqual(2, GainFeedbackRules.NewLeader(0, new[] { 2, 1 }, PointsOf));
        }

        [Test]
        public void NewLeader_TheLastLeaderLeft_ATieForFirstIsNobody()
        {
            SetPoints(0, 60, 60);

            Assert.AreEqual(-1, GainFeedbackRules.NewLeader(0, new[] { 2, 1 }, PointsOf));
        }

        [Test]
        public void NewLeader_ALonePlayer_NeedsMoreThanNothing()
        {
            SetPoints(0, 0, 0, 0);
            Assert.AreEqual(-1, GainFeedbackRules.NewLeader(-1, new[] { 3 }, PointsOf));
            Assert.AreEqual(3, GainFeedbackRules.NewLeader(3, new[] { 3 }, PointsOf), "the last leader, still ranked, keeps it");

            SetPoints(0, 0, 0, 10);
            Assert.AreEqual(3, GainFeedbackRules.NewLeader(-1, new[] { 3 }, PointsOf));
        }

        [Test]
        public void NewLeader_NobodyRanked_IsNobody()
        {
            SetPoints(5);

            Assert.AreEqual(-1, GainFeedbackRules.NewLeader(0, new int[0], PointsOf));
        }

        [Test]
        public void LeadChanged_OnlyWhenOneLeaderGivesWayToAnother()
        {
            Assert.IsFalse(GainFeedbackRules.LeadChanged(-1, 1));
            Assert.IsTrue(GainFeedbackRules.LeadChanged(0, 1));
            Assert.IsFalse(GainFeedbackRules.LeadChanged(1, 1));
            Assert.IsFalse(GainFeedbackRules.LeadChanged(0, -1));
        }

        [Test]
        public void TheSixNewLines_ExistInEveryLanguage()
        {
            StringsCsv csv = StringsCsv.Load();
            string[] keys = { "gain.delivered", "gain.delivered_stolen", "gain.golden", "gain.lode", "lead.you", "lead.other" };

            foreach (string key in keys)
                foreach (string locale in LocaleCodes.All)
                    Assert.IsNotNull(csv.Get(key, locale), key + " " + locale);
        }

        [Test]
        public void TheStolenLine_ReadsTheStampAndTheMoney()
        {
            StringsCsv csv = StringsCsv.Load();

            Assert.AreEqual("DELIVERED ×2 +$130", string.Format(csv.Get("gain.delivered_stolen", "en"), 130, "×2"));
        }
    }
}

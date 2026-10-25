using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// End-of-match awards: what a match counts for each seat (MatchStats), and who gets which card (AwardRules). One per
    /// player: their best award they top alone, then the best of what's left, then TOURIST
    /// </summary>
    public class AwardRulesTests
    {
        static readonly int[] TWO = { 0, 1 };

        static MatchStats Stats() { return new MatchStats(); }

        static void Steals(MatchStats stats, int seat, int times)
        {
            for (int i = 0; i < times; i++)
                stats.Stole(seat, GameState.Begin);
        }

        static void Deliveries(MatchStats stats, int seat, int times, Constants.OrderValue value = Constants.OrderValue.Easy)
        {
            for (int i = 0; i < times; i++)
                stats.Delivered(seat, value, GameState.Begin);
        }

        [Test]
        public void Pick_EachTopsOne_GetsTheirOwn()
        {
            MatchStats stats = Stats();
            Steals(stats, 0, 3);
            Deliveries(stats, 1, 5);

            Dictionary<int, Award> awards = AwardRules.Pick(stats, TWO);

            Assert.AreEqual(Award.Kleptoghost, awards[0]);
            Assert.AreEqual(Award.BusyBee, awards[1]);
        }

        [Test]
        public void Pick_TopsTwo_TakesTheBest_AndTheOtherGoesToTheNext()
        {
            MatchStats stats = Stats();
            Steals(stats, 0, 3);
            Deliveries(stats, 0, 5);
            Deliveries(stats, 1, 2);

            Dictionary<int, Award> awards = AwardRules.Pick(stats, TWO);

            Assert.AreEqual(Award.Kleptoghost, awards[0]);
            Assert.AreEqual(Award.BusyBee, awards[1], "the best of what's left");
        }

        [Test]
        public void Pick_ATie_GoesToTheLowerSeat_AndTheOtherIsATourist()
        {
            MatchStats stats = Stats();
            Steals(stats, 0, 2);
            Steals(stats, 1, 2);

            Dictionary<int, Award> awards = AwardRules.Pick(stats, TWO);

            Assert.AreEqual(Award.Kleptoghost, awards[0]);
            Assert.AreEqual(Award.Tourist, awards[1]);
        }

        [Test]
        public void Pick_NobodyDidAnything_EveryoneIsATourist()
        {
            Dictionary<int, Award> awards = AwardRules.Pick(Stats(), new[] { 0, 1, 2 });

            Assert.AreEqual(3, awards.Count);
            foreach (Award award in awards.Values)
                Assert.AreEqual(Award.Tourist, award);
        }

        [Test]
        public void Pick_GoldenGrip_IsTheBest()
        {
            MatchStats stats = Stats();
            Steals(stats, 0, 3);
            stats.HeldGolden(0, 4.5f);

            Assert.AreEqual(Award.GoldenGrip, AwardRules.Pick(stats, new[] { 0 })[0]);
        }

        [Test]
        public void Pick_OnlyTheSeatsPlaying()
        {
            MatchStats stats = Stats();
            Steals(stats, 3, 2);

            Dictionary<int, Award> awards = AwardRules.Pick(stats, TWO);

            Assert.IsFalse(awards.ContainsKey(3), "seat 4 left");
            Assert.AreEqual(Award.Tourist, awards[0]);
        }

        [Test]
        public void Pick_EveryAward_HasItsCount()
        {
            MatchStats stats = Stats();
            Deliveries(stats, 0, 2, Constants.OrderValue.Hard);
            stats.Robbed(1, GameState.Begin);
            stats.Sank(2, GameState.Begin);
            stats.Sank(2, GameState.Begin);

            Dictionary<int, Award> awards = AwardRules.Pick(stats, new[] { 0, 1, 2 });

            Assert.AreEqual(Award.BusyBee, awards[0], "busy bee ranks above high roller");
            Assert.AreEqual(Award.Butterfingers, awards[1]);
            Assert.AreEqual(Award.FishFood, awards[2]);
            Assert.AreEqual(2, stats.Count(0, Award.HighRoller));
            Assert.AreEqual(2, stats.Count(2, Award.FishFood));
        }

        [Test]
        public void Tutorial_CountsNothing()
        {
            MatchStats stats = Stats();
            stats.Delivered(0, Constants.OrderValue.Hard, GameState.Tutorial);
            stats.Stole(0, GameState.Tutorial);
            stats.Robbed(0, GameState.Tutorial);
            stats.Sank(0, GameState.Tutorial);

            foreach (Award award in AwardRules.PRIORITY)
                Assert.AreEqual(0, stats.Count(0, award), award.ToString());
        }

        [Test]
        public void HeldGolden_CountsSecondsToTheNearest()
        {
            MatchStats stats = Stats();
            stats.HeldGolden(1, 0.6f);
            stats.HeldGolden(1, 0.6f);

            Assert.AreEqual(1, stats.Count(1, Award.GoldenGrip));
            Assert.AreEqual(Award.GoldenGrip, AwardRules.Pick(stats, new[] { 1 })[1]);

            MatchStats brief = Stats();
            brief.HeldGolden(1, 0.4f);
            Assert.AreEqual(Award.Tourist, AwardRules.Pick(brief, new[] { 1 })[1], "under half a second isn't a grip");
        }

        [Test]
        public void NewMatch_ForgetsTheLast()
        {
            MatchStats stats = Stats();
            Steals(stats, 0, 2);
            stats.HeldGolden(0, 3f);

            stats.NewMatch();

            Assert.AreEqual(0, stats.Count(0, Award.Kleptoghost));
            Assert.AreEqual(0, stats.Count(0, Award.GoldenGrip));
        }

        [TestCase(Award.BusyBee, 1, "1 delivery")]
        [TestCase(Award.BusyBee, 9, "9 deliveries")]
        [TestCase(Award.Kleptoghost, 1, "1 steal")]
        [TestCase(Award.Kleptoghost, 3, "3 steals")]
        [TestCase(Award.HighRoller, 1, "1 hard order")]
        [TestCase(Award.HighRoller, 2, "2 hard orders")]
        [TestCase(Award.Butterfingers, 1, "robbed once")]
        [TestCase(Award.Butterfingers, 5, "robbed 5 times")]
        [TestCase(Award.FishFood, 1, "1 swim")]
        [TestCase(Award.FishFood, 4, "4 swims")]
        [TestCase(Award.GoldenGrip, 12, "held the gold 12s")]
        [TestCase(Award.Tourist, 0, "enjoyed the view")]
        public void Line_IsACount(Award award, int count, string line)
        {
            Assert.AreEqual(line, AwardRules.Line(award, count));
        }

        [TestCase(Award.GoldenGrip, "GOLDEN GRIP")]
        [TestCase(Award.Kleptoghost, "KLEPTOGHOST")]
        [TestCase(Award.BusyBee, "BUSY BEE")]
        [TestCase(Award.HighRoller, "HIGH ROLLER")]
        [TestCase(Award.Butterfingers, "BUTTERFINGERS")]
        [TestCase(Award.FishFood, "FISH FOOD")]
        [TestCase(Award.Tourist, "TOURIST")]
        public void Name_IsTheJoke(Award award, string name)
        {
            Assert.AreEqual(name, AwardRules.Name(award));
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The awards on this machine (MatchAwards): where the rules run it counts what the game raises (FeatSync) and the
    /// golden order's holders, and picks the awards at the results (AwardSync). An online client counts nothing: it shows
    /// the host's. A new match forgets the last
    /// </summary>
    public class MatchAwardsTests
    {
        readonly TestObjects objects = new TestObjects();
        static readonly HashSet<int> holdingGold = new HashSet<int>();
        MatchAwards awards;

        [SetUp]
        public void SetUp()
        {
            holdingGold.Clear();
            awards = objects.Add<MatchAwards>();
            awards.HoldsGolden = HoldsGolden;
            awards.Begin();
        }

        [TearDown]
        public void TearDown()
        {
            awards.End();
            objects.DestroyAll();
            GameAuthority.Role = NetworkRole.Offline;
            AwardSync.Reset();
        }

        static bool HoldsGolden(int seat) { return holdingGold.Contains(seat); }

        [Test]
        public void Offline_WhatTheGameRaises_PicksTheAwards()
        {
            FeatSync.Steal(0, false);
            FeatSync.Rob(1);
            FeatSync.Sink(2);
            FeatSync.Sink(2);

            awards.Results(new[] { 0, 1, 2 });

            Assert.IsTrue(AwardSync.HasLast);
            Assert.AreEqual(Award.Kleptoghost, AwardSync.Last.AwardOf(0));
            Assert.AreEqual(Award.Butterfingers, AwardSync.Last.AwardOf(1));
            Assert.AreEqual(Award.FishFood, AwardSync.Last.AwardOf(2));
            Assert.AreEqual(2, AwardSync.Last.CountOf(2));
        }

        [Test]
        public void Deliveries_CountTowardsBusyBeeAndHighRoller()
        {
            FeatSync.Deliver(0, Constants.OrderValue.Hard);
            FeatSync.Deliver(1, Constants.OrderValue.Hard);
            FeatSync.Deliver(1, Constants.OrderValue.Hard);
            FeatSync.Deliver(0, Constants.OrderValue.Easy);
            FeatSync.Deliver(0, Constants.OrderValue.Easy);

            awards.Results(new[] { 0, 1 });

            Assert.AreEqual(Award.BusyBee, AwardSync.Last.AwardOf(0));
            Assert.AreEqual(Award.HighRoller, AwardSync.Last.AwardOf(1));
        }

        [Test]
        public void InTheGoldenRound_HoldingTheGold_CountsItsTime()
        {
            holdingGold.Add(1);
            for (int i = 0; i < 30; i++)
                awards.Tick(0.1f, GameState.FinalPackage);
            awards.Tick(5f, GameState.Begin); // not the golden round

            awards.Results(new[] { 0, 1 });

            Assert.AreEqual(Award.GoldenGrip, AwardSync.Last.AwardOf(1));
            Assert.AreEqual(3, AwardSync.Last.CountOf(1));
        }

        [Test]
        public void OnAClient_NothingCounts_AndNothingIsPicked()
        {
            GameAuthority.Role = NetworkRole.Client;
            FeatSync.Steal(0, false);
            holdingGold.Add(0);
            awards.Tick(5f, GameState.FinalPackage);

            awards.Results(new[] { 0 });

            Assert.IsFalse(AwardSync.HasLast, "the host's arrive instead");
        }

        [Test]
        public void ANewMatch_ForgetsTheLast()
        {
            FeatSync.Steal(0, false);
            awards.Results(new[] { 0 });

            awards.NewMatch();

            Assert.IsFalse(AwardSync.HasLast);
            awards.Results(new[] { 0 });
            Assert.AreEqual(Award.Tourist, AwardSync.Last.AwardOf(0));
        }

        [Test]
        public void Ended_HearsNothing()
        {
            awards.End();
            FeatSync.Steal(0, false);
            awards.Begin();

            awards.Results(new[] { 0 });

            Assert.AreEqual(Award.Tourist, AwardSync.Last.AwardOf(0));
        }
    }
}

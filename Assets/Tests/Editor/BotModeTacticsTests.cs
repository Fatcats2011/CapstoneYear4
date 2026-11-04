using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>The bots' plan in the timed modes (BotModeTactics): chase, run, or go for the crown</summary>
    public class BotModeTacticsTests
    {
        const int FOUR = 0b1111;
        static readonly Dictionary<int, Vector3> balls = new Dictionary<int, Vector3>();
        static Vector3? BallOf(int seat) { return balls.TryGetValue(seat, out Vector3 ball) ? ball : (Vector3?)null; }
        static void Nothing(int seat, int points) { }

        [SetUp]
        public void SetUp()
        {
            balls.Clear();
            balls[0] = new Vector3(0f, 0f, 0f);
            balls[1] = new Vector3(10f, 0f, 0f);
            balls[2] = new Vector3(50f, 0f, 0f);
            balls[3] = new Vector3(-80f, 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            ModeMatch.End();
        }

        [Test]
        public void Crown_OnTheRoad_GoesForIt_ThenChasesItsHolder_OrRunsWithIt()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.KingsCrown, FOUR, new Vector3(200f, 0f, 0f), 0f);
            Assert.AreEqual(new Vector3(200f, 0f, 0f), BotModeTactics.Choose(match, 0, balls[0], BallOf).Place);

            balls[2] = new Vector3(200f, 0f, 0f);
            match.Tick(0f, 0.1f, BallOf, Nothing);
            Assert.AreEqual(2, match.CrownSeat);

            Assert.AreEqual(2, BotModeTactics.Choose(match, 0, balls[0], BallOf).Chase);
            ModeGoal holder = BotModeTactics.Choose(match, 2, balls[2], BallOf);
            Assert.AreEqual(ModeMatch.NONE, holder.Chase);
            Assert.Greater(holder.Place.Value.x, 200f, "away from the nearest rival (seat 1, behind it)");
        }

        [Test]
        public void Infection_HuntersChaseTheNearestThief_ThievesRunFromTheNearestHunter()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.Infection, FOUR, Vector3.zero, 0f, new System.Random(7));
            int hunter = match.FirstHunter;
            ModeGoal hunting = BotModeTactics.Choose(match, hunter, balls[hunter], BallOf);
            Assert.AreNotEqual(ModeMatch.NONE, hunting.Chase);
            Assert.IsFalse(match.IsInfected(hunting.Chase));

            int thief = (hunter + 1) % 4;
            ModeGoal running = BotModeTactics.Choose(match, thief, balls[thief], BallOf);
            Assert.AreEqual(ModeMatch.NONE, running.Chase);
            Assert.Greater(Vector3.Distance(running.Place.Value, balls[hunter]), Vector3.Distance(balls[thief], balls[hunter]), "further from the hunter");
        }

        [Test]
        public void HotPotato_TheHolderChasesTheNearest_OthersRunFromTheHolder_OutDoNothing()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, FOUR, Vector3.zero, 0f, new System.Random(8));
            match.Tick(TimedModeRules.POTATO_DELAY, 0.1f, BallOf, Nothing);
            int holder = match.PotatoSeat;

            ModeGoal passing = BotModeTactics.Choose(match, holder, balls[holder], BallOf);
            Assert.AreNotEqual(ModeMatch.NONE, passing.Chase);
            Assert.AreNotEqual(holder, passing.Chase);

            int other = (holder + 1) % 4;
            ModeGoal running = BotModeTactics.Choose(match, other, balls[other], BallOf);
            Assert.Greater(Vector3.Distance(running.Place.Value, balls[holder]), Vector3.Distance(balls[other], balls[holder]));

            match.Tick(match.FuseEnd, 0.1f, BallOf, Nothing);
            Assert.AreEqual(ModeMatch.NONE, BotModeTactics.Choose(match, holder, balls[holder], BallOf).Chase);
            Assert.IsNull(BotModeTactics.Choose(match, holder, balls[holder], BallOf).Place, "out: nowhere to go");
        }

        [Test]
        public void NoTimedMatch_NoGoal()
        {
            Assert.IsNull(BotModeTactics.Choose(null, 0, Vector3.zero, BallOf).Place);
        }
    }
}

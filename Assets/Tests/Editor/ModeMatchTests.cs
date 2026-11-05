using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Kings Crown, Infection and Hot Potato's rules on the authority (ModeMatch, TimedModeRules): who holds the crown or
    /// the potato, who is caught or out, the points, and when a match ends early
    /// </summary>
    public class ModeMatchTests
    {
        const int FOUR = 0b1111;
        readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        readonly List<ModeMoment> moments = new List<ModeMoment>();
        static readonly Dictionary<int, Vector3> balls = new Dictionary<int, Vector3>();

        void AddScore(int seat, int points) { scores[seat] = (scores.TryGetValue(seat, out int s) ? s : 0) + points; }
        void Saw(ModeMoment moment) { moments.Add(moment); }
        static Vector3? BallOf(int seat) { return balls.TryGetValue(seat, out Vector3 ball) ? ball : (Vector3?)null; }

        [SetUp]
        public void SetUp()
        {
            ModeMatch.Happened += Saw;
            balls.Clear();
            for (int seat = 0; seat < 4; seat++)
                balls[seat] = new Vector3(100f * (seat + 1), 0f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            ModeMatch.Happened -= Saw;
            ModeMatch.End();
            scores.Clear();
            moments.Clear();
        }

        int Score(int seat) => scores.TryGetValue(seat, out int s) ? s : 0;

        void Run(ModeMatch match, float from, float seconds)
        {
            for (float t = 0f; t < seconds; t += 0.1f)
                match.Tick(from + t, 0.1f, BallOf, AddScore);
        }

        [Test]
        public void Standard_IsNotATimedMode()
        {
            Assert.IsNull(ModeMatch.Begin(GameMode.Standard, FOUR, Vector3.zero, 0f));
            Assert.IsNull(ModeMatch.Begin(GameMode.GoldRush, FOUR, Vector3.zero, 0f));
            Assert.IsNull(ModeMatch.Current);
        }

        [Test]
        public void Crown_LiesOnTheRoad_UntilSomeoneDrivesThrough()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.KingsCrown, FOUR, Vector3.zero, 0f);
            Run(match, 0f, 1f);
            Assert.AreEqual(ModeMatch.NONE, match.CrownSeat);

            balls[2] = new Vector3(3f, 0f, 0f);
            Run(match, 1f, 0.1f);

            Assert.AreEqual(2, match.CrownSeat);
            Assert.AreEqual(ModeMomentKind.CrownTaken, moments[0].Kind);
        }

        [Test]
        public void Crown_PaysItsHolder_TenASecond()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.KingsCrown, FOUR, Vector3.zero, 0f);
            balls[1] = Vector3.zero;
            Run(match, 0f, 10f);

            Assert.AreEqual(100, Score(1), 2);
            Assert.AreEqual(0, Score(0));
        }

        [Test]
        public void Crown_ABoostingHitTakesIt_ButNotInTheFirstTwoSeconds()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.KingsCrown, FOUR, Vector3.zero, 0f);
            balls[1] = Vector3.zero;
            Run(match, 0f, 0.1f);
            balls[1] = new Vector3(500f, 0f, 0f);

            Assert.IsTrue(match.Strike(3, 1, 1f), "the mode uses the hit");
            Assert.AreEqual(1, match.CrownSeat, "safe just after taking it");

            match.Strike(3, 1, 3f);
            Assert.AreEqual(3, match.CrownSeat);

            match.Strike(1, 3, 4f);
            Assert.AreEqual(3, match.CrownSeat, "no instant steal-back");
            match.Strike(0, 2, 9f);
            Assert.AreEqual(3, match.CrownSeat, "hitting someone without it does nothing");
        }

        [Test]
        public void Infection_OneHunterToStart_ACatchSpreadsIt()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.Infection, FOUR, Vector3.zero, 0f, new System.Random(1));
            int hunter = match.FirstHunter;
            Assert.AreNotEqual(ModeMatch.NONE, hunter);
            Assert.AreEqual(1 << hunter, match.Infected);

            int thief = (hunter + 1) % 4;
            match.Strike(thief, hunter, 1f);
            Assert.IsFalse(match.IsInfected(thief), "a thief hitting a hunter catches nobody");

            match.Strike(hunter, thief, 1f);
            Assert.IsTrue(match.IsInfected(thief));
            Assert.AreEqual(ModeMomentKind.Infected, moments[0].Kind);
            Assert.AreEqual(TimedModeRules.CATCH_POINTS, ModeMatch.CatchPoints(moments[0]));
        }

        [Test]
        public void Infection_FreeThievesScore_AndCatchingEveryoneEndsIt()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.Infection, FOUR, Vector3.zero, 0f, new System.Random(2));
            int hunter = match.FirstHunter;
            Run(match, 0f, 2f);
            Assert.AreEqual(0, Score(hunter));
            Assert.AreEqual(10, Score((hunter + 1) % 4), 1);

            for (int seat = 0; seat < 4; seat++)
                match.Strike(hunter, seat, 3f);

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(ModeMomentKind.LastCaught, moments[moments.Count - 1].Kind);
        }

        [Test]
        public void HotPotato_LightsAfterADelay_AndExplodesOnItsHolder()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, FOUR, Vector3.zero, 0f, new System.Random(3));
            Run(match, 0f, TimedModeRules.POTATO_DELAY - 0.5f);
            Assert.AreEqual(ModeMatch.NONE, match.PotatoSeat);

            Run(match, TimedModeRules.POTATO_DELAY, 0.2f);
            int holder = match.PotatoSeat;
            Assert.AreNotEqual(ModeMatch.NONE, holder);
            float fuse = match.FuseEnd - match.FuseLit;
            Assert.That(fuse, Is.InRange(TimedModeRules.FUSE_MIN, TimedModeRules.FUSE_MAX));

            Run(match, match.FuseEnd, 0.2f);

            Assert.IsTrue(match.IsOut(holder));
            Assert.AreEqual(ModeMatch.NONE, match.PotatoSeat);
            Assert.AreEqual(ModeMomentKind.PotatoExploded, moments[0].Kind);
        }

        [Test]
        public void HotPotato_TheHolderPassesItByHitting_NoTagBacks()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, FOUR, Vector3.zero, 0f, new System.Random(4));
            Run(match, TimedModeRules.POTATO_DELAY, 0.1f);
            int holder = match.PotatoSeat;
            int other = (holder + 1) % 4;

            match.Strike(other, holder, 5f);
            Assert.AreEqual(holder, match.PotatoSeat, "hitting the holder doesn't take it");

            match.Strike(holder, other, 5f);
            Assert.AreEqual(other, match.PotatoSeat);
            Assert.AreEqual(ModeMomentKind.PotatoPassed, moments[0].Kind);
            Assert.AreEqual(holder, moments[0].Seat, "the passer did it");

            match.Strike(other, holder, 5.5f);
            Assert.AreEqual(other, match.PotatoSeat, "no tag-backs straight away");
            match.Strike(other, holder, 7f);
            Assert.AreEqual(holder, match.PotatoSeat);
        }

        [Test]
        public void HotPotato_PlayersOutAreIgnored_AndTheLastOneInWins()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, 0b0011, Vector3.zero, 0f, new System.Random(5));
            Run(match, TimedModeRules.POTATO_DELAY, 0.1f);
            int holder = match.PotatoSeat;
            int other = 1 - holder;

            Run(match, match.FuseEnd, 0.2f);

            Assert.IsTrue(match.IsOver);
            Assert.AreEqual(ModeMomentKind.LastStanding, moments[moments.Count - 1].Kind);
            Assert.AreEqual(other, moments[moments.Count - 1].Seat);
        }

        [Test]
        public void HotPotato_TheHolderCantPassToSomeoneOut()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, 0b0111, Vector3.zero, 0f, new System.Random(5));
            Run(match, TimedModeRules.POTATO_DELAY, 0.1f);
            int gone = match.PotatoSeat;
            Run(match, match.FuseEnd, 0.2f);
            Assert.IsTrue(match.IsOut(gone));

            Run(match, match.FuseEnd + TimedModeRules.POTATO_DELAY + 1f, 0.1f);
            int holder = match.PotatoSeat;
            Assert.AreNotEqual(ModeMatch.NONE, holder, "the next potato is lit");

            match.Strike(holder, gone, match.FuseLit + 5f);
            Assert.AreEqual(holder, match.PotatoSeat, "someone out can't be passed the potato");
            match.Strike(gone, holder, match.FuseLit + 5f);
            Assert.AreEqual(holder, match.PotatoSeat, "someone out can't hit anyone");
        }

        [Test]
        public void HotPotato_HolderLeaves_TheNextPotatoWaits()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, FOUR, Vector3.zero, 0f, new System.Random(5));
            float lit = TimedModeRules.POTATO_DELAY;
            Run(match, lit, 0.1f);
            match.Leave(match.PotatoSeat, lit + 1f);
            Assert.AreEqual(ModeMatch.NONE, match.PotatoSeat);

            Run(match, lit + 1.1f, TimedModeRules.POTATO_DELAY - 0.5f);
            Assert.AreEqual(ModeMatch.NONE, match.PotatoSeat, "no potato before POTATO_DELAY");
            Run(match, lit + 1f + TimedModeRules.POTATO_DELAY, 0.2f);
            Assert.AreNotEqual(ModeMatch.NONE, match.PotatoSeat);
        }

        [Test]
        public void Infection_LastHunterLeaves_ANewHunterIsPicked()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.Infection, FOUR, Vector3.zero, 0f, new System.Random(3));
            int hunter = match.FirstHunter;
            match.Leave(hunter, 1f);

            Assert.IsFalse(match.IsInfected(hunter), "someone gone isn't infected");
            Assert.AreEqual(1, TimedModeRules.Count(match.Infected), "a new hunter");
            Assert.AreEqual(match.Infected, match.Infected & match.Players, "the new hunter is still playing");
            Assert.IsFalse(match.IsOver);
        }

        [Test]
        public void Infection_AHunterLeaves_TheOthersKeepHunting()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.Infection, FOUR, Vector3.zero, 0f, new System.Random(3));
            int hunter = match.FirstHunter;
            int caught = (hunter + 1) % 4;
            match.Strike(hunter, caught, 1f);
            match.Leave(hunter, 2f);

            Assert.AreEqual(1 << caught, match.Infected, "no extra hunter while one is left");
        }

        [TestCase(GameMode.HotPotato, true)]
        [TestCase(GameMode.Infection, true)]
        [TestCase(GameMode.KingsCrown, false)]
        public void EveryoneElseLeaves_EndsTheRound_ButNotAMatchPlayedAlone(GameMode mode, bool over)
        {
            ModeMatch match = ModeMatch.Begin(mode, 0b0011, Vector3.zero, 0f, new System.Random(3));
            match.Leave(1, 1f);

            Assert.AreEqual(over, match.IsOver, "the last player left");
            Assert.IsFalse(ModeMatch.Begin(mode, 0b0001, Vector3.zero, 0f, new System.Random(3)).IsOver, "alone from the start");
        }

        [Test]
        public void HotPotato_NeverEndsEarlyAlone()
        {
            Assert.IsFalse(TimedModeRules.EndsEarly(GameMode.HotPotato, 0b0001, 0, 0));
            Assert.IsFalse(TimedModeRules.EndsEarly(GameMode.Infection, 0b0001, 0b0001, 0));
        }

        [Test]
        public void Snapshot_ShowsTheSameMatch_OnAnotherMachine()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, FOUR, new Vector3(1f, 2f, 3f), 0f, new System.Random(6));
            Run(match, TimedModeRules.POTATO_DELAY, 0.1f);
            ModeSnapshot snapshot = match.Snapshot(5f);
            float left = match.FuseEnd - 5f;
            ModeMatch.End();

            ModeMatch.Show(ModeSnapshot.Unpack(snapshot.PackSeats(), snapshot.CrownSpot, snapshot.FuseLeft, snapshot.FuseLength), 100f);

            ModeMatch shown = ModeMatch.Current;
            Assert.AreEqual(GameMode.HotPotato, shown.Mode);
            Assert.AreEqual(match.PotatoSeat, shown.PotatoSeat);
            Assert.AreEqual(100f + left, shown.FuseEnd, 0.01f);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), shown.CrownSpot);
        }

        [TestCase(0f, 0f, 10f, 0f)]
        [TestCase(5f, 0f, 10f, 0.5f)]
        [TestCase(20f, 0f, 10f, 1f)]
        public void FuseProgress(float now, float lit, float explodes, float progress)
        {
            Assert.AreEqual(progress, TimedModeRules.FuseProgress(now, lit, explodes), 0.001f);
        }
    }
}

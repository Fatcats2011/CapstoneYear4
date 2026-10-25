using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// A cutscene's skip (CutsceneSkip): the prompt shows a second in, each player on this machine holds A for half a
    /// second to vote, and once everyone has, the cutscene ends once. An online client sends its votes to the host
    /// instead of skipping
    /// </summary>
    public class CutsceneSkipTests
    {
        readonly TestObjects objects = new TestObjects();
        static readonly HashSet<int> down = new HashSet<int>(); // the seats holding A
        static readonly HashSet<int> gone = new HashSet<int>(); // the seats gone from the roster
        static int skips;
        static readonly List<int> sent = new List<int>();
        CutsceneSkip skip;

        [SetUp]
        public void SetUp()
        {
            down.Clear();
            gone.Clear();
            sent.Clear();
            skips = 0;
            skip = objects.Add<CutsceneSkip>();
            skip.Pressed = Pressed;
            skip.Skip = CountSkip;
            skip.SeatStillThere = StillThere;
            skip.LocalSeats = BothSeats;
        }

        [TearDown]
        public void TearDown()
        {
            SkipSync.Reset();
            objects.DestroyAll();
        }

        static bool Pressed(int seat) { return down.Contains(seat); }
        static void CountSkip() { skips++; }
        static bool StillThere(int seat) { return !gone.Contains(seat); }
        static IEnumerable<int> BothSeats() { return new[] { 0, 1 }; }
        static void Sent(int seat) { sent.Add(seat); }

        // Past the prompt's delay with nobody pressing: every hold is armed
        void ToThePrompt()
        {
            skip.Tick(SkipHold.SHOW_AFTER + 0.1f);
            skip.Tick(0.1f);
        }

        void Hold(float seconds)
        {
            for (float t = 0; t < seconds; t += 0.1f)
                skip.Tick(0.1f);
        }

        [Test]
        public void EveryoneHolds_SkipsOnce()
        {
            skip.Open(new[] { 0, 1 }, true);
            ToThePrompt();
            down.Add(0);
            down.Add(1);

            Hold(1f);
            Hold(1f);

            Assert.AreEqual(1, skips);
        }

        [Test]
        public void OnePlayerHolds_OfTwo_DoesNotSkip()
        {
            skip.Open(new[] { 0, 1 }, true);
            ToThePrompt();
            down.Add(0);

            Hold(2f);

            Assert.AreEqual(0, skips);
        }

        [Test]
        public void ASeatGoneFromTheRoster_DoesNotHoldTheOthersUp()
        {
            skip.Open(new[] { 0, 1 }, true);
            ToThePrompt();
            down.Add(0);
            gone.Add(1);

            Hold(1f);

            Assert.AreEqual(1, skips);
        }

        [Test]
        public void HeldFromBeforeThePrompt_DoesNotSkip()
        {
            down.Add(0);
            down.Add(1);
            skip.Open(new[] { 0, 1 }, true);

            Hold(SkipHold.SHOW_AFTER + 2f);

            Assert.AreEqual(0, skips, "an A still held from the loading screen");
        }

        [Test]
        public void Closed_ThenHeld_DoesNothing()
        {
            skip.Open(new[] { 0, 1 }, true);
            ToThePrompt();
            skip.Close();
            down.Add(0);
            down.Add(1);

            Hold(1f);

            Assert.AreEqual(0, skips, "the cutscene ended by itself");
        }

        [Test]
        public void OpenAgain_StartsFresh()
        {
            skip.Open(new[] { 0, 1 }, true);
            ToThePrompt();
            down.Add(0);
            Hold(1f);
            skip.Close();

            down.Clear();
            skip.Open(new[] { 0, 1 }, true);
            ToThePrompt();
            down.Add(1);
            Hold(1f);

            Assert.AreEqual(0, skips, "the opening's vote doesn't carry over to the golden cutscene");
        }

        [Test]
        public void OnAClient_ACompletedHold_SendsTheVote()
        {
            SkipSync.VoteSent += Sent;
            skip.Open(new[] { 0, 1 }, false);
            ToThePrompt();
            down.Add(1);
            down.Add(0);

            Hold(1f);

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, sent);
            Assert.AreEqual(0, skips, "the host skips");
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Skipping a cutscene: every player holds A for half a second (SkipHold), and once everyone still in has voted the
    /// cutscene ends (SkipVote). A hold only counts from a fresh press after the prompt shows, so an A still held from the
    /// loading screen never skips
    /// </summary>
    public class SkipVoteTests
    {
        [Test]
        public void Everyone_AllSeatsVoted_IsTrue()
        {
            SkipVote vote = new SkipVote(new[] { 0, 1 });
            vote.Vote(0);
            vote.Vote(1);

            Assert.IsTrue(vote.Everyone);
            Assert.AreEqual(2, vote.Voted);
            Assert.AreEqual(2, vote.Total);
        }

        [Test]
        public void Everyone_SomeToVote_IsFalse()
        {
            SkipVote vote = new SkipVote(new[] { 0, 1 });
            vote.Vote(0);

            Assert.IsFalse(vote.Everyone);
            Assert.AreEqual(1, vote.Voted);
        }

        [Test]
        public void Vote_NotIn_OrTwice_IsRefused()
        {
            SkipVote vote = new SkipVote(new[] { 0, 1 });

            Assert.IsFalse(vote.Vote(3));
            Assert.IsTrue(vote.Vote(0));
            Assert.IsFalse(vote.Vote(0));
        }

        [Test]
        public void Leave_TheLastHoldout_MakesItEveryone()
        {
            SkipVote vote = new SkipVote(new[] { 0, 1 });
            vote.Vote(0);
            vote.Leave(1);

            Assert.IsTrue(vote.Everyone);
            Assert.IsFalse(vote.IsIn(1));
            Assert.AreEqual(1, vote.Total);
        }

        [Test]
        public void Everyone_NoSeats_IsFalse()
        {
            Assert.IsFalse(new SkipVote(new int[0]).Everyone);
        }

        [Test]
        public void Hold_HalfASecondAfterThePrompt_Votes()
        {
            SkipHold hold = new SkipHold();
            Assert.IsFalse(hold.Tick(false, 0.1f, true), "up: armed");

            for (int i = 0; i < 4; i++)
                Assert.IsFalse(hold.Tick(true, 0.1f, true), "still holding");
            Assert.IsTrue(hold.Tick(true, 0.1f, true), "half a second");
        }

        [Test]
        public void Hold_HeldSinceBeforeThePrompt_DoesNotCount()
        {
            SkipHold hold = new SkipHold();
            for (int i = 0; i < 20; i++)
                Assert.IsFalse(hold.Tick(true, 0.1f, true), "A held from the loading screen");

            hold.Tick(false, 0.1f, true);
            bool voted = false;
            for (int i = 0; i < 5; i++)
                voted |= hold.Tick(true, 0.1f, true);
            Assert.IsTrue(voted, "a fresh press counts");
        }

        [Test]
        public void Tap_ShorterThanTheHold_DoesNothing()
        {
            SkipHold hold = new SkipHold();
            hold.Tick(false, 0.1f, true);
            bool voted = false;
            for (int round = 0; round < 3; round++)
            {
                for (int i = 0; i < 3; i++)
                    voted |= hold.Tick(true, 0.1f, true);
                voted |= hold.Tick(false, 0.1f, true);
            }

            Assert.IsFalse(voted);
        }

        [Test]
        public void Hold_BeforeThePromptShows_DoesNotCount()
        {
            SkipHold hold = new SkipHold();
            hold.Tick(false, 0.1f, false);
            bool voted = false;
            for (int i = 0; i < 10; i++)
                voted |= hold.Tick(true, 0.1f, false);

            Assert.IsFalse(voted);
        }

        [Test]
        public void Hold_OnceVoted_NeverAgain()
        {
            SkipHold hold = new SkipHold();
            hold.Tick(false, 0.1f, true);
            hold.Tick(true, SkipHold.HOLD, true);

            hold.Tick(false, 0.1f, true);
            Assert.IsFalse(hold.Tick(true, SkipHold.HOLD, true));
        }
    }
}

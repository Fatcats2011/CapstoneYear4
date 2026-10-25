using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The results screen's rematch vote: every player still in votes Rematch, or the countdown takes out whoever didn't
    /// </summary>
    public class RematchVoteTests
    {
        [Test]
        public void Decide_EveryoneVoted_RematchesAtOnce()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Vote(0);
            vote.Vote(1);

            Assert.AreEqual(RematchOutcome.Rematch, vote.Decide(1));
            CollectionAssert.AreEqual(new[] { 0, 1 }, vote.Playing);
            CollectionAssert.IsEmpty(vote.Out);
        }

        [Test]
        public void Decide_SomeStillToVote_Waits()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Vote(0);

            Assert.AreEqual(RematchOutcome.Waiting, vote.Decide(5));
        }

        [Test]
        public void Vote_ASeatNotIn_IsRefused()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);

            Assert.IsFalse(vote.Vote(3));
        }

        [Test]
        public void Vote_Twice_IsRefusedTheSecondTime()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);

            Assert.IsTrue(vote.Vote(0));
            Assert.IsFalse(vote.Vote(0));
        }

        [Test]
        public void Leave_OneOfTwo_TheOtherRematchesAlone()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Leave(1);
            vote.Vote(0);

            Assert.AreEqual(RematchOutcome.Rematch, vote.Decide(1));
            CollectionAssert.AreEqual(new[] { 0 }, vote.Playing);
            CollectionAssert.AreEqual(new[] { 1 }, vote.Out);
            Assert.IsFalse(vote.IsIn(1));
        }

        [Test]
        public void Leave_Everyone_GoesToTheLobby()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Leave(0);
            vote.Leave(1);

            Assert.AreEqual(RematchOutcome.Lobby, vote.Decide(1));
        }

        [Test]
        public void TakeBack_ThenTimeRunsOut_TakesThatSeatOut()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Vote(0);
            vote.Vote(1);
            Assert.IsTrue(vote.TakeBack(1));
            Assert.IsFalse(vote.HasVoted(1));

            Assert.AreEqual(RematchOutcome.Rematch, vote.Decide(16));
            CollectionAssert.AreEqual(new[] { 1 }, vote.Out);
        }

        [Test]
        public void TakeBack_WithoutAVote_IsRefused()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);

            Assert.IsFalse(vote.TakeBack(0));
        }

        [Test]
        public void Decide_TimeRunsOutWithSomeVotes_RematchesThoseAndTakesTheRestOut()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1, 2 }, 0);
            vote.Vote(2);

            Assert.AreEqual(RematchOutcome.Rematch, vote.Decide(RematchVote.SECONDS));
            CollectionAssert.AreEqual(new[] { 2 }, vote.Playing);
            CollectionAssert.AreEqual(new[] { 0, 1 }, vote.Out);
        }

        [Test]
        public void Decide_TimeRunsOutWithNoVotes_GoesToTheLobby()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);

            Assert.AreEqual(RematchOutcome.Lobby, vote.Decide(RematchVote.SECONDS));
        }

        [Test]
        public void SecondsLeft_CountsDownAndStopsAtZero()
        {
            RematchVote vote = new RematchVote(new[] { 0 }, 10);

            Assert.AreEqual(15, vote.SecondsLeft(10));
            Assert.AreEqual(15, vote.SecondsLeft(10.2));
            Assert.AreEqual(1, vote.SecondsLeft(24.5));
            Assert.AreEqual(0, vote.SecondsLeft(30));
        }

        [Test]
        public void Decide_OnceDecided_LaterPressesChangeNothing()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Vote(0);
            vote.Vote(1);
            Assert.AreEqual(RematchOutcome.Rematch, vote.Decide(1));

            Assert.IsFalse(vote.TakeBack(0));
            vote.Leave(0);

            Assert.AreEqual(RematchOutcome.Rematch, vote.Decide(99));
            CollectionAssert.AreEqual(new[] { 0, 1 }, vote.Playing);
            CollectionAssert.IsEmpty(vote.Out);
        }

        [Test]
        public void Leave_TheHost_SendsEveryoneToTheLobby()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0, hostSeat: 0);
            vote.Vote(1);
            vote.Leave(0);

            Assert.AreEqual(RematchOutcome.Lobby, vote.Decide(1));
        }

        [Test]
        public void Decide_TimeRunsOutWithoutTheHostsVote_GoesToTheLobby()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0, hostSeat: 0);
            vote.Vote(1);

            Assert.AreEqual(RematchOutcome.Lobby, vote.Decide(RematchVote.SECONDS));
        }

        [Test]
        public void Decide_NoSeats_GoesToTheLobby()
        {
            Assert.AreEqual(RematchOutcome.Lobby, new RematchVote(new int[0], 0).Decide(0));
        }
    }
}

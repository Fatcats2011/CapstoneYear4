using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The rematch vote as the host shows it to every machine (RematchBoard), the checks a client makes on it, and the
    /// vote's messages on their way out of the game code (RematchSync)
    /// </summary>
    public class RematchBoardTests
    {
        int arrivedSeat = -1;
        bool arrivedRematch;

        [TearDown]
        public void TearDown()
        {
            RematchSync.Reset();
        }

        [Test]
        public void From_ReflectsSeatsVotesAndTime()
        {
            RematchVote vote = new RematchVote(new[] { 0, 2 }, 0);
            vote.Vote(2);

            RematchBoard board = RematchBoard.From(vote, 3);

            Assert.AreEqual(0b0101, board.InMask);
            Assert.AreEqual(0b0100, board.VotedMask);
            Assert.AreEqual(12, board.SecondsLeft);
            Assert.AreEqual(RematchOutcome.Waiting, board.Outcome);
            Assert.IsTrue(board.IsIn(0));
            Assert.IsFalse(board.IsIn(1));
            Assert.IsTrue(board.HasVoted(2));
            Assert.IsFalse(board.HasVoted(0));
        }

        [Test]
        public void From_ADecidedVote_CarriesTheOutcome()
        {
            RematchVote vote = new RematchVote(new[] { 0, 1 }, 0);
            vote.Vote(0);
            vote.Vote(1);
            vote.Decide(1);

            Assert.AreEqual(RematchOutcome.Rematch, RematchBoard.From(vote, 1).Outcome);
        }

        [Test]
        public void Sane_RefusesSeatsBeyondFour_VotesOutsideTheSeats_AndTooMuchTime()
        {
            Assert.IsFalse(NetChecks.Sane(new RematchBoard { InMask = 0b10000 }), "a fifth seat");
            Assert.IsFalse(NetChecks.Sane(new RematchBoard { InMask = 0b0001, VotedMask = 0b0010 }), "a vote from a seat not in");
            Assert.IsFalse(NetChecks.Sane(new RematchBoard { InMask = 0b0001, SecondsLeft = 200 }), "a countdown longer than the vote");
            Assert.IsFalse(NetChecks.Sane(new RematchBoard { InMask = 0b0001, Outcome = (RematchOutcome)9 }), "an outcome nobody sends");
        }

        [Test]
        public void Sane_AcceptsAnHonestBoard()
        {
            Assert.IsTrue(NetChecks.Sane(new RematchBoard { InMask = 0b1011, VotedMask = 0b0011, SecondsLeft = 15 }));
        }

        [Test]
        public void RematchSync_ArriveVote_RaisesVoteArrived()
        {
            RematchSync.VoteArrived += Arrived;

            RematchSync.ArriveVote(2, true);

            Assert.AreEqual(2, arrivedSeat);
            Assert.IsTrue(arrivedRematch);
        }

        void Arrived(int seat, bool rematch)
        {
            arrivedSeat = seat;
            arrivedRematch = rematch;
        }
    }
}

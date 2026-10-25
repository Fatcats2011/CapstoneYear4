using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The cutscene skip vote as the host shows it to every machine (SkipBoard), the checks a client makes on it, and the
    /// vote's messages on their way out of the game code (SkipSync)
    /// </summary>
    public class SkipBoardTests
    {
        static int arrived = -1;

        [TearDown]
        public void TearDown()
        {
            SkipSync.Reset();
            arrived = -1;
        }

        static void Arrived(int seat) { arrived = seat; }

        [Test]
        public void From_ReflectsSeatsAndVotes()
        {
            SkipVote vote = new SkipVote(new[] { 0, 2, 3 });
            vote.Vote(2);

            SkipBoard board = SkipBoard.From(vote);

            Assert.AreEqual(0b1101, board.InMask);
            Assert.AreEqual(0b0100, board.VotedMask);
            Assert.AreEqual(1, board.Voted);
            Assert.AreEqual(3, board.Total);
            Assert.IsTrue(board.HasVoted(2));
            Assert.IsFalse(board.IsIn(1));
        }

        [Test]
        public void Sane_RefusesAFifthSeat_AndAVoteOutsideTheSeats()
        {
            Assert.IsFalse(NetChecks.Sane(new SkipBoard { InMask = 0b10000 }));
            Assert.IsFalse(NetChecks.Sane(new SkipBoard { InMask = 0b0001, VotedMask = 0b0010 }));
        }

        [Test]
        public void Sane_AcceptsAnHonestBoard()
        {
            Assert.IsTrue(NetChecks.Sane(new SkipBoard { InMask = 0b0111, VotedMask = 0b0101 }));
        }

        [Test]
        public void SkipSync_ArriveVote_RaisesVoteArrived()
        {
            SkipSync.VoteArrived += Arrived;

            SkipSync.ArriveVote(3);

            Assert.AreEqual(3, arrived);
        }
    }
}

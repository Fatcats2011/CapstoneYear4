using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The awards as the host shares them with every machine (AwardBoard), the checks a client makes on them, and the
    /// last ones kept for a results screen that opens later (AwardSync)
    /// </summary>
    public class AwardBoardTests
    {
        static int shared;
        static int arrived;

        [TearDown]
        public void TearDown()
        {
            AwardSync.Reset();
            shared = arrived = 0;
        }

        static void Shared(AwardBoard board) { shared++; }
        static void Arrived(AwardBoard board) { arrived++; }

        static AwardBoard ABoard()
        {
            MatchStats stats = new MatchStats();
            stats.Stole(1, GameState.Begin);
            stats.Stole(1, GameState.Begin);
            return AwardBoard.From(stats, new[] { 0, 1 });
        }

        [Test]
        public void From_EachSeatsAwardAndCount()
        {
            AwardBoard board = ABoard();

            Assert.AreEqual(Award.Tourist, board.AwardOf(0));
            Assert.AreEqual(Award.Kleptoghost, board.AwardOf(1));
            Assert.AreEqual(2, board.CountOf(1));
            Assert.AreEqual(Award.None, board.AwardOf(2), "nobody in seat 3");
            Assert.AreEqual(Award.None, board.AwardOf(7));
        }

        [Test]
        public void Sane_TheHostsBoard()
        {
            Assert.IsTrue(NetChecks.Sane(ABoard()));
        }

        [Test]
        public void Sane_AnAwardTheGameDoesntKnow_IsRefused()
        {
            AwardBoard board = ABoard();
            board.Award2 = (Award)99;

            Assert.IsFalse(NetChecks.Sane(board));
        }

        [Test]
        public void Sane_AHugeCount_IsRefused()
        {
            AwardBoard board = ABoard();
            board.Count1 = AwardBoard.MAX_COUNT + 1;

            Assert.IsFalse(NetChecks.Sane(board));
        }

        [Test]
        public void Decide_KeepsIt_AndSharesIt()
        {
            AwardSync.Shared += Shared;

            AwardSync.Decide(ABoard());

            Assert.IsTrue(AwardSync.HasLast);
            Assert.AreEqual(Award.Kleptoghost, AwardSync.Last.AwardOf(1));
            Assert.AreEqual(1, shared);
        }

        [Test]
        public void Arrive_KeepsIt()
        {
            AwardSync.Arrived += Arrived;

            AwardSync.Arrive(ABoard());

            Assert.IsTrue(AwardSync.HasLast);
            Assert.AreEqual(1, arrived);
        }

        [Test]
        public void Clear_ForgetsIt()
        {
            AwardSync.Decide(ABoard());

            AwardSync.Clear();

            Assert.IsFalse(AwardSync.HasLast);
        }
    }
}

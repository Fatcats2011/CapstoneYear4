using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

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
        public void Money_PerSeatAndSource()
        {
            AwardBoard board = ABoard();

            board.SetMoney(0, MoneySource.Deliveries, 520);
            board.SetMoney(0, MoneySource.Mode, 3);
            board.SetMoney(3, MoneySource.Golden, 80);

            Assert.AreEqual(520, board.MoneyOf(0, MoneySource.Deliveries));
            Assert.AreEqual(3, board.MoneyOf(0, MoneySource.Mode));
            Assert.AreEqual(0, board.MoneyOf(0, MoneySource.Steals));
            Assert.AreEqual(80, board.MoneyOf(3, MoneySource.Golden));
            Assert.AreEqual(0, board.MoneyOf(1, MoneySource.Deliveries));
            Assert.AreEqual(0, board.MoneyOf(7, MoneySource.Deliveries), "no such seat");
        }

        [Test]
        public void Money_OverTheMost_IsClamped_NotWrapped()
        {
            AwardBoard board = ABoard();

            board.SetMoney(1, MoneySource.Deliveries, 70000);
            board.SetMoney(1, MoneySource.Steals, -4);

            Assert.AreEqual(65535, board.MoneyOf(1, MoneySource.Deliveries));
            Assert.AreEqual(0, board.MoneyOf(1, MoneySource.Steals));
        }

        [Test]
        public void Money_SurvivesTheNetworkRoundTrip()
        {
            AwardBoard sent = ABoard();
            for (int seat = 0; seat < 4; seat++)
                for (int source = 0; source < MoneyRules.SOURCES; source++)
                    sent.SetMoney(seat, (MoneySource)source, seat * 1000 + source * 7 + 1);

            using (FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out AwardBoard received);
                    Assert.AreEqual(sent, received);
                    Assert.AreEqual(2000 + 14 + 1, received.MoneyOf(2, MoneySource.Golden));
                    Assert.AreEqual(Award.Kleptoghost, received.AwardOf(1));
                }
            }
        }

        [Test]
        public void Equals_NoticesMoney()
        {
            AwardBoard a = ABoard();
            AwardBoard b = ABoard();
            Assert.AreEqual(a, b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());

            b.SetMoney(2, MoneySource.Steals, 1);

            Assert.AreNotEqual(a, b);
        }

        [Test]
        public void From_WithATally_CopiesItsAmounts()
        {
            MoneyTally tally = new MoneyTally();
            tally.Add(0, MoneySource.Deliveries, 120);
            tally.Add(1, MoneySource.Steals, 7);
            tally.Add(1, MoneySource.Golden, 70000);

            AwardBoard board = AwardBoard.From(new MatchStats(), new[] { 0, 1 }, tally);

            Assert.AreEqual(120, board.MoneyOf(0, MoneySource.Deliveries));
            Assert.AreEqual(7, board.MoneyOf(1, MoneySource.Steals));
            Assert.AreEqual(65535, board.MoneyOf(1, MoneySource.Golden));
        }

        [Test]
        public void From_WithoutATally_HasNoMoney()
        {
            AwardBoard board = ABoard();

            for (int seat = 0; seat < 4; seat++)
                for (int source = 0; source < MoneyRules.SOURCES; source++)
                    Assert.AreEqual(0, board.MoneyOf(seat, (MoneySource)source));
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

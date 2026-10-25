using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Connection info online (PingRules, PingBoard, PingSync): each machine's round trip as the host measures it, the
    /// bars it shows, the checks a client makes on the host's board, and the last board kept for the pause menu
    /// </summary>
    public class PingRulesTests
    {
        static int arrived;
        static void Arrived(PingBoard board) { arrived++; }

        [TearDown]
        public void TearDown()
        {
            PingSync.Reset();
            arrived = 0;
        }

        [TestCase(0, 3)]
        [TestCase(79, 3)]
        [TestCase(80, 2)]
        [TestCase(150, 2)]
        [TestCase(151, 1)]
        [TestCase(900, 1)]
        [TestCase(-1, 0)]
        public void Bars_ByRoundTrip(int ms, int bars)
        {
            Assert.AreEqual(bars, PingRules.Bars(ms));
        }

        [Test]
        public void Line_BarsAndMilliseconds()
        {
            Assert.AreEqual(PingRules.LIT + "III" + PingRules.LIT + " 24MS", PingRules.Line(24));
            Assert.AreEqual(PingRules.LIT + "II" + PingRules.DIM + "I" + PingRules.LIT + " 96MS", PingRules.Line(96));
            Assert.AreEqual(PingRules.LIT + "I" + PingRules.DIM + "II" + PingRules.LIT + " 180MS", PingRules.Line(180));
        }

        [Test]
        public void Line_UsesOnlyCharactersTheGamesFontHas()
        {
            TMPro.TMP_FontAsset bangers = UnityEngine.Resources.Load<TMPro.TMP_FontAsset>("Fonts & Materials/Bangers SDF");
            Assert.IsNotNull(bangers);
            string shown = System.Text.RegularExpressions.Regex.Replace(PingRules.Line(96), "<[^>]*>", "");
            // A dynamic atlas adds a character the font file has on first use: tryAddCharacter
            Assert.IsTrue(bangers.HasCharacters(shown, out uint[] missing, false, true),
                "missing: " + (missing != null ? string.Join(" ", missing) : ""));
            Assert.AreEqual("", PingRules.Line(-1), "unknown: nothing shown");
        }

        [Test]
        public void Board_EachSeatsRoundTrip_UnknownByDefault()
        {
            PingBoard board = new PingBoard();
            board.Set(0, 0);
            board.Set(2, 140);

            Assert.AreEqual(0, board.Of(0));
            Assert.AreEqual(140, board.Of(2));
            Assert.AreEqual(-1, board.Of(1), "nobody measured");
            Assert.AreEqual(-1, board.Of(7));
        }

        [Test]
        public void Board_AHugeRoundTrip_IsCapped()
        {
            PingBoard board = new PingBoard();
            board.Set(1, 70000);

            Assert.AreEqual(PingBoard.MAX_MS, board.Of(1));
        }

        [Test]
        public void Sane_TheHostsBoard()
        {
            PingBoard board = new PingBoard();
            board.Set(1, 50);

            Assert.IsTrue(NetChecks.Sane(board));
        }

        [Test]
        public void Sane_ARoundTripPastTheCap_IsRefused()
        {
            PingBoard board = new PingBoard { Ms2 = PingBoard.MAX_MS + 1 };

            Assert.IsFalse(NetChecks.Sane(board));
        }

        [Test]
        public void Sync_KeepsTheLast()
        {
            PingSync.Arrived += Arrived;
            PingBoard board = new PingBoard();
            board.Set(3, 33);

            PingSync.Arrive(board);

            Assert.AreEqual(33, PingSync.Last.Of(3));
            Assert.AreEqual(1, arrived);
            PingSync.Reset();
            Assert.AreEqual(-1, PingSync.Last.Of(3));
        }

        [TestCase(true, 2f, true)]
        [TestCase(true, 1.9f, false)]
        [TestCase(false, 5f, false)]
        public void Due_EveryTwoSeconds_OnTheHost(bool host, float sinceLast, bool due)
        {
            Assert.AreEqual(due, PingRules.Due(host, sinceLast));
        }
    }
}

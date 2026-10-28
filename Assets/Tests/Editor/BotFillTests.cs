using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>How many bots an online match starts with: the empty seats, unless the Bots rule is off</summary>
    public class BotFillTests
    {
        [TestCase(1, 3)]
        [TestCase(2, 2)]
        [TestCase(3, 1)]
        [TestCase(4, 0)]
        public void EmptySeats_FillUpToFour(int seated, int bots)
        {
            Assert.AreEqual(bots, BotFill.Needed(seated, true));
        }

        [TestCase(1)]
        [TestCase(3)]
        public void BotsOff_NoneAreAdded(int seated)
        {
            Assert.AreEqual(0, BotFill.Needed(seated, false));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(7)]
        public void NoPlayersOrTooMany_NoneAreAdded(int seated)
        {
            Assert.AreEqual(0, BotFill.Needed(seated, true));
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// How many seats this machine asks the host for (SeatAsker, Phase 3J): one per player here without a seat, and not
    /// again while an ask is on its way
    /// </summary>
    public class SeatAskerTests
    {
        [Test]
        public void ToAsk_OncePerUnseatedPlayer_NotAgainWhileWaiting()
        {
            SeatAsker asker = new SeatAsker();

            Assert.AreEqual(2, asker.ToAsk(2), "two players without a seat");
            Assert.AreEqual(0, asker.ToAsk(2), "their asks are on their way");
            asker.Answered(); // one seat came
            Assert.AreEqual(0, asker.ToAsk(1), "the other's ask is still on its way");
            Assert.AreEqual(1, asker.ToAsk(2), "a third player arrived");
            Assert.AreEqual(2, asker.Asking);
        }

        [Test]
        public void Answered_WithNothingAsked_StaysAtZero()
        {
            SeatAsker asker = new SeatAsker();
            asker.Answered(); // the seat a machine gets as it joins: nobody asked for it

            Assert.AreEqual(0, asker.Asking);
            Assert.AreEqual(1, asker.ToAsk(1));
        }

        [Test]
        public void Reset_ForgetsTheAsks()
        {
            SeatAsker asker = new SeatAsker();
            asker.ToAsk(3);

            asker.Reset(); // the session ended

            Assert.AreEqual(0, asker.Asking);
        }
    }
}

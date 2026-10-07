using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The match clock online: an end time in the server time every machine shares. Clients work out the time left; the
    /// host only sends the end again when its clock moved
    /// </summary>
    public class MatchClockTests
    {
        [Test]
        public void TheEnd_IsNowPlusTheTimeLeft()
        {
            Assert.AreEqual(130.5, MatchClock.EndTime(100.5, 30f), 1e-6);
        }

        [Test]
        public void Remaining_NonsenseEnds_AreZeroOrCapped()
        {
            Assert.AreEqual(0f, MatchClock.Remaining(double.NaN, 10), "not a number");
            Assert.AreEqual(MatchClock.MAX_REMAINING, MatchClock.Remaining(double.PositiveInfinity, 10), "forever");
            Assert.AreEqual(MatchClock.MAX_REMAINING, MatchClock.Remaining(1e30, 10), "far away");
            Assert.AreEqual(0f, MatchClock.Remaining(5, 10), "past");
        }

        [Test]
        public void TheTimeLeft_CountsDownToTheEnd_AndStopsAtZero()
        {
            Assert.AreEqual(20f, MatchClock.Remaining(130, 110), 1e-4f);
            Assert.AreEqual(0f, MatchClock.Remaining(130, 140), "past the end");
        }

        [Test]
        public void AClient_SeesTheHostsTimeLeft()
        {
            double end = MatchClock.EndTime(1000, 90f); // the host, with 90 s to go

            Assert.AreEqual(60f, MatchClock.Remaining(end, 1030), 1e-4f, "30 s later on any machine");
        }

        [Test]
        public void TheHost_SendsTheEndAgain_OnlyWhenItsClockMoved()
        {
            Assert.IsFalse(MatchClock.NeedsRepublish(130, 110, 20.1f), "0.1 s of drift");
            Assert.IsTrue(MatchClock.NeedsRepublish(130, 110, 45f), "a new wave's clock");
        }
    }
}

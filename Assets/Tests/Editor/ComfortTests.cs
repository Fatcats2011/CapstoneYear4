using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Reduce motion (Comfort): with it on, boosting never widens the view and the speed lines never show
    /// </summary>
    public class ComfortTests
    {
        [TearDown]
        public void TearDown()
        {
            Comfort.ReduceMotion = false;
        }

        [Test]
        public void Off_TheBoostWidensTheView()
        {
            Assert.AreEqual(90f, Comfort.TargetFov(true, 90f, 70f));
            Assert.AreEqual(70f, Comfort.TargetFov(false, 90f, 70f));
        }

        [Test]
        public void On_TheViewStaysAsItIs()
        {
            Comfort.ReduceMotion = true;

            Assert.AreEqual(70f, Comfort.TargetFov(true, 90f, 70f));
        }

        [Test]
        public void Off_TheSpeedLinesShow()
        {
            Assert.AreEqual(0.85f, Comfort.SpeedLines(0.85f));
        }

        [Test]
        public void On_NoSpeedLines()
        {
            Comfort.ReduceMotion = true;

            Assert.AreEqual(Comfort.NO_SPEED_LINES, Comfort.SpeedLines(0.8f));
        }
    }
}

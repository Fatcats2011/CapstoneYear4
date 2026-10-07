using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Measuring the frame rate (FrameStats): the average and the 1% low over the latest frames, and the "Perf:" line the
    /// opt-in session log keeps, the same in any culture. See docs/performance.md
    /// </summary>
    public class FrameStatsTests
    {
        [Test]
        public void AverageAndOnePercentLow()
        {
            FrameStats stats = new FrameStats();
            for (int i = 0; i < 99; i++)
                stats.Add(1f / 120f);
            stats.Add(1f / 30f);

            Assert.AreEqual(100, stats.Count);
            Assert.AreEqual(100f / (99f / 120f + 1f / 30f), stats.AverageFps, 0.1f, "frames over their total time");
            Assert.AreEqual(30f, stats.OnePercentLowFps, 0.1f, "the slowest 1%: one frame of 1/30 s");
        }

        [Test]
        public void KeepsOnlyTheLatestFrames()
        {
            FrameStats stats = new FrameStats(10);
            for (int i = 0; i < 10; i++)
                stats.Add(1f / 30f);
            for (int i = 0; i < 10; i++)
                stats.Add(1f / 60f);

            Assert.AreEqual(10, stats.Count);
            Assert.AreEqual(60f, stats.AverageFps, 0.1f, "the 30 fps frames are gone");
        }

        [Test]
        public void Empty_IsZero()
        {
            FrameStats stats = new FrameStats();

            Assert.AreEqual(0f, stats.AverageFps);
            Assert.AreEqual(0f, stats.OnePercentLowFps);
        }

        [Test]
        public void Line_InAnyCulture()
        {
            CultureInfo was = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("Perf: 4 players, 118 fps average, 92 fps 1% low, 12 cameras", FrameStats.Line(117.6f, 91.7f, 4, 12));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = was;
            }
        }
    }
}

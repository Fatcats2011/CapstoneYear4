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
        public void OnePercentLow_AllocatesNothing()
        {
            FrameStats stats = new FrameStats();
            for (int i = 0; i < 500; i++)
                stats.Add(1f / (60f + i % 7));
            float warm = stats.OnePercentLowFps; // the first call may size its buffer

            // Unity's Mono has no per-thread allocation count: the heap's growth over 100 calls shows it instead (an array
            // per call would be 200 KB)
            long before = System.GC.GetTotalMemory(false);
            for (int i = 0; i < 100; i++)
                warm += stats.OnePercentLowFps;
            long allocated = System.GC.GetTotalMemory(false) - before;

            Assert.Less(allocated, 4096, "the F6 overlay asks for it every OnGUI");
            Assert.Greater(warm, 0f);
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

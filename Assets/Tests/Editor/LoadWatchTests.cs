using System.Globalization;
using System.Threading;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Timing a match load (LoadWatch): how long until the scene was ready here, and the longest frame from the load's start
    /// until just after the scene showed
    /// </summary>
    public class LoadWatchTests
    {
        [Test]
        public void Report_GivesTheTimeToReady_AndTheLongestFrame_TheSceneShowingIncluded()
        {
            LoadWatch watch = new LoadWatch();
            watch.Start(MatchScene.Game, 10f);
            Assert.IsNull(watch.Tick(10.5f));
            Assert.IsNull(watch.Tick(14.5f));       // a 4 s frame
            watch.Ready(15f);                       // ready after 5 s
            Assert.IsNull(watch.Tick(15.2f));
            watch.Up();
            Assert.IsNull(watch.Tick(21.2f));       // the scene's activation: a 6 s frame
            Assert.AreEqual(LoadWatch.Report(MatchScene.Game, 5f, 6f), watch.Tick(21.3f));
            Assert.IsFalse(watch.Running);
            Assert.IsNull(watch.Tick(30f), "it reports once");
        }

        [Test]
        public void Tick_BeforeAnyLoad_ReportsNothing()
        {
            Assert.IsNull(new LoadWatch().Tick(1f));
        }

        [Test]
        public void Report_ReadsTheSameEverywhere()
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); // writes 12,3
            try
            {
                Assert.AreEqual("Online: the Game scene was ready here after 12.3 s; its longest frame took 4.0 s",
                    LoadWatch.Report(MatchScene.Game, 12.34f, 4f));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }
    }
}

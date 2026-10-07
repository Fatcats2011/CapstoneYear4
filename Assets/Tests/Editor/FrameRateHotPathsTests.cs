using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Per-frame work made cheaper without changing what players see (Phase 4D): the boost's wide view (FovHold, which
    /// replaced a coroutine started every frame), the compass's nearest-last ordering (sorted in place, the same order as
    /// before), and the match timer's text (rebuilt only when the whole second changes). See docs/performance.md
    /// </summary>
    public class FrameRateHotPathsTests
    {
        [Test]
        public void Boosting_HoldsTheWideView_WithoutACoroutinePerFrame()
        {
            FovHold hold = new FovHold(0.3f);

            // Boosting from 0 s to 1 s, asked every frame (as the boost did, each time starting a coroutine)
            for (float t = 0f; t <= 1f; t += 1f / 60f)
            {
                hold.Request(t);
                Assert.AreEqual(t >= 0.3f - 1e-4f, hold.Active(t), "at " + t + " s: wide 0.3 s after the boost starts");
            }

            Assert.IsTrue(hold.Active(1.2f), "still wide just after the boost (the last request's delay)");
            Assert.IsFalse(hold.Active(1.35f), "back to normal 0.3 s after the boost ends");
        }

        [Test]
        public void Boosting_AgainLater_WaitsAgain()
        {
            FovHold hold = new FovHold(0.3f);
            hold.Request(0f);
            hold.Request(0.5f);

            hold.Request(5f); // a new boost, long after, held on
            hold.Request(5.2f);

            Assert.IsFalse(hold.Active(5.25f), "a new boost waits its 0.3 s again");
            Assert.IsTrue(hold.Active(5.31f));
        }

        [Test]
        public void Compass_OrdersIconsByDistance_AsBefore()
        {
            List<float> distances = new List<float> { 30f, 5f, 18f, 18f, 120f, 5f };
            List<int> items = Enumerable.Range(0, distances.Count).ToList();
            List<int> before = items.OrderByDescending(i => distances[i]).ToList(); // what the compass did

            Compass.SortByDistanceDescending(items, i => distances[i]);

            CollectionAssert.AreEqual(before, items, "the same order, ties as before");
        }

        [Test]
        public void HornRedraw_SkipsTinyChanges_ButNeverReachingFullOrEmpty()
        {
            Assert.IsFalse(PhaseIndicator.NeedsRedraw(0.50005f, 0.5f), "a change too small to see");
            Assert.IsTrue(PhaseIndicator.NeedsRedraw(1f, 0.99995f), "reaching full: the boost-ready cue and colour");
            Assert.IsTrue(PhaseIndicator.NeedsRedraw(0.99995f, 1f), "leaving full");
            Assert.IsTrue(PhaseIndicator.NeedsRedraw(0f, 0.00005f), "reaching empty");
            Assert.IsTrue(PhaseIndicator.NeedsRedraw(0.3f, float.NaN), "nothing drawn yet");
            Assert.IsFalse(PhaseIndicator.NeedsRedraw(1f, 1f), "still full");
        }

        [Test]
        public void CompassDistanceText_IsWholeFeet()
        {
            Assert.AreEqual("5ft", CompassIconUI.DistanceText(5));
            Assert.AreEqual("120ft", CompassIconUI.DistanceText(120));
            Assert.AreEqual("0ft", CompassIconUI.DistanceText(0));
        }

        [Test]
        public void TimerText_IsMinutesAndSeconds_AsBefore()
        {
            foreach (float seconds in new[] { 0f, 9.99f, 59.5f, 60f, 65.4f, 599.9f })
                Assert.AreEqual(TimeSpan.FromSeconds(seconds).ToString("m\\:ss"), DynamicNumberUI.TimerText(seconds), seconds + " s");
        }
    }
}

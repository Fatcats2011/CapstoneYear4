using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// When a ghost shows off a new colour or hat (RestyleWatch): on DONE, once the look settles in Local Play, and as
    /// another machine's player closes their pop-up. Never on every scroll (playtest, 2026-11-25)
    /// </summary>
    public class RestyleWatchTests
    {
        [Test]
        public void Take_AChangeSinceOpening_Once()
        {
            var watch = new RestyleWatch();
            watch.Mark(0, 0);

            Assert.IsFalse(watch.Take(0, 0), "nothing changed");
            watch.Mark(0, 0);
            Assert.IsTrue(watch.Take(2, 0), "a new colour");
            Assert.IsFalse(watch.Take(2, 0), "already taken");
            Assert.IsTrue(watch.Take(2, 1), "a new hat");
        }

        [Test]
        public void Take_ScrolledAwayAndBack_IsNoChange()
        {
            var watch = new RestyleWatch();
            watch.Mark(3, 4);

            Assert.IsFalse(watch.Take(3, 4));
        }

        [Test]
        public void Take_NothingMarked_IsNoChange()
        {
            Assert.IsFalse(new RestyleWatch().Take(5, 5));
        }

        [Test]
        public void Settled_OnlyOnceTheLookStaysPut()
        {
            var watch = new RestyleWatch();
            watch.Mark(0, 0);

            Assert.IsFalse(watch.Settled(0, 0, 0f), "nothing changed");
            Assert.IsFalse(watch.Settled(1, 0, 1f), "scrolling");
            Assert.IsFalse(watch.Settled(2, 0, 1.5f), "still scrolling");
            Assert.IsFalse(watch.Settled(2, 0, 1.5f + RestyleWatch.SETTLE - 0.01f), "not quite settled");
            Assert.IsTrue(watch.Settled(2, 0, 1.5f + RestyleWatch.SETTLE + 0.01f), "settled");
            Assert.IsFalse(watch.Settled(2, 0, 10f), "once");
        }

        [Test]
        public void Settled_BackWhereItWas_ShowsNothingOff()
        {
            var watch = new RestyleWatch();
            watch.Mark(0, 0);

            watch.Settled(1, 0, 0f);
            watch.Settled(0, 0, 0.5f);
            Assert.IsFalse(watch.Settled(0, 0, 5f));
        }

        [Test]
        public void Settled_AfterReadyingMarksTheLook_ShowsNothingOff()
        {
            var watch = new RestyleWatch();
            watch.Mark(0, 0);

            watch.Settled(0, 1, 0f);
            watch.Mark(0, 1); // A: the ghost cheers instead
            Assert.IsFalse(watch.Settled(0, 1, 5f));
        }

        [Test]
        public void Closed_ShowsOffAsThePopUpCloses_WhenTheLookChanged()
        {
            var watch = new RestyleWatch();

            Assert.IsFalse(watch.Closed(false, 0, 0, false), "their first look");
            Assert.IsFalse(watch.Closed(true, 0, 0, false), "X: open");
            Assert.IsFalse(watch.Closed(true, 1, 0, false), "scrolling shows the colour, no emote");
            Assert.IsFalse(watch.Closed(true, 1, 2, false));
            Assert.IsTrue(watch.Closed(false, 1, 2, false), "DONE");
            Assert.IsFalse(watch.Closed(false, 1, 2, false), "once");

            Assert.IsFalse(watch.Closed(true, 1, 2, false), "open again");
            Assert.IsFalse(watch.Closed(false, 1, 2, false), "closed with nothing changed");
        }

        [Test]
        public void Closed_ByReadying_Cheers_NoShowOff()
        {
            var watch = new RestyleWatch();
            watch.Closed(true, 0, 0, false);
            watch.Closed(true, 3, 0, false);

            Assert.IsFalse(watch.Closed(false, 3, 0, true), "A from the pop-up readies: the ready emote plays");
        }
    }
}

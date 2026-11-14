using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What the mode watcher announces (ModeWatcher, ModeFeedback): the lines a change of state gives, each once; nothing
    /// for a state it has already seen, until it forgets; and the 30-second warning, once, in the timed modes only. Edit
    /// mode: nothing starts, so the tests hand the watcher the states and the clock directly
    /// </summary>
    public class ModeWatcherTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<ModeLine> said = new List<ModeLine>();
        int warned;
        ModeWatcher watcher;

        void Say(ModeLine line)
        {
            said.Add(line);
        }

        void Warn()
        {
            warned++;
        }

        [SetUp]
        public void SetUp()
        {
            said.Clear();
            warned = 0;
            ModeFeedback.Said += Say;
            ModeFeedback.Warned += Warn;
            watcher = objects.Add<ModeWatcher>();
        }

        [TearDown]
        public void TearDown()
        {
            ModeFeedback.Said -= Say;
            ModeFeedback.Warned -= Warn;
            objects.DestroyAll();
        }

        static ModeView Crown(int seat)
        {
            ModeView view = ModeView.Nothing;
            view.Mode = GameMode.KingsCrown;
            view.Players = 0b1111;
            view.Crown = seat;
            return view;
        }

        [Test]
        public void ANewCrownHolder_IsSaid()
        {
            watcher.See(Crown(2));

            Assert.AreEqual(1, said.Count);
            Assert.AreEqual(2, said[0].Seat);
            Assert.AreEqual(ModeLineKind.CrownYours, said[0].Kind);
            Assert.AreEqual(ModeMatch.NONE, said[0].Other);
        }

        [Test]
        public void TheSameStateAgain_SaysNothing()
        {
            watcher.See(Crown(2));
            said.Clear();

            watcher.See(Crown(2));

            Assert.AreEqual(0, said.Count);
        }

        [Test]
        public void AfterForgetting_TheSameStateIsSaidAgain()
        {
            watcher.See(Crown(2));
            said.Clear();

            watcher.Forget();
            watcher.See(Crown(2));

            Assert.AreEqual(1, said.Count);
            Assert.AreEqual(ModeLineKind.CrownYours, said[0].Kind);
        }

        [Test]
        public void TheWarning_ComesOnceWhenTheClockCrossesThirtySeconds()
        {
            watcher.SeeClock(GameMode.KingsCrown, 31f, true);
            watcher.SeeClock(GameMode.KingsCrown, 29.5f, true);

            Assert.AreEqual(1, warned);

            watcher.SeeClock(GameMode.KingsCrown, 29f, true);

            Assert.AreEqual(1, warned, "once");
        }

        [Test]
        public void TheWarning_IsForTheTimedModesOnly()
        {
            watcher.SeeClock(GameMode.Standard, 31f, true);
            watcher.SeeClock(GameMode.Standard, 29f, true);

            Assert.AreEqual(0, warned);
        }

        [Test]
        public void AClockNotRunning_ForgetsWhatItSaw()
        {
            watcher.SeeClock(GameMode.Infection, 31f, true);
            watcher.SeeClock(GameMode.Infection, 31f, false);
            watcher.SeeClock(GameMode.Infection, 29f, true);

            Assert.AreEqual(0, warned, "no crossing was seen");
        }

        [Test]
        public void NoMatch_IsNothing()
        {
            ModeView view = ModeWatcher.ViewOf(null);
            ModeView nothing = ModeView.Nothing;

            Assert.AreEqual(nothing.Mode, view.Mode);
            Assert.AreEqual(nothing.Players, view.Players);
            Assert.AreEqual(nothing.Crown, view.Crown);
            Assert.AreEqual(nothing.Potato, view.Potato);
            Assert.AreEqual(nothing.Infected, view.Infected);
            Assert.AreEqual(nothing.Out, view.Out);
        }
    }
}

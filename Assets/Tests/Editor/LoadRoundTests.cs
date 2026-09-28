using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Host, online: one scene load across the session's machines. It's ready once every machine that was in the session
    /// when it began has the scene loaded, or has left
    /// </summary>
    public class LoadRoundTests
    {
        [Test]
        public void Ready_OnlyOnceEveryMachineHasTheSceneLoaded()
        {
            LoadRound round = new LoadRound(MatchScene.Game, new ulong[] { 0, 1, 2 });

            round.Loaded(0);
            round.Loaded(2);
            Assert.IsFalse(round.Ready, "machine 1 is still loading");

            round.Loaded(1);
            Assert.IsTrue(round.Ready);
            Assert.AreEqual(MatchScene.Game, round.Scene);
        }

        [Test]
        public void AMachineThatLeaves_IsNoLongerWaitedFor()
        {
            LoadRound round = new LoadRound(MatchScene.FinalOrder, new ulong[] { 0, 1 });

            round.Loaded(0);
            round.Left(1);

            Assert.IsTrue(round.Ready);
        }

        [Test]
        public void AMachineOutsideTheRound_OrHeardTwice_ChangesNothing()
        {
            LoadRound round = new LoadRound(MatchScene.Game, new ulong[] { 0, 1 });

            round.Loaded(0);
            round.Loaded(0);
            round.Loaded(7);

            Assert.IsFalse(round.Ready);
        }

        [Test]
        public void TheMachines_AreThoseInTheSessionWhenItBegan()
        {
            List<ulong> machines = new List<ulong> { 0 };
            LoadRound round = new LoadRound(MatchScene.Game, machines);
            machines.Add(1); // a machine connecting later isn't waited for (and there's no mid-match joining)

            round.Loaded(0);

            Assert.IsTrue(round.Ready);
        }
    }
}

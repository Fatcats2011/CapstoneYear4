using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Playtest rules, 2026-11-25: a Hot Potato pass buys time (never past a full fuse), the Speed mutator turns faster as it
    /// drives faster, and the crown's pillar of light funnels out of the crown
    /// </summary>
    public class PlaytestRulesTests
    {
        [Test]
        public void APass_AddsTime_UpToAFullFuse()
        {
            Assert.AreEqual(25f, TimedModeRules.PassedFuse(20f, 10f), 1e-4f, "5 s more");
            Assert.AreEqual(10f + TimedModeRules.FUSE_MAX, TimedModeRules.PassedFuse(48f, 10f), 1e-4f, "never more than a full fuse left");
        }

        [Test]
        public void APassInTheMatch_MovesTheFuseOn()
        {
            ModeMatch match = ModeMatch.Begin(GameMode.HotPotato, 0b0011, Vector3.zero, 0f, new System.Random(3));
            match.Tick(TimedModeRules.POTATO_DELAY + 0.1f, 0.1f, seat => Vector3.zero, (seat, points) => { });
            Assert.AreNotEqual(ModeMatch.NONE, match.PotatoSeat, "lit");
            float now = TimedModeRules.POTATO_DELAY + 20f;
            float before = match.FuseEnd;
            int holder = match.PotatoSeat;
            match.Strike(holder, 1 - holder, now);
            Assert.AreEqual(1 - holder, match.PotatoSeat, "passed");
            Assert.AreEqual(TimedModeRules.PassedFuse(before, now), match.FuseEnd, 1e-4f);
            Assert.Greater(match.FuseEnd, before);
        }

        [TestCase(1f, 1f)]
        [TestCase(2f, 1.6818f)]
        [TestCase(0.5f, 0.5946f)]
        public void Turning_ScalesWithTheSpeedMutator(float speed, float turn)
        {
            Assert.AreEqual(turn, MatchPhysics.TurnScaleFor(speed), 1e-3f);
        }

        [Test]
        public void TheBeacon_FunnelsOutOfTheCrown()
        {
            Assert.AreEqual(ModeVisuals.BEACON_FOOT, ModeVisuals.BeaconWidth(0f), 1e-4f, "narrow at the crown");
            Assert.Less(ModeVisuals.BeaconWidth(0.05f), ModeVisuals.BeaconWidth(0.15f), "widening");
            Assert.AreEqual(1f, ModeVisuals.BeaconWidth(ModeVisuals.BEACON_FUNNEL), 1e-4f, "full width a quarter of the way up");
            Assert.AreEqual(1f, ModeVisuals.BeaconWidth(1f), 1e-4f);
        }
    }
}

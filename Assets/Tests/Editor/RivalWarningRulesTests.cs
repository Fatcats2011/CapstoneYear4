using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// When a carrier is warned of a rival: which rivals count, how serious one is, which side of the screen to warn on,
    /// which of two rivals is worse, and how solid the warning mark is
    /// </summary>
    public class RivalWarningRulesTests
    {
        [Test]
        public void Counts_OnlyWhenEveryConditionHolds()
        {
            Assert.IsTrue(RivalWarningRules.Counts(true, true, true, true, false));
            Assert.IsFalse(RivalWarningRules.Counts(false, true, true, true, false));
            Assert.IsFalse(RivalWarningRules.Counts(true, false, true, true, false));
            Assert.IsFalse(RivalWarningRules.Counts(true, true, false, true, false));
            Assert.IsFalse(RivalWarningRules.Counts(true, true, true, false, false));
            Assert.IsFalse(RivalWarningRules.Counts(true, true, true, true, true));
        }

        [Test]
        public void Level_InsideTheRange_IsNear()
        {
            Assert.AreEqual(ThreatLevel.Near, RivalWarningRules.Level(29f, false, false));
        }

        [Test]
        public void Level_OutsideTheRange_IsNone()
        {
            Assert.AreEqual(ThreatLevel.None, RivalWarningRules.Level(31f, false, false));
            Assert.AreEqual(ThreatLevel.None, RivalWarningRules.Level(31f, true, true));
        }

        [Test]
        public void Level_BoostingAndClosing_IsCharging()
        {
            Assert.AreEqual(ThreatLevel.Charging, RivalWarningRules.Level(10f, true, true));
        }

        [Test]
        public void Level_BoostingButNotClosing_IsNear()
        {
            Assert.AreEqual(ThreatLevel.Near, RivalWarningRules.Level(10f, true, false));
            Assert.AreEqual(ThreatLevel.Near, RivalWarningRules.Level(10f, false, true));
        }

        [Test]
        public void Side_ADeadAheadRival_IsNone()
        {
            Assert.AreEqual(ThreatSide.None, RivalWarningRules.Side(Vector3.forward * 5f, Vector3.forward));
        }

        [Test]
        public void Side_JustInsideTheFront_IsNone()
        {
            Vector3 toRival = Quaternion.Euler(0f, 49f, 0f) * Vector3.forward;
            Assert.AreEqual(ThreatSide.None, RivalWarningRules.Side(toRival, Vector3.forward));
        }

        [Test]
        public void Side_ToTheRight_IsRight()
        {
            Assert.AreEqual(ThreatSide.Right, RivalWarningRules.Side(Vector3.right, Vector3.forward));
        }

        [Test]
        public void Side_ToTheLeft_IsLeft()
        {
            Assert.AreEqual(ThreatSide.Left, RivalWarningRules.Side(Vector3.left, Vector3.forward));
        }

        [Test]
        public void Side_PastTheBehindAngle_IsBehind()
        {
            Vector3 justBehind = Quaternion.Euler(0f, 131f, 0f) * Vector3.forward;
            Assert.AreEqual(ThreatSide.Behind, RivalWarningRules.Side(justBehind, Vector3.forward));
            Assert.AreEqual(ThreatSide.Behind, RivalWarningRules.Side(Vector3.back, Vector3.forward));
        }

        [Test]
        public void Side_ARivalOnTheCarrier_IsNone()
        {
            Assert.AreEqual(ThreatSide.None, RivalWarningRules.Side(Vector3.zero, Vector3.forward));
        }

        [Test]
        public void Side_AZeroCameraVector_IsNone()
        {
            Assert.AreEqual(ThreatSide.None, RivalWarningRules.Side(Vector3.right, Vector3.zero));
        }

        [Test]
        public void Side_AStraightAboveRival_IsNone()
        {
            Assert.AreEqual(ThreatSide.None, RivalWarningRules.Side(Vector3.up * 10f, Vector3.forward));
        }

        [Test]
        public void Side_IgnoresHeight()
        {
            Vector3 toRival = new Vector3(5f, 20f, 0f);
            Vector3 camera = new Vector3(0f, -0.5f, 1f);
            Assert.AreEqual(ThreatSide.Right, RivalWarningRules.Side(toRival, camera));
        }

        [Test]
        public void Worse_ChargingBeatsANearerNear()
        {
            Threat charging = new Threat(ThreatLevel.Charging, ThreatSide.Left, 25f);
            Threat near = new Threat(ThreatLevel.Near, ThreatSide.Right, 5f);
            Assert.AreEqual(ThreatLevel.Charging, RivalWarningRules.Worse(near, charging).Level);
            Assert.AreEqual(ThreatLevel.Charging, RivalWarningRules.Worse(charging, near).Level);
        }

        [Test]
        public void Worse_OfTwoEqualLevels_IsTheNearer()
        {
            Threat close = new Threat(ThreatLevel.Near, ThreatSide.Left, 8f);
            Threat far = new Threat(ThreatLevel.Near, ThreatSide.Right, 20f);
            Assert.AreEqual(8f, RivalWarningRules.Worse(far, close).Distance);
            Assert.AreEqual(8f, RivalWarningRules.Worse(close, far).Distance);
        }

        [Test]
        public void Worse_AnythingWithASide_BeatsNothing()
        {
            Threat behind = new Threat(ThreatLevel.Near, ThreatSide.Behind, 29f);
            Assert.AreEqual(ThreatSide.Behind, RivalWarningRules.Worse(Threat.Nothing, behind).Side);
            Assert.AreEqual(ThreatSide.Behind, RivalWarningRules.Worse(behind, Threat.Nothing).Side);
        }

        [Test]
        public void Worse_ARivalOnScreen_NeverBeatsOneOffScreen()
        {
            Threat onScreen = new Threat(ThreatLevel.Charging, ThreatSide.None, 2f);
            Threat offScreen = new Threat(ThreatLevel.Near, ThreatSide.Left, 29f);
            Assert.AreEqual(ThreatSide.Left, RivalWarningRules.Worse(onScreen, offScreen).Side);
            Assert.AreEqual(ThreatSide.Left, RivalWarningRules.Worse(offScreen, onScreen).Side);
        }

        [Test]
        public void Nothing_IsNoLevelNoSideAndFarAway()
        {
            Assert.AreEqual(ThreatLevel.None, Threat.Nothing.Level);
            Assert.AreEqual(ThreatSide.None, Threat.Nothing.Side);
            Assert.AreEqual(float.MaxValue, Threat.Nothing.Distance);
        }

        [Test]
        public void Alpha_NoThreat_IsInvisible()
        {
            Assert.AreEqual(0f, RivalWarningRules.Alpha(ThreatLevel.None, 0f, false), 0.0001f);
        }

        [Test]
        public void Alpha_Near_IsSteady()
        {
            Assert.AreEqual(RivalWarningRules.NEAR_ALPHA, RivalWarningRules.Alpha(ThreatLevel.Near, 0f, false), 0.0001f);
            Assert.AreEqual(RivalWarningRules.NEAR_ALPHA, RivalWarningRules.Alpha(ThreatLevel.Near, 1.3f, false), 0.0001f);
        }

        [Test]
        public void Alpha_Charging_PulsesBetweenNearAndSolid()
        {
            Assert.AreEqual(1f, RivalWarningRules.Alpha(ThreatLevel.Charging, 0f, false), 0.0001f);
            Assert.AreEqual(RivalWarningRules.NEAR_ALPHA, RivalWarningRules.Alpha(ThreatLevel.Charging, RivalWarningRules.PULSE_SECONDS / 2f, false), 0.0001f);
            Assert.AreEqual(1f, RivalWarningRules.Alpha(ThreatLevel.Charging, RivalWarningRules.PULSE_SECONDS, false), 0.0001f);
        }

        [Test]
        public void Alpha_Charging_WithReducedMotion_IsAlwaysSolid()
        {
            Assert.AreEqual(1f, RivalWarningRules.Alpha(ThreatLevel.Charging, RivalWarningRules.PULSE_SECONDS / 2f, true), 0.0001f);
        }
    }
}

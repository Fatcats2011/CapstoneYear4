using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// What a player feels driving: a boost pad crossing counts once however many physics steps sit on it, its speed
    /// line burst is short, its sound rises with speed, and a respawn countdown shows whole seconds, never 0 or less
    /// </summary>
    public class DrivingFeedbackRulesTests
    {
        readonly TestObjects objects = new TestObjects();

        [SetUp]
        public void SetUp()
        {
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        // A scooter in the next free seat, as StealCalloutTests builds one
        BallDriving NewScooter()
        {
            GameObject player = objects.NewGameObject("Player 1");
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            return control.AddComponent<BallDriving>();
        }

        [Test]
        public void NewCrossing_FirstTouch_IsOne()
        {
            Assert.IsTrue(DrivingFeedbackRules.NewCrossing(-1f, 0f));
        }

        [Test]
        public void NewCrossing_JustAfterTheLastTouch_IsNot()
        {
            Assert.IsFalse(DrivingFeedbackRules.NewCrossing(5f, 5.1f));
            Assert.IsFalse(DrivingFeedbackRules.NewCrossing(5f, 5f + DrivingFeedbackRules.PAD_GAP - 0.01f));
        }

        [Test]
        public void NewCrossing_AfterTheGap_IsOne()
        {
            Assert.IsTrue(DrivingFeedbackRules.NewCrossing(5f, 5f + DrivingFeedbackRules.PAD_GAP));
        }

        [Test]
        public void Bursting_NeverCrossed_IsNot()
        {
            Assert.IsFalse(DrivingFeedbackRules.Bursting(-1f, 0f));
        }

        [Test]
        public void Bursting_LastsItsSeconds()
        {
            Assert.IsTrue(DrivingFeedbackRules.Bursting(3f, 3.01f));
            Assert.IsFalse(DrivingFeedbackRules.Bursting(3f, 3f + DrivingFeedbackRules.BURST_SECONDS));
        }

        [Test]
        public void Pitch_RisesWithSpeed_WithinItsRange()
        {
            float mid = (DrivingFeedbackRules.SLOW + DrivingFeedbackRules.FAST) / 2f;

            Assert.AreEqual(DrivingFeedbackRules.MIN_PITCH, DrivingFeedbackRules.Pitch(0f), 0.0001f);
            Assert.AreEqual(DrivingFeedbackRules.MIN_PITCH, DrivingFeedbackRules.Pitch(DrivingFeedbackRules.SLOW), 0.0001f);
            Assert.AreEqual((DrivingFeedbackRules.MIN_PITCH + DrivingFeedbackRules.MAX_PITCH) / 2f, DrivingFeedbackRules.Pitch(mid), 0.0001f);
            Assert.AreEqual(DrivingFeedbackRules.MAX_PITCH, DrivingFeedbackRules.Pitch(DrivingFeedbackRules.FAST), 0.0001f);
            Assert.AreEqual(DrivingFeedbackRules.MAX_PITCH, DrivingFeedbackRules.Pitch(100f), 0.0001f);
        }

        [TestCase(7f, 7)]
        [TestCase(6.01f, 7)]
        [TestCase(6f, 6)]
        [TestCase(0.2f, 1)]
        [TestCase(0f, 0)]
        [TestCase(-1f, 0)]
        public void Countdown_IsTheWholeSecondsLeft_RoundedUp(float left, int shown)
        {
            Assert.AreEqual(shown, DrivingFeedbackRules.Countdown(left));
        }

        [Test]
        public void ThePad_CountsOneCrossing_NotOnePerPhysicsStep()
        {
            BallDriving scooter = NewScooter();

            Assert.IsTrue(scooter.TouchPad(0f));
            for (float t = 0.02f; t <= 1f; t += 0.02f)
                Assert.IsFalse(scooter.TouchPad(t), "still on the pad at " + t);
        }

        [Test]
        public void ThePad_AfterTheGapOffPads_CountsAgain()
        {
            BallDriving scooter = NewScooter();
            scooter.TouchPad(0f);
            scooter.TouchPad(0.02f);

            Assert.IsFalse(scooter.TouchPad(0.02f + DrivingFeedbackRules.PAD_GAP - 0.01f));
            Assert.IsTrue(scooter.TouchPad(5f));
        }

        [Test]
        public void ThePadBurst_FollowsACrossing_ThenEnds()
        {
            BallDriving scooter = NewScooter();

            Assert.IsFalse(scooter.PadBursting(0f), "no pad crossed yet");
            scooter.TouchPad(10f);
            Assert.IsTrue(scooter.PadBursting(10.01f));
            Assert.IsFalse(scooter.PadBursting(10f + DrivingFeedbackRules.BURST_SECONDS));
        }

        [Test]
        public void ThePadJolt_WithNoRumbler_IsNoError()
        {
            BallDriving scooter = NewScooter();

            Assert.DoesNotThrow(scooter.PadJolt);
        }
    }
}

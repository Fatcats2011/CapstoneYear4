using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A scooter tilted a little levels out the short way, never tumbling over (BallDriving.LevelTurn); and a rematch's
    /// city start faces its point's order spots, level (RespawnPoint.FacingFrom). Before, a start pitched a few degrees
    /// up levelled out through 270 degrees and pushed the scooter at the sky
    /// </summary>
    public class ScooterLevelTests
    {
        const float FRAME = 1f / 60f;

        [Test]
        public void LevelTurn_TurnsByTheStepsShare()
        {
            Quaternion turned = BallDriving.LevelTurn(new Vector3(0, 90, 0), 30f, 0.5f);

            Assert.AreEqual(105f, turned.eulerAngles.y, 0.01f);
        }

        [Test]
        public void LevelTurn_PitchedUpALittle_LevelsOutTheShortWay()
        {
            Vector3 euler = new Vector3(357, 90, 0);
            for (int i = 0; i < 60; i++)
            {
                euler = BallDriving.LevelTurn(euler, 0f, FRAME).eulerAngles;
                Assert.IsTrue(euler.x > 350f || euler.x < 1f, "frame " + i + ": pitch " + euler.x + " tumbled");
            }
            Assert.AreEqual(90f, euler.y, 0.5f, "still facing the same way");
        }

        [Test]
        public void LevelTurn_FromStraightUp_ComesBackLevel()
        {
            Quaternion rotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            for (int i = 0; i < 600; i++)
                rotation = BallDriving.LevelTurn(rotation.eulerAngles, 0f, FRAME);

            Assert.Less(Vector3.Angle(rotation * Vector3.up, Vector3.up), 5f, "upright again within 10 s");
        }

        [Test]
        public void FacingFrom_TowardsTheSpot_Level()
        {
            Quaternion facing = RespawnPoint.FacingFrom(new Vector3(-24, 3, -61), new Vector3(-24, 4, -51));

            Assert.Less(Vector3.Angle(facing * Vector3.forward, Vector3.forward), 0.01f, "faces the spot, ignoring height");
        }

        [Test]
        public void FacingFrom_TheSpotOnTheSpawn_StaysLevel()
        {
            Quaternion facing = RespawnPoint.FacingFrom(new Vector3(5, 3, 5), new Vector3(5, 4, 5));

            Assert.AreEqual(Quaternion.identity, facing);
        }
    }
}

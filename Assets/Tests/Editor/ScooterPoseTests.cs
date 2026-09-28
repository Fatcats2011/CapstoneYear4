using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A scooter's pose (what another machine shows of it): read from one scooter, it puts another in the same spot, down
    /// to the model's lean. EditMode, on instances of PlayerAvatar.prefab (their scripts don't run)
    /// </summary>
    public class ScooterPoseTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        readonly List<GameObject> scooters = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject scooter in scooters)
                Object.DestroyImmediate(scooter);
            scooters.Clear();
        }

        BallDriving NewScooter()
        {
            GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR));
            scooters.Add(avatar);
            return avatar.GetComponentInChildren<BallDriving>(true);
        }

        [Test]
        public void Read_TakesTheBall_TheHeading_AndTheModelsWorldPose()
        {
            BallDriving driving = NewScooter();
            driving.Sphere.transform.position = new Vector3(10, 2, -4);
            driving.transform.rotation = Quaternion.Euler(0, 75, 0);
            driving.ScooterModel.SetPositionAndRotation(new Vector3(10, 1.1f, -4), Quaternion.Euler(-20, 80, 5));

            ScooterPose pose = ScooterPose.Read(driving);

            Assert.AreEqual(new Vector3(10, 2, -4), pose.Ball);
            Assert.AreEqual(75f, pose.Heading, 0.01f);
            Assert.Less(Vector3.Distance(new Vector3(10, 1.1f, -4), pose.ModelPosition), 0.001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(-20, 80, 5), pose.ModelRotation), 0.01f);
        }

        [Test]
        public void Apply_MovesTheBall_PutsTheScooterBelowIt_AndPosesTheModel()
        {
            BallDriving driving = NewScooter();
            ScooterPose pose = new ScooterPose
            {
                Ball = new Vector3(3, 5, 7),
                Heading = 200f,
                ModelPosition = new Vector3(3, 4.2f, 7),
                ModelRotation = Quaternion.Euler(10, 190, -3),
            };

            ScooterPose.Apply(driving, pose);

            Assert.AreEqual(pose.Ball, driving.Sphere.transform.position, "the ball");
            Assert.Less(Vector3.Distance(pose.Ball - new Vector3(0, BallDriving.SCOOTER_BELOW_BALL, 0), driving.transform.position), 0.001f, "the scooter follows its ball");
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 200, 0), driving.transform.rotation), 0.01f, "its heading");
            Assert.Less(Vector3.Distance(pose.ModelPosition, driving.ScooterModel.position), 0.001f, "the model");
            Assert.Less(Quaternion.Angle(pose.ModelRotation, driving.ScooterModel.rotation), 0.01f, "the model's lean");
        }

        [Test]
        public void APoseReadFromOneScooter_PutsAnotherInTheSameSpot()
        {
            BallDriving owner = NewScooter();
            owner.Sphere.transform.position = new Vector3(-8, 0.5f, 12);
            owner.transform.SetPositionAndRotation(new Vector3(-8, 0.5f - BallDriving.SCOOTER_BELOW_BALL, 12), Quaternion.Euler(0, 33, 0));
            owner.ScooterModel.localRotation = Quaternion.Euler(-45, 90, 0); // mid-wheelie, leaning
            BallDriving copy = NewScooter();

            ScooterPose.Apply(copy, ScooterPose.Read(owner));

            Assert.Less(Vector3.Distance(owner.transform.position, copy.transform.position), 0.001f, "the scooter");
            Assert.Less(Vector3.Distance(owner.ScooterModel.position, copy.ScooterModel.position), 0.001f, "the model");
            Assert.Less(Quaternion.Angle(owner.ScooterModel.rotation, copy.ScooterModel.rotation), 0.01f, "the model's lean");
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Another machine's scooter shows what its owner's is doing (BallDriving.ShowRemote): the boost trail starts once per
    /// boost, skid marks while it drifts on the ground, its drift tier's sparks, and the rider's speed. In the real menu
    /// scene (a scooter's scripts need its managers). Enters Play Mode (about 10 s)
    /// </summary>
    public class RemoteDrivingTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        // Counts the boosts a scooter starts (its boost trail grows on each)
        class BoostRecorder
        {
            public int Boosts;

            public BoostRecorder(BallDriving driving)
            {
                driving.OnBoostStart += OnBoost;
            }

            void OnBoost()
            {
                Boosts++;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static bool SparkOn(Transform sparks, int child)
        {
            return sparks.GetChild(child).gameObject.activeSelf;
        }

        [UnityTest]
        public IEnumerator AnotherMachinesScooter_ShowsItsOwnersBoostDriftAndSpeed()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            RemoteAvatar remote = RemoteAvatar.Create(OnlinePrefabs.Load().RemoteAvatarPrefab, null);
            PlayerInstantiate.Instance.AddRemotePlayer(remote.gameObject, 1, 5); // seated in the same frame, as online: in its company's colours
            yield return null; // its scripts start
            BallDriving driving = remote.Driving;
            BoostRecorder boosts = new BoostRecorder(driving);
            Transform sparks = driving.ScooterModel.Find("Particles"); // base, wide, flare 1, flare 2, flare 3, long
            TrailRenderer skid = (TrailRenderer)Reflect.GetField(remote.GetComponentInChildren<SkideeSkidoo>(), "frontTire");
            Animator rider = (Animator)Reflect.GetField(driving, "playerAnimator");

            // A boost: its trail starts once, however often the same flags arrive
            DriveFlags boosting = new DriveFlags(true, false, false, 0, true, false);
            driving.ShowRemote(boosting, 25f);
            driving.ShowRemote(boosting, 25f);
            Assert.IsTrue(driving.Boosting, "boosting");
            Assert.AreEqual(1, boosts.Boosts, "one boost, one trail");

            // A tier-2 drift to the right, on the ground: skid marks, and that tier's sparks
            driving.ShowRemote(new DriveFlags(false, true, true, 2, true, false), 15f);
            yield return null; // the skid marks follow in their Update
            Assert.IsFalse(driving.Boosting, "the boost ended");
            Assert.IsTrue(driving.Drifting, "drifting");
            Assert.IsTrue(skid.emitting, "skid marks");
            Assert.IsTrue(SparkOn(sparks, 0) && SparkOn(sparks, 1) && SparkOn(sparks, 2) && SparkOn(sparks, 3) && SparkOn(sparks, 5), "tier-2 sparks");
            Assert.IsFalse(SparkOn(sparks, 4), "no tier-3 flare");
            Assert.AreEqual(5f, rider.GetFloat(HashReference._speedFloat), 0.01f, "15 m/s: the rider's speed is 5 of 10");

            // Nothing going on: the sparks and skid marks stop
            driving.ShowRemote(new DriveFlags(0), 0f);
            yield return null;
            Assert.IsFalse(SparkOn(sparks, 0), "sparks off");
            Assert.IsFalse(skid.emitting, "skid marks stop");

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

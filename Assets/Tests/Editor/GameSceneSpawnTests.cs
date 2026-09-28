using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoA.Tests
{
    /// <summary>
    /// The game scene's city start points, where the players start when the tutorial is skipped (online): one for each
    /// seat, each in the open over ground that isn't water or the tutorial area (which goes a second into the first
    /// wave). Opens the game scene in the editor, which takes a while
    /// </summary>
    public class GameSceneSpawnTests
    {
        const string GAME_SCENE = "Assets/Scenes/Design Scene(Main).unity";
        const float DROP = 10f; // the furthest a player may fall from a start point onto the ground
        const float ROOM = 0.5f; // the space a start point needs around it

        Scene scene;

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }

        T Find<T>() where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T found = root.GetComponentInChildren<T>(true);
                if (found != null)
                    return found;
            }
            Assert.Fail("no " + typeof(T).Name + " in the game scene");
            return null;
        }

        [Test]
        public void CityStartPoints_OnePerSeat_EachInTheOpenOverGroundOutsideTheTutorialArea()
        {
            scene = EditorSceneManager.OpenScene(GAME_SCENE, OpenSceneMode.Additive);
            Physics.SyncTransforms();
            GameObject[] points = (GameObject[])Reflect.GetField(Find<SpawnManager>(), "nonTutorialSpawnPositions");
            Transform tutorialArea = ((GameObject)Reflect.GetField(Find<TutorialAreaDisabler>(), "TutorialGO")).transform;

            Assert.AreEqual(Constants.MAX_PLAYERS, points.Length, "a start point per seat");
            for (int seat = 0; seat < points.Length; seat++)
            {
                string name = "seat " + (seat + 1) + "'s start point";
                Assert.IsNotNull(points[seat], name);
                Vector3 start = points[seat].transform.position;

                Assert.IsFalse(Physics.CheckSphere(start, ROOM, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), name + " is in the open");
                Assert.IsTrue(Physics.Raycast(start, Vector3.down, out RaycastHit ground, DROP, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                    name + " has ground under it");
                Assert.IsFalse(ground.transform.IsChildOf(tutorialArea), name + " isn't over the tutorial area");
                foreach (RaycastHit hit in Physics.RaycastAll(start, Vector3.down, ground.distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    Assert.AreNotEqual("Water", hit.collider.tag, name + " isn't over water");
            }
        }
    }
}

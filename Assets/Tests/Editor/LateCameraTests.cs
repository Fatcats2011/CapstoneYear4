using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The players' views stop rendering under a full-screen camera (CameraBudget) in the same frame that camera comes on,
    /// even when another script turns it on late in the frame (in its LateUpdate). Enters Play Mode in the menu scene
    /// with one player (about 10 s). See docs/performance.md
    /// </summary>
    public class LateCameraTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        /// <summary>Turns a full-screen camera on in its LateUpdate, late in the frame (but before the cameras' budget)</summary>
        [DefaultExecutionOrder(500)]
        class LateCover : MonoBehaviour
        {
            public bool TurnOn;
            public int TurnedOnFrame = -1;

            void LateUpdate()
            {
                if (!TurnOn || TurnedOnFrame >= 0)
                    return;

                Camera cover = new GameObject("Late Cover Camera").AddComponent<Camera>();
                cover.depth = 20;
                cover.clearFlags = CameraClearFlags.SolidColor;
                TurnedOnFrame = Time.frameCount;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        [UnityTest]
        public IEnumerator ACameraTurnedOnInLateUpdate_TurnsThePlayersViewOffThatFrame()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance == null || GameManager.Instance.MainState != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.Roster[0] == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerCameraResizer resizer = PlayerInstantiate.Instance.Roster[0].Player.GetComponentInChildren<PlayerCameraResizer>();
            Camera view = resizer.PlayerReferenceCamera;
            Assert.IsTrue(view.enabled, "the player's view renders in the menu");

            LateCover late = new GameObject("Late Cover").AddComponent<LateCover>();
            late.TurnOn = true;
            yield return new WaitForEndOfFrame();

            Assert.AreEqual(Time.frameCount, late.TurnedOnFrame, "the cover came on this frame");
            Assert.IsFalse(view.enabled, "the view is off the frame the cover comes on: no blank frame under it");
        }
    }
}

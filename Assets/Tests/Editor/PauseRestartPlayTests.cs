using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Restart in a local match, for real: from the game scene's pause menu, A on Restart and A again loads the game scene
    /// afresh, the tutorial skipped, unpaused. Enters Play Mode and loads the game scene twice (a minute or so). No lambda
    /// captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class PauseRestartPlayTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const float LOADING = 90f;
        const string GAME_SCENE = "Assets/Scenes/Design Scene(Main).unity";

        static int gameSceneLoads;

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.path == GAME_SCENE)
                gameSceneLoads++;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            SceneFlow.Current = null;
            gameSceneLoads = 0;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        [UnityTest]
        public IEnumerator Local_RestartFromThePause_LoadsTheMatchAfresh()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;

            // Into a match the quick way: a rematch skips the loading screen's A and the tutorial
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            Assert.AreEqual(1, gameSceneLoads);

            // Pause, Restart, yes
            PlayerSlot slot = PlayerInstantiate.Instance.Roster[0];
            PlayerInstantiate.Instance.PlayerPause(slot.Input);
            PauseMenu pause = slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>().pauseMenu;
            Assert.AreEqual(4, pause.selectorObjects.Length, "Resume, Restart, Controls, Main Menu");
            pause.ScrollMenu(true);
            pause.ConfirmMenu();
            Assert.IsTrue(pause.Confirming, "asks first");
            pause.ConfirmMenu();

            deadline = Time.realtimeSinceStartup + LOADING;
            while (gameSceneLoads < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, gameSceneLoads, "the game scene loaded again");
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "a fresh match, the tutorial skipped");
            Assert.IsFalse(PlayerInstantiate.Instance.IsPaused, "unpaused");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.AreEqual(0, OrderSync.HandlerIn(0).Score);

            // The pause works again in the new match
            PlayerInstantiate.Instance.PlayerPause(slot.Input);
            Assert.IsFalse(pause.goToMainMenu, "the pause menu is ready again");
            PlayerInstantiate.Instance.PlayerPlay();
        }
    }
}

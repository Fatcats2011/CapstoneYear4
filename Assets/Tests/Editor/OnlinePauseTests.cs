using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Pausing online: the match goes on (time doesn't stop), the pause menu is this machine's own and says what Main Menu
    /// does, and it closes when the host's state takes players out of driving. A controller lost while paused leaves the
    /// pause menu as it was. In the real menu scene, with a player who is driving (their pause menu is what Start opens).
    /// No network: GameAuthority.Role is set as a session would set it. Enters Play Mode (about 15 s each)
    /// </summary>
    public class OnlinePauseTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            Time.timeScale = 1f;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        // A player's menus
        static MenuInteractions MenuOf(int slot)
        {
            return Slot(slot).Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>();
        }

        // The pause menu's tint: up while it's open
        static GameObject Tint(MenuInteractions menu)
        {
            return (GameObject)Reflect.GetField(menu.pauseMenu, "tint");
        }

        // The pause menu's selector, beside the row it's on
        static GameObject Selector(MenuInteractions menu)
        {
            return (GameObject)Reflect.GetField(menu.pauseMenu, "selector");
        }

        // How high a row of the pause menu is (0: Resume, 1: Main Menu)
        static float RowY(MenuInteractions menu, int row)
        {
            return ((GameObject[])Reflect.GetField(menu.pauseMenu, "selectorObjects"))[row].transform.position.y;
        }

        [UnityTest]
        public IEnumerator Online_PausingLetsTheMatchGoOn_UntilTheHostsStateMovesOn()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "a player");
            MenuInteractions menu = MenuOf(0);
            menu.SwapMenuType(MenuType.PauseMenu); // driving: Start opens their pause menu
            GameAuthority.Role = NetworkRole.Client;

            PlayerInstantiate.Instance.PlayerPause(Slot(0).Input);
            Assert.IsTrue(PlayerInstantiate.Instance.IsPaused);
            Assert.AreEqual(1f, Time.timeScale, "online the match goes on");
            Assert.IsTrue(Tint(menu).activeSelf, "the pause menu is up");
            Assert.AreEqual(PausePolicy.CLIENT_HINT, ControllerPrompts.Instance.HintText);
            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown, "it says what Main Menu does online");

            // The host takes everyone back to the lobby
            GameManager.Instance.ApplyGameState(GameState.PlayerSelect);
            Assert.IsFalse(PlayerInstantiate.Instance.IsPaused);
            Assert.IsFalse(Tint(menu).activeSelf, "it closes with the host's state");

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Online_AControllerLostWhilePaused_LeavesThePauseMenuAsItWas()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "a player");
            MenuInteractions menu = MenuOf(0);
            menu.SwapMenuType(MenuType.PauseMenu); // driving: Start opens their pause menu
            GameAuthority.Role = NetworkRole.Host;
            Assert.Greater(Mathf.Abs(RowY(menu, 0) - RowY(menu, 1)), 0.01f, "two rows to tell apart");

            PlayerInstantiate.Instance.PlayerPause(Slot(0).Input);
            menu.pauseMenu.ScrollMenu(true); // the selector on Main Menu
            Assert.AreEqual(RowY(menu, 1), Selector(menu).transform.position.y, 0.01f, "on Main Menu");

            TestPlayers.ToggleNewest(); // its controller is unplugged
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsTrue(PlayerInstantiate.IsMissingController(Slot(0).Input.user), "it lost its controller");
            Assert.AreEqual(RowY(menu, 1), Selector(menu).transform.position.y, 0.01f, "still on Main Menu: it didn't pause again");
            Assert.IsTrue(PlayerInstantiate.Instance.IsPaused);

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

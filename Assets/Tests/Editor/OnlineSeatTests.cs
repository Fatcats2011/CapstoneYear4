using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Online, this machine's one player sits in the seat the host gave them, in the real menu scene: they move there
    /// with their controller, another controller is turned away, and offline again players join as usual. Enters Play
    /// Mode (about 10 s each)
    /// </summary>
    public class OnlineSeatTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static bool LocalIn(int slot)
        {
            PlayerSlot player = PlayerInstantiate.Instance.Roster[slot];
            return player != null && player.IsLocal;
        }

        static void PressA(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator ThisMachinesPlayer_MovesToTheSeatTheHostGave_WithTheSameController()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            Gamepad pad = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
                yield return null;

            players.SetOnlineSeat(2);
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(2) && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerSlot moved = players.Roster[2];
            Assert.IsTrue(LocalIn(2), "in seat 3");
            Assert.IsNull(players.Roster[0], "seat 1 is free");
            Assert.AreEqual(1, players.Roster.LocalCount);
            Assert.IsTrue(moved.Input.devices.Contains(pad), "with the same controller");
            Assert.AreEqual("P3", moved.Player.name);
            BallDriving scooter = moved.Player.GetComponentInChildren<BallDriving>();
            Assert.AreEqual(3, scooter.playerIndex);
            Assert.AreEqual(12, scooter.Sphere.layer, "player 3's ball layer");
            Assert.IsTrue(moved.Input.GetComponent<PlayerUIHandler>().menuInteractions.hostPlayer, "still runs this machine's menus");

            // One player per machine online: another controller is turned away
            TestPlayers.Add();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, players.PlayerCount, "the second controller was turned away");

            // Offline again, a second player joins player select as usual (lowest free slot)
            players.SetOnlineSeat(-1);
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            Gamepad second = TestPlayers.Pads[1];
            deadline = Time.realtimeSinceStartup + 10;
            float nextPress = 0;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(second);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.IsTrue(LocalIn(0), "joined seat 1");
            Assert.AreEqual(2, players.PlayerCount);

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Online_APlayerJoiningLater_SitsInTheSeat()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerInstantiate.Instance.SetOnlineSeat(1);
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(1) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(LocalIn(1), "in seat 2");
            Assert.IsNull(PlayerInstantiate.Instance.Roster[0]);
            MenuInteractions menus = PlayerInstantiate.Instance.Roster[1].Input.GetComponent<PlayerUIHandler>().menuInteractions;
            Assert.IsTrue(menus.hostPlayer, "runs this machine's menus");
            Assert.AreEqual(MenuType.MainMenu, menus.curentMenuType, "on the title screen");
        }
    }
}

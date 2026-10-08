using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Online Play from the title screen in the real menu scene, over a fake Steam: a lobby that fails after the player
    /// readied up takes them back to the title screen unready, with no countdown left to start a match. Enters Play Mode
    /// (about 10 s)
    /// </summary>
    public class OnlinePlayMenuTests
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

        [UnityTest]
        public IEnumerator OnlinePlay_LobbyFailsAfterThePlayerReadied_BackAtTheTitleScreenUnready()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            FakeLobbyService lobbies = new FakeLobbyService();
            OnlinePlay.Instance.UseDirect(lobbies, "127.0.0.1", 7816);

            MainMenu.Instance.Choose(TitleEntry.Online);
            players.ReadyUp(0);
            Assert.IsNotNull(players.readyUpCountdown, "a lone ready player starts the countdown");
            lobbies.RaiseCreated(0);

            Assert.AreEqual(GameState.Menu, GameManager.Instance.MainState, "back at the title screen");
            Assert.IsFalse(players.IsReady(0), "unready");
            Assert.IsNull(players.readyUpCountdown, "no countdown left to start a match");
        }
    }
}

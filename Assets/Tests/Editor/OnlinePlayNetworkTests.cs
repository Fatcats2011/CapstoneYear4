using System.Collections;
using System.Collections.Generic;
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
    /// Playing online from the game's menus (OnlinePlay) on this computer (127.0.0.1), with a fake Steam: its lobbies
    /// answer at once, and sessions go by IP address (a Steam account can't join itself). The other machine is a
    /// session without the game. Real menu scene, about 20 s each. No lambda captures a local: after EnterPlayMode even
    /// assigning one throws
    /// </summary>
    public class OnlinePlayNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7797;
        const float WAIT = 15f;

        // Why the other machine's session ended
        class EndRecorder
        {
            public readonly List<string> Reasons = new List<string>();

            public void Ended(string reason)
            {
                Reasons.Add(reason);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static bool AtTitleScreen()
        {
            return State() == GameState.Menu;
        }

        static int LocalCount()
        {
            return PlayerInstantiate.Instance.Roster.LocalCount;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static void Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        static IEnumerator WaitForOnlinePlay()
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (OnlinePlay.Instance == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(OnlinePlay.Instance, "made at launch");
        }

        [UnityTest]
        public IEnumerator Hosting_YHostsALobby_FriendsJoin_AndBEndsIt()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return WaitForOnlinePlay();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Two players here: the title lets only the first in, the second joins in player select
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (LocalCount() < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (LocalCount() < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, LocalCount());

            FakeLobbyService lobbies = new FakeLobbyService { AutoAnswer = true };
            OnlinePlay.Instance.UseDirect(lobbies, THIS_COMPUTER, PORT);

            // The menus ignore buttons for a moment after they open
            yield return WaitSeconds(0.5f);
            Press(TestPlayers.Pads[0], GamepadButton.North);
            yield return WaitSeconds(0.3f);
            Assert.AreEqual(LobbyRules.ONE_PLAYER, ControllerPrompts.Instance.HintText, "one player per machine online");
            Assert.IsEmpty(lobbies.Creates, "no lobby");

            // Player 2 leaves, and Y hosts a lobby
            Press(TestPlayers.Pads[1], GamepadButton.East);
            deadline = Time.realtimeSinceStartup + 10;
            while (LocalCount() > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, LocalCount());
            yield return WaitSeconds(0.5f);
            Press(TestPlayers.Pads[0], GamepadButton.North);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameAuthority.Role != NetworkRole.Host && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "Y hosts");
            Assert.AreEqual("doa", lobbies.Data[42]["game"]);
            Assert.AreEqual(OnlinePlay.BuildTag(), lobbies.GetData(OnlinePlay.Instance.Lobby.Current, LobbyRules.BUILD_KEY),
                "tagged with this build's version and Netcode setup");
            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Invites, "and opens the invite dialog");
            deadline = Time.realtimeSinceStartup + 2;
            while ((!LobbyPrompt.Exists || LobbyPrompt.Instance.Text != OnlinePlay.HINT_IN_LOBBY) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(OnlinePlay.HINT_IN_LOBBY, LobbyPrompt.Instance.Text, "the line says what Y and B do now");

            // A friend joins
            OnlineSession other = OnlineSession.Create(Application.version);
            Assert.IsTrue(other.JoinDirect(THIS_COMPUTER, PORT), "the friend joins");
            EndRecorder ends = new EndRecorder();
            other.Ended += ends.Ended;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(1) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "the friend takes the second seat");

            // B (not ready) ends it for everyone
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            Press(TestPlayers.Pads[0], GamepadButton.East);
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((GameAuthority.Role != NetworkRole.Offline || State() != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "B leaves");
            Assert.AreEqual(GameState.Menu, State(), "back on the title screen");
            CollectionAssert.Contains(lobbies.Left, 42UL, "the lobby is left");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (other.IsRunning && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { OnlineSession.HOST_LEFT }, ends.Reasons, "the friend hears why");

            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_AFriendsLobbyFromTheTitle_LandsInTheirPlayerSelect_AndTheirLeavingSaysSo()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return WaitForOnlinePlay();

            // A friend hosts, and their invite arrives as the game starts: it waits for the title screen
            FakeLobbyService lobbies = new FakeLobbyService { AutoAnswer = true };
            lobbies.AddLobby(42, "doa", OnlinePlay.BuildTag(), 99); // the friend is on this build
            OnlinePlay.Instance.UseDirect(lobbies, THIS_COMPUTER, PORT);
            OnlineSession host = OnlineSession.Create(Application.version);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "the friend hosts");
            OnlinePlay.Instance.RequestJoin(42);

            float deadline = Time.realtimeSinceStartup + 60;
            while (GameAuthority.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role, "joined the friend's match");
            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Joins);
            Assert.AreEqual(42UL, OnlinePlay.Instance.Lobby.Current, "in the friend's lobby");

            host.Match.SendState(GameState.PlayerSelect);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.PlayerSelect, State(), "in the friend's player select");

            // The friend leaves
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            host.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameAuthority.Role != NetworkRole.Offline && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText, "the player hears why");
            CollectionAssert.Contains(lobbies.Left, 42UL, "the lobby is left");
            Assert.AreEqual(GameState.PlayerSelect, State(), "still in player select, offline");

            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

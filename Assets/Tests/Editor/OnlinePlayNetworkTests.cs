using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
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
        const ushort PORT_ONE_PLAYER = 7817;
        const ushort PORT_FIND = 7819;
        const ushort PORT_FOUND_HOST = 7820; // the other host Find moves to: two hosts on this computer need two ports
        const ulong FOUND_LOBBY = 77;
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

            OnlineRules.OnePlayerPerMachine = true;
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

        static bool LocalIn(int slot)
        {
            PlayerSlot player = PlayerInstantiate.Instance.Roster[slot];
            return player != null && player.IsLocal;
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
        public IEnumerator Hosting_OnlinePlayHostsALobby_FriendsJoin_AndBEndsIt()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            OnlineRules.OnePlayerPerMachine = false; // two players here host together (Phase 3J)
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

            // Online Play (the title screen's entry) hosts a lobby, both players here playing (Phase 3J)
            MainMenu.Instance.Choose(TitleEntry.Online);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameAuthority.Role != NetworkRole.Host && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "Online Play hosts, with two players here");
            Assert.AreEqual(1, lobbies.Creates.Count, "one lobby");
            OnlineSession hosting = OnlinePlay.Instance.Session;
            deadline = Time.realtimeSinceStartup + 5;
            while (!(hosting.Players.Count == 2 && LocalIn(0) && LocalIn(1) && PlayerInstantiate.Instance.UnseatedLocalCount == 0)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, hosting.Players.Count, "a player for each player here");
            Assert.IsTrue(hosting.Owns(NetworkManager.ServerClientId, 0) && hosting.Owns(NetworkManager.ServerClientId, 1), "the host's seats 0 and 1");
            Assert.IsTrue(LocalIn(0) && LocalIn(1), "both sit in their seats here");
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
            while (Slot(2) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(2), "the friend takes the third seat (this machine's two players hold the first two)");

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
        public IEnumerator GoingOnline_WithTwoLocalPlayers_KeepsOnlyTheMenuPlayer()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return WaitForOnlinePlay();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Two players here, joined in offline player select
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
            PlayerInput menuPlayer = Slot(0).Input;
            PlayerInput other = Slot(1).Input;
            Assert.IsTrue(menuPlayer.GetComponent<PlayerUIHandler>().menuInteractions.hostPlayer, "the first player is the menu player");

            FakeLobbyService lobbies = new FakeLobbyService { AutoAnswer = true };
            OnlinePlay.Instance.UseDirect(lobbies, THIS_COMPUTER, PORT_ONE_PLAYER);
            MainMenu.Instance.Choose(TitleEntry.Online);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameAuthority.Role != NetworkRole.Host && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "Online Play hosts");

            // Long enough for a second seat to be asked, and answered
            yield return WaitSeconds(3f);
            OnlineSession hosting = OnlinePlay.Instance.Session;
            Assert.AreEqual(1, LocalCount(), "one player per machine online");
            Assert.AreEqual(1, hosting.Players.Count, "no seat asked of the host for the other");
            Assert.IsTrue(other == null, "the other player is gone");
            Assert.IsTrue(menuPlayer != null, "the menu player stays");
            Assert.IsTrue(menuPlayer.GetComponent<PlayerUIHandler>().menuInteractions.hostPlayer, "and is still the menu player");
            Assert.AreEqual(0, PlayerInstantiate.Instance.UnseatedLocalCount, "and sits in its seat");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            hosting.Leave();
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

        // The seats the other host gave this machine
        static int SeatsOnOtherHost(OnlineSession other)
        {
            return other.SeatsHeldBy(OnlinePlay.Instance.Session.Network.LocalClientId);
        }

        // One player here hosts alone on Dispatch (Online Play), and another host's open lobby is what Find will find
        static IEnumerator HostAloneWithAnOpenLobbyAround(FakeLobbyService lobbies)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (LocalCount() < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);

            lobbies.AddLobby(FOUND_LOBBY, LobbyRules.GAME, OnlinePlay.BuildTag(), 99);
            lobbies.SetData(FOUND_LOBBY, MatchRules.MM_KEY, MatchRules.MM_ON);
            lobbies.SetData(FOUND_LOBBY, MatchRules.OPEN_KEY, "3");
            OnlinePlay.Instance.UseDirect(lobbies, THIS_COMPUTER, PORT_FIND, PORT_FOUND_HOST);
            MainMenu.Instance.Choose(TitleEntry.Online);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(GameAuthority.Role == NetworkRole.Host && PlayerInstantiate.Instance.UnseatedLocalCount == 0
                && OnlinePlay.Instance.Session.Players.Count == 1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "Online Play hosts");
            Assert.AreEqual(0, PlayerInstantiate.Instance.UnseatedLocalCount, "seated");
        }

        [UnityTest]
        public IEnumerator Find_SwitchesToAnotherHost_StaysOnDispatch()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return WaitForOnlinePlay();
            FakeLobbyService lobbies = new FakeLobbyService { AutoAnswer = true };
            yield return HostAloneWithAnOpenLobbyAround(lobbies);

            OnlineSession other = OnlineSession.Create(Application.version);
            Assert.IsTrue(other.HostDirect(THIS_COMPUTER, PORT_FOUND_HOST), "another host, open to anyone");

            log.MachinesLeave(); // Find leaves this machine's own session: see LogCollector.CLOSED_PORT
            Assert.IsTrue(OnlinePlay.Instance.Match.Find(), "Find");
            lobbies.AnswerSearch(FOUND_LOBBY);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(GameAuthority.Role == NetworkRole.Client && SeatsOnOtherHost(other) == 1
                && PlayerInstantiate.Instance.UnseatedLocalCount == 0) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role, "a client of the other host");
            Assert.AreEqual(FOUND_LOBBY, OnlinePlay.Instance.Lobby.Current, "in its lobby");
            Assert.IsFalse(OnlinePlay.Instance.Lobby.IsHost);
            CollectionAssert.Contains(lobbies.Left, 42UL, "its own lobby is left");
            Assert.AreEqual(1, SeatsOnOtherHost(other), "seated on the other host");
            Assert.AreEqual(0, PlayerInstantiate.Instance.UnseatedLocalCount, "and here");
            Assert.IsFalse(OnlinePlay.Instance.Match.Searching, "Find is done");

            yield return WaitSeconds(1f);
            Assert.AreEqual(GameState.PlayerSelect, State(), "still on Dispatch");
            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role);

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameAuthority.Role != NetworkRole.Offline && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Find_HostGoneAfterSwitch_RehostsOnDispatch()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return WaitForOnlinePlay();
            FakeLobbyService lobbies = new FakeLobbyService { AutoAnswer = true };
            yield return HostAloneWithAnOpenLobbyAround(lobbies);
            OnlinePlay.Instance.Session.Direct.MaxConnectAttempts = 2; // give up on the gone host after about 2 s

            OnlineSession other = OnlineSession.Create(Application.version);
            Assert.IsTrue(other.HostDirect(THIS_COMPUTER, PORT_FOUND_HOST), "another host, open to anyone");

            log.MachinesLeave(); // Find leaves this machine's own session: see LogCollector.CLOSED_PORT
            Assert.IsTrue(OnlinePlay.Instance.Match.Find(), "Find");
            lobbies.AnswerSearch(FOUND_LOBBY);
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "its own session is left");

            // The other host stops before this machine reaches it. Unity Transport logs a socket error per attempt
            log.Dispose();
            other.Leave();
            float deadline = Time.realtimeSinceStartup + 30;
            while (!(GameAuthority.Role == NetworkRole.Host && OnlinePlay.Instance.Lobby.Filling)
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "hosts again");
            Assert.IsTrue(OnlinePlay.Instance.Lobby.IsHost);
            Assert.IsTrue(OnlinePlay.Instance.Lobby.Filling, "open to anyone");
            Assert.AreEqual(2, lobbies.Creates.Count, "in a new lobby");
            Assert.AreEqual(1, lobbies.Invites.Count, "without opening the invite dialog again");
            Assert.IsFalse(OnlinePlay.Instance.Match.Searching);
            Assert.AreEqual(GameState.PlayerSelect, State(), "still on Dispatch");

            LogCollector after = new LogCollector();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (PlayerInstantiate.Instance.UnseatedLocalCount != 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(0, PlayerInstantiate.Instance.UnseatedLocalCount, "seated in it");
            Assert.AreEqual(GameState.PlayerSelect, State());

            after.MachinesLeave();
            OnlinePlay.Instance.Session.Leave();
            yield return null;
            after.Dispose();
            Assert.IsEmpty(after.Problems, "Errors:\n\n" + string.Join("\n\n", after.Problems));
        }
    }
}

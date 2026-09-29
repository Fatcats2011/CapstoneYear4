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
    /// The game online, in the real menu scene on this computer (127.0.0.1). This machine hosts or joins with the game,
    /// and another machine is stood in for by a session without the game (it changes nothing here but what it sends).
    /// Enters Play Mode (about 20 s each). No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineGameNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7793;
        const string VERSION = "test";
        const float WAIT = 15f;

        class StateRecorder
        {
            public GameState Last = GameState.Default;

            public StateRecorder(OnlineMatch match)
            {
                match.StateReceived += OnState;
            }

            void OnState(GameState state)
            {
                Last = state;
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

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static OnlinePlayer PlayerInSeat(OnlineSession session, int seat)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.Seat == seat)
                    return player;
            }
            return null;
        }

        static void PressA(Gamepad pad)
        {
            Press(pad, GamepadButton.South);
        }

        static void Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        // The colour a local player picked in player select
        static int Colour(int slot)
        {
            return Slot(slot).Input.GetComponent<PlayerUIHandler>().customizationSelector.ColourIndex;
        }

        [UnityTest]
        public IEnumerator Hosting_AnotherMachinesPlayer_SitsInTheirSeat_DressedAsThem()
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
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(OnlineTestMenuTests.MenuCanStart(), "the Online menu offers Host and Join on the title screen");

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role);
            Assert.IsInstanceOf<OnlineSceneFlow>(SceneFlow.Current);
            GameManager.Instance.SetGameState(GameState.PlayerSelect);

            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((Slot(1) == null || other.Match == null || PlayerInSeat(other, 1) == null) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            Assert.IsFalse(Slot(1).IsLocal);
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "their session left this machine's role alone");
            Assert.AreEqual(GameState.PlayerSelect, other.Match.State, "they see where the host is");

            PlayerInSeat(other, 1).Share(2, 3, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!players.IsReady(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(players.IsReady(1), "their ready counts here");
            CustomizationSelector customization = OnlinePrefabs.Load().LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);
            Transform scooter = Slot(1).Player.transform;
            Assert.AreSame(customization.Colours[2].colorMaterial, scooter.Find(ScooterLook.GHOST).GetComponent<SkinnedMeshRenderer>().sharedMaterials[0], "their colour");
            Assert.AreEqual(customization.Hats[3].displayHat, scooter.Find(ScooterLook.HAT).gameObject.activeSelf, "their hat");

            // This machine's controller still drives player select with them here (menus ignore buttons for a moment
            // after they open, so keep pressing)
            int colour = Colour(0);
            deadline = Time.realtimeSinceStartup + WAIT;
            float nextPress = 0;
            while (Colour(0) == colour && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(pad, GamepadButton.DpadRight);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.AreNotEqual(colour, Colour(0), "this machine's d-pad changes its player's colour");

            StateRecorder heard = new StateRecorder(other.Match);
            GameManager.Instance.SetGameState(GameState.Menu);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Last != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Menu, heard.Last, "the host's switch reached them");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(1) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(Slot(1), "they left: their seat is free");

            host.Leave();
            yield return null;
            yield return null;
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "local again");
            Assert.IsNotInstanceOf<OnlineSceneFlow>(SceneFlow.Current, "the local loader again");
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_WithNoWaves_SharesAStoppedClock()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.Match == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(host.Match);
            Assert.IsNull(OrderManager.Instance, "no waves in the menu");

            // A match ended by pause -> Main Menu leaves the old clock running on the host
            host.Match.ShareClock(host.Network.ServerTime.Time + 100, true, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((host.Match.ClockStarted || host.Match.FinalOrder) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(host.Match.ClockStarted, "the host shares a stopped clock");
            Assert.IsFalse(host.Match.FinalOrder, "and no final order");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_ThisMachinesPlayer_MovesToItsSeat_AndFollowsTheHost()
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
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION); // another machine hosting: a session without the game
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(mine);
            mine.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player moved to seat 2");
            Assert.IsTrue(Slot(1).Input.devices.Contains(pad), "with their controller");
            Assert.IsNotNull(Slot(0), "the host's scooter is in seat 1");
            Assert.IsFalse(Slot(0).IsLocal);
            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role);

            host.Match.SendState(GameState.PlayerSelect);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameManager.Instance.MainState != GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState, "followed the host into player select");
            Assert.IsTrue(((Canvas)Reflect.GetField(MainMenu.Instance, "PlayerSelectCanvas")).enabled, "player select's menu is open");

            // Ready up here (menus ignore buttons for a moment after they open, so keep pressing): the host hears it
            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            float nextPress = 0;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(pad);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.IsTrue(onHost.Ready, "the host sees this player ready");

            // The host looks at its options: here that's the title screen
            host.Match.SendState(GameState.Options);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameManager.Instance.MainState != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Menu, GameManager.Instance.MainState);

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(0) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(Slot(0), "the host's scooter went with the session");
            Assert.IsTrue(Slot(1).IsLocal, "this machine's player stays");
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);

            // One side at a time: closing both ends in one frame makes Windows report the closed port as a socket error
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_WhileTheHostIsInPlayerSelect_ThisMachinesControllerDrivesPlayerSelect()
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
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Another machine hosting, already in player select (as Tools > Online > Host leaves it): this machine
            // joins straight into player select, and its player moves seat there
            OnlineSession host = OnlineSession.Create(VERSION);
            host.HostDirect(THIS_COMPUTER, PORT);
            host.Match.SendState(GameState.PlayerSelect);
            OnlineSession mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(mine);
            mine.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && GameManager.Instance.MainState == GameState.PlayerSelect)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player moved to seat 2");
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState, "straight into player select");

            // Their controller changes their colour, then readies them up (keep pressing: see above)
            int colour = Colour(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            float nextPress = 0;
            while (Colour(1) == colour && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(pad, GamepadButton.DpadRight);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.AreNotEqual(colour, Colour(1), "the d-pad changes their colour");

            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            nextPress = 0;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(pad);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.IsTrue(onHost.Ready, "A readies them up, and the host sees it");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        // Set by JoinPlayerSelectReady
        static OnlineSession sessionHost;
        static OnlineSession sessionMine;
        static Gamepad sessionPad;

        static bool CountingDown()
        {
            return Reflect.GetField(PlayerInstantiate.Instance, "readyUpCountdown") != null;
        }

        // The host (another machine) is in player select and this machine has joined and readied up. With hostReady the host
        // is ready too, so this machine's ready starts the countdown
        static IEnumerator JoinPlayerSelectReady(bool hostReady)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            sessionPad = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            sessionHost = OnlineSession.Create(VERSION);
            sessionHost.HostDirect(THIS_COMPUTER, PORT);
            sessionHost.Match.SendState(GameState.PlayerSelect);
            sessionMine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(sessionMine);
            sessionMine.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null && GameManager.Instance.MainState == GameState.PlayerSelect)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null, "this machine's player and the host's are seated");
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState);

            if (hostReady)
            {
                PlayerInSeat(sessionHost, 0).Share(0, 0, true);
                deadline = Time.realtimeSinceStartup + WAIT;
                while (!players.IsReady(0) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsTrue(players.IsReady(0), "the host's ready counts here");
            }

            OnlinePlayer onHost = PlayerInSeat(sessionHost, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            float nextPress = 0;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(sessionPad);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.IsTrue(onHost.Ready, "A readies this machine's player up");
        }

        // The session ends on this machine, then the other end closes (one side at a time: see above)
        static IEnumerator LeaveSession(LogCollector log)
        {
            log.MachinesLeave();
            sessionMine.Leave();
            yield return null;
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (sessionHost.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            sessionHost.Leave();
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        // Ready up again with the controller (menus ignore buttons for a moment, so keep pressing) until the countdown runs
        static IEnumerator ReadyUpAgain()
        {
            float deadline = Time.realtimeSinceStartup + WAIT;
            float nextPress = 0;
            while (!CountingDown() && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(sessionPad);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheSessionEnding_InPlayerSelect_StartsNoCountdown_ForAReadyPlayer()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return JoinPlayerSelectReady(false);
            PlayerInstantiate players = PlayerInstantiate.Instance;
            Assert.IsFalse(CountingDown(), "the host isn't ready yet");

            // The host leaves: their scooter goes, and this machine's ready player is alone
            yield return LeaveSession(log);
            Assert.IsNull(Slot(0), "the host's scooter went with the session");
            yield return WaitSeconds(3.6f);

            Assert.IsFalse(CountingDown(), "no countdown");
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState, "still in player select, no offline match");
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player stays");
            Assert.IsFalse(players.IsReady(1), "and isn't ready any more");
            Assert.IsFalse(((GameObject)Reflect.GetField(Slot(1).Input.GetComponent<PlayerUIHandler>().menuInteractions, "readyUpText")).activeSelf,
                "the ready text is off too");

            // Ready up again: the countdown starts as usual
            yield return ReadyUpAgain();
            Assert.IsTrue(CountingDown(), "readying up again starts the countdown");
            Assert.IsTrue(players.IsReady(1));

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator TheSessionEnding_DuringTheCountdown_StopsIt()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return JoinPlayerSelectReady(true);
            PlayerInstantiate players = PlayerInstantiate.Instance;
            Assert.IsTrue(CountingDown(), "everyone's ready: counting down");

            yield return LeaveSession(log);
            yield return WaitSeconds(3.6f);

            Assert.IsFalse(CountingDown(), "the countdown stopped");
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState, "still in player select, no offline match");
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player stays");
            Assert.IsFalse(players.IsReady(1), "and isn't ready any more");

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

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
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.QueueStateEvent(pad, new GamepadState());
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
            TestPlayers.Add();
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

            StateRecorder heard = new StateRecorder(other.Match);
            GameManager.Instance.SetGameState(GameState.Menu);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Last != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Menu, heard.Last, "the host's switch reached them");

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
    }
}

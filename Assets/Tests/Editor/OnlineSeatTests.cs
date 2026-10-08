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

            players.GiveSeat(2);
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

            // Online, more players join only in player select: on the title screen another controller is turned away
            TestPlayers.Add();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, players.PlayerCount, "the second controller was turned away outside player select");

            // Offline again, a second player joins player select as usual (lowest free slot)
            players.GoOffline();
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

            PlayerInstantiate.Instance.GiveSeat(1);
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

        [UnityTest]
        public IEnumerator MovingToTheSeat_LeavesNoReadyPlayerInTheOldSeat()
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
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            players.ReadyUp(0);
            Assert.IsTrue(players.IsReady(0), "ready in seat 1");

            players.GiveSeat(2);
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(2) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(LocalIn(2), "in seat 3");
            Assert.IsNull(players.Roster[0], "seat 1 is free");
            Assert.IsFalse(players.IsReady(0), "a seat with nobody in it isn't ready");
            Assert.IsFalse(players.IsReady(2), "and the player joining starts unready");
        }

        // Phase 3J: several players on this machine online

        // The seats this machine's players gave back
        class SeatRecorder
        {
            public readonly System.Collections.Generic.List<int> GivenUp = new System.Collections.Generic.List<int>();

            public SeatRecorder(PlayerInstantiate players)
            {
                players.SeatGivenUp += OnGivenUp;
            }

            void OnGivenUp(int seat)
            {
                GivenUp.Add(seat);
            }
        }

        static bool LocalWith(int slot, Gamepad pad)
        {
            return LocalIn(slot) && PlayerInstantiate.Instance.Roster[slot].Input.devices.Contains(pad);
        }

        static bool MenuPlayer(int slot)
        {
            return PlayerInstantiate.Instance.Roster[slot].Input.GetComponent<PlayerUIHandler>().menuInteractions.hostPlayer;
        }

        // Pad A joins on the title screen (this machine's menu player); then player select, where pad B joins
        static IEnumerator TwoPlayersInPlayerSelect(Gamepad[] pads)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            pads[0] = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            pads[1] = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.Roster.LocalCount < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, PlayerInstantiate.Instance.Roster.LocalCount, "two players here");
        }

        [UnityTest]
        public IEnumerator TwoPlayersHere_MoveIntoTheSeatsTheyAreGiven_MenuPlayerFirst()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            Gamepad[] pads = new Gamepad[2];
            yield return TwoPlayersInPlayerSelect(pads);
            PlayerInstantiate players = PlayerInstantiate.Instance;

            Assert.IsTrue(players.GiveSeat(2), "kept");
            float deadline = Time.realtimeSinceStartup + 10;
            while (!LocalWith(2, pads[0]) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(LocalWith(2, pads[0]), "the menu player moves into the first seat, with their pad");
            Assert.IsTrue(MenuPlayer(2), "still runs this machine's menus");
            Assert.AreEqual(1, players.UnseatedLocalCount, "the other waits for a seat");

            Assert.IsTrue(players.GiveSeat(3));
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalWith(3, pads[1]) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(LocalWith(3, pads[1]), "the second player takes the second seat, with their pad");
            Assert.IsFalse(MenuPlayer(3));
            Assert.AreEqual(2, players.Roster.LocalCount);
            Assert.AreEqual(0, players.UnseatedLocalCount);
            Assert.AreEqual(SplitScreenLayout.CalculateRects(2)[0], players.Roster[2].Input.camera.rect, "two views: the first one's");

            players.GoOffline();
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator TheSeatAnotherPlayerStandsIn_GoesToTheMenuPlayer()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            Gamepad[] pads = new Gamepad[2];
            yield return TwoPlayersInPlayerSelect(pads);
            PlayerInstantiate players = PlayerInstantiate.Instance;
            Assert.IsTrue(LocalWith(1, pads[1]), "the second player stands in slot 2");

            players.GiveSeat(1); // the host gives this machine seat 2, where the second player stands
            float deadline = Time.realtimeSinceStartup + 10;
            while (!(LocalWith(1, pads[0]) && players.Roster.LocalCount == 2) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(LocalWith(1, pads[0]), "the menu player takes it: this machine's menus need their player");
            Assert.IsTrue(MenuPlayer(1));
            Assert.AreEqual(2, players.Roster.LocalCount, "the second player is still here");
            Assert.AreEqual(1, players.UnseatedLocalCount, "waiting for a seat of their own");
            players.GoOffline();
        }

        [UnityTest]
        public IEnumerator Online_ASecondPlayerLeaving_GivesTheirSeatBack()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            Gamepad[] pads = new Gamepad[2];
            yield return TwoPlayersInPlayerSelect(pads);
            PlayerInstantiate players = PlayerInstantiate.Instance;
            players.GiveSeat(2); // seats nobody here sits in yet: the menu player takes 2, the other 3
            players.GiveSeat(3);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!(LocalWith(2, pads[0]) && LocalWith(3, pads[1])) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(LocalWith(3, pads[1]), "the second player in seat 4");
            SeatRecorder recorder = new SeatRecorder(players);

            // The menus ignore buttons for a moment after they open
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            deadline = Time.realtimeSinceStartup + 10;
            float nextPress = 0;
            while (players.Roster[3] != null && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    InputSystem.QueueStateEvent(pads[1], new GamepadState().WithButton(GamepadButton.East));
                    InputSystem.QueueStateEvent(pads[1], new GamepadState());
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }

            Assert.IsNull(players.Roster[3], "B: they left player select");
            CollectionAssert.AreEqual(new[] { 3 }, recorder.GivenUp, "and gave their seat back");
            CollectionAssert.AreEquivalent(new[] { 2 }, players.OnlineSeats);
            Assert.IsTrue(LocalWith(2, pads[0]), "the menu player stays");
            players.GoOffline();
        }

        [UnityTest]
        public IEnumerator GiveSeat_WithNobodyToSit_ReturnsFalse()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Gamepad pad = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;

            Assert.IsTrue(players.GiveSeat(1), "the first seat: kept");
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalWith(1, pad) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsFalse(players.GiveSeat(2), "nobody here to sit in it: the caller gives it back");
            CollectionAssert.AreEquivalent(new[] { 1 }, players.OnlineSeats);
            Assert.IsNull(players.Roster[2]);
            players.GoOffline();
        }

        [UnityTest]
        public IEnumerator TurningAwayUnseated_RemovesThemWithTheReason()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Gamepad padA = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            players.GiveSeat(1);
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalWith(1, padA) && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            Gamepad padB = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalWith(0, padB) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(LocalWith(0, padB), "B joins the lowest free slot, unseated");
            Assert.AreEqual(1, players.UnseatedLocalCount);

            Assert.AreEqual(1, players.TurnAwayUnseated(JoinRules.FULL, 1));

            Assert.IsNull(players.Roster[0], "B left");
            Assert.AreEqual(JoinRules.FULL, ControllerPrompts.Instance.HintText, "and heard why");
            Assert.IsTrue(LocalWith(1, padA), "A stays");
            players.GoOffline();
        }
    }
}

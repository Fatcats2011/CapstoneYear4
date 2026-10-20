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
    /// The menus' buttons: Y (triangle) reaches the menus through the UI map's "North Face" action, which each player's
    /// menus subscribe to in code. The Play Mode test uses the real menu scene (about 10 s)
    /// </summary>
    public class MenuButtonsTests
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

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        static bool IsGamepadNorth(InputBinding binding)
        {
            return binding.path == "<Gamepad>/buttonNorth" && binding.groups == "Gamepad";
        }

        [Test]
        public void UIMap_HasNorthFace_OnTheGamepadsNorthButton()
        {
            InputActionAsset actions = Resources.Load<InputActionAsset>("CapstoneYear4");
            InputAction north = actions.FindAction(PlayerUIHandler.NORTH_ACTION);

            Assert.IsNotNull(north);
            Assert.AreEqual(InputActionType.Button, north.type);
            Assert.IsTrue(north.bindings.Any(IsGamepadNorth), "on the gamepad's north button (Y, triangle)");
        }

        [UnityTest]
        public IEnumerator NorthButton_InPlayerSelect_ReachesTheMenus()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);

            PlayerUIHandler handler = PlayerInstantiate.Instance.Roster[0].Input.GetComponent<PlayerUIHandler>();
            NorthRecorder recorder = new NorthRecorder();
            handler.NorthFaceEvent.AddListener(recorder.Press);

            // The menus ignore buttons for a moment after they open
            yield return WaitSeconds(0.5f);
            Gamepad pad = TestPlayers.Pads[0];
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return WaitSeconds(0.3f);

            Assert.AreEqual(1, recorder.Presses, "Y reaches player select's menus");
        }

        static void Set(MenuInteractions menu, string field, object value)
        {
            typeof(MenuInteractions).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(menu, value);
        }

        // Components added in Edit Mode don't get their serialized UnityEvents made
        static void GiveEvents(PlayerUIHandler ui, InputManager driving)
        {
            ui.NorthFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.EastFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.SouthFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.WestFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.LeftShoulderEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.RightShoulderEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.LeftPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.RightPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.UpPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.DownPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.StartPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            driving.StartPadEvent = new UnityEngine.Events.UnityEvent<bool>();
        }

        [Test]
        public void Online_PlayerSelect_SlidersHiddenUntilWest()
        {
            TestObjects objects = new TestObjects();
            try
            {
                GameAuthority.Role = NetworkRole.Host;
                MenuInteractions menu = objects.Add<MenuInteractions>();
                PlayerUIHandler ui = objects.Add<PlayerUIHandler>();
                InputManager driving = objects.Add<InputManager>();
                CustomizationSelector sliders = objects.Add<CustomizationSelector>();
                Set(menu, "uiHandler", ui);
                Set(menu, "drivingHandler", driving);
                Set(menu, "customizationSelector", sliders);
                GiveEvents(ui, driving);

                menu.SwapMenuType(MenuType.PlayerSelect);
                Assert.IsFalse(sliders.gameObject.activeSelf, "hidden when player select opens");

                ui.WestFaceEvent.Invoke(true);
                Assert.IsTrue(sliders.gameObject.activeSelf, "X opens them");

                ui.EastFaceEvent.Invoke(true);
                Assert.IsFalse(sliders.gameObject.activeSelf, "B closes them");

                ui.WestFaceEvent.Invoke(true);
                Assert.IsTrue(sliders.gameObject.activeSelf);
                menu.SwapToPlayerSelect();
                Assert.IsFalse(sliders.gameObject.activeSelf, "back in player select they start closed");
            }
            finally
            {
                GameAuthority.Role = NetworkRole.Offline;
                objects.DestroyAll();
            }
        }

        [Test]
        public void Offline_PlayerSelect_SlidersAlwaysShown_AndXDoesNothing()
        {
            TestObjects objects = new TestObjects();
            try
            {
                MenuInteractions menu = objects.Add<MenuInteractions>();
                PlayerUIHandler ui = objects.Add<PlayerUIHandler>();
                InputManager driving = objects.Add<InputManager>();
                CustomizationSelector sliders = objects.Add<CustomizationSelector>();
                Set(menu, "uiHandler", ui);
                Set(menu, "drivingHandler", driving);
                Set(menu, "customizationSelector", sliders);
                GiveEvents(ui, driving);

                menu.SwapMenuType(MenuType.PlayerSelect);
                Assert.IsTrue(sliders.gameObject.activeSelf);

                ui.WestFaceEvent.Invoke(true);
                Assert.IsTrue(sliders.gameObject.activeSelf, "X changes nothing offline");
            }
            finally
            {
                objects.DestroyAll();
            }
        }

        [Test]
        public void Online_RightShoulder_OnClient_DoesNothing()
        {
            TestObjects objects = new TestObjects();
            try
            {
                GameAuthority.Role = NetworkRole.Client;
                FakeLobbyService lobbies = new FakeLobbyService();
                OnlinePlay play = objects.Add<OnlinePlay>();
                play.UseDirect(lobbies, "127.0.0.1", 7777);
                OnlinePlay.Instance = play;
                MenuInteractions menu = objects.Add<MenuInteractions>();
                PlayerUIHandler ui = objects.Add<PlayerUIHandler>();
                InputManager driving = objects.Add<InputManager>();
                CustomizationSelector sliders = objects.Add<CustomizationSelector>();
                Set(menu, "uiHandler", ui);
                Set(menu, "drivingHandler", driving);
                Set(menu, "customizationSelector", sliders);
                GiveEvents(ui, driving);
                menu.SwapMenuType(MenuType.PlayerSelect);

                ui.RightShoulderEvent.Invoke(true);

                Assert.IsFalse(play.Lobby.Filling, "a client can't open the lobby");
                Assert.IsEmpty(lobbies.PublicCalls);
                Assert.IsEmpty(lobbies.DataCalls);
            }
            finally
            {
                OnlinePlay.Instance = null;
                GameAuthority.Role = NetworkRole.Offline;
                objects.DestroyAll();
            }
        }

        // A clock the switching tests move (a quiet search waits RESEARCH)
        class Clock
        {
            public double Now = 100;

            public double Read()
            {
                return Now;
            }
        }

        static int NoOtherMachines()
        {
            return 0;
        }

        static bool InTheMenus()
        {
            return true;
        }

        // Set by SwitchAway
        TestObjects switching;
        FakeLobbyService lobbies;
        FakeSessionControl session;
        OnlineLobby lobby;
        Matchmaker match;
        GameManager game;
        MenuInteractions menu;
        PlayerUIHandler ui;
        CustomizationSelector sliders;

        // A host alone in lobby 42, in player select with Fill on or off, whose search (the player's Find, or a lone Fill
        // host's quiet one) has left it for lobby 70's host: offline meanwhile. Its menu player's buttons are wired
        void SwitchAway(bool fill, bool quiet)
        {
            switching = new TestObjects();
            game = switching.Add<GameManager>();
            GameManager.instance = game;
            game.SetGameState(GameState.PlayerSelect);
            MainMenu.instance = switching.Add<MainMenu>();

            Clock clock = new Clock();
            lobbies = new FakeLobbyService { Me = 500 };
            session = new FakeSessionControl();
            lobby = new OnlineLobby(lobbies, session, "1.2", clock.Read);
            lobby.ShowState(GameState.PlayerSelect);
            match = new Matchmaker(lobbies, lobby, "1.2", clock.Read, NoOtherMachines, InTheMenus);
            lobby.Host(invite: false);
            lobbies.RaiseCreated(42);
            lobby.SetFill(fill);
            lobbies.AddLobby(70, LobbyRules.GAME, "1.2", 170);
            lobbies.SetData(70, MatchRules.MM_KEY, MatchRules.MM_ON);
            lobbies.SetData(70, MatchRules.OPEN_KEY, "3");
            if (quiet)
            {
                clock.Now += MatchRules.RESEARCH;
                match.Tick();
                Assert.IsTrue(match.ResearchingQuietly, "a quiet search again");
            }
            else
                Assert.IsTrue(match.Find());
            lobbies.AnswerSearch(70);
            lobbies.RaiseEntered(70, true);
            Assert.IsTrue(match.Switching, "between sessions");
            Assert.IsFalse(GameAuthority.IsOnline, "offline meanwhile");

            OnlinePlay play = switching.Add<OnlinePlay>();
            play.Match = match;
            OnlinePlay.Instance = play;
            menu = switching.Add<MenuInteractions>();
            ui = switching.Add<PlayerUIHandler>();
            InputManager driving = switching.Add<InputManager>();
            sliders = switching.Add<CustomizationSelector>();
            Set(menu, "uiHandler", ui);
            Set(menu, "drivingHandler", driving);
            Set(menu, "customizationSelector", sliders);
            GiveEvents(ui, driving);
            menu.SwapMenuType(MenuType.PlayerSelect);
            Assert.IsFalse(sliders.gameObject.activeSelf, "Dispatch's sliders, closed (offline they'd show)");
        }

        void EndSwitching()
        {
            OnlinePlay.Instance = null;
            GameManager.instance = null;
            MainMenu.instance = null;
            if (switching != null)
                switching.DestroyAll();
            switching = null;
        }

        // I-2 and R1: a lobby switch has this machine offline for a moment, between sessions. Dispatch's buttons mustn't
        // fall through to Local Play: A doesn't ready (a solo local match). B cancels the player's Find and lands them
        // back in their own lobby, hosted again friends only as it was, still in Dispatch
        [Test]
        public void Switching_Offline_ADoesNothing_BHostsTheLobbyAgain()
        {
            try
            {
                SwitchAway(fill: false, quiet: false);

                ui.SouthFaceEvent.Invoke(true);
                Assert.IsFalse((bool)Get(menu, "readiedUp"), "A doesn't ready");

                ui.EastFaceEvent.Invoke(true);
                Assert.IsFalse(match.Searching, "B cancels Find");
                CollectionAssert.Contains(lobbies.Left, 70UL, "the switch's lobby is left");
                Assert.AreEqual(2, session.Leaves, "and the session joining its host");
                Assert.AreEqual(2, lobbies.Creates.Count, "the player's lobby is made again");
                Assert.IsTrue(match.Switching, "Dispatch stays up while it's made");
                ui.SouthFaceEvent.Invoke(true);
                Assert.IsFalse((bool)Get(menu, "readiedUp"), "A still waits");
                Assert.IsFalse(sliders.gameObject.activeSelf);

                lobbies.RaiseCreated(43);
                Assert.IsTrue(lobby.IsHost, "hosting again");
                Assert.AreEqual(43UL, lobby.Current);
                Assert.IsFalse(lobby.Filling, "friends only, as it was");
                Assert.IsEmpty(lobbies.Invites, "without the invite dialog");
                Assert.AreEqual(GameState.PlayerSelect, game.MainState, "still in player select");
            }
            finally
            {
                EndSwitching();
            }
        }

        // R1: a lobby open to anyone before the player's Find is hosted again open
        [Test]
        public void Switching_FindFromAnOpenLobby_BHostsItAgainOpen()
        {
            try
            {
                SwitchAway(fill: true, quiet: false);

                ui.EastFaceEvent.Invoke(true);
                lobbies.RaiseCreated(43);

                Assert.IsTrue(lobby.IsHost, "hosting again");
                Assert.IsTrue(lobby.Filling, "open to anyone, as it was");
                CollectionAssert.Contains(lobbies.PublicCalls, (43UL, true));
                Assert.AreEqual(GameState.PlayerSelect, game.MainState, "still in player select");
            }
            finally
            {
                EndSwitching();
            }
        }

        // R1: B on a lone Fill host's quiet search again (its ticket's B says LEAVE) is the leave it meant: the title
        // screen, as a lone host's B always went, and no lobby is hosted again
        [Test]
        public void Switching_QuietSearch_BGoesToTheTitleScreen()
        {
            try
            {
                SwitchAway(fill: true, quiet: true);

                ui.EastFaceEvent.Invoke(true);

                Assert.AreEqual(GameState.Menu, game.MainState, "the title screen");
                Assert.IsFalse(match.ResearchingQuietly);
                Assert.IsFalse(match.Switching);
                CollectionAssert.Contains(lobbies.Left, 70UL, "the switch's lobby is left");
                Assert.AreEqual(1, lobbies.Creates.Count, "no lobby hosted again");
            }
            finally
            {
                EndSwitching();
            }
        }

        // A host in lobby 42 in player select, online, with its own lobby and matchmaker (OnlinePlay.Lobby is set by
        // reflection: UseDirect's lobby would host a real session). Lobby 70 is open for a Find. Ends with EndHosting
        void HostOnline(bool fill)
        {
            switching = new TestObjects();
            GameAuthority.Role = NetworkRole.Host;
            game = switching.Add<GameManager>();
            GameManager.instance = game;
            game.SetGameState(GameState.PlayerSelect);
            MainMenu.instance = switching.Add<MainMenu>();

            Clock clock = new Clock();
            lobbies = new FakeLobbyService { Me = 500 };
            session = new FakeSessionControl();
            lobby = new OnlineLobby(lobbies, session, "1.2", clock.Read);
            lobby.ShowState(GameState.PlayerSelect);
            match = new Matchmaker(lobbies, lobby, "1.2", clock.Read, NoOtherMachines, InTheMenus);
            lobby.Host(invite: false);
            lobbies.RaiseCreated(42);
            lobby.SetFill(fill);
            lobbies.AddLobby(70, LobbyRules.GAME, "1.2", 170);
            lobbies.SetData(70, MatchRules.MM_KEY, MatchRules.MM_ON);
            lobbies.SetData(70, MatchRules.OPEN_KEY, "3");
            Assert.IsTrue(lobby.IsHost);

            OnlinePlay play = switching.Add<OnlinePlay>();
            play.Match = match;
            typeof(OnlinePlay).GetProperty("Lobby").SetValue(play, lobby);
            OnlinePlay.Instance = play;
            menu = switching.Add<MenuInteractions>();
            ui = switching.Add<PlayerUIHandler>();
            InputManager driving = switching.Add<InputManager>();
            sliders = switching.Add<CustomizationSelector>();
            Set(menu, "uiHandler", ui);
            Set(menu, "drivingHandler", driving);
            Set(menu, "customizationSelector", sliders);
            GiveEvents(ui, driving);
            menu.SwapMenuType(MenuType.PlayerSelect);
        }

        void EndHosting()
        {
            GameAuthority.Role = NetworkRole.Offline;
            EndSwitching();
        }

        [Test]
        public void Online_LeftShoulder_OnTheHost_FindsAMatch()
        {
            try
            {
                HostOnline(fill: false);
                Assert.IsFalse(match.Searching);

                ui.LeftShoulderEvent.Invoke(true);

                Assert.IsTrue(match.Searching, "L1 starts a search");
            }
            finally
            {
                EndHosting();
            }
        }

        [Test]
        public void Offline_LeftShoulder_DoesNothing()
        {
            try
            {
                HostOnline(fill: false);
                GameAuthority.Role = NetworkRole.Offline;

                ui.LeftShoulderEvent.Invoke(true);

                Assert.IsFalse(match.Searching, "no search offline");
            }
            finally
            {
                EndHosting();
            }
        }

        [Test]
        public void Online_B_WhileSearching_CancelsTheFind_AndDoesNotUnreadyOrLeave()
        {
            try
            {
                HostOnline(fill: false);
                ui.LeftShoulderEvent.Invoke(true);
                Assert.IsTrue(match.Searching);
                Set(menu, "readiedUp", true);

                ui.EastFaceEvent.Invoke(true);

                Assert.IsFalse(match.Searching, "B cancels the search");
                Assert.IsTrue((bool)Get(menu, "readiedUp"), "still ready");
                Assert.IsTrue(lobby.IsHost, "still hosting");
                Assert.AreEqual(42UL, lobby.Current, "still in the lobby");
                Assert.AreEqual(0, session.Leaves, "the session isn't left");
                Assert.AreEqual(GameState.PlayerSelect, game.MainState, "still in player select");
            }
            finally
            {
                EndHosting();
            }
        }

        [Test]
        public void Online_RightShoulder_OnTheHost_OpensTheLobby_ThenClosesItAgain()
        {
            try
            {
                HostOnline(fill: false);
                Assert.IsFalse(lobby.Filling);

                ui.RightShoulderEvent.Invoke(true);
                Assert.IsTrue(lobby.Filling, "R1 opens it to anyone");

                ui.RightShoulderEvent.Invoke(true);
                Assert.IsFalse(lobby.Filling, "and back to friends only");
            }
            finally
            {
                EndHosting();
            }
        }

        static object Get(MenuInteractions menu, string field)
        {
            return typeof(MenuInteractions).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .GetValue(menu);
        }

        class NorthRecorder
        {
            public int Presses;

            public void Press(bool pressed)
            {
                Presses++;
            }
        }
    }
}

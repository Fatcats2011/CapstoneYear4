using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Machines that leave or stop answering mid-match, on this computer (127.0.0.1). The other machine is a session
    /// without the game:
    /// - A machine that goes silent (its Unity Transport switched off: a crashed machine sends nothing) is dropped after
    ///   the timeout, 2 s here. The host plays on without it, and a client without its host goes back to the menu.
    /// - A host whose own connection fails goes back to the menu too.
    /// - The host leaving during the opening cutscene (this machine paused), or during either half of a load: this machine
    ///   ends on the title screen, says why, and never starts the match.
    /// The cutscene and load tests load the game scene: a minute or two each. No lambda captures a local: after
    /// EnterPlayMode even assigning one throws
    /// </summary>
    public class DisconnectsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string MENU = "Alex Player Testing";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7809;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode
        const int SILENT_MS = 2000; // how long the silence tests' sessions wait for a machine that stopped answering

        // Mid-match in the menu scene: the state a match is in from its opening on (SpawnManager). The opening's own state
        // needs the match scene (its players' order handlers reach for the order manager)
        const GameState MAIN_LOOP = GameState.MainLoop;

        // Why a session ended by itself
        class EndedRecorder
        {
            public string Reason;

            public void OnEnded(string reason)
            {
                Reason = reason;
            }
        }

        // How often the game headed back to the menu
        class ReturnRecorder
        {
            public int Returns;

            public void OnReturn()
            {
                Returns++;
            }
        }

        // Every state the game switched to
        class StateRecorder
        {
            public readonly List<GameState> States = new List<GameState>();

            public void OnState(GameState state)
            {
                States.Add(state);
            }
        }

        // Every scene that loaded, from when it's made until it's disposed
        class SceneRecorder : IDisposable
        {
            public readonly List<string> Loaded = new List<string>();

            public SceneRecorder()
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnLoaded;
            }

            void OnLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
            {
                Loaded.Add(scene.name);
            }

            public void Dispose()
            {
                UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnLoaded;
            }
        }

        // The game scene's load times this machine logged (LoadWatch)
        class LoadLineRecorder : IDisposable
        {
            static readonly Regex LINE = new Regex(@"^Online: the Game scene was ready here after \d+\.\d s; its longest frame took \d+\.\d s$");

            public readonly List<string> Lines = new List<string>();

            public LoadLineRecorder()
            {
                Application.logMessageReceived += OnLog;
            }

            void OnLog(string message, string stackTrace, LogType type)
            {
                if (type == LogType.Log && LINE.IsMatch(message))
                    Lines.Add(message);
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
            }
        }

        // Which machines told the host they have the game loaded
        class ReportRecorder
        {
            public readonly List<ulong> Machines = new List<ulong>();

            public ReportRecorder(OnlineMatch match)
            {
                match.MachineLoaded += OnLoaded;
            }

            void OnLoaded(ulong machine, MatchScene scene)
            {
                if (scene == MatchScene.Game)
                    Machines.Add(machine);
            }
        }

        // The joining tests' sessions (JoinAHost)
        class Sessions
        {
            public OnlineSession Host; // another machine: a session without the game
            public OnlineSession Mine;
        }

        // What happened here after the host left during a load (HostLeavesDuringTheLoad)
        class AfterTheLoad
        {
            public readonly StateRecorder States = new StateRecorder();
            public SceneRecorder Scenes;
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
            OrderSync.Reset();
            TutorialSync.Reset();
            RespawnSync.Reset();
            CueSync.Reset();
            StealSync.Reset();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static string ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        // Whether this machine shows a cutscene
        static bool CutsceneOn()
        {
            return CutsceneManager.Instance != null && CutsceneManager.Instance.cutsceneCamera.enabled;
        }

        static MenuInteractions MenuOf(int slot)
        {
            return Slot(slot).Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>();
        }

        // A machine that stops answering, as a crashed one does: Unity Transport sends and beats only from its Update
        static void GoSilent(OnlineSession session)
        {
            session.Direct.enabled = false;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        // The title screen is up, with one player here
        static IEnumerator OnePlayerAtTheTitle()
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "a player");
        }

        // Joining tests: another machine hosts, and this machine joins it with the game: its player moves to seat 2. Both
        // drop a machine they haven't heard from in disconnectTimeoutMs
        static IEnumerator JoinAHost(Sessions sessions, int disconnectTimeoutMs)
        {
            sessions.Host = OnlineSession.Create(VERSION);
            sessions.Host.Direct.DisconnectTimeoutMS = disconnectTimeoutMs;
            Assert.IsTrue(sessions.Host.HostDirect(THIS_COMPUTER, PORT), "the other machine hosts");
            sessions.Mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(sessions.Mine);
            sessions.Mine.Direct.DisconnectTimeoutMS = disconnectTimeoutMs;
            sessions.Mine.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player moved to seat 2");
        }

        // Joining tests: the host loads the game as a real host does (its load request, then the Loading state its own load
        // sends), and leaves while this machine still loads it, or once it's loaded and held for the host's show. Then waits
        // for this machine's title screen
        static IEnumerator HostLeavesDuringTheLoad(Sessions sessions, bool loaded, AfterTheLoad after, LogCollector log)
        {
            ReportRecorder reports = new ReportRecorder(sessions.Host.Match);
            sessions.Host.Match.RequestLoad(MatchScene.Game);
            sessions.Host.Match.SendState(GameState.Loading);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Loading && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Loading, State(), "on the loading screen");

            if (loaded)
            {
                deadline = Time.realtimeSinceStartup + LOADING;
                while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.IsNotEmpty(reports.Machines, "loaded, and held for the host's show");
            }
            else
                Assert.IsEmpty(reports.Machines, "still loading");

            GameManager.Instance.StateApplied += after.States.OnState;
            after.Scenes = new SceneRecorder();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            sessions.Host.Leave();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == MENU && State() == GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.StateApplied -= after.States.OnState;
            after.Scenes.Dispose();
        }

        [UnityTest]
        public IEnumerator Hosting_AMachineGoesSilent_ItsPlayerGoes_AndTheHostPlaysOn()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            host.Direct.DisconnectTimeoutMS = SILENT_MS;
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.Direct.DisconnectTimeoutMS = SILENT_MS;
            other.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Role == NetworkRole.Client && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");

            // Mid-match (MAIN_LOOP), the other machine stops answering
            GameManager.Instance.SetGameState(MAIN_LOOP);
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            GoSilent(other);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(1) != null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsNull(Slot(1), "their scooter went, as if they'd left");
            Assert.AreEqual(1, host.PlayersIn);
            Assert.IsTrue(host.IsRunning, "the host plays on");
            Assert.AreEqual(MAIN_LOOP, State(), "the match goes on");

            other.Direct.enabled = true;
            other.Leave();
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostGoesSilentMidMatch_ThisMachineHeadsBackToTheMenu_AndSaysWhy()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();
            Sessions sessions = new Sessions();
            yield return JoinAHost(sessions, SILENT_MS);
            OnlineSession host = sessions.Host;

            // Mid-match (MAIN_LOOP), the host stops answering
            host.Match.SendState(MAIN_LOOP);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != MAIN_LOOP && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MAIN_LOOP, State(), "mid-match");
            ReturnRecorder returns = new ReturnRecorder();
            SceneManager.Instance.OnReturnToMenu += returns.OnReturn;
            EndedRecorder ended = new EndedRecorder();
            sessions.Mine.Ended += ended.OnEnded;
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            GoSilent(host);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            // In the menu scene the way back loads nothing: the menu is up already
            Assert.AreEqual(OnlineSession.HOST_LEFT, ended.Reason);
            Assert.AreEqual(1, returns.Returns, "back to the menu");
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);
            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown);

            SceneManager.Instance.OnReturnToMenu -= returns.OnReturn;
            host.Direct.enabled = true;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_ItsConnectionFailsMidMatch_TheHostHeadsBackToTheMenu_AndSaysWhy()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(MAIN_LOOP); // mid-match
            ReturnRecorder returns = new ReturnRecorder();
            SceneManager.Instance.OnReturnToMenu += returns.OnReturn;
            EndedRecorder ended = new EndedRecorder();
            host.Ended += ended.OnEnded;

            host.Network.Shutdown(); // Netcode stopping by itself (a transport failure), not through Leave
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.CONNECTION_LOST, ended.Reason);
            Assert.AreEqual(1, returns.Returns, "back to the menu");
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.AreEqual(OnlineSession.CONNECTION_LOST, ControllerPrompts.Instance.HintText);
            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown);

            SceneManager.Instance.OnReturnToMenu -= returns.OnReturn;
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostLeavesDuringTheOpeningCutscene_ThisMachineGoesBackToTheMenu_WithItsPauseClosed()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();
            LoadLineRecorder loadLines = new LoadLineRecorder();
            Sessions sessions = new Sessions();
            yield return JoinAHost(sessions, OnlineSession.DIRECT_DISCONNECT_MS);
            OnlineSession host = sessions.Host;
            ReportRecorder reports = new ReportRecorder(host.Match);

            // The host's match comes up here, with its opening cutscene
            host.Match.RequestLoad(MatchScene.Game);
            float deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Match.RequestShow(MatchScene.Game);
            host.Match.SendState(GameState.StartingCutscene);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.StartingCutscene && CutsceneOn()) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(CutsceneOn(), "the opening cutscene plays here");
            yield return WaitSeconds(0.5f); // the load's time goes to the log 2 frames after the scene is up

            // This machine's player pauses (driving, Start opens their pause menu)
            MenuOf(1).SwapMenuType(MenuType.PauseMenu);
            PlayerInstantiate.Instance.PlayerPause(Slot(1).Input);
            Assert.IsTrue(PlayerInstantiate.Instance.IsPaused, "paused");

            // The host leaves
            StateRecorder states = new StateRecorder();
            GameManager.Instance.StateApplied += states.OnState;
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            host.Leave();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == MENU && State() == GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.DoesNotContain(states.States, GameState.Tutorial, "the cutscene stopped with the load: it asked for nothing");
            Assert.AreEqual(GameState.Menu, State(), "the title screen");
            Assert.IsFalse(PlayerInstantiate.Instance.IsPaused, "its pause menu closed");
            Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);
            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown, "said again once the menu is up");
            Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "this machine's player is still here");
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.AreEqual(1, loadLines.Lines.Count, "this machine logged its load's time");

            GameManager.Instance.StateApplied -= states.OnState;
            loadLines.Dispose();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostLeavesWhileThisMachineLoads_ItGoesBackToTheMenu_WithoutStartingTheMatch()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();
            Sessions sessions = new Sessions();
            yield return JoinAHost(sessions, OnlineSession.DIRECT_DISCONNECT_MS);

            AfterTheLoad after = new AfterTheLoad();
            yield return HostLeavesDuringTheLoad(sessions, false, after, log);

            CollectionAssert.AreEqual(new[] { GAME, MENU }, after.Scenes.Loaded, "the load finished, then the menu");
            CollectionAssert.DoesNotContain(after.States.States, GameState.StartingCutscene, "the match never started");
            Assert.AreEqual(GameState.Menu, State(), "the title screen");
            Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);

            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostLeavesWhileThisMachinesLoadWaits_ItGoesBackToTheMenu_WithoutStartingTheMatch()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();
            Sessions sessions = new Sessions();
            yield return JoinAHost(sessions, OnlineSession.DIRECT_DISCONNECT_MS);

            AfterTheLoad after = new AfterTheLoad();
            yield return HostLeavesDuringTheLoad(sessions, true, after, log);

            CollectionAssert.AreEqual(new[] { GAME, MENU }, after.Scenes.Loaded, "the load showed, then the menu");
            CollectionAssert.DoesNotContain(after.States.States, GameState.StartingCutscene, "the match never started");
            Assert.AreEqual(GameState.Menu, State(), "the title screen");
            Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);

            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostReturnsWhileThisMachinesSceneComesUp_ThisMachineReachesTheMenu()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();
            Sessions sessions = new Sessions();
            yield return JoinAHost(sessions, OnlineSession.DIRECT_DISCONNECT_MS);

            // This machine loads the game and holds it for the host's show
            ReportRecorder reports = new ReportRecorder(sessions.Host.Match);
            sessions.Host.Match.RequestLoad(MatchScene.Game);
            sessions.Host.Match.SendState(GameState.Loading);
            float deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotEmpty(reports.Machines, "loaded, and held for the host's show");

            // The host shows the match, then takes everyone back a few frames later, while this (slow) machine's scene is
            // still coming up
            sessions.Host.Match.RequestShow(MatchScene.Game);
            sessions.Host.Match.SendState(GameState.StartingCutscene);
            OnlineSceneFlow flow = (OnlineSceneFlow)SceneFlow.Current;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!flow.Changing && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(flow.Changing, "the return reaches this machine while its scene still comes up");
            sessions.Host.Match.RequestReturn();
            sessions.Host.Match.SendState(GameState.PlayerSelect);

            // The game scene comes up, then the menu follows (after the loading screen's delay)
            deadline = Time.realtimeSinceStartup + LOADING;
            while (flow.Changing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(flow.Changing, "the game scene came up");
            Assert.AreNotEqual(MENU_SCENE, UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, "the game scene shows first");
            deadline = Time.realtimeSinceStartup + 30f;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != MENU_SCENE && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;

            Assert.AreEqual(MENU_SCENE, UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, "back in the menu scene");
            Assert.IsTrue(LobbyRules.InTheMenus(State()), "in the menus: " + State());

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            sessions.Mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (sessions.Host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            sessions.Host.Leave();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            log.Dispose();
        }

        [UnityTest]
        public IEnumerator Joining_TheHostReturnsWhileThisMachinesSceneComesUp_NoMatchStateShowsHere()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return OnePlayerAtTheTitle();
            Sessions sessions = new Sessions();
            yield return JoinAHost(sessions, OnlineSession.DIRECT_DISCONNECT_MS);

            // This machine loads the game and holds it for the host's show
            ReportRecorder reports = new ReportRecorder(sessions.Host.Match);
            sessions.Host.Match.RequestLoad(MatchScene.Game);
            sessions.Host.Match.SendState(GameState.Loading);
            float deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotEmpty(reports.Machines, "loaded, and held for the host's show");

            // The host shows the match, starts it, and takes everyone back at once: here the states wait for the scene
            StateRecorder states = new StateRecorder();
            GameManager.Instance.StateApplied += states.OnState;
            SceneRecorder scenes = new SceneRecorder();
            sessions.Host.Match.RequestShow(MatchScene.Game);
            sessions.Host.Match.SendState(GameState.StartingCutscene);
            sessions.Host.Match.SendState(GameState.Tutorial);
            // A few frames later the host picks Main Menu, while this (slow) machine's scene is still coming up
            OnlineSceneFlow flow = (OnlineSceneFlow)SceneFlow.Current;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!flow.Changing && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            yield return null;
            Assert.IsTrue(flow.Changing, "the return reaches this machine while its scene still comes up");
            sessions.Host.Match.RequestReturn();
            // The scene comes up: what waited for it belonged to the match being left
            deadline = Time.realtimeSinceStartup + LOADING;
            while (flow.Changing && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(flow.Changing, "the scene came up");
            for (float until = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < until;)
                yield return null;
            GameManager.Instance.StateApplied -= states.OnState;
            scenes.Dispose();

            CollectionAssert.DoesNotContain(states.States, GameState.StartingCutscene, "the abandoned match's states never show");
            CollectionAssert.DoesNotContain(states.States, GameState.Tutorial);

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            sessions.Mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (sessions.Host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            sessions.Host.Leave();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

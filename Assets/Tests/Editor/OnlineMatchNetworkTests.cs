using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// An online match on this computer (127.0.0.1), from the menu into the game scene and back.
    /// - Hosting: this machine hosts with the game. Another machine is a session without the game, which reports its
    ///   load when the test says.
    /// - Joining: this machine joins with the game. The host is a session without the game, whose requests and states
    ///   the test sends.
    /// Loads the game scene: about a minute each. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineMatchNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string MENU = "Alex Player Testing";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7796;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        // What another machine hears from the host's scene flow
        class LoadRecorder
        {
            public readonly List<MatchScene> Loads = new List<MatchScene>();
            public readonly List<MatchScene> Shows = new List<MatchScene>();
            public int Returns;

            public LoadRecorder(OnlineMatch match)
            {
                match.LoadRequested += OnLoad;
                match.ShowRequested += OnShow;
                match.ReturnRequested += OnReturn;
            }

            void OnLoad(MatchScene scene) { Loads.Add(scene); }
            void OnShow(MatchScene scene) { Shows.Add(scene); }
            void OnReturn() { Returns++; }
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

        static string ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
        }

        // The game scene is loaded here, held behind the loading screen until it's shown
        static bool HeldHere()
        {
            SceneManager loader = SceneFlow.Loader as SceneManager;
            return loader != null && Reflect.GetField(loader, "sceneLoad") != null;
        }

        static GameObject[] GameSpawns()
        {
            return (GameObject[])Reflect.GetField(SpawnManager.Instance, "gameSpawnPositions");
        }

        [UnityTest]
        public IEnumerator Hosting_TheMatchStarts_OnceEveryMachineHasItLoaded_AndTheHostTakesEveryoneBack()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            LoadRecorder theirs = new LoadRecorder(other.Match);
            Vector3 theirSpot = ScooterIn(1).Sphere.transform.position; // where the other machine's scooter stands in the menu

            // Everyone's ready: the host asks every machine to load the game, and loads it behind its own loading screen
            SceneFlow.Current.LoadGameScene();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (theirs.Loads.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, theirs.Loads, "the other machine is asked to load the game");

            // The host has it loaded; the other machine hasn't said so: the host waits
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!HeldHere() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(HeldHere(), "the game is loaded here, behind the loading screen");
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "still waiting for the other machine");
            CollectionAssert.IsEmpty(theirs.Shows, "nobody's shown the game yet");

            // The other machine has it too: the match starts everywhere
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GAME, ActiveScene());
            Assert.AreEqual(GameState.MainLoop, State(), "the opening cutscene started, and the players are placed");
            deadline = Time.realtimeSinceStartup + WAIT; // the host can be in its match before the other machine hears
            while (theirs.Shows.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, theirs.Shows, "the other machine shows it too");

            // Each machine places its own scooter: this one on seat 1's spawn point. The other machine's is where its owner
            // puts it; it hasn't shared a pose, so it's still where it stood in the menu
            Assert.Less(Vector3.Distance(GameSpawns()[0].transform.position, ScooterIn(0).Sphere.transform.position), 1.5f, "this machine's scooter on its spawn point");
            Assert.Less(Vector3.Distance(theirSpot, ScooterIn(1).Sphere.transform.position), 0.01f, "the other machine's scooter isn't placed here");

            // The first wave starts after the opening cutscene, without the tutorial: online it's skipped until orders are shared
            deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");
            Assert.IsTrue(Slot(0).Player.GetComponentInChildren<TutorialHandler>().HasLearnt, "this machine's player is done with the tutorial");

            // The match clock reaches the other machine as an end time in server time: its time left matches the host's
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!other.Match.ClockStarted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(other.Match.ClockStarted, "the waves started");
            Assert.AreEqual(OrderManager.Instance.GameTimer, MatchClock.Remaining(other.Match.ClockEnd, other.Network.ServerTime.Time), 1f, "the other machine's time left");

            // Back to the menu: the host takes everyone
            SceneFlow.Current.ReturnToMenu();
            deadline = Time.realtimeSinceStartup + 60;
            while (ActiveScene() != MENU && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "back in the menu");
            Assert.AreEqual(1, theirs.Returns, "the other machine is taken back too");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_AMachineLeavingMidMatch_TakesItsScooter_AndTheMatchGoesOn()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");

            // The match starts on both machines (this machine's player readied up for it)
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, State(), "the match started");
            GameObject theirScooter = Slot(1).Player;

            // They leave mid-match: their scooter goes, and the match goes on (nobody in the match counts down to a new one)
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            Assert.IsNull(Slot(1), "their seat is free");
            Assert.IsTrue(theirScooter == null, "their scooter went");
            Assert.IsNull(Reflect.GetField(PlayerInstantiate.Instance, "readyUpCountdown"), "nobody in the match counts down to a new one");
            Assert.AreEqual(GAME, ActiveScene(), "the match goes on");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostsMatchLoadsHere_ItsStatesWaitForTheScene_AndTheHostBringsUsBack()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
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
            ReportRecorder reports = new ReportRecorder(host.Match);
            Vector3 hostSpot = ScooterIn(0).Sphere.transform.position; // where the host's scooter stands in the menu

            // The host asks for the game: it loads here behind the loading screen, and the host hears when it's ready
            host.Match.RequestLoad(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { mine.Network.LocalClientId }, reports.Machines, "this machine says it has the game loaded");
            Assert.AreEqual(MENU, ActiveScene(), "held until the host shows it");

            // The host shows it and starts the match at once: those states wait here until the scene is up
            host.Match.RequestShow(MatchScene.Game);
            host.Match.SendState(GameState.StartingCutscene);
            host.Match.SendState(GameState.MainLoop);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GAME, ActiveScene());
            Assert.AreEqual(GameState.MainLoop, State(), "the host's states arrived in order");
            Assert.Less(Vector3.Distance(GameSpawns()[1].transform.position, ScooterIn(1).Sphere.transform.position), 1.5f,
                "the opening cutscene put this machine's scooter on seat 2's spawn point: the state waited for the scene");

            // Another machine's scooter isn't placed here: its owner places it (it hasn't shared a pose: it's where it stood)
            Assert.Less(Vector3.Distance(hostSpot, ScooterIn(0).Sphere.transform.position), 0.01f, "the host's scooter isn't placed here");

            // The host skips the tutorial online: this machine's player finishes it at once, then the host starts the first wave
            host.Match.SendState(GameState.Tutorial);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Slot(1).Player.GetComponentInChildren<TutorialHandler>().HasLearnt && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1).Player.GetComponentInChildren<TutorialHandler>().HasLearnt, "this machine's player is done with the tutorial");
            host.Match.SendState(GameState.Begin);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave, as the host says");

            // The host's match clock shows here: its end, in the server time both machines share
            host.Match.ShareClock(host.Network.ServerTime.Time + 100, true, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!OrderManager.Instance.GameStarted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(OrderManager.Instance.GameStarted, "the waves started, as on the host");
            Assert.AreEqual(100f, OrderManager.Instance.GameTimer, 1.5f, "the host's time left");

            // The host takes everyone back to the menu
            host.Match.RequestReturn();
            deadline = Time.realtimeSinceStartup + 60;
            while (ActiveScene() != MENU && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "back in the menu");
            Assert.IsTrue(mine.IsRunning, "still in the session");

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
    }
}

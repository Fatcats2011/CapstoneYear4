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
    /// The host's match moments on a client, on this computer (127.0.0.1). This machine joins with the game; the host is a
    /// session without the game. Its opening cutscene ends when the host's next state comes, and only the host can skip
    /// it. The host's clock rings here: a new wave's bells, then time up, when this machine's player stops and the
    /// whistle blows. Loads the game scene: a minute or two. No lambda captures a local: after EnterPlayMode even
    /// assigning one throws
    /// </summary>
    public class OnlineMatchFlowNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7808;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

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
            OrderSync.Reset();
            TutorialSync.Reset();
            RespawnSync.Reset();
            CueSync.Reset();
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

        // Whether this machine shows a cutscene
        static bool CutsceneOn()
        {
            return CutsceneManager.Instance != null && CutsceneManager.Instance.cutsceneCamera.enabled;
        }

        // Whether this machine's player's ball is held still (the end of the main game)
        static bool Stopped(Rigidbody ball)
        {
            return (ball.constraints & RigidbodyConstraints.FreezePositionX) != 0;
        }

        [UnityTest]
        public IEnumerator Joining_TheHostsCutscenesAndClock_PlayHereAsOnTheHost()
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

            // The host's match comes up here, with its opening cutscene
            host.Match.RequestLoad(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Match.RequestShow(MatchScene.Game);
            host.Match.SendState(GameState.StartingCutscene);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.StartingCutscene && CutsceneOn()) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(CutsceneOn(), "the opening cutscene plays here");
            float started = Time.realtimeSinceStartup;

            // Only the host can skip it
            CutsceneManager.Instance.Skip();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsTrue(CutsceneOn(), "only the host can skip");
            Assert.AreEqual(GameState.StartingCutscene, State());

            // The host skipped: its next state ends the cutscene here at once
            host.Match.SendState(GameState.Tutorial);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(State() == GameState.Tutorial && !CutsceneOn()) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State());
            Assert.IsFalse(CutsceneOn(), "it ends with the host's");
            Assert.Less(Time.realtimeSinceStartup - started, 8f, "long before its own 9.2 s ran out");

            // The host's clock: a new wave's bells
            AudioSource clock = OrderManager.Instance.clockSource;
            clock.clip = null; // whatever the scene left on it
            AudioClip bells = SoundManager.Instance.GetSFX("bells").clip;
            host.Match.RingClock(ClockCue.WaveBells);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (clock.clip != bells && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(bells, clock.clip, "the bells ring here");

            // Time up: this machine's player stops, and the whistle blows
            Rigidbody ball = ScooterIn(1).Sphere.GetComponent<Rigidbody>();
            Assert.IsFalse(Stopped(ball), "driving before the whistle");
            host.Match.RingClock(ClockCue.TimeUp);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Stopped(ball) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Stopped(ball), "this machine's player stops");
            Assert.AreSame(SoundManager.Instance.GetSFX("timeout").clip, clock.clip, "the whistle");

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

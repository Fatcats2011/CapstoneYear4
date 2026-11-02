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
    /// An online match has no tutorial (playtest, 2026-10-31: the tutorial is for local play, not the multiplayer lobbies'
    /// loop): on this computer (127.0.0.1) this machine hosts with the game, the other machine is a session without it.
    /// The opening cutscene hands straight over to the first wave, this machine's player starts in the city, and no
    /// tutorial order comes out. Loads the game scene: a minute or two. No lambda captures a local: after EnterPlayMode
    /// even assigning one throws
    /// </summary>
    public class OnlineTutorialNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7801;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        /// <summary>
        /// Remembers the order changes a machine heard from the host
        /// </summary>
        class ChangeRecorder
        {
            public readonly List<OrderChange> Changes = new List<OrderChange>();

            public ChangeRecorder(OnlineMatch match)
            {
                match.OrderReceived += OnChange;
            }

            void OnChange(OrderChange change)
            {
                Changes.Add(change);
            }
        }

        /// <summary>
        /// Remembers the last seat message the host heard (who sent it, which seat), and how many it heard
        /// </summary>
        class SeatRecorder
        {
            public ulong Machine;
            public int Seat = -1;
            public int Count;

            public void Heard(ulong machine, int seat)
            {
                Machine = machine;
                Seat = seat;
                Count++;
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

        static TutorialHandler TutorialHandlerOf(int seat)
        {
            return Slot(seat).Player.GetComponentInChildren<TutorialHandler>();
        }

        // Where the players start: the start of their seat's tutorial lane
        static GameObject[] TutorialStarts()
        {
            return SpawnManager.Instance.gameSpawnPositions;
        }

        [UnityTest]
        public IEnumerator Hosting_AnOnlineMatch_SkipsTheTutorial_AndStartsInTheCity()
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
            ChangeRecorder heard = new ChangeRecorder(other.Match);

            // The match starts on both machines, and goes from the opening cutscene straight to the first wave
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == GAME && State() == GameState.Begin) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave, with no tutorial waiting");
            Assert.IsTrue(TutorialHandlerOf(0) == null || TutorialHandlerOf(0).HasLearnt, "this machine's player starts as if they'd learnt it");

            // In the city, on a respawn point, not at the start of the tutorial lane
            Vector3 scooter = ScooterIn(0).Sphere.transform.position;
            Assert.Greater(Vector3.Distance(TutorialStarts()[0].transform.position, scooter), 1.5f, "not at the start of its lane");
            float nearest = float.MaxValue;
            foreach (RespawnPoint point in Object.FindObjectsOfType<RespawnPoint>())
                nearest = Mathf.Min(nearest, Vector3.Distance(point.PlayerSpawn, scooter));
            Assert.Less(nearest, 3f, "on a city respawn point");

            // No tutorial order came out for either lane
            Order[] lessons = OrderManager.Instance.tutorialOrders;
            foreach (Order lesson in lessons)
                Assert.IsFalse(heard.Changes.Contains(OrderChange.Spawn(lesson.Key, false)), "no tutorial order: " + lesson.name);

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
    }
}

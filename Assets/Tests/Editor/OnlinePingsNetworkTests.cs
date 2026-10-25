using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Connection info online, on this computer (127.0.0.1). This machine hosts with the game; the other is a session
    /// without the game. Every couple of seconds the host measures each seated machine's round trip (its own is 0) and
    /// the other machine hears it. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlinePingsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7834;
        const string VERSION = "test";
        const float WAIT = 15f;

        class Heard
        {
            public PingBoard Board = PingBoard.Unknown;
            public int Times;

            public Heard(OnlineMatch match)
            {
                match.PingsReceived += OnPings;
            }

            void OnPings(PingBoard board)
            {
                Board = board;
                Times++;
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
            PingSync.Reset();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Hosting_EachMachinesRoundTrip_ReachesTheOthers()
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
            OnlineSession other = OnlineSession.Create(VERSION);
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(PlayerInstantiate.Instance.Roster[1] != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(other.Match, "joined");
            Heard heard = new Heard(other.Match);

            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Board.Of(1) < 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(0, heard.Board.Of(0), "the host's own seat");
            Assert.GreaterOrEqual(heard.Board.Of(1), 0, "their round trip was measured");
            Assert.Less(heard.Board.Of(1), 1000, "on this computer");
            Assert.AreEqual(-1, heard.Board.Of(2), "nobody in seat 3");
            Assert.AreEqual(heard.Board.Of(1), PingSync.Last.Of(1), "the host keeps the same board");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return Wait(1f);
            log.Dispose();
        }
    }
}

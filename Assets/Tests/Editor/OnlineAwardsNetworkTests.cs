using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The end-of-match awards online, on this computer (127.0.0.1). This machine hosts with the game; the other is a
    /// session without the game. The awards the host picks reach the other machine as they were. They're picked by hand:
    /// no match is needed. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineAwardsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7833;
        const string VERSION = "test";
        const float WAIT = 15f;

        // What the other machine heard from the host
        class Heard
        {
            public AwardBoard Board;
            public int Times;

            public Heard(OnlineMatch match)
            {
                match.AwardsReceived += OnAwards;
            }

            void OnAwards(AwardBoard board)
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
            AwardSync.Reset();
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
        public IEnumerator Hosting_TheAwardsPicked_ReachTheOtherMachine()
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
            while (!(PlayerInstantiate.Instance.Roster[1] != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(other.Match, "joined");
            Heard heard = new Heard(other.Match);

            MatchStats stats = new MatchStats();
            stats.Stole(1, GameState.Begin);
            stats.Stole(1, GameState.Begin);
            stats.Sank(0, GameState.Begin);
            AwardBoard picked = AwardBoard.From(stats, new[] { 0, 1 });
            AwardSync.Decide(picked);

            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Times == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, heard.Times, "the awards arrived once");
            Assert.AreEqual(picked, heard.Board);
            Assert.AreEqual(Award.Kleptoghost, heard.Board.AwardOf(1));
            Assert.AreEqual(Award.FishFood, heard.Board.AwardOf(0));

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

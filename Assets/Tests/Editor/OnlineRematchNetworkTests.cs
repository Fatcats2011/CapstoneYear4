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
    /// The results screen's rematch vote online, on this computer (127.0.0.1). This machine runs the game; the other is a
    /// session without the game. The host holds the vote: a client's vote counts only for its own seat, every machine
    /// sees the host's board, and the outcome reaches everyone (a rematch with no tutorial, or everyone back). A seat
    /// taken out of the rematch has its machine disconnected. The vote is opened by hand: the results scene isn't needed.
    /// No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineRematchNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7831;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        static double clockAhead; // added to the vote's clock: the countdown runs out at once

        static double AheadClock() { return Time.realtimeSinceStartupAsDouble + clockAhead; }

        // What the other machine heard from the host
        class Heard
        {
            public int Rematches, Returns;
            public readonly List<MatchScene> Loads = new List<MatchScene>();
            public RematchBoard Board;
            public int Boards;

            public Heard(OnlineMatch match)
            {
                match.RematchRequested += OnRematch;
                match.ReturnRequested += OnReturn;
                match.LoadRequested += OnLoad;
                match.BoardReceived += OnBoard;
            }

            void OnRematch() { Rematches++; }
            void OnReturn() { Returns++; }
            void OnLoad(MatchScene scene) { Loads.Add(scene); }
            void OnBoard(RematchBoard board) { Board = board; Boards++; }
        }

        // The host's side: votes it received from a client machine
        class Votes
        {
            public readonly List<int> Seats = new List<int>();

            public Votes(OnlineMatch match)
            {
                match.VoteReceived += OnVote;
            }

            void OnVote(ulong machine, int seat, bool rematch)
            {
                if (rematch)
                    Seats.Add(seat);
            }
        }

        // Every state this machine switched to
        class States
        {
            public readonly List<GameState> Applied = new List<GameState>();

            public States(GameManager game)
            {
                game.StateApplied += Applied.Add;
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
            RematchSync.Reset();
            TutorialManager.SkipNextTutorial = false;
            clockAhead = 0;
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

        static bool CutsceneOn()
        {
            return CutsceneManager.Instance != null && CutsceneManager.Instance.cutsceneCamera.enabled;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        static IEnumerator ToTheMenuWithAPlayer()
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        // The other machine leaves, then this one: on one computer Windows reports the closed port as an error, which
        // isn't a problem once the session is ending (LogCollector.CLOSED_PORT)
        static IEnumerator EndSession(LogCollector log, OnlineSession leaving, OnlineSession staying)
        {
            log.MachinesLeave();
            if (leaving != null)
                leaving.Leave();
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (staying.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            staying.Leave();
            yield return Wait(1f);
            log.Dispose();
        }

        static ResultsVote OpenVote(int hostSeat, bool decides)
        {
            ResultsVote vote = new GameObject("Results").AddComponent<ResultsVote>();
            vote.Open(new[] { 0, 1 }, hostSeat, decides);
            return vote;
        }

        [UnityTest]
        public IEnumerator Hosting_BothVoteRematch_TheClientIsToldAndTheMatchStartsWithoutTheTutorial()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheMenuWithAPlayer();

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            Heard heard = new Heard(other.Match);
            States states = new States(GameManager.Instance);
            ScoreManager.Instance.GetHandlerOfIndex(0).Score = 50; // the last match's score
            ResultsVote vote = OpenVote(0, true);

            // A vote for a seat that isn't theirs counts for nothing
            other.Match.SendVote(0, true);
            yield return Wait(1f);
            Assert.IsFalse(vote.Voted(0), "not their seat");

            // Their own seat's vote counts, and every machine sees it
            other.Match.SendVote(1, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(vote.Voted(1) && heard.Board.HasVoted(1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(vote.Voted(1), "their vote counts on the host");
            Assert.IsTrue(heard.Board.HasVoted(1), "and shows on their machine");

            // This machine's player votes too: the rematch starts at once on every machine
            vote.Press(0, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Loads.Contains(MatchScene.Game) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, heard.Rematches, "the client starts the next match fresh");
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, heard.Loads, "then loads the match");
            Assert.AreEqual(RematchOutcome.Rematch, heard.Board.Outcome);
            Assert.AreEqual(0, ScoreManager.Instance.GetHandlerOfIndex(0).Score, "scores start over");

            // The match comes up with its opening cutscene, then goes straight to the first wave
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == GAME && CutsceneOn()) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(CutsceneOn(), "the opening cutscene");
            CutsceneManager.Instance.Skip();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!states.Applied.Contains(GameState.Begin) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Contains(GameState.Begin, states.Applied, "the first wave");
            int tutorial = states.Applied.IndexOf(GameState.Tutorial);
            Assert.GreaterOrEqual(tutorial, 0, "the tutorial state hands over the driving controls");
            Assert.AreEqual(GameState.Begin, states.Applied[tutorial + 1], "and goes straight on to the first wave");
            Assert.IsFalse(TutorialManager.SkipNextTutorial, "used up: the next match teaches again");
            Rigidbody ball = Slot(0).Player.GetComponentInChildren<BallDriving>(true).Sphere.GetComponent<Rigidbody>();
            Assert.AreEqual(RigidbodyConstraints.None, ball.constraints & RigidbodyConstraints.FreezePositionX, "the scooter can drive");
            GameObject laneStart = Object.FindObjectOfType<SpawnManager>().gameSpawnPositions[0];
            Assert.Greater(Vector3.Distance(laneStart.transform.position, ball.position), 5f, "it starts in the city, not its tutorial lane");

            yield return EndSession(log, other, host);
        }

        [UnityTest]
        public IEnumerator Hosting_TheHostPicksBackToLobby_EveryoneGoesBack()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheMenuWithAPlayer();

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION);
            other.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Heard heard = new Heard(other.Match);
            ResultsVote vote = OpenVote(0, true);
            other.Match.SendVote(1, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!vote.Voted(1) && Time.realtimeSinceStartup < deadline)
                yield return null;

            vote.Press(0, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Returns == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, heard.Returns, "the host can't leave alone: everyone goes back");
            Assert.AreEqual(0, heard.Rematches);

            yield return EndSession(log, other, host);
        }

        [UnityTest]
        public IEnumerator Hosting_AClientPicksBackToLobby_TheHostRematchesAlone()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheMenuWithAPlayer();

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION);
            other.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            ResultsVote vote = OpenVote(0, true);

            // Back to lobby on a client: that machine leaves the session
            log.MachinesLeave();
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(1) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(Slot(1), "their seat is gone");

            vote.Press(0, true);
            yield return null;
            yield return null;

            Assert.IsTrue(TutorialManager.SkipNextTutorial, "the host rematches alone: nobody else to wait for");

            yield return EndSession(log, null, host);
        }

        [UnityTest]
        public IEnumerator Hosting_AClientWhoDoesntVote_IsTakenOutWhenTimeRunsOut()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheMenuWithAPlayer();

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION);
            other.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            ResultsVote vote = OpenVote(0, true);
            vote.Press(0, true);

            clockAhead = RematchVote.SECONDS + 1;
            vote.Clock = AheadClock;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (other.Network != null && other.Network.IsConnectedClient && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsFalse(other.Network != null && other.Network.IsConnectedClient, "their machine is disconnected");

            yield return EndSession(log, null, host);
        }

        [UnityTest]
        public IEnumerator Joining_ThisMachinesVoteGoesToTheHost_AndARematchSkipsTheTutorialHere()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheMenuWithAPlayer();

            OnlineSession host = OnlineSession.Create(VERSION); // another machine hosting: a session without the game
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(mine);
            mine.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && host.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player is in seat 2");
            Votes votes = new Votes(host.Match);
            ResultsVote vote = OpenVote(0, false);

            vote.Press(1, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (votes.Seats.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { 1 }, votes.Seats, "the host heard this machine's vote");

            host.Match.RequestRematch();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!TutorialManager.SkipNextTutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(TutorialManager.SkipNextTutorial, "the next match here skips its tutorial");

            yield return EndSession(log, mine, host);
        }
    }
}

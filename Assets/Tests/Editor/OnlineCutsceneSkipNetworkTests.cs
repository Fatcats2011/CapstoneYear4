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
    /// Skipping a cutscene online, on this computer (127.0.0.1). This machine hosts with the game; the other is a session
    /// without the game. A client's vote counts only for its own seat, it sees the host's count, and once every seat has
    /// voted the host skips. The vote is opened by hand: no cutscene is needed. No lambda captures a local: after
    /// EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineCutsceneSkipNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7832;
        const string VERSION = "test";
        const float WAIT = 15f;

        static bool holding; // this machine's player holds A
        static int skips;

        static bool Pressed(int seat) { return holding && seat == 0; }
        static void CountSkip() { skips++; }
        static IEnumerable<int> MySeat() { return new[] { 0 }; }

        // What the other machine heard from the host
        class Heard
        {
            public SkipBoard Board;

            public Heard(OnlineMatch match)
            {
                match.SkipBoardReceived += OnBoard;
            }

            void OnBoard(SkipBoard board) { Board = board; }
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
            SkipSync.Reset();
            holding = false;
            skips = 0;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static IEnumerator Wait(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Hosting_AClientsSkipCountsOnlyForItsOwnSeat_AndEveryonesSkips()
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
            Heard heard = new Heard(other.Match);

            CutsceneSkip skip = new GameObject("Cutscene").AddComponent<CutsceneSkip>();
            skip.Pressed = Pressed;
            skip.Skip = CountSkip;
            skip.LocalSeats = MySeat;
            skip.Open(new[] { 0, 1 }, true);

            // A vote for a seat that isn't theirs counts for nothing
            other.Match.SendSkipVote(0);
            yield return Wait(1.5f);
            Assert.IsFalse(heard.Board.HasVoted(0), "not their seat");

            // Their own seat's vote counts, and their machine sees the count
            other.Match.SendSkipVote(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Board.HasVoted(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Board.HasVoted(1), "their vote shows on their machine");
            Assert.AreEqual(2, heard.Board.Total);
            Assert.AreEqual(0, skips, "this machine's player hasn't voted yet");

            // This machine's player holds A: everyone has voted, and the host skips once
            holding = true;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (skips == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Wait(0.5f);
            Assert.AreEqual(1, skips);

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

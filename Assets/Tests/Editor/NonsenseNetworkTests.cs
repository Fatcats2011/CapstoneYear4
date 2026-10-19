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
    /// A client ignores what no honest host sends (NetChecks, checked where OnlineMatch receives it): an undefined game
    /// state, scene, clock cue, one-shot or achievement, an order change at no place, a clock that never ends, and an
    /// automatic start at no time. A host and a client on this computer (127.0.0.1), in one empty Play Mode scene (the
    /// automatic start's client plays the real menu scene). No lambda here captures a local
    /// </summary>
    public class NonsenseNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7812;
        const float WAIT = 10f;

        /// <summary>Counts every message a client passed on to the game</summary>
        class HeardRecorder
        {
            public readonly List<string> Heard = new List<string>();

            public void State(GameState state) { Heard.Add("state " + state); }
            public void Load(MatchScene scene) { Heard.Add("load " + scene); }
            public void Show(MatchScene scene) { Heard.Add("show " + scene); }
            public void Order(OrderChange change) { Heard.Add("order " + change.Kind); }
            public void Cue(int seat, ScooterCue cue) { Heard.Add("cue " + cue.Kind); }
            public void Clock(ClockCue cue) { Heard.Add("clock " + cue); }
            public void Achievement(int seat, Achievement achievement) { Heard.Add("achievement " + achievement); }
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

        static bool Sees(OnlineSession session, int players)
        {
            return session.Match != null && session.Players.Count == players;
        }

        static void Listen(OnlineMatch match, HeardRecorder heard)
        {
            match.StateReceived += heard.State;
            match.LoadRequested += heard.Load;
            match.ShowRequested += heard.Show;
            match.OrderReceived += heard.Order;
            match.CueReceived += heard.Cue;
            match.ClockRang += heard.Clock;
            match.AchievementReceived += heard.Achievement;
        }

        [UnityTest]
        public IEnumerator Joining_NonsenseFromTheHost_IsIgnored()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            HeardRecorder heard = new HeardRecorder();
            Listen(client.Match, heard);
            LogCollector log = new LogCollector();

            host.Match.SendState((GameState)99);
            host.Match.RequestLoad((MatchScene)77);
            host.Match.RequestShow((MatchScene)77);
            host.Match.RingClock((ClockCue)55);
            host.Match.SendCue(1, ScooterCue.Of((CueKind)66));
            host.Match.SendAchievement(1, (Achievement)12345);
            OrderChange nowhere = OrderChange.Drop(1, 3, new Vector3(float.NaN, 0, 0), 2f, 4, new Vector3(1e30f, 0, 0), 2f, true);
            host.Match.SendOrder(nowhere);
            host.Match.ShareClock(double.NaN, true, false);
            // Then one honest state, so the test knows the nonsense has arrived (it's sent first, in order)
            host.Match.SendState(GameState.Credits);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard.Contains("state Credits") && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { "state Credits" }, heard.Heard, "only the honest state reached the game");
            // The host's latest state also travels as a shared value, which lands at the next network tick
            deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Match.State != GameState.Credits && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Credits, client.Match.State);
            Assert.AreEqual(0f, MatchClock.Remaining(client.Match.ClockEnd, 0), "a clock that never ends shows no time");

            log.MachinesLeave();
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        static OnlinePlayer PlayerInSeat(OnlineSession session, int seat)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.Seat == seat)
                    return player;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator AutoStart_NaNFromHost_Ignored()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance == null || GameManager.Instance.MainState != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            // A host without the game, in player select; this machine plays the game and joins it
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            host.Match.SendState(GameState.PlayerSelect);
            OnlineSession client = OnlineSession.Create("1.0.0");
            OnlineGame.Attach(client);
            client.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(players.Roster[1] != null && players.Roster[1].IsLocal && GameManager.Instance.MainState == GameState.PlayerSelect)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(players.Roster[1] != null && players.Roster[1].IsLocal, "this machine's player is seated");
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState, "in the host's player select");

            host.Match.SetAutoStart(double.NaN);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!double.IsNaN(client.Match.AutoStartAt) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(double.IsNaN(client.Match.AutoStartAt), "the nonsense arrived");
            for (float until = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsFalse(players.IsReady(1), "this machine's player stays unready");
            Assert.IsFalse(PlayerInSeat(host, 1).Ready, "on the host too");

            // An honest start a second away does ready them: the nonsense was what was ignored
            host.Match.SetAutoStart(host.Network.ServerTime.Time + 1);
            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(onHost.Ready, "an honest start readies this machine's player");

            log.MachinesLeave();
            client.Leave();
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

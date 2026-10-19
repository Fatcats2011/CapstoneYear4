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
    /// Hosts and clients on this computer (127.0.0.1), in one empty Play Mode scene: the host spawns the match and one
    /// OnlinePlayer per machine in its seat, players' choices reach the other machines, the host's states reach clients in
    /// order, and a player who leaves takes their OnlinePlayer with them. Each test enters Play Mode (a few seconds).
    /// The auto-start tests host from the real menu scene (OnlinePlay with a fake Steam) and take about 15 s each.
    /// No lambda here captures a local: after EnterPlayMode even assigning a captured local throws
    /// </summary>
    public class OnlinePlayersNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7792;
        const ushort PORT_NAME = 7818;
        const ushort PORT_AUTO_START = 7821;
        const float WAIT = 10f;
        const double AUTO_START = 2; // MatchRules.AutoStartSecondsOverride, for these tests
        const float COUNTDOWN = 3f; // Dispatch's countdown once everyone is ready (PlayerInstantiate)
        const float SLACK = 3f;

        /// <summary>
        /// Remembers the match scenes the host asked a client to load
        /// </summary>
        class LoadRecorder
        {
            public readonly List<MatchScene> Loads = new List<MatchScene>();

            public LoadRecorder(OnlineMatch match)
            {
                match.LoadRequested += OnLoad;
            }

            void OnLoad(MatchScene scene)
            {
                Loads.Add(scene);
            }
        }

        /// <summary>
        /// Remembers the states a client heard from the host
        /// </summary>
        class StateRecorder
        {
            public readonly List<GameState> States = new List<GameState>();

            public StateRecorder(OnlineMatch match)
            {
                match.StateReceived += OnState;
            }

            void OnState(GameState state)
            {
                States.Add(state);
            }
        }

        [SetUp]
        public void SetUp()
        {
            MatchRules.AutoStartSecondsOverride = AUTO_START;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            MatchRules.AutoStartSecondsOverride = 0;
            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
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

        static bool Sees(OnlineSession session, int players)
        {
            return session.Match != null && session.Players.Count == players;
        }

        [UnityTest]
        public IEnumerator Hosting_SpawnsTheMatchAndTheHostsPlayer_InSeatZero()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "host started");

            Assert.IsNotNull(host.Match, "the match");
            Assert.AreEqual(1, host.Players.Count, "players");
            Assert.AreEqual(0, host.Players[0].Seat);
            Assert.IsTrue(host.Players[0].IsOwner, "the host's own player");

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Joining_EveryMachineSeesBothPlayers_InTheirSeats()
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

            Assert.IsTrue(Sees(client, 2), "the client sees the match and both players");
            Assert.IsTrue(PlayerInSeat(client, 1).IsOwner, "the client's own player sits in seat 1");
            Assert.IsFalse(PlayerInSeat(client, 0).IsOwner, "seat 0 is the host's");
            Assert.IsTrue(Sees(host, 2), "the host sees both players");
            Assert.IsFalse(PlayerInSeat(host, 1).IsOwner, "on the host, seat 1 is another machine's player");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator PlayersChoices_ReachTheOtherMachines_OnlyFromTheirOwnMachine()
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

            PlayerInSeat(client, 1).Share(2, 5, true);
            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(2, onHost.Colour, "colour");
            Assert.AreEqual(5, onHost.Hat, "hat");
            Assert.IsTrue(onHost.Ready, "ready");

            onHost.Share(0, 0, false); // not the host's player: only its own machine may change it
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(2, PlayerInSeat(client, 1).Colour, "unchanged on its own machine");
            Assert.IsTrue(onHost.Ready, "unchanged on the host");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator HostStates_ReachClientsInOrder_EvenTwoInOneFrame()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Match == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            StateRecorder heard = new StateRecorder(client.Match);

            // What the opening cutscene does: the spawn handler switches to the main loop inside the cutscene's own switch
            host.Match.SendState(GameState.StartingCutscene);
            host.Match.SendState(GameState.MainLoop);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.States.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { GameState.StartingCutscene, GameState.MainLoop }, heard.States);

            // The latest state, for players who join later: Netcode sends variables at its next tick, after the RPCs
            deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Match.State != GameState.MainLoop && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, client.Match.State, "the latest, for players who join later");

            client.Match.SendState(GameState.Menu); // only the host decides states
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, host.Match.State);

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator PlayerLeaving_TheirPlayerGoesEverywhere_AndTheNextJoinerGetsTheirSeat()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession first = OnlineSession.Create("1.0.0");
            first.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(first, 2) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineSession second = OnlineSession.Create("1.0.0");
            second.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(second, 3) && Sees(first, 3)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(PlayerInSeat(second, 2).IsOwner, "the second joiner sits in seat 2");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            first.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(host, 2) && Sees(second, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(PlayerInSeat(host, 1), "gone from the host");
            Assert.IsNull(PlayerInSeat(second, 1), "gone from the other player's machine");

            OnlineSession third = OnlineSession.Create("1.0.0");
            third.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(third, 3) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(PlayerInSeat(third, 1).IsOwner, "the next joiner takes the free seat");

            // One side at a time: closing both ends in one frame makes Windows report the closed port as a socket error
            third.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            second.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator ClientName_ReachesTheHost()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT_NAME);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT_NAME);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerInSeat(client, 1).ShareName("Sam");
            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (onHost.Name != "Sam" && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual("Sam", onHost.Name, "the client's name on the host");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static bool CountingDown()
        {
            return PlayerInstantiate.Instance.readyUpCountdown != null;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        // One player here hosts on Dispatch through Online Play (a fake Steam), open to anyone with fill
        static IEnumerator HostOnDispatch(bool fill)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (OnlinePlay.Instance == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(OnlinePlay.Instance, "made at launch");
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (PlayerInstantiate.Instance.Roster.LocalCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);

            OnlinePlay.Instance.UseDirect(new FakeLobbyService { AutoAnswer = true }, THIS_COMPUTER, PORT_AUTO_START);
            MainMenu.Instance.Choose(TitleEntry.Online);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(GameAuthority.Role == NetworkRole.Host && PlayerInstantiate.Instance.UnseatedLocalCount == 0
                && OnlinePlay.Instance.Session.Players.Count == 1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "Online Play hosts");
            Assert.AreEqual(0, PlayerInstantiate.Instance.UnseatedLocalCount, "seated");

            if (fill)
                OnlinePlay.Instance.Lobby.SetFill(true);
            Assert.AreEqual(fill, OnlinePlay.Instance.Lobby.Filling, "open to anyone");
        }

        // Set by JoinTheHost
        static OnlineSession joiner;

        // Another machine (a session without the game, which never readies) joins the host and is seated there
        static IEnumerator JoinTheHost()
        {
            joiner = OnlineSession.Create(Application.version);
            Assert.IsTrue(joiner.JoinDirect(THIS_COMPUTER, PORT_AUTO_START), "the other machine joins");
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(joiner.Match != null && PlayerInstantiate.Instance.Roster[1] != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(PlayerInstantiate.Instance.Roster[1], "seated on the host");
        }

        // The other machine leaves, then the host
        static IEnumerator LeaveBoth()
        {
            if (joiner != null)
            {
                joiner.Leave();
                float deadline = Time.realtimeSinceStartup + WAIT;
                while (OnlinePlay.Instance.Session.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                    yield return null;
                joiner = null;
            }
            OnlinePlay.Instance.Session.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AutoStart_FillingWithTwo_StartsUnready()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return HostOnDispatch(true);
            yield return JoinTheHost();
            LoadRecorder loads = new LoadRecorder(joiner.Match);

            // The host readies its own player at the deadline, the other machine's FORCE_GRACE later, then Dispatch counts
            // down in game time (SLACK more for the load request to travel and for the editor's slow frames)
            float deadline = Time.realtimeSinceStartup + (float)(AUTO_START + MatchRules.FORCE_GRACE) + COUNTDOWN + SLACK;
            while (State() == GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreNotEqual(GameState.PlayerSelect, State(), "the host's match starts");
            float startedWithSpare = deadline - Time.realtimeSinceStartup;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (loads.Loads.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, loads.Loads.Count, "and the other machine is asked to load it (the start came with "
                + startedWithSpare + " s to spare; " + (WAIT - (deadline - Time.realtimeSinceStartup)) + " s waited for the load)");
            Assert.IsFalse(PlayerInSeat(OnlinePlay.Instance.Session, 1).Ready, "though that machine never readied");
            Assert.AreEqual(0.0, OnlinePlay.Instance.Session.Match.AutoStartAt, "the match starting ends the count");

            // The host is loading a match scene: leaving now isn't what this test checks
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
            yield return LeaveBoth();
        }

        [UnityTest]
        public IEnumerator AutoStart_ForcedSeat_HoldsWhileSet_ThenFollowsItsOwnReady()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return HostOnDispatch(true);
            yield return JoinTheHost();
            OnlinePlayer theirs = PlayerInSeat(joiner, 1);
            OnlinePlayer onHost = PlayerInSeat(OnlinePlay.Instance.Session, 1);
            Assert.IsTrue(theirs.IsOwner, "the other machine's own player");

            // FORCE_GRACE after the deadline the host makes the other machine's player ready: Dispatch counts down
            float deadline = Time.realtimeSinceStartup + (float)(AUTO_START + MatchRules.FORCE_GRACE) + SLACK;
            while (!CountingDown() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(CountingDown(), "counting down");
            Assert.IsTrue(PlayerInstantiate.Instance.IsReady(1), "made ready");

            // Their machine readies, then sends not ready (before it heard of the start): the start is still set, so it holds
            theirs.Share(0, 0, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
                yield return null;
            theirs.Share(0, 0, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (onHost.Ready && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(onHost.Ready, "their not ready reached the host");
            Assert.IsTrue(PlayerInstantiate.Instance.IsReady(1), "still ready on the host");
            Assert.IsTrue(CountingDown(), "still counting down");
            Assert.AreNotEqual(0.0, OnlinePlay.Instance.Session.Match.AutoStartAt, "the start is still set");

            // Fill off: no start is set any more, so their own not ready counts again
            OnlinePlay.Instance.Lobby.SetFill(false);
            deadline = Time.realtimeSinceStartup + 1f;
            while (PlayerInstantiate.Instance.IsReady(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(PlayerInstantiate.Instance.IsReady(1), "back to their own not ready");
            Assert.IsFalse(CountingDown(), "which stops the countdown");
            Assert.AreEqual(GameState.PlayerSelect, State(), "still on Dispatch");
            Assert.AreEqual(0.0, OnlinePlay.Instance.Session.Match.AutoStartAt, "no start set");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            yield return LeaveBoth();
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AutoStart_FriendsOnly_NeverStarts()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return HostOnDispatch(false);
            yield return JoinTheHost();

            yield return WaitSeconds(8f);
            Assert.AreEqual(GameState.PlayerSelect, State(), "still on Dispatch");
            Assert.IsFalse(PlayerInstantiate.Instance.IsReady(0), "the host's player wasn't readied");
            Assert.IsFalse(PlayerInstantiate.Instance.IsReady(1), "nor the other machine's");
            Assert.IsFalse(CountingDown(), "no countdown");
            Assert.AreEqual(0.0, OnlinePlay.Instance.Session.Match.AutoStartAt, "no start time shared");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            yield return LeaveBoth();
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AutoStart_HostAlone_NeverStarts()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return HostOnDispatch(true);

            yield return WaitSeconds(8f);
            Assert.AreEqual(GameState.PlayerSelect, State(), "still on Dispatch");
            Assert.IsFalse(PlayerInstantiate.Instance.IsReady(0), "the host's player wasn't readied");
            Assert.IsFalse(CountingDown(), "no countdown");
            Assert.AreEqual(0.0, OnlinePlay.Instance.Session.Match.AutoStartAt, "no start time shared");

            log.MachinesLeave();
            yield return LeaveBoth();
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Hosts and clients on this computer (127.0.0.1), in one empty Play Mode scene: the host spawns the match and one
    /// OnlinePlayer per machine in its seat, players' choices reach the other machines, the host's states reach clients in
    /// order, and a player who leaves takes their OnlinePlayer with them. Each test enters Play Mode (a few seconds).
    /// No lambda here captures a local: after EnterPlayMode even assigning a captured local throws
    /// </summary>
    public class OnlinePlayersNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7792;
        const float WAIT = 10f;

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

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
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

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
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

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
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

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
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
        }
    }
}

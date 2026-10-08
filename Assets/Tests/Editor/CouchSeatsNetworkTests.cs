using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Several players on one machine online (Phase 3J), on this computer (127.0.0.1) in an empty Play Mode scene: a machine
    /// asks the host for another seat, and the host spawns that player and scooter on every machine; a seat given back goes
    /// everywhere; a machine can't give back another's seat or its own last one; a full match refuses with the reason; a
    /// machine that leaves frees all its seats. Each test enters Play Mode (a few seconds). No lambda here captures a local
    /// </summary>
    public class CouchSeatsNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7813;
        const float WAIT = 10f;

        // The reasons a machine heard its seat asks refused
        class RefusalRecorder
        {
            public readonly List<string> Reasons = new List<string>();

            public RefusalRecorder(OnlineSession session)
            {
                session.SeatRefused += OnRefused;
            }

            void OnRefused(string reason)
            {
                Reasons.Add(reason);
            }
        }

        // The seats whose players are ready, for DropUnreadyExtras
        class ReadySeats
        {
            public readonly HashSet<int> Seats = new HashSet<int>();

            public bool IsReady(int seat)
            {
                return Seats.Contains(seat);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
        }

        // Each test enters Play Mode itself (Unity takes EnterPlayMode only from the test's own enumerator)
        static void EmptyScene()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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

        static bool ScooterInSeat(OnlineSession session, int seat)
        {
            foreach (OnlineScooter scooter in session.Scooters)
            {
                if (scooter.Seat == seat)
                    return true;
            }
            return false;
        }

        static List<int> OwnSeats(OnlineSession session)
        {
            List<int> seats = new List<int>();
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.IsOwner)
                    seats.Add(player.Seat);
            }
            seats.Sort();
            return seats;
        }

        static bool Sees(OnlineSession session, int players)
        {
            return session.Match != null && session.Players.Count == players && session.Scooters.Count == players;
        }

        static IEnumerator HostAndClient(OnlineSession[] sessions)
        {
            sessions[0] = OnlineSession.Create("1.0.0");
            Assert.IsTrue(sessions[0].HostDirect(THIS_COMPUTER, PORT), "hosting");
            sessions[1] = OnlineSession.Create("1.0.0");
            sessions[1].JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(sessions[1], 2) && Sees(sessions[0], 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Sees(sessions[1], 2), "the client is in");
        }

        static IEnumerator Leave(OnlineSession host, OnlineSession client)
        {
            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AMachineThatLeft_AsksForASeat_GetsNone()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];

            // An ask the host gets to only after its machine left (or from one it never let in)
            host.OnSeatAsked(99);
            yield return null;

            Assert.AreEqual(0, host.SeatsHeldBy(99), "no seat for a machine that isn't here");
            Assert.IsTrue(Sees(host, 2), "and no player or scooter for one");

            yield return Leave(host, client);
        }

        [UnityTest]
        public IEnumerator TheMatchStarting_DropsAnUnreadyExtraSeat()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];
            client.AskSeat();
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 3) && Sees(host, 3)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Sees(client, 3), "the client's second seat");

            // The host and the client's first player readied; the second seat came as the match started
            ReadySeats ready = new ReadySeats();
            ready.Seats.Add(0);
            ready.Seats.Add(1);
            host.DropUnreadyExtras(ready.IsReady);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(Sees(host, 2), "the unready seat's player and scooter go on the host");
            Assert.IsTrue(Sees(client, 2), "and on the client");
            CollectionAssert.AreEqual(new[] { 1 }, OwnSeats(client), "the client keeps its first");

            yield return Leave(host, client);
        }

        [UnityTest]
        public IEnumerator ClientAskingASeat_SpawnsAnotherPlayerAndScooterForIt_Everywhere()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];

            client.AskSeat();
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 3) && Sees(host, 3)) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(Sees(client, 3), "the client sees 3 players and scooters");
            Assert.IsTrue(Sees(host, 3), "so does the host");
            CollectionAssert.AreEqual(new[] { 1, 2 }, OwnSeats(client), "the client's two players, in seats 1 and 2");
            Assert.IsTrue(ScooterInSeat(client, 2), "with a scooter in seat 2");
            ulong clientId = client.Network.LocalClientId;
            Assert.IsTrue(host.Owns(clientId, 2));
            Assert.AreEqual(2, host.SeatsHeldBy(clientId));

            yield return Leave(host, client);
        }

        [UnityTest]
        public IEnumerator HostAskingASeat_SeatsItsSecondPlayerAtOnce()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");

            host.AskSeat();

            Assert.AreEqual(2, host.Players.Count, "seated in the same frame");
            Assert.IsTrue(PlayerInSeat(host, 1).IsOwner, "the host's own, in seat 1");
            Assert.IsTrue(ScooterInSeat(host, 1));

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator FreeingASeat_DespawnsItEverywhere_AndTheNextAskGetsIt()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];
            client.AskSeat();
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 3) && Sees(host, 3)) && Time.realtimeSinceStartup < deadline)
                yield return null;

            client.FreeSeat(2);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Sees(client, 2), "gone on the client");
            Assert.IsTrue(Sees(host, 2), "and on the host");
            Assert.IsNull(PlayerInSeat(host, 2));

            client.AskSeat();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(host, 3) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(PlayerInSeat(host, 2), "the seat given back is the next one taken");

            yield return Leave(host, client);
        }

        [UnityTest]
        public IEnumerator FreeingAnotherMachinesSeat_OrItsLastSeat_IsIgnored()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];

            client.FreeSeat(0); // the host's
            client.FreeSeat(1); // its only one
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;

            Assert.IsTrue(Sees(host, 2), "both players stay on the host");
            Assert.IsTrue(Sees(client, 2), "and on the client");

            yield return Leave(host, client);
        }

        [UnityTest]
        public IEnumerator AskingWhenFull_IsRefused_WithTheReason()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];
            RefusalRecorder refused = new RefusalRecorder(client);
            host.AskSeat();
            host.AskSeat(); // the host holds 0, 2 and 3; the client 1
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(client, 4) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Sees(client, 4), "4 players");

            client.AskSeat();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (refused.Reasons.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { JoinRules.FULL }, refused.Reasons);
            Assert.AreEqual(4, host.Players.Count, "still 4");

            yield return Leave(host, client);
        }

        [UnityTest]
        public IEnumerator AMachineLeaving_FreesAllItsSeats()
        {
            EmptyScene();
            yield return new EnterPlayMode();
            OnlineSession[] sessions = new OnlineSession[2];
            yield return HostAndClient(sessions);
            OnlineSession host = sessions[0], client = sessions[1];
            client.AskSeat();
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(host, 3) && Time.realtimeSinceStartup < deadline)
                yield return null;

            LogCollector log = new LogCollector();
            log.MachinesLeave();
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(host.Players.Count == 1 && host.PlayersIn == 1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, host.Players.Count, "both of its players went");
            Assert.AreEqual(1, host.PlayersIn, "both of its seats are free");

            OnlineSession next = OnlineSession.Create("1.0.0");
            next.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(next, 2) && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { 1 }, OwnSeats(next), "the next machine gets seat 1");

            next.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

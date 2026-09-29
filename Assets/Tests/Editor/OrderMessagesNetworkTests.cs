using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// What the match carries for orders: the host's order changes reach every client in order, a client's drop request
    /// reaches the host with who asked, and the host's scores and golden-order value reach every client. Only the host
    /// sends changes, scores and the golden value. A host and a client on this computer (127.0.0.1), in one empty Play
    /// Mode scene; each test enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode even
    /// assigning a captured local throws
    /// </summary>
    public class OrderMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7798;
        const float WAIT = 10f;

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
        /// Remembers the last drop request the host heard, and how many it heard
        /// </summary>
        class DropRecorder
        {
            public ulong Machine;
            public int Seat;
            public Vector3 Spot1, Spot2;
            public bool SpinOut;
            public int Count;

            public DropRecorder(OnlineMatch match)
            {
                match.DropAsked += OnDrop;
            }

            void OnDrop(ulong machine, int seat, Vector3 spot1, Vector3 spot2, bool spinOut)
            {
                Machine = machine;
                Seat = seat;
                Spot1 = spot1;
                Spot2 = spot2;
                SpinOut = spinOut;
                Count++;
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
        public IEnumerator TheHostsOrderChanges_ReachEveryClient_InOrder()
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
            ChangeRecorder hostHeard = new ChangeRecorder(host.Match);
            ChangeRecorder clientHeard = new ChangeRecorder(client.Match);

            host.Match.SendOrder(OrderChange.Spawn(5, true));
            host.Match.SendOrder(OrderChange.Pickup(5, 1));
            host.Match.SendOrder(OrderChange.Drop(1, 5, new Vector3(1, 2, 3), 4f, OrderBook.NONE, Vector3.zero, 4f, false));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (clientHeard.Changes.Count < 3 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { OrderChange.Spawn(5, true), OrderChange.Pickup(5, 1),
                OrderChange.Drop(1, 5, new Vector3(1, 2, 3), 4f, OrderBook.NONE, Vector3.zero, 4f, false) }, clientHeard.Changes);
            CollectionAssert.IsEmpty(hostHeard.Changes, "the host made them");

            client.Match.SendOrder(OrderChange.Erase(5)); // only the host changes orders
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(3, clientHeard.Changes.Count, "a client can't send changes");
            CollectionAssert.IsEmpty(hostHeard.Changes);

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
        public IEnumerator AClientsDropRequest_ReachesTheHost_WithWhoAsked()
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
            DropRecorder asked = new DropRecorder(host.Match);

            client.Match.AskDrop(1, new Vector3(1, 2, 3), new Vector3(4, 5, 6), false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(client.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Seat);
            Assert.AreEqual(new Vector3(1, 2, 3), asked.Spot1);
            Assert.AreEqual(new Vector3(4, 5, 6), asked.Spot2);
            Assert.IsFalse(asked.SpinOut);

            host.Match.AskDrop(0, Vector3.zero, Vector3.zero, false);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, asked.Count, "the host drops its own at once: it never asks");

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
        public IEnumerator ScoresAndTheGoldenValue_ReachEveryClient_OnlyFromTheHost()
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
            Assert.AreEqual(50, client.Match.GoldenValue, "the golden order's starting value");

            host.Match.ShareGoldenValue(75);
            PlayerInSeat(host, 1).ShareScore(120);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(client.Match.GoldenValue == 75 && PlayerInSeat(client, 1).Score == 120) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(75, client.Match.GoldenValue);
            Assert.AreEqual(120, PlayerInSeat(client, 1).Score);

            client.Match.ShareGoldenValue(5);
            PlayerInSeat(client, 1).ShareScore(1);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(75, host.Match.GoldenValue, "a client can't change it");
            Assert.AreEqual(120, PlayerInSeat(host, 1).Score, "nor a score");

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
    }
}

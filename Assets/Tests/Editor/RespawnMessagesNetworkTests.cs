using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// What the match carries for respawns: a client's request for where its player rises, reaching the host with who
    /// sent it and where they were last on the ground, and the host's answer, reaching every client. The host never asks
    /// (it picks its own player's point at once), and a client never answers. A host and a client on this computer
    /// (127.0.0.1), in one empty Play Mode scene; each test enters Play Mode (a few seconds). No lambda here captures a
    /// local: after EnterPlayMode even assigning a captured local throws
    /// </summary>
    public class RespawnMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7803;
        const float WAIT = 10f;

        /// <summary>
        /// Remembers the last respawn request the host heard (who sent it, their seat, where they were last on the
        /// ground), and how many it heard
        /// </summary>
        class RespawnAskRecorder
        {
            public ulong Machine;
            public int Seat = -1;
            public Vector3 LastGrounded;
            public int Count;

            public void Heard(ulong machine, int seat, Vector3 lastGrounded)
            {
                Machine = machine;
                Seat = seat;
                LastGrounded = lastGrounded;
                Count++;
            }
        }

        /// <summary>
        /// Remembers the last respawn point a machine heard from the host (the seat, the point's index), and how many
        /// </summary>
        class PointRecorder
        {
            public int Seat = -1;
            public int Point = -1;
            public int Count;

            public void Heard(int seat, int point)
            {
                Seat = seat;
                Point = point;
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

        static bool Sees(OnlineSession session, int players)
        {
            return session.Match != null && session.Players.Count == players;
        }

        [UnityTest]
        public IEnumerator AClientsRespawnRequest_ReachesTheHost_WithWhoSentItAndWhere()
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
            RespawnAskRecorder asked = new RespawnAskRecorder();
            host.Match.RespawnAsked += asked.Heard;

            client.Match.AskRespawn(1, new Vector3(10, 2, -30));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(client.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Seat);
            Assert.AreEqual(new Vector3(10, 2, -30), asked.LastGrounded);

            host.Match.AskRespawn(0, Vector3.zero);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, asked.Count, "the host picks its own player's point at once: it never asks");

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
        public IEnumerator TheHostsRespawnPoints_ReachEveryClient()
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
            PointRecorder rise = new PointRecorder();
            client.Match.RespawnReceived += rise.Heard;
            PointRecorder hostHeard = new PointRecorder();
            host.Match.RespawnReceived += hostHeard.Heard;

            host.Match.SendRespawn(1, 17);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (rise.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, rise.Seat);
            Assert.AreEqual(17, rise.Point);

            client.Match.SendRespawn(0, 3);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, rise.Count, "a client never answers");
            Assert.AreEqual(0, hostHeard.Count, "the host skips its own");

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

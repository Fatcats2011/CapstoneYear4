using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// What the match carries for steals and clashes: a client's request, reaching the host with who sent it, and the
    /// host's hits, reaching every client in order. The host never asks (it judges its own player's at once), and a
    /// client never sends hits. A host and a client on this computer (127.0.0.1), in one empty Play Mode scene; each test
    /// enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode even assigning a captured
    /// local throws
    /// </summary>
    public class StealMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7802;
        const float WAIT = 10f;

        /// <summary>
        /// Remembers the last steal request the host heard (who sent it, the attacker's and the victim's seats), and how
        /// many it heard
        /// </summary>
        class StealRecorder
        {
            public ulong Machine;
            public int Attacker = -1;
            public int Victim = -1;
            public int Count;

            public void Heard(ulong machine, int attacker, int victim)
            {
                Machine = machine;
                Attacker = attacker;
                Victim = victim;
                Count++;
            }
        }

        /// <summary>
        /// Remembers the hits a machine heard from the host, in order
        /// </summary>
        class HitRecorder
        {
            public readonly List<PlayerHit> Hits = new List<PlayerHit>();

            public void Heard(PlayerHit hit)
            {
                Hits.Add(hit);
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
        public IEnumerator AClientsStealRequest_ReachesTheHost_WithWhoSentIt()
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
            StealRecorder asked = new StealRecorder();
            host.Match.StealAsked += asked.Heard;

            client.Match.AskSteal(1, 0);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(client.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Attacker);
            Assert.AreEqual(0, asked.Victim);

            host.Match.AskSteal(0, 1);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, asked.Count, "the host judges its own player's at once: it never asks");

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
        public IEnumerator TheHostsHits_ReachEveryClient_InOrder()
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
            HitRecorder hits = new HitRecorder();
            client.Match.HitReceived += hits.Heard;
            HitRecorder hostHeard = new HitRecorder();
            host.Match.HitReceived += hostHeard.Heard;

            host.Match.SendHit(PlayerHit.Steal(0, 1));
            host.Match.SendHit(PlayerHit.Clash(1, 0));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (hits.Hits.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { PlayerHit.Steal(0, 1), PlayerHit.Clash(1, 0) }, hits.Hits);

            client.Match.SendHit(PlayerHit.Clash(0, 1));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(2, hits.Hits.Count, "a client sends none");
            Assert.IsEmpty(hostHeard.Hits, "the host skips its own");

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

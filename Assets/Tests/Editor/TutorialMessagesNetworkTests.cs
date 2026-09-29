using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// What the match carries for the tutorial: a client's report that its player finished it, and its request for the
    /// order of the cutout its player boosted into, each reaching the host with who sent it. The host never sends
    /// either: it acts at once. A host and a client on this computer (127.0.0.1), in one empty Play Mode scene; each
    /// test enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode even assigning a
    /// captured local throws
    /// </summary>
    public class TutorialMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7800;
        const float WAIT = 10f;

        /// <summary>
        /// Remembers the last seat message the host heard (who sent it, which seat), and how many it heard
        /// </summary>
        class SeatRecorder
        {
            public ulong Machine;
            public int Seat = -1;
            public int Count;

            public void Heard(ulong machine, int seat)
            {
                Machine = machine;
                Seat = seat;
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
        public IEnumerator AClientsTutorialReport_ReachesTheHost_WithWhoSentIt()
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
            SeatRecorder learnt = new SeatRecorder();
            host.Match.MachineLearnt += learnt.Heard;

            client.Match.ReportLearnt(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (learnt.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(client.Network.LocalClientId, learnt.Machine);
            Assert.AreEqual(1, learnt.Seat);

            host.Match.ReportLearnt(0);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, learnt.Count, "the host counts its own player at once: it never reports");

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
        public IEnumerator AClientsCutoutRequest_ReachesTheHost_WithWhoSentIt()
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
            SeatRecorder asked = new SeatRecorder();
            host.Match.CutoutAsked += asked.Heard;

            client.Match.AskCutout(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(client.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Seat);

            host.Match.AskCutout(0);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, asked.Count, "the host's player steals at once: it never asks");

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

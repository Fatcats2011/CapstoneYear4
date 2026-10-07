using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The host's limits on what a machine sends it (RateGate in OnlineMatch): a flood of one-shots reaches the host only
    /// up to the allowance, and the machine that floods is disconnected; an honest pace all arrives. A host and a client
    /// on this computer (127.0.0.1), in one empty Play Mode scene. No lambda here captures a local: after EnterPlayMode
    /// even assigning a captured local throws
    /// </summary>
    public class MessageLimitsNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7811;
        const float WAIT = 10f;

        /// <summary>The one-shots the host heard from clients</summary>
        class CueReportRecorder
        {
            public int Count;

            public void Heard(ulong machine, int seat, ScooterCue cue)
            {
                Count++;
            }
        }

        /// <summary>Why a session ended (null until it does)</summary>
        class EndRecorder
        {
            public string Reason;

            public void Ended(string reason)
            {
                Reason = reason;
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

        static void NewEmptyScene()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [UnityTest]
        public IEnumerator Hosting_AFloodOfCues_IsCapped_AndTheSenderLeaves()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            CueReportRecorder reported = new CueReportRecorder();
            host.Match.CueReported += reported.Heard;
            EndRecorder ended = new EndRecorder();
            client.Ended += ended.Ended;
            LogCollector log = new LogCollector();
            log.MachinesLeave(); // the flooding machine is disconnected: see LogCollector.CLOSED_PORT

            float floodStart = Time.realtimeSinceStartup;
            for (int i = 0; i < 500; i++)
                client.Match.ReportCue(1, ScooterCue.Of(CueKind.Boost));
            deadline = Time.realtimeSinceStartup + 5f;
            while (client.IsRunning && Time.realtimeSinceStartup < deadline)
                yield return null;

            // The flood arrives over a few frames, and the allowance refills meanwhile (10 a second after a burst of 20)
            float took = Time.realtimeSinceStartup - floodStart;
            Assert.LessOrEqual(reported.Count, 20 + Mathf.CeilToInt(10 * took) + 1, "only the allowance reaches the host (" + took + " s)");
            Assert.Less(reported.Count, 100, "far from all 500");
            Assert.Greater(reported.Count, 0, "the allowance does");
            Assert.IsFalse(client.IsRunning, "the flooding machine is disconnected");
            Assert.AreEqual(1, host.PlayersIn, "only the host is left");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_HonestCues_AllArrive()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            CueReportRecorder reported = new CueReportRecorder();
            host.Match.CueReported += reported.Heard;

            for (int i = 0; i < 8; i++)
            {
                client.Match.ReportCue(1, ScooterCue.Of(CueKind.Boost));
                for (float until = Time.realtimeSinceStartup + 0.2f; Time.realtimeSinceStartup < until;)
                    yield return null;
            }
            deadline = Time.realtimeSinceStartup + WAIT;
            while (reported.Count < 8 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(8, reported.Count);
            Assert.IsTrue(client.IsRunning, "still in");

            LogCollector log = new LogCollector();
            log.MachinesLeave();
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

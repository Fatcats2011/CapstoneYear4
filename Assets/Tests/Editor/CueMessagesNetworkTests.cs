using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// What the match carries for one-shots: a client's player's one-shot reaching the host with who sent it, and the
    /// host's one-shots and clock reaching every client, in order. The host never reports (it sends its own player's
    /// one-shots itself), and a client never sends. A host and a client on this computer (127.0.0.1), in one empty Play
    /// Mode scene; each test enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode even
    /// assigning a captured local throws
    /// </summary>
    public class CueMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7806;
        const float WAIT = 10f;

        /// <summary>
        /// Remembers the one-shots the host heard from clients (who sent the last, each one's seat and cue, in order)
        /// </summary>
        class CueReportRecorder
        {
            public ulong Machine;
            public readonly List<int> Seats = new List<int>();
            public readonly List<ScooterCue> Cues = new List<ScooterCue>();

            public void Heard(ulong machine, int seat, ScooterCue cue)
            {
                Machine = machine;
                Seats.Add(seat);
                Cues.Add(cue);
            }
        }

        /// <summary>
        /// Remembers the one-shots a machine heard from the host (each one's seat and cue, in order)
        /// </summary>
        class CueRecorder
        {
            public readonly List<int> Seats = new List<int>();
            public readonly List<ScooterCue> Cues = new List<ScooterCue>();

            public void Heard(int seat, ScooterCue cue)
            {
                Seats.Add(seat);
                Cues.Add(cue);
            }
        }

        /// <summary>
        /// Remembers the host's clock's one-shots a machine heard, in order
        /// </summary>
        class ClockRecorder
        {
            public readonly List<ClockCue> Cues = new List<ClockCue>();

            public void Heard(ClockCue cue)
            {
                Cues.Add(cue);
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
        public IEnumerator AClientsCue_ReachesTheHost_WithWhoSentIt()
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
            CueReportRecorder reported = new CueReportRecorder();
            host.Match.CueReported += reported.Heard;

            client.Match.ReportCue(1, ScooterCue.Rise(17));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (reported.Cues.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(client.Network.LocalClientId, reported.Machine);
            Assert.AreEqual(1, reported.Seats[0]);
            Assert.AreEqual(ScooterCue.Rise(17), reported.Cues[0]);

            host.Match.ReportCue(0, ScooterCue.Of(CueKind.Boost));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, reported.Cues.Count, "the host sends its own player's cues: it never reports");

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
        public IEnumerator TheHostsCues_ReachEveryClient_InOrder()
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
            CueRecorder heard = new CueRecorder();
            client.Match.CueReceived += heard.Heard;
            CueRecorder hostHeard = new CueRecorder();
            host.Match.CueReceived += hostHeard.Heard;

            host.Match.SendCue(0, ScooterCue.Of(CueKind.Boost));
            host.Match.SendCue(1, ScooterCue.Of(CueKind.Death));
            host.Match.SendCue(0, ScooterCue.Rise(3));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Cues.Count < 3 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { ScooterCue.Of(CueKind.Boost), ScooterCue.Of(CueKind.Death), ScooterCue.Rise(3) }, heard.Cues);
            CollectionAssert.AreEqual(new[] { 0, 1, 0 }, heard.Seats);

            client.Match.SendCue(1, ScooterCue.Of(CueKind.Boost));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(3, heard.Cues.Count, "a client never sends");
            Assert.AreEqual(0, hostHeard.Cues.Count, "the host skips its own");

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
        public IEnumerator TheHostsClock_ReachesEveryClient_InOrder()
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
            ClockRecorder rang = new ClockRecorder();
            client.Match.ClockRang += rang.Heard;
            ClockRecorder hostRang = new ClockRecorder();
            host.Match.ClockRang += hostRang.Heard;

            host.Match.RingClock(ClockCue.WaveBells);
            host.Match.RingClock(ClockCue.TimeUp);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (rang.Cues.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { ClockCue.WaveBells, ClockCue.TimeUp }, rang.Cues);

            client.Match.RingClock(ClockCue.WaveBells);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(2, rang.Cues.Count, "a client's clock rings nowhere else");
            Assert.AreEqual(0, hostRang.Cues.Count, "the host skips its own");

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

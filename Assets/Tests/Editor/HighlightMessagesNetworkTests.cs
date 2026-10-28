using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The highlight reel's list online: the host sends it to every client (OnlineMatch.ShareReel), which keep it only if
    /// it is sane (NetChecks.Sane), and a client never sends. A host and a client on this computer (127.0.0.1), in one empty
    /// Play Mode scene; each test enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode
    /// even assigning a captured local throws
    /// </summary>
    public class HighlightMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7835;
        const float WAIT = 10f;

        /// <summary>The reel lists a machine heard from the host, in order</summary>
        class ReelRecorder
        {
            public readonly List<ReelList> Heard = new List<ReelList>();

            public void Hear(ReelList list)
            {
                Heard.Add(list);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            HighlightSync.Reset();
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

        static IEnumerator Wait(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        static IEnumerator BothLeave(OnlineSession host, OnlineSession client)
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

        // Two clips, the first with a slow shot: enough to tell a list that arrived whole from one that didn't
        static ReelList AList()
        {
            ReelClip first = new ReelClip
            {
                Start = 101, End = 104, KeyTime = 102, SeatsMask = 0b11, Caption = Caption.Theft, TimeLeftAtStart = 30,
                Shots = new[]
                {
                    new ReelShot { Kind = ShotKind.HighWide, Cut = 101, Position = new Vector3(20, 20, 20), FramedMask = 0b11, Speed = 1 },
                    new ReelShot { Kind = ShotKind.Behind, Cut = 102, Subject = 1, Offset = new Vector3(0, 3, -7), FramedMask = 0b10, Speed = 0.5f },
                },
            };
            ReelClip second = new ReelClip
            {
                Start = 110, End = 112, KeyTime = 111, SeatsMask = 0b01, Caption = Caption.Water, TimeLeftAtStart = 12,
                Shots = new[] { new ReelShot { Kind = ShotKind.HighWide, Cut = 110, Position = new Vector3(-5, 12, 8), FramedMask = 0b01, Speed = 1 } },
            };
            return new ReelList { StartAt = 2000.5, Clips = new[] { first, second }, Length = first.RealLength + second.RealLength };
        }

        [UnityTest]
        public IEnumerator Host_SharesAReel_ClientReceivesTheSameList()
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
            ReelRecorder heard = new ReelRecorder();
            client.Match.ReelReceived += heard.Hear;
            ReelRecorder hostHeard = new ReelRecorder();
            host.Match.ReelReceived += hostHeard.Hear;

            host.Match.ShareReel(AList());
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Heard.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, heard.Heard.Count, "the client heard the reel");
            Assert.AreEqual(AList(), heard.Heard[0], "the same list");
            Assert.AreEqual(0, hostHeard.Heard.Count, "the host skips its own");

            yield return BothLeave(host, client);
        }

        [UnityTest]
        public IEnumerator Client_NonsenseReel_IsIgnored()
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
            ReelRecorder heard = new ReelRecorder();
            client.Match.ReelReceived += heard.Hear;

            ReelList nonsense = AList();
            nonsense.StartAt = double.NaN;
            host.Match.ShareReel(nonsense);
            yield return Wait(1f);
            Assert.AreEqual(0, heard.Heard.Count, "a list with a NaN is dropped");

            host.Match.ShareReel(AList()); // and the next one still arrives
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Heard.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, heard.Heard.Count);

            yield return BothLeave(host, client);
        }

        [UnityTest]
        public IEnumerator Client_NeverSends()
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
            ReelRecorder heard = new ReelRecorder();
            client.Match.ReelReceived += heard.Hear;
            ReelRecorder hostHeard = new ReelRecorder();
            host.Match.ReelReceived += hostHeard.Hear;

            client.Match.ShareReel(AList());
            yield return Wait(1f);

            Assert.AreEqual(0, hostHeard.Heard.Count);
            Assert.AreEqual(0, heard.Heard.Count);

            yield return BothLeave(host, client);
        }

        [UnityTest]
        public IEnumerator OnlineHighlights_CarriesTheSharedList_ToTheClientsArrival()
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
            new GameObject("Host highlights").AddComponent<OnlineHighlights>().Begin(host);
            new GameObject("Client highlights").AddComponent<OnlineHighlights>().Begin(client);
            ReelRecorder arrived = new ReelRecorder();
            HighlightSync.Arrived += arrived.Hear;

            HighlightSync.Share(AList()); // both relays hear it, but only the host's sends
            deadline = Time.realtimeSinceStartup + WAIT;
            while (arrived.Heard.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return Wait(0.5f);

            Assert.AreEqual(1, arrived.Heard.Count, "the client's relay handed it on once");
            Assert.AreEqual(AList(), arrived.Heard[0]);

            yield return BothLeave(host, client);
        }
    }
}

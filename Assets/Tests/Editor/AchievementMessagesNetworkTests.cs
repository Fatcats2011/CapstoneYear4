using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Achievements online: the host decides (SteamFeatures.Earned), unlocks its own seat's, and sends the others' to every
    /// client with their seat (OnlineMatch.SendAchievement); each client unlocks only its own seat's. A client never sends.
    /// A host and a client on this computer (127.0.0.1), in one empty Play Mode scene, each with a fake store; each test
    /// enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode even assigning a captured local
    /// throws
    /// </summary>
    public class AchievementMessagesNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7810;
        const float WAIT = 10f;

        /// <summary>Achievements a machine heard from the host (each one's seat and achievement, in order)</summary>
        class AchievementRecorder
        {
            public readonly List<int> Seats = new List<int>();
            public readonly List<Achievement> Achievements = new List<Achievement>();

            public void Heard(int seat, Achievement achievement)
            {
                Seats.Add(seat);
                Achievements.Add(achievement);
            }
        }

        /// <summary>A machine's achievements store: what unlocked there, in order</summary>
        class FakeAchievements : IAchievementStore
        {
            public readonly List<Achievement> Unlocked = new List<Achievement>();
            public bool Available { get { return true; } }
            public void Unlock(Achievement achievement) { Unlocked.Add(achievement); }
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

        // A machine's own player's seat (their OnlinePlayer's), or -1
        static int OwnSeat(OnlineSession session)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.IsOwner)
                    return player.Seat;
            }
            return -1;
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

        [UnityTest]
        public IEnumerator TheHostsAchievements_ReachEveryClient_WithTheirSeat()
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
            AchievementRecorder heard = new AchievementRecorder();
            client.Match.AchievementReceived += heard.Heard;
            AchievementRecorder hostHeard = new AchievementRecorder();
            host.Match.AchievementReceived += hostHeard.Heard;

            host.Match.SendAchievement(1, Achievement.GoldenWin);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Achievements.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { 1 }, heard.Seats);
            CollectionAssert.AreEqual(new[] { Achievement.GoldenWin }, heard.Achievements);
            Assert.AreEqual(0, hostHeard.Achievements.Count, "the host skips its own");

            yield return BothLeave(host, client);
        }

        [UnityTest]
        public IEnumerator AClientNeverSends()
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
            AchievementRecorder heard = new AchievementRecorder();
            client.Match.AchievementReceived += heard.Heard;
            AchievementRecorder hostHeard = new AchievementRecorder();
            host.Match.AchievementReceived += hostHeard.Heard;

            client.Match.SendAchievement(0, Achievement.FirstDelivery);
            yield return Wait(1f);

            Assert.AreEqual(0, hostHeard.Achievements.Count);
            Assert.AreEqual(0, heard.Achievements.Count);

            yield return BothLeave(host, client);
        }

        [UnityTest]
        public IEnumerator Hosting_AnotherSeatEarnsAnAchievement_OnlyItsMachineUnlocksIt()
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
            Assert.AreEqual(0, OwnSeat(host), "the host's seat");
            Assert.AreEqual(1, OwnSeat(client), "the client's seat");
            FakeAchievements hostStore = new FakeAchievements();
            new GameObject("Host achievements").AddComponent<OnlineAchievements>().Begin(host, hostStore);
            FakeAchievements clientStore = new FakeAchievements();
            new GameObject("Client achievements").AddComponent<OnlineAchievements>().Begin(client, clientStore);

            // The client's seat earns one: it unlocks on the client only
            SteamFeatures.RaiseEarned(1, Achievement.FirstDelivery);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (clientStore.Unlocked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { Achievement.FirstDelivery }, clientStore.Unlocked);
            CollectionAssert.IsEmpty(hostStore.Unlocked);

            // The host's seat earns one: it unlocks on the host only
            SteamFeatures.RaiseEarned(0, Achievement.GoldenWin);
            yield return Wait(1f);
            CollectionAssert.AreEqual(new[] { Achievement.GoldenWin }, hostStore.Unlocked);
            CollectionAssert.AreEqual(new[] { Achievement.FirstDelivery }, clientStore.Unlocked);

            yield return BothLeave(host, client);
        }
    }
}

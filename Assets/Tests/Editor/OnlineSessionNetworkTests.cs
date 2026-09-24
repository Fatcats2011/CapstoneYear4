using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Hosts and clients on this computer (127.0.0.1), in one empty Play Mode scene: joining, being turned away, the
    /// host or a player leaving, a host nobody can reach, Netcode stopping on its own. Each test enters Play Mode (a few
    /// seconds).
    /// After EnterPlayMode a test resumes in a reloaded script domain, where a lambda that captures the test's locals
    /// throws (its closure was made before the reload): these tests record events with EndedRecorder and wait in loops
    /// </summary>
    public class OnlineSessionNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7791; // not the game's 7777, in case an editor is hosting
        const float WAIT = 10f;

        /// <summary>
        /// Remembers why a session ended
        /// </summary>
        class EndedRecorder
        {
            public string Reason;

            public EndedRecorder(OnlineSession session)
            {
                session.Ended += OnEnded;
            }

            void OnEnded(string reason)
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

        [UnityTest]
        public IEnumerator Join_SameBuild_ConnectsAsAClient()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "host started");
            OnlineSession client = OnlineSession.Create("1.0.0");
            Assert.IsTrue(client.JoinDirect(THIS_COMPUTER, PORT), "client started");

            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Client, client.Role, "the client's part");
            Assert.AreEqual(NetworkRole.Host, host.Role, "the host's part");
            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role, "this machine's part (the client set it last)");
            Assert.AreEqual(2, host.PlayersIn, "players the host let in");
            Assert.IsFalse(host.HostDirect(THIS_COMPUTER, PORT), "hosting again while hosting");
            Assert.AreEqual(2, host.PlayersIn, "players after hosting again");

            // One side at a time: closing both in one frame makes Windows report the closed port as a socket error
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Join_AnotherBuild_IsTurnedAwayWithBothVersions()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("0.9.0");
            EndedRecorder ended = new EndedRecorder(client);
            client.JoinDirect(THIS_COMPUTER, PORT);

            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(JoinRules.Refusal("1.0.0", "0.9.0", 1, GameState.Default), ended.Reason);
            Assert.IsFalse(client.IsRunning, "the client's session ended");
            Assert.AreEqual(NetworkRole.Offline, client.Role);
            Assert.AreEqual(1, host.PlayersIn, "only the host is in");

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostLeaving_EndsTheClientsSession()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            EndedRecorder hostEnded = new EndedRecorder(host);
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            EndedRecorder clientEnded = new EndedRecorder(client);
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;

            host.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (clientEnded.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.HOST_LEFT, clientEnded.Reason);
            Assert.AreEqual(NetworkRole.Offline, client.Role, "the client is local again");
            Assert.AreEqual(NetworkRole.Offline, host.Role, "the host is local again");
            Assert.IsNull(hostEnded.Reason, "the host left on purpose");
        }

        [UnityTest]
        public IEnumerator PlayerLeaving_FreesTheirSeat()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            EndedRecorder clientEnded = new EndedRecorder(client);
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, host.PlayersIn, "the player's seat is free again");
            Assert.AreEqual(NetworkRole.Host, host.Role, "the host keeps hosting");
            Assert.AreEqual(NetworkRole.Offline, client.Role);
            Assert.IsNull(clientEnded.Reason, "the player left on purpose");

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Join_NobodyHosting_EndsWithHostUnreachable()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // Unity Transport logs a socket error per attempt when nobody answers on this computer

            OnlineSession client = OnlineSession.Create("1.0.0");
            client.Leave(); // nothing to leave yet: must not hide the end of the next session
            client.Direct.MaxConnectAttempts = 2; // give up after about 2 s (the game waits 10)
            EndedRecorder ended = new EndedRecorder(client);
            Assert.IsTrue(client.JoinDirect(THIS_COMPUTER, PORT), "client started");

            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.HOST_UNREACHABLE, ended.Reason);
            Assert.IsFalse(client.IsRunning);
            Assert.AreEqual(NetworkRole.Offline, client.Role);
        }

        [UnityTest]
        public IEnumerator NetcodeStoppingOnItsOwn_EndsTheHostsSession()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            EndedRecorder ended = new EndedRecorder(host);
            host.HostDirect(THIS_COMPUTER, PORT);

            host.Network.Shutdown(); // what a transport failure does
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.CONNECTION_LOST, ended.Reason);
            Assert.AreEqual(NetworkRole.Offline, host.Role);
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "this machine is local again");
        }
    }
}

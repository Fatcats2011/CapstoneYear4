using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// OnlineSession without networking: its Netcode setup and who it lets in. OnlineSessionNetworkTests connect for real
    /// </summary>
    public class OnlineSessionTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<OnlineSession> sessions = new List<OnlineSession>();

        [TearDown]
        public void TearDown()
        {
            foreach (OnlineSession session in sessions)
            {
                if (session != null)
                    Object.DestroyImmediate(session.gameObject);
            }
            sessions.Clear();
            objects.DestroyAll();
            Reflect.SetSingleton<GameManager>(null);
            GameAuthority.Role = NetworkRole.Offline;
        }

        OnlineSession NewSession(string version = "1.0.0")
        {
            OnlineSession session = OnlineSession.Create(version);
            sessions.Add(session);
            return session;
        }

        // What the host decides about a player asking to join
        static NetworkManager.ConnectionApprovalResponse Approve(OnlineSession host, ulong clientId, string version)
        {
            NetworkManager.ConnectionApprovalResponse response = new NetworkManager.ConnectionApprovalResponse();
            NetworkManager.ConnectionApprovalRequest request = new NetworkManager.ConnectionApprovalRequest
            {
                ClientNetworkId = clientId,
                Payload = Encoding.UTF8.GetBytes(version),
            };
            host.Network.ConnectionApprovalCallback(request, response);
            return response;
        }

        [Test]
        public void Create_SetsUpNetcodeToApproveJoinsOverUnityTransport()
        {
            OnlineSession session = NewSession();

            NetworkConfig config = session.Network.NetworkConfig;
            Assert.AreSame(session.Direct, config.NetworkTransport, "starts on Unity Transport");
            Assert.IsTrue(config.ConnectionApproval, "the host approves every join");
            Assert.IsFalse(config.EnableSceneManagement, "scene loads stay local until roadmap Task 3.4");
            Assert.AreEqual("1.0.0", session.Version);
        }

        [Test]
        public void Approve_TheHostItself_IsLetInWhateverItSends()
        {
            OnlineSession session = NewSession();

            Assert.IsTrue(Approve(session, NetworkManager.ServerClientId, "").Approved);
            Assert.AreEqual(1, session.PlayersIn);
        }

        [Test]
        public void Approve_PlayersOnTheSameBuild_TakeTheFreeSeats_ThenTheMatchIsFull()
        {
            OnlineSession session = NewSession();
            Approve(session, NetworkManager.ServerClientId, "1.0.0");
            for (ulong player = 1; player < Constants.MAX_PLAYERS; player++)
                Assert.IsTrue(Approve(session, player, "1.0.0").Approved, "player " + player);

            // Netcode lists approved players only later in the frame: the session counts them as it approves
            NetworkManager.ConnectionApprovalResponse late = Approve(session, Constants.MAX_PLAYERS, "1.0.0");

            Assert.IsFalse(late.Approved);
            Assert.AreEqual(JoinRules.FULL, late.Reason);
            Assert.AreEqual(Constants.MAX_PLAYERS, session.PlayersIn);
        }

        [Test]
        public void Approve_APlayerOnAnotherBuild_IsTurnedAwayWithTheReason()
        {
            OnlineSession session = NewSession("1.0.0");
            Approve(session, NetworkManager.ServerClientId, "1.0.0");

            NetworkManager.ConnectionApprovalResponse response = Approve(session, 1, "0.9.0");

            Assert.IsFalse(response.Approved);
            Assert.AreEqual(JoinRules.Refusal("1.0.0", "0.9.0", 1, GameState.Default), response.Reason);
            Assert.AreEqual(1, session.PlayersIn);
        }

        [Test]
        public void Approve_WhileTheHostsMatchIsUnderWay_TurnsPlayersAway()
        {
            GameManager game = objects.Add<GameManager>();
            Reflect.SetSingleton(game);
            game.SetGameState(GameState.MainLoop);
            OnlineSession session = NewSession();
            Approve(session, NetworkManager.ServerClientId, "1.0.0");

            NetworkManager.ConnectionApprovalResponse response = Approve(session, 1, "1.0.0");

            Assert.IsFalse(response.Approved);
            Assert.AreEqual(JoinRules.STARTED, response.Reason);
        }

        [TestCase(true, "Disconnected due to host shutting down.", OnlineSession.HOST_LEFT)]
        [TestCase(true, "", OnlineSession.HOST_LEFT)]
        [TestCase(false, JoinRules.FULL, JoinRules.FULL)]
        [TestCase(false, "", OnlineSession.HOST_UNREACHABLE)]
        [TestCase(false, null, OnlineSession.HOST_UNREACHABLE)]
        public void EndReason_TellsAClientWhatHappened(bool reachedHost, string netcodeReason, string expected)
        {
            Assert.AreEqual(expected, OnlineSession.EndReason(reachedHost, netcodeReason));
        }

        [Test]
        public void HostSteam_WithoutSteam_StaysOffline()
        {
            OnlineSession session = NewSession();
            LogAssert.Expect(LogType.Warning, OnlineSession.NO_STEAM);

            Assert.IsFalse(session.HostSteam());
            Assert.IsFalse(session.IsRunning);
            Assert.AreSame(session.Direct, session.Network.NetworkConfig.NetworkTransport, "still set up for direct sessions");
        }

        [Test]
        public void JoinSteam_WithoutSteam_StaysOffline()
        {
            OnlineSession session = NewSession();
            LogAssert.Expect(LogType.Warning, OnlineSession.NO_STEAM);

            Assert.IsFalse(session.JoinSteam(76561198000000000UL));
            Assert.IsFalse(session.IsRunning);
            Assert.AreSame(session.Direct, session.Network.NetworkConfig.NetworkTransport, "still set up for direct sessions");
        }
    }
}

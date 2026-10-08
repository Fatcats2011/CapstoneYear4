using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// OnlineGame without networking: the role and scene flow follow the session, clients show the host's own menus as
    /// the title screen, and a colour or hat from outside this build's lists is ignored
    /// </summary>
    public class OnlineGameTests
    {
        OnlineSession session;

        [TearDown]
        public void TearDown()
        {
            if (session != null)
                Object.DestroyImmediate(session.gameObject);
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
        }

        [Test]
        public void TheSessionsRole_BecomesThisMachines_WithTheOnlineSceneFlow()
        {
            session = OnlineSession.Create("1.0.0");
            OnlineGame.Attach(session);

            session.SetRole(NetworkRole.Host);
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role);
            Assert.IsInstanceOf<OnlineSceneFlow>(SceneFlow.Current);

            session.SetRole(NetworkRole.Offline);
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.IsNotInstanceOf<OnlineSceneFlow>(SceneFlow.Current);
        }

        [Test]
        public void ASessionWithoutTheGame_LeavesThisMachinesRoleAlone()
        {
            session = OnlineSession.Create("1.0.0");

            session.SetRole(NetworkRole.Client);

            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
        }

        [TestCase(GameState.Options, GameState.Menu)]
        [TestCase(GameState.Credits, GameState.Menu)]
        [TestCase(GameState.Menu, GameState.Menu)]
        [TestCase(GameState.PlayerSelect, GameState.PlayerSelect)]
        [TestCase(GameState.MainLoop, GameState.MainLoop)]
        public void ForClient_HostsOwnMenus_ShowAsTheTitleScreen(GameState host, GameState client)
        {
            Assert.AreEqual(client, OnlineGame.ForClient(host));
        }

        [Test]
        public void Pick_OutsideTheList_IsNothing()
        {
            List<string> colours = new List<string> { "red", "blue" };

            Assert.AreEqual("blue", OnlineGame.Pick(colours, 1));
            Assert.IsNull(OnlineGame.Pick(colours, 2));
            Assert.IsNull(OnlineGame.Pick(colours, -1));
        }

        [Test]
        public void SeatsToGiveBack_OnlyThoseThisMachineNoLongerHolds()
        {
            // The host still has players for seats 1 and 3 here, but this machine gave 3 back: its ask may have been
            // dropped (the host's rate limit), so it's asked again
            CollectionAssert.AreEqual(new[] { 3 }, OnlineGame.SeatsToGiveBack(new[] { 1, 3 }, new[] { 1 }));
            CollectionAssert.IsEmpty(OnlineGame.SeatsToGiveBack(new[] { 1 }, new[] { 1 }));
        }

        [Test]
        public void MachineGone_OnlyWhenNoneOfItsPlayersRemain()
        {
            // A machine giving one of its seats back (a player left player select) hasn't left
            Assert.IsFalse(OnlineGame.MachineGone(new ulong[] { 2, 5 }, 5));
            Assert.IsTrue(OnlineGame.MachineGone(new ulong[] { 2, 5 }, 7));
            Assert.IsTrue(OnlineGame.MachineGone(new ulong[0], 5));
        }
    }
}

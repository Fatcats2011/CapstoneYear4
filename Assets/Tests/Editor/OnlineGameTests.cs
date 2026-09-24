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

            Reflect.Invoke(session, "SetRole", NetworkRole.Host);
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role);
            Assert.IsInstanceOf<OnlineSceneFlow>(SceneFlow.Current);

            Reflect.Invoke(session, "SetRole", NetworkRole.Offline);
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.IsNotInstanceOf<OnlineSceneFlow>(SceneFlow.Current);
        }

        [Test]
        public void ASessionWithoutTheGame_LeavesThisMachinesRoleAlone()
        {
            session = OnlineSession.Create("1.0.0");

            Reflect.Invoke(session, "SetRole", NetworkRole.Client);

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
    }
}

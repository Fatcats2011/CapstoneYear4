using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class OnlineTestMenuTests
    {
        bool badConnectionBefore;
        OnlineSession session;

        [SetUp]
        public void SetUp()
        {
            badConnectionBefore = OnlineTestMenu.BadConnection;
        }

        [TearDown]
        public void TearDown()
        {
            OnlineTestMenu.BadConnection = badConnectionBefore;
            if (session != null)
                Object.DestroyImmediate(session.gameObject);
        }

        [UnityTearDown]
        public IEnumerator LeavePlayMode()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
        }

        /// <summary>
        /// Whether the menu offers Host and Join right now (its menu-item validator)
        /// </summary>
        public static bool MenuCanStart()
        {
            return (bool)typeof(OnlineTestMenu).GetMethod("CanStart", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        }

        [Test]
        public void Prepare_WithBadConnectionTicked_Adds150msAnd1PercentLoss()
        {
            OnlineTestMenu.BadConnection = true;

            session = OnlineTestMenu.Prepare();

            Assert.AreEqual(150, session.Direct.DebugSimulator.PacketDelayMS);
            Assert.AreEqual(1, session.Direct.DebugSimulator.PacketDropRate);
        }

        [Test]
        public void Prepare_AfterBadConnectionIsUnticked_AddsNothing()
        {
            OnlineTestMenu.BadConnection = true;
            session = OnlineTestMenu.Prepare();
            OnlineTestMenu.BadConnection = false;

            OnlineSession again = OnlineTestMenu.Prepare();

            Assert.AreSame(session, again, "the menu keeps one session");
            Assert.AreEqual(0, again.Direct.DebugSimulator.PacketDelayMS);
            Assert.AreEqual(0, again.Direct.DebugSimulator.PacketDropRate);
        }

        [Test]
        public void Prepare_PlaysTheGameOverTheSession()
        {
            session = OnlineTestMenu.Prepare();

            Assert.IsNotNull(session.GetComponent<OnlineGame>());
            Assert.AreSame(session, OnlineTestMenu.Prepare(), "the same session next time");
            Assert.AreEqual(1, session.GetComponents<OnlineGame>().Length, "played once");
        }

        [UnityTest]
        public IEnumerator HostAndJoin_WaitForTheTitleScreen()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            // Like the splash screen: the game's managers aren't loaded, so a session started now would miss them
            Assert.IsFalse(MenuCanStart());
        }
    }
}

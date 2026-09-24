using NUnit.Framework;
using UnityEngine;

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
    }
}

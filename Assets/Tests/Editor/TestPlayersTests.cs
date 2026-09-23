using NUnit.Framework;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class TestPlayersTests
    {
        [TearDown]
        public void TearDown()
        {
            TestPlayers.RemoveAll();
        }

        [Test]
        public void Add_PlugsInAVirtualController()
        {
            Gamepad pad = TestPlayers.Add();

            Assert.IsTrue(pad.added);
            Assert.AreEqual(TestPlayers.PAD_NAME, pad.name);
            CollectionAssert.Contains(InputSystem.devices, pad);
        }

        [Test]
        public void ToggleNewest_UnplugsOnlyTheNewestController()
        {
            Gamepad first = TestPlayers.Add();
            Gamepad newest = TestPlayers.Add();

            TestPlayers.ToggleNewest();

            Assert.IsTrue(first.added);
            Assert.IsFalse(newest.added);
        }

        [Test]
        public void ToggleNewest_Twice_PlugsItBackIn()
        {
            Gamepad pad = TestPlayers.Add();

            TestPlayers.ToggleNewest();
            TestPlayers.ToggleNewest();

            Assert.IsTrue(pad.added);
        }

        [Test]
        public void RemoveAll_UnplugsEveryTestController()
        {
            Gamepad first = TestPlayers.Add();
            Gamepad second = TestPlayers.Add();

            TestPlayers.RemoveAll();

            Assert.IsFalse(first.added);
            Assert.IsFalse(second.added);
            Assert.AreEqual(0, TestPlayers.Pads.Count);
        }
    }
}

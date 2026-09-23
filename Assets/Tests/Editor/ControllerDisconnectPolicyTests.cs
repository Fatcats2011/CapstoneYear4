using NUnit.Framework;

namespace DoA.Tests
{
    public class ControllerDisconnectPolicyTests
    {
        [Test]
        public void ShouldPause_WhileDriving_Pauses()
        {
            Assert.IsTrue(ControllerDisconnectPolicy.ShouldPause(pauseMenuIsActive: true, gameIsPaused: false));
        }

        [Test]
        public void ShouldPause_WhenAlreadyPaused_DoesNothing()
        {
            Assert.IsFalse(ControllerDisconnectPolicy.ShouldPause(pauseMenuIsActive: true, gameIsPaused: true));
        }

        [Test]
        public void ShouldPause_OutsideDriving_DoesNothing()
        {
            // Menus, cutscenes and the 3-2-1 countdown have no pause menu to resume from
            Assert.IsFalse(ControllerDisconnectPolicy.ShouldPause(pauseMenuIsActive: false, gameIsPaused: false));
        }

        [Test]
        public void PickPauseHost_FirstPlayerWithAControllerHostsThePause()
        {
            Assert.AreEqual(0, ControllerDisconnectPolicy.PickPauseHost(new[] { true, false, true, true }, 1));
        }

        [Test]
        public void PickPauseHost_SkipsTheDisconnectedPlayer()
        {
            Assert.AreEqual(1, ControllerDisconnectPolicy.PickPauseHost(new[] { false, true, true, false }, 0));
        }

        [Test]
        public void PickPauseHost_NobodyHasAController_DisconnectedPlayerHosts()
        {
            Assert.AreEqual(2, ControllerDisconnectPolicy.PickPauseHost(new[] { false, false, false, false }, 2));
        }

        [Test]
        public void PickSlotForReplacement_GoesToThePlayerMissingAController()
        {
            Assert.AreEqual(2, ControllerDisconnectPolicy.PickSlotForReplacement(new[] { false, false, true, false }));
        }

        [Test]
        public void PickSlotForReplacement_SeveralMissing_LowestPlayerFirst()
        {
            Assert.AreEqual(1, ControllerDisconnectPolicy.PickSlotForReplacement(new[] { false, true, false, true }));
        }

        [Test]
        public void PickSlotForReplacement_NobodyMissing_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, ControllerDisconnectPolicy.PickSlotForReplacement(new[] { false, false, false, false }));
        }
    }
}

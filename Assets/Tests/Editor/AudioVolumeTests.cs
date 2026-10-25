using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The game's overall loudness (AudioVolume): the Master row, in tenths, and silence while the window isn't focused
    /// when In the background is Mute. The game itself keeps running
    /// </summary>
    public class AudioVolumeTests
    {
        [Test]
        public void Master_InTenths()
        {
            Assert.AreEqual(1f, AudioVolume.ListenerVolume(10, true, true), 1e-5f);
            Assert.AreEqual(0.4f, AudioVolume.ListenerVolume(4, true, true), 1e-5f);
            Assert.AreEqual(0f, AudioVolume.ListenerVolume(0, false, true), 1e-5f);
        }

        [Test]
        public void InTheBackground_Muted_IsSilent()
        {
            Assert.AreEqual(0f, AudioVolume.ListenerVolume(10, true, false), 1e-5f);
        }

        [Test]
        public void InTheBackground_KeepPlaying_KeepsTheMaster()
        {
            Assert.AreEqual(0.7f, AudioVolume.ListenerVolume(7, false, false), 1e-5f);
        }

        [Test]
        public void Master_OutOfRange_IsClamped()
        {
            Assert.AreEqual(1f, AudioVolume.ListenerVolume(40, true, true), 1e-5f);
            Assert.AreEqual(0f, AudioVolume.ListenerVolume(-2, true, true), 1e-5f);
        }
    }
}

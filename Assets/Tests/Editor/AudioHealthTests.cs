using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The sound's state goes in the log beside the Perf line, so a playtest that loses all sound shows which part went
    /// </summary>
    public class AudioHealthTests
    {
        [Test]
        public void Line_SaysEachPart()
        {
            Assert.AreEqual("Audio: 1 listener, volume 1, music -4 dB, effects -8 dB, music playing, 23 sources (3 playing), 32 real voices",
                AudioHealth.Line(1, 1f, false, -4f, -8f, true, 23, 3, 32));
        }

        [Test]
        public void Line_MarksWhatsWrong()
        {
            string line = AudioHealth.Line(0, 0.5f, true, -80f, -8f, false, 140, 0, 32);
            StringAssert.Contains("0 listeners", line);
            StringAssert.Contains("volume 0.5 (paused)", line);
            StringAssert.Contains("music STOPPED", line);
        }
    }
}

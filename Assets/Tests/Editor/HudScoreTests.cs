using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// A player's score in the match HUD, which has room for three digits (NumberHandler): a higher score reads 999, not a
    /// number left over from before. The results screen shows the true score
    /// </summary>
    public class HudScoreTests
    {
        [TestCase("0", "0")]
        [TestCase("55", "55")]
        [TestCase("999", "999")]
        [TestCase("1000", "999")]
        [TestCase("1280", "999")]
        [TestCase("52000", "999")]
        public void HudScoreText_NeverNeedsMoreThanThreeDigits(string score, string shown)
        {
            Assert.AreEqual(shown, NumberHandler.HudScoreText(score));
        }

        [Test]
        public void HudScoreText_NotANumber_IsLeftAlone()
        {
            Assert.AreEqual("abc", NumberHandler.HudScoreText("abc"));
        }
    }
}

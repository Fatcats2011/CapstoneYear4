using NUnit.Framework;

namespace DoA.Tests
{
    public class GameSettingsTests
    {
        static readonly GameSettings Defaults = new GameSettings(5, 6, 1);

        static void AssertSettings(int bgm, int sfx, int fullscreen, GameSettings actual)
        {
            Assert.AreEqual(bgm, actual.bgmPosition, "bgm");
            Assert.AreEqual(sfx, actual.sfxPosition, "sfx");
            Assert.AreEqual(fullscreen, actual.fullscreenPosition, "fullscreen");
        }

        [Test]
        public void SavedSettings_LoadBackUnchanged()
        {
            GameSettings saved = new GameSettings(3, 9, 0);
            AssertSettings(3, 9, 0, GameSettings.Parse(saved.ToText(), Defaults));
        }

        [Test]
        public void UnreadableFile_FallsBackToDefaults()
        {
            AssertSettings(5, 6, 1, GameSettings.Parse("\0\u0001\u0002 binary settings from the old BinaryFormatter save", Defaults));
        }

        [Test]
        public void OutOfRangeValues_AreClamped()
        {
            AssertSettings(11, 0, 1, GameSettings.Parse("bgm=99\nsfx=-4\nfullscreen=7", Defaults));
        }

        [Test]
        public void MissingValues_KeepTheirDefaults()
        {
            AssertSettings(2, 6, 1, GameSettings.Parse("bgm=2", Defaults));
        }

        [Test]
        public void WindowsLineEndings_AreRead()
        {
            AssertSettings(1, 2, 0, GameSettings.Parse("bgm=1\r\nsfx=2\r\nfullscreen=0\r\n", Defaults));
        }

        [Test]
        public void SavedQuality_LoadsBack()
        {
            GameSettings saved = new GameSettings(3, 9, 0, GraphicsQuality.LOW);

            Assert.AreEqual(GraphicsQuality.LOW, GameSettings.Parse(saved.ToText(), Defaults).qualityLevel);
        }

        [Test]
        public void SettingsFromBeforeQualityExisted_KeepTheDefaultQuality()
        {
            GameSettings deckDefaults = new GameSettings(5, 6, 1, GraphicsQuality.MEDIUM);

            Assert.AreEqual(GraphicsQuality.MEDIUM, GameSettings.Parse("bgm=3\nsfx=9\nfullscreen=0\n", deckDefaults).qualityLevel);
        }

        [Test]
        public void OutOfRangeQuality_IsClamped()
        {
            GameSettings mediumDefaults = new GameSettings(5, 6, 1, GraphicsQuality.MEDIUM);

            Assert.AreEqual(GraphicsQuality.HIGH, GameSettings.Parse("quality=9", mediumDefaults).qualityLevel);
            Assert.AreEqual(GraphicsQuality.LOW, GameSettings.Parse("quality=-5", mediumDefaults).qualityLevel);
        }
    }
}

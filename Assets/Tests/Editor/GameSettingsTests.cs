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

        static void AssertDisplay(int exclusive, int width, int height, int vsync, int frameCap, GameSettings actual)
        {
            Assert.AreEqual(exclusive, actual.exclusiveFullscreen, "exclusive");
            Assert.AreEqual(width, actual.width, "width");
            Assert.AreEqual(height, actual.height, "height");
            Assert.AreEqual(vsync, actual.vsync, "vsync");
            Assert.AreEqual(frameCap, actual.frameCap, "framecap");
        }

        [Test]
        public void SavedDisplayKeys_LoadBack()
        {
            GameSettings saved = new GameSettings(3, 9, 1);
            saved.exclusiveFullscreen = 1;
            saved.width = 1600;
            saved.height = 900;
            saved.vsync = 0;
            saved.frameCap = 144;

            AssertDisplay(1, 1600, 900, 0, 144, GameSettings.Parse(saved.ToText(), Defaults));
        }

        [Test]
        public void SettingsFromBeforeDisplayKeys_KeepTheDisplayDefaults()
        {
            AssertDisplay(0, 0, 0, 1, 0, GameSettings.Parse("bgm=3\nsfx=9\nfullscreen=1\nquality=2\n", Defaults));
        }

        [Test]
        public void DisplayKeys_AreClamped()
        {
            AssertDisplay(0, 640, 360, 1, 30, GameSettings.Parse("exclusive=-1\nwidth=100\nheight=100\nvsync=5\nframecap=10", Defaults));
            Assert.AreEqual(16384, GameSettings.Parse("width=99999", Defaults).width);
            Assert.AreEqual(500, GameSettings.Parse("framecap=9999", Defaults).frameCap);
            Assert.AreEqual(0, GameSettings.Parse("framecap=-4", Defaults).frameCap);
        }

        [Test]
        public void ZeroSize_MeansTheDesktop_AndStaysZero()
        {
            GameSettings sized = GameSettings.Parse("width=1600\nheight=900", Defaults);

            AssertDisplay(0, 0, 0, 1, 0, GameSettings.Parse("width=0\nheight=0", sized));
        }

        [Test]
        public void WithMenuPositions_KeepsTheDisplayKeys()
        {
            GameSettings loaded = GameSettings.Parse("framecap=60\nwidth=1600\nheight=900", Defaults);

            GameSettings saved = loaded.WithMenuPositions(1, 2, 0, GraphicsQuality.MEDIUM);

            AssertSettings(1, 2, 0, saved);
            Assert.AreEqual(GraphicsQuality.MEDIUM, saved.qualityLevel);
            AssertDisplay(0, 1600, 900, 1, 60, saved);
        }
    }
}

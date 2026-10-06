using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// How saved display settings become Unity's (DisplayRules): the fullscreen mode, a size the screen can show (a
    /// fullscreen size this monitor doesn't list falls back to the desktop's; a window fits inside the desktop), VSync and
    /// the frame cap
    /// </summary>
    public class DisplayRulesTests
    {
        static readonly ScreenSize DESKTOP_1080 = new ScreenSize(1920, 1080);

        static readonly List<ScreenSize> LISTED_1080 = new List<ScreenSize>
        {
            new ScreenSize(1280, 720), new ScreenSize(1600, 900), new ScreenSize(1920, 1080)
        };

        [Test]
        public void ModeFor_TheFullscreenRow_AndExclusive()
        {
            Assert.AreEqual(FullScreenMode.Windowed, DisplayRules.ModeFor(0, 1));
            Assert.AreEqual(FullScreenMode.FullScreenWindow, DisplayRules.ModeFor(1, 0));
            Assert.AreEqual(FullScreenMode.ExclusiveFullScreen, DisplayRules.ModeFor(1, 1));
        }

        [Test]
        public void Fullscreen_ZeroSize_IsTheDesktop()
        {
            ScreenSize desktop = new ScreenSize(2560, 1440);

            Assert.AreEqual(desktop, DisplayRules.SizeFor(FullScreenMode.FullScreenWindow, 0, 0, desktop, new List<ScreenSize> { desktop }));
        }

        [Test]
        public void Fullscreen_AListedSize_IsKept()
        {
            Assert.AreEqual(DESKTOP_1080, DisplayRules.SizeFor(FullScreenMode.ExclusiveFullScreen, 1920, 1080, new ScreenSize(2560, 1440),
                new List<ScreenSize> { DESKTOP_1080, new ScreenSize(2560, 1440) }));
        }

        [Test]
        public void Fullscreen_AnUnlistedSize_FallsBackToTheDesktop()
        {
            Assert.AreEqual(DESKTOP_1080, DisplayRules.SizeFor(FullScreenMode.ExclusiveFullScreen, 3840, 2160, DESKTOP_1080, LISTED_1080));
        }

        [Test]
        public void Windowed_ZeroSize_Is1280x720_OrTheDesktopIfSmaller()
        {
            Assert.AreEqual(new ScreenSize(1280, 720), DisplayRules.SizeFor(FullScreenMode.Windowed, 0, 0, DESKTOP_1080, LISTED_1080));
            ScreenSize deck = new ScreenSize(1280, 800);
            Assert.AreEqual(new ScreenSize(1280, 720), DisplayRules.SizeFor(FullScreenMode.Windowed, 0, 0, deck, new List<ScreenSize> { deck }));
            ScreenSize small = new ScreenSize(1024, 600);
            Assert.AreEqual(small, DisplayRules.SizeFor(FullScreenMode.Windowed, 0, 0, small, new List<ScreenSize> { small }));
        }

        [Test]
        public void Windowed_ABigSize_ShrinksIntoTheDesktop()
        {
            Assert.AreEqual(DESKTOP_1080, DisplayRules.SizeFor(FullScreenMode.Windowed, 2560, 1440, DESKTOP_1080, LISTED_1080));
        }

        [Test]
        public void VSyncAndFrameCap()
        {
            Assert.AreEqual(1, DisplayRules.VSyncCount(1));
            Assert.AreEqual(0, DisplayRules.VSyncCount(0));
            Assert.AreEqual(-1, DisplayRules.TargetFrameRate(0));
            Assert.AreEqual(144, DisplayRules.TargetFrameRate(144));
        }
    }
}

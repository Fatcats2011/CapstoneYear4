using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The Options screen's Resolution row (ResolutionRules): the sizes it offers, where it starts, how left and right
    /// step, and what it says
    /// </summary>
    public class ResolutionRulesTests
    {
        static readonly ScreenSize SMALL = new ScreenSize(800, 600);
        static readonly ScreenSize HD = new ScreenSize(1280, 720);
        static readonly ScreenSize FULL_HD = new ScreenSize(1920, 1080);

        [Test]
        public void Choices_EachSizeOnce_SmallestFirst()
        {
            // The monitor lists each size once per refresh rate
            List<ScreenSize> listed = new List<ScreenSize> { FULL_HD, HD, FULL_HD, SMALL, new ScreenSize(1280, 1024) };

            CollectionAssert.AreEqual(new[] { SMALL, HD, new ScreenSize(1280, 1024), FULL_HD }, ResolutionRules.Choices(listed));
        }

        [Test]
        public void StartIndex_TheSavedSizeIfListed_ElseTheCurrent_ElseTheLargest()
        {
            List<ScreenSize> choices = new List<ScreenSize> { SMALL, HD, FULL_HD };

            Assert.AreEqual(1, ResolutionRules.StartIndex(choices, HD, FULL_HD), "saved");
            Assert.AreEqual(2, ResolutionRules.StartIndex(choices, new ScreenSize(0, 0), FULL_HD), "nothing saved: the current size");
            Assert.AreEqual(0, ResolutionRules.StartIndex(choices, new ScreenSize(1111, 500), SMALL), "a saved size this monitor lacks: the current size");
            Assert.AreEqual(2, ResolutionRules.StartIndex(choices, new ScreenSize(0, 0), new ScreenSize(1366, 768)), "neither listed: the largest");
        }

        [Test]
        public void Step_StopsAtBothEnds()
        {
            Assert.AreEqual(1, ResolutionRules.Step(0, 3, true));
            Assert.AreEqual(2, ResolutionRules.Step(2, 3, true), "the largest stays");
            Assert.AreEqual(0, ResolutionRules.Step(0, 3, false), "the smallest stays");
            Assert.AreEqual(1, ResolutionRules.Step(2, 3, false));
        }

        [Test]
        public void Label_SaysTheSize_AndWhenAPressApplies()
        {
            Assert.AreEqual("<  1920 x 1080  >", ResolutionRules.Label(FULL_HD, false));
            Assert.AreEqual("<  1280 x 720  >   A: apply", ResolutionRules.Label(HD, true));
        }

        [Test]
        public void Rows_TheScenesRowsInOrder_ThenResolution()
        {
            CollectionAssert.AreEqual(
                new[] { OptionsMenu.OptionSelected.BGM, OptionsMenu.OptionSelected.SFX, OptionsMenu.OptionSelected.FULLSCREEN, OptionsMenu.OptionSelected.RESOLUTION },
                OptionsMenu.RowsFor(3));
            CollectionAssert.AreEqual(
                new[] { OptionsMenu.OptionSelected.BGM, OptionsMenu.OptionSelected.SFX, OptionsMenu.OptionSelected.FULLSCREEN, OptionsMenu.OptionSelected.QUALITY, OptionsMenu.OptionSelected.RESOLUTION },
                OptionsMenu.RowsFor(4));
        }
    }
}

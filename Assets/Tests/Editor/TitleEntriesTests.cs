using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What each place on the title screen does (TitleEntries): with the scene's Online Play entry, Local Play and Online
    /// Play; without it (4 entries), the old title screen
    /// </summary>
    public class TitleEntriesTests
    {
        [Test]
        public void FiveEntries_LocalOnlineOptionsCreditsQuit()
        {
            CollectionAssert.AreEqual(
                new[] { TitleEntry.Local, TitleEntry.Online, TitleEntry.Options, TitleEntry.Credits, TitleEntry.Quit },
                new[] { 0, 1, 2, 3, 4 }.Select(i => TitleEntries.At(i, 5)));
            Assert.IsTrue(TitleEntries.HasOnline(5));
        }

        [Test]
        public void FourEntries_TheOldTitleScreen_HasNoOnline()
        {
            CollectionAssert.AreEqual(
                new[] { TitleEntry.Local, TitleEntry.Options, TitleEntry.Credits, TitleEntry.Quit },
                new[] { 0, 1, 2, 3 }.Select(i => TitleEntries.At(i, 4)));
            Assert.IsFalse(TitleEntries.HasOnline(4));
        }

        [TestCase(-1, 5)]
        [TestCase(5, 5)]
        [TestCase(4, 4)]
        [TestCase(0, 3)]   // a count the code doesn't know: nothing
        public void OutsideTheKnownEntries_IsNothing(int index, int count)
        {
            Assert.AreEqual(TitleEntry.None, TitleEntries.At(index, count));
        }
    }
}

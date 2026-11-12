using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The order the title screen skits play in (TitlePlaylist): all 12 before any repeats, never the same one twice
    /// running
    /// </summary>
    public class TitlePlaylistTests
    {
        [Test]
        public void Next_PlaysAllTwelveBeforeAnyRepeat()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                TitlePlaylist playlist = new TitlePlaylist(12, new System.Random(seed));

                for (int round = 0; round < 5; round++)
                {
                    int[] played = Enumerable.Range(0, 12).Select(_ => playlist.Next()).ToArray();
                    CollectionAssert.AreEquivalent(Enumerable.Range(0, 12), played, "seed " + seed + ", round " + round);
                }
            }
        }

        [Test]
        public void Next_NeverTheSameTwiceRunning()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                TitlePlaylist playlist = new TitlePlaylist(12, new System.Random(seed));
                int last = -1;

                for (int draw = 0; draw < 1000; draw++)
                {
                    int next = playlist.Next();
                    Assert.AreNotEqual(last, next, "seed " + seed + ", draw " + draw);
                    last = next;
                }
            }
        }

        [Test]
        public void Next_OneSkit_AlwaysZero()
        {
            TitlePlaylist playlist = new TitlePlaylist(1, new System.Random(0));

            for (int draw = 0; draw < 10; draw++)
                Assert.AreEqual(0, playlist.Next());
        }
    }
}

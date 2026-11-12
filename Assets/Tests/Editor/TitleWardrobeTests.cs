using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The outfits dealt for each title screen skit (TitleWardrobe): 7 hats, 6 ghost colours and 4 companies as in the
    /// game
    /// </summary>
    public class TitleWardrobeTests
    {
        const int HATS = 7, COLOURS = 6, COMPANIES = 4;

        static TitleWardrobe Make(int seed)
        {
            return new TitleWardrobe(HATS, COLOURS, COMPANIES, new System.Random(seed));
        }

        static (int, int) Pair(TitleOutfit outfit)
        {
            return (outfit.hat, outfit.colour);
        }

        [Test]
        public void Deal_HatsAndColoursDifferWithinASkit()
        {
            TitleWardrobe wardrobe = Make(1);

            for (int deal = 0; deal < 200; deal++)
            {
                TitleOutfit[] outfits = wardrobe.Deal(4);

                Assert.AreEqual(4, outfits.Length);
                Assert.AreEqual(4, outfits.Select(o => o.hat).Distinct().Count(), "hats, deal " + deal);
                Assert.AreEqual(4, outfits.Select(o => o.colour).Distinct().Count(), "colours, deal " + deal);
            }
        }

        [Test]
        public void Deal_NoPairRepeatsBeforeAllFortyTwoAreWorn()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                TitleWardrobe wardrobe = Make(seed);
                HashSet<(int, int)> worn = new HashSet<(int, int)>();

                // 21 deals of 2 are exactly the 42 pairs
                for (int deal = 0; deal < 21; deal++)
                    foreach (TitleOutfit outfit in wardrobe.Deal(2))
                        Assert.IsTrue(worn.Add(Pair(outfit)), "seed " + seed + " repeated a pair in deal " + deal);

                Assert.AreEqual(HATS * COLOURS, worn.Count);
            }
        }

        [Test]
        public void Deal_SharesNoPairWithThePreviousDeal()
        {
            for (int seed = 0; seed < 10; seed++)
            {
                TitleWardrobe wardrobe = Make(seed);
                HashSet<(int, int)> previous = new HashSet<(int, int)>();

                for (int deal = 0; deal < 500; deal++)
                {
                    TitleOutfit[] outfits = wardrobe.Deal(4);

                    foreach (TitleOutfit outfit in outfits)
                        Assert.IsFalse(previous.Contains(Pair(outfit)), "seed " + seed + ", deal " + deal);

                    previous = new HashSet<(int, int)>(outfits.Select(Pair));
                }
            }
        }

        [Test]
        public void Deal_CompanyIsInRange()
        {
            TitleWardrobe wardrobe = Make(3);

            for (int deal = 0; deal < 200; deal++)
                foreach (TitleOutfit outfit in wardrobe.Deal(3))
                {
                    Assert.That(outfit.company, Is.InRange(0, COMPANIES - 1));
                    Assert.That(outfit.hat, Is.InRange(0, HATS - 1));
                    Assert.That(outfit.colour, Is.InRange(0, COLOURS - 1));
                }
        }

        [Test]
        public void Deal_MoreThanHatsOrColours_Throws()
        {
            // 7 hats could dress 7 ghosts, but there are only 6 colours
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(0).Deal(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => Make(0).Deal(0));
        }
    }
}

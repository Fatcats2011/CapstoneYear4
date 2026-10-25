using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The match rules the host sets (HouseRules): normal is today's game, the wave length's factor, a public lobby
    /// always playing normal, the summary line, and settings.cfg's keys
    /// </summary>
    public class HouseRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            HouseRules.Current = HouseRules.Normal;
        }

        [Test]
        public void Normal_IsTodaysGame()
        {
            HouseRules normal = HouseRules.Normal;

            Assert.AreEqual(MatchLength.Normal, normal.Length);
            Assert.IsTrue(normal.Steals);
            Assert.IsTrue(normal.GoldenOrder);
            Assert.AreEqual(1f, normal.WaveFactor);
            Assert.AreEqual("", normal.Summary);
        }

        [TestCase(MatchLength.Short, 0.75f)]
        [TestCase(MatchLength.Normal, 1f)]
        [TestCase(MatchLength.Long, 1.5f)]
        public void WaveFactor_ByLength(MatchLength length, float factor)
        {
            Assert.AreEqual(factor, new HouseRules(length, true, true).WaveFactor);
        }

        [Test]
        public void APublicLobby_PlaysNormal()
        {
            HouseRules mine = new HouseRules(MatchLength.Short, false, false);

            Assert.AreEqual(HouseRules.Normal, mine.ForMatch(true));
            Assert.AreEqual(mine, mine.ForMatch(false));
        }

        [Test]
        public void Summary_NamesWhatsChanged()
        {
            Assert.AreEqual("SHORT · NO STEALS · NO GOLDEN ORDER", new HouseRules(MatchLength.Short, false, false).Summary);
            Assert.AreEqual("LONG", new HouseRules(MatchLength.Long, true, true).Summary);
        }

        [Test]
        public void Settings_LoadBack_AndOldFilesPlayNormal()
        {
            GameSettings saved = new GameSettings(3, 4, 1);
            saved.matchLength = (int)MatchLength.Long;
            saved.steals = 0;
            saved.goldenOrder = 0;

            GameSettings loaded = GameSettings.Parse(saved.ToText(), new GameSettings(5, 6, 1));
            Assert.AreEqual(new HouseRules(MatchLength.Long, false, false), HouseRules.From(loaded));

            GameSettings old = GameSettings.Parse("bgm=3", new GameSettings(5, 6, 1));
            Assert.AreEqual(HouseRules.Normal, HouseRules.From(old));
        }

        [Test]
        public void Settings_AreClamped()
        {
            GameSettings loaded = GameSettings.Parse("length=9\nsteals=4\ngolden=-1", new GameSettings(5, 6, 1));

            Assert.AreEqual((int)MatchLength.Long, loaded.matchLength);
            Assert.AreEqual(1, loaded.steals);
            Assert.AreEqual(0, loaded.goldenOrder);
        }

        [Test]
        public void ForMatch_TheMatchUsesCurrent()
        {
            HouseRules.Current = new HouseRules(MatchLength.Short, true, false);

            Assert.IsFalse(HouseRules.Current.GoldenOrder);
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>Which MATCH RULES rows each mode shows, hides or locks (docs/game-modes-ideas.md, "Mutators per Mode")</summary>
    public class ModeRulesTests
    {
        const RuleShow S = RuleShow.Shown;
        const RuleShow H = RuleShow.Hidden;
        const RuleShow L = RuleShow.LockedOn;

        //                      Speed LowG Waves WaveLen Round Steals Golden Bots
        [TestCase(GameMode.Standard, S, S, S, S, H, S, S, S)]
        [TestCase(GameMode.GoldRush, S, S, S, S, H, L, S, S)]
        [TestCase(GameMode.KingsCrown, S, S, H, H, S, L, H, S)]
        [TestCase(GameMode.Infection, S, S, H, H, S, H, H, S)]
        [TestCase(GameMode.HotPotato, S, S, H, H, S, L, H, S)]
        public void Table(GameMode mode, RuleShow speed, RuleShow lowGravity, RuleShow waves, RuleShow waveLength,
            RuleShow round, RuleShow steals, RuleShow golden, RuleShow bots)
        {
            Assert.AreEqual(speed, ModeRules.For(mode, Rule.Speed), "speed");
            Assert.AreEqual(lowGravity, ModeRules.For(mode, Rule.LowGravity), "low gravity");
            Assert.AreEqual(waves, ModeRules.For(mode, Rule.Waves), "waves");
            Assert.AreEqual(waveLength, ModeRules.For(mode, Rule.WaveLength), "wave length");
            Assert.AreEqual(round, ModeRules.For(mode, Rule.RoundTimer), "round timer");
            Assert.AreEqual(steals, ModeRules.For(mode, Rule.Steals), "steals");
            Assert.AreEqual(golden, ModeRules.For(mode, Rule.GoldenOrder), "golden");
            Assert.AreEqual(bots, ModeRules.For(mode, Rule.Bots), "bots");
        }

        [Test]
        public void OnlyStandardIsPlayable_UntilAModeIsBuilt()
        {
            CollectionAssert.AreEqual(new[] { GameMode.Standard }, GameModes.Playable);
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    public class JoinRulesTests
    {
        [Test]
        public void Refusal_SameBuildWithASeatFreeBeforeTheMatch_LetsThePlayerIn()
        {
            Assert.IsNull(JoinRules.Refusal("1.0.0", "1.0.0", Constants.MAX_PLAYERS - 1, GameState.PlayerSelect));
        }

        [Test]
        public void Refusal_AnotherBuild_NamesBothVersions()
        {
            string refusal = JoinRules.Refusal("1.0.0", "1.0.1", 1, GameState.PlayerSelect);

            StringAssert.Contains("1.0.0", refusal);
            StringAssert.Contains("1.0.1", refusal);
        }

        [Test]
        public void Refusal_NoBuildSent_CallsTheVersionUnknown()
        {
            StringAssert.Contains("an unknown version", JoinRules.Refusal("1.0.0", "", 1, GameState.PlayerSelect));
        }

        [Test]
        public void Refusal_EverySeatTaken_SaysTheMatchIsFull()
        {
            Assert.AreEqual(JoinRules.FULL, JoinRules.Refusal("1.0.0", "1.0.0", Constants.MAX_PLAYERS, GameState.PlayerSelect));
        }

        [TestCase(GameState.Default, false)]
        [TestCase(GameState.Menu, false)]
        [TestCase(GameState.Options, false)]
        [TestCase(GameState.Credits, false)]
        [TestCase(GameState.PlayerSelect, false)]
        [TestCase(GameState.Loading, true)]
        [TestCase(GameState.StartingCutscene, true)]
        [TestCase(GameState.Tutorial, true)]
        [TestCase(GameState.Begin, true)]
        [TestCase(GameState.MainLoop, true)]
        [TestCase(GameState.GoldenCutscene, true)]
        [TestCase(GameState.FinalPackage, true)]
        [TestCase(GameState.Results, true)]
        [TestCase(GameState.Paused, true)]
        public void Refusal_OnceTheHostsMatchHasStarted_SaysSo(GameState hostState, bool started)
        {
            Assert.AreEqual(started ? JoinRules.STARTED : null, JoinRules.Refusal("1.0.0", "1.0.0", 1, hostState));
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The host's rules when one player boosts into another online: a steal from a player who isn't boosting, a clash
    /// with one who is. The host judges with its own view: a respawning player can't be hit, the two must be within
    /// reach, and a pair counts once until its cooldown ends, whichever machine asks first. So two machines asking about
    /// one bump make one hit. Hits only count while players drive
    /// </summary>
    public class StealRulesTests
    {
        StealRules rules;

        [SetUp]
        public void SetUp()
        {
            rules = new StealRules();
        }

        [Test]
        public void BoostingIntoAPlayerWhoIsnt_Steals()
        {
            Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 1, 10f, 3f, false, false));
        }

        [Test]
        public void BoostingIntoABoostingPlayer_Clashes()
        {
            Assert.AreEqual(HitVerdict.Clash, rules.Judge(0, 1, 10f, 3f, true, false));
        }

        [Test]
        public void TheSamePairAgainSoon_CountsForNothing_EitherWayRound()
        {
            // Both machines asking about one bump
            rules.Judge(0, 1, 10f, 3f, false, false);
            Assert.AreEqual(HitVerdict.None, rules.Judge(1, 0, 10.2f, 3f, false, false), "the other machine's request");
            Assert.AreEqual(HitVerdict.None, rules.Judge(0, 1, 10f + StealRules.PAIR_COOLDOWN - 0.1f, 3f, true, false));
        }

        [Test]
        public void TheSamePairAfterTheCooldown_CountsAgain()
        {
            rules.Judge(0, 1, 10f, 3f, false, false);
            Assert.AreEqual(HitVerdict.Steal, rules.Judge(1, 0, 10f + StealRules.PAIR_COOLDOWN, 3f, false, false));
        }

        [Test]
        public void AnotherPair_IsntHeldUp()
        {
            rules.Judge(0, 1, 10f, 3f, false, false);
            Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 2, 10.1f, 3f, false, false));
            Assert.AreEqual(HitVerdict.Steal, rules.Judge(2, 1, 10.2f, 3f, false, false));
        }

        [Test]
        public void TooFarApartForTheHost_CountsForNothing_AndDoesntHoldThePairUp()
        {
            Assert.AreEqual(HitVerdict.None, rules.Judge(0, 1, 10f, StealRules.REACH + 1f, false, false));
            Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 1, 10.1f, 3f, false, false));
        }

        [Test]
        public void ARespawningPlayer_CantBeHit()
        {
            Assert.AreEqual(HitVerdict.None, rules.Judge(0, 1, 10f, 3f, false, true));
            Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 1, 10.1f, 3f, false, false), "not held up");
        }

        [Test]
        public void NobodyHitsThemselves_OrAnEmptySeat()
        {
            Assert.AreEqual(HitVerdict.None, rules.Judge(1, 1, 10f, 0f, false, false));
            Assert.AreEqual(HitVerdict.None, rules.Judge(-1, 1, 10f, 3f, false, false));
            Assert.AreEqual(HitVerdict.None, rules.Judge(0, Constants.MAX_PLAYERS, 10f, 3f, false, false));
        }

        [Test]
        public void HitsCount_OnlyWhilePlayersDrive()
        {
            GameState[] driving = { GameState.Tutorial, GameState.Begin, GameState.MainLoop, GameState.FinalPackage };
            foreach (GameState state in (GameState[])System.Enum.GetValues(typeof(GameState)))
                Assert.AreEqual(System.Array.IndexOf(driving, state) >= 0, StealRules.Counts(state), state.ToString());
        }
    }
}

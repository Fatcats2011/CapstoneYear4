using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The achievements and who earns them (MatchFeats): a first delivery in a match, and a golden finish (delivering the
    /// golden order, then finishing first, ties included). Which machine unlocks an earned one, and Steam's store without
    /// Steam. See docs/steam/in-game-features.md
    /// </summary>
    public class AchievementsTests
    {
        static Dictionary<int, int> Scores(params int[] seatThenScore)
        {
            Dictionary<int, int> scores = new Dictionary<int, int>();
            for (int i = 0; i < seatThenScore.Length; i += 2)
                scores[seatThenScore[i]] = seatThenScore[i + 1];
            return scores;
        }

        [Test]
        public void ApiName_IsWhatTheDashboardUses()
        {
            Assert.AreEqual("FIRST_DELIVERY", AchievementNames.ApiName(Achievement.FirstDelivery));
            Assert.AreEqual("GOLDEN_WIN", AchievementNames.ApiName(Achievement.GoldenWin));
            Assert.AreEqual("TUTORIAL_DONE", AchievementNames.ApiName(Achievement.TutorialDone));
            Assert.AreEqual("STEAL", AchievementNames.ApiName(Achievement.Steal));
            Assert.AreEqual("GOLDEN_STEAL", AchievementNames.ApiName(Achievement.GoldenSteal));
            Assert.AreEqual("HARD_DELIVERY", AchievementNames.ApiName(Achievement.HardDelivery));
            Assert.AreEqual("WIN", AchievementNames.ApiName(Achievement.Win));
            Assert.AreEqual("LAST_TO_FIRST", AchievementNames.ApiName(Achievement.LastToFirst));
            Assert.AreEqual("FELL_IN_WATER", AchievementNames.ApiName(Achievement.FellInWater));
        }

        [Test]
        public void ApiName_EveryAchievementHasOne()
        {
            foreach (Achievement achievement in System.Enum.GetValues(typeof(Achievement)))
                Assert.IsNotNull(AchievementNames.ApiName(achievement), achievement.ToString());
        }

        [Test]
        public void Delivered_InTheMatch_EarnsFirstDelivery_OncePerSeat()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.FirstDelivery) }, feats.Delivered(1, Constants.OrderValue.Easy, GameState.MainLoop));
            CollectionAssert.IsEmpty(feats.Delivered(1, Constants.OrderValue.Easy, GameState.MainLoop));
            CollectionAssert.AreEqual(new[] { new Feat(2, Achievement.FirstDelivery) }, feats.Delivered(2, Constants.OrderValue.Easy, GameState.MainLoop));
        }

        [Test]
        public void Delivered_InTheTutorial_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.IsEmpty(feats.Delivered(0, Constants.OrderValue.Easy, GameState.Tutorial));
            CollectionAssert.AreEqual(new[] { new Feat(0, Achievement.FirstDelivery) }, feats.Delivered(0, Constants.OrderValue.Easy, GameState.MainLoop));
        }

        [Test]
        public void Results_TheGoldenDelivererLeads_EarnsGoldenWin()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.FirstDelivery) }, feats.Delivered(1, Constants.OrderValue.Golden, GameState.FinalPackage));
            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.Win), new Feat(1, Achievement.GoldenWin) }, feats.Results(Scores(0, 40, 1, 90)));
        }

        [Test]
        public void Results_TheGoldenDelivererTiesForFirst_EarnsGoldenWin()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, Constants.OrderValue.Golden, GameState.FinalPackage);

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.GoldenWin) }, feats.Results(Scores(0, 90, 1, 90)));
        }

        [Test]
        public void Results_TheGoldenDelivererTrails_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, Constants.OrderValue.Golden, GameState.FinalPackage);

            CollectionAssert.AreEqual(new[] { new Feat(0, Achievement.Win) }, feats.Results(Scores(0, 120, 1, 90)), "only the winner's win");
        }

        [Test]
        public void Results_WithoutAGoldenDelivery_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, Constants.OrderValue.Easy, GameState.MainLoop);

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.Win) }, feats.Results(Scores(0, 40, 1, 90)), "no golden finish");
        }

        [Test]
        public void Delivered_TheGoldenOrderOutsideTheGoldenRound_IsNoGoldenDelivery()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, Constants.OrderValue.Golden, GameState.MainLoop);

            CollectionAssert.IsEmpty(feats.Results(Scores(1, 90)));
        }

        [Test]
        public void NewMatch_ForgetsTheLastMatch()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, Constants.OrderValue.Golden, GameState.FinalPackage);

            feats.NewMatch();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.FirstDelivery) }, feats.Delivered(1, Constants.OrderValue.Easy, GameState.MainLoop));
            CollectionAssert.DoesNotContain(feats.Results(Scores(0, 40, 1, 90)), new Feat(1, Achievement.GoldenWin), "the golden delivery was last match's");
        }

        [Test]
        public void Results_TwiceInAMatch_EarnsGoldenWinOnce()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, Constants.OrderValue.Golden, GameState.FinalPackage);

            CollectionAssert.IsNotEmpty(feats.Results(Scores(1, 90)));
            CollectionAssert.IsEmpty(feats.Results(Scores(1, 90)));
        }

        [Test]
        public void Delivered_AHardOrder_EarnsHardDelivery_OncePerSeat()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(0, Achievement.FirstDelivery), new Feat(0, Achievement.HardDelivery) },
                feats.Delivered(0, Constants.OrderValue.Hard, GameState.MainLoop));
            CollectionAssert.IsEmpty(feats.Delivered(0, Constants.OrderValue.Hard, GameState.MainLoop));
            CollectionAssert.IsEmpty(feats.Delivered(1, Constants.OrderValue.Hard, GameState.Tutorial), "not in the tutorial");
        }

        [Test]
        public void Delivered_AMediumOrder_IsNoHardDelivery()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.DoesNotContain(feats.Delivered(0, Constants.OrderValue.Medium, GameState.MainLoop), new Feat(0, Achievement.HardDelivery));
        }

        [Test]
        public void Stole_InTheMatch_EarnsSteal_OncePerSeat()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(2, Achievement.Steal) }, feats.Stole(2, false, GameState.MainLoop));
            CollectionAssert.IsEmpty(feats.Stole(2, false, GameState.MainLoop));
        }

        [Test]
        public void Stole_InTheTutorial_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.IsEmpty(feats.Stole(0, false, GameState.Tutorial), "stealing from the cutout");
        }

        [Test]
        public void Stole_TheGoldenOrder_EarnsGoldenSteal()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.Steal), new Feat(1, Achievement.GoldenSteal) },
                feats.Stole(1, true, GameState.FinalPackage));
            CollectionAssert.IsEmpty(feats.Stole(1, true, GameState.FinalPackage));
        }

        [Test]
        public void Learnt_EarnsTutorialDone_OncePerSeat()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(3, Achievement.TutorialDone) }, feats.Learnt(3));
            CollectionAssert.IsEmpty(feats.Learnt(3));
        }

        [Test]
        public void Results_TheTopScoreAheadOfSomeone_EarnsWin_TiesIncluded()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEquivalent(new[] { new Feat(0, Achievement.Win), new Feat(2, Achievement.Win) },
                feats.Results(Scores(0, 120, 1, 40, 2, 120)));
        }

        [Test]
        public void Results_EveryoneLevel_OrAlone_EarnsNoWin()
        {
            CollectionAssert.IsEmpty(new MatchFeats().Results(Scores(0, 0, 1, 0)), "nobody beat anyone");
            CollectionAssert.IsEmpty(new MatchFeats().Results(Scores(0, 300)), "playing alone");
            CollectionAssert.IsEmpty(new MatchFeats().Results(Scores()), "nobody left");
        }

        [Test]
        public void Results_LastAtTheGoldenRound_ThenWinning_EarnsLastToFirst()
        {
            MatchFeats feats = new MatchFeats();
            feats.GoldenRound(Scores(0, 200, 1, 40, 2, 120));

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.Win), new Feat(1, Achievement.LastToFirst) },
                feats.Results(Scores(0, 200, 1, 250, 2, 120)));
        }

        [Test]
        public void Results_LastAtTheGoldenRound_StillBehind_EarnsNoLastToFirst()
        {
            MatchFeats feats = new MatchFeats();
            feats.GoldenRound(Scores(0, 200, 1, 40));

            CollectionAssert.AreEqual(new[] { new Feat(0, Achievement.Win) }, feats.Results(Scores(0, 200, 1, 190)));
        }

        [Test]
        public void Results_TiedForLastAtTheGoldenRound_ThenWinning_EarnsLastToFirst()
        {
            MatchFeats feats = new MatchFeats();
            feats.GoldenRound(Scores(0, 200, 1, 40, 2, 40));

            CollectionAssert.Contains(feats.Results(Scores(0, 200, 1, 40, 2, 260)), new Feat(2, Achievement.LastToFirst));
        }

        [Test]
        public void Results_EveryoneLevelAtTheGoldenRound_EarnsNoLastToFirst()
        {
            MatchFeats feats = new MatchFeats();
            feats.GoldenRound(Scores(0, 0, 1, 0));

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.Win) }, feats.Results(Scores(0, 0, 1, 50)), "nobody was last");
        }

        [Test]
        public void NewMatch_ForgetsWhoWasLastAtTheGoldenRound()
        {
            MatchFeats feats = new MatchFeats();
            feats.GoldenRound(Scores(0, 200, 1, 40));

            feats.NewMatch();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.Win) }, feats.Results(Scores(0, 10, 1, 90)));
        }

        [Test]
        public void UnlocksHere_OfflineEverySeat_OnlineAnyOfThisMachines()
        {
            Assert.IsTrue(MatchFeats.UnlocksHere(false, 3, new int[0]), "offline: every player here");
            Assert.IsTrue(MatchFeats.UnlocksHere(true, 3, new[] { 1, 3 }), "online: any player on this machine");
            Assert.IsFalse(MatchFeats.UnlocksHere(true, 0, new[] { 1, 3 }), "not another machine's");
        }

        [Test]
        public void SteamAchievementStore_WithoutSteam_IsUnavailable_AndUnlockDoesNothing()
        {
            SteamAchievementStore store = new SteamAchievementStore();

            Assert.IsFalse(store.Available, "Steam isn't running in batch mode");
            Assert.DoesNotThrow(() => store.Unlock(Achievement.FirstDelivery));
            LogAssert.NoUnexpectedReceived();
        }
    }
}

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
        }

        [Test]
        public void Delivered_InTheMatch_EarnsFirstDelivery_OncePerSeat()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.FirstDelivery) }, feats.Delivered(1, false, GameState.MainLoop));
            CollectionAssert.IsEmpty(feats.Delivered(1, false, GameState.MainLoop));
            CollectionAssert.AreEqual(new[] { new Feat(2, Achievement.FirstDelivery) }, feats.Delivered(2, false, GameState.MainLoop));
        }

        [Test]
        public void Delivered_InTheTutorial_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.IsEmpty(feats.Delivered(0, false, GameState.Tutorial));
            CollectionAssert.AreEqual(new[] { new Feat(0, Achievement.FirstDelivery) }, feats.Delivered(0, false, GameState.MainLoop));
        }

        [Test]
        public void Results_TheGoldenDelivererLeads_EarnsGoldenWin()
        {
            MatchFeats feats = new MatchFeats();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.FirstDelivery) }, feats.Delivered(1, true, GameState.FinalPackage));
            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.GoldenWin) }, feats.Results(Scores(0, 40, 1, 90)));
        }

        [Test]
        public void Results_TheGoldenDelivererTiesForFirst_EarnsGoldenWin()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, true, GameState.FinalPackage);

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.GoldenWin) }, feats.Results(Scores(0, 90, 1, 90)));
        }

        [Test]
        public void Results_TheGoldenDelivererTrails_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, true, GameState.FinalPackage);

            CollectionAssert.IsEmpty(feats.Results(Scores(0, 120, 1, 90)));
        }

        [Test]
        public void Results_WithoutAGoldenDelivery_EarnsNothing()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, false, GameState.MainLoop);

            CollectionAssert.IsEmpty(feats.Results(Scores(0, 40, 1, 90)));
        }

        [Test]
        public void Delivered_TheGoldenOrderOutsideTheGoldenRound_IsNoGoldenDelivery()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, true, GameState.MainLoop);

            CollectionAssert.IsEmpty(feats.Results(Scores(1, 90)));
        }

        [Test]
        public void NewMatch_ForgetsTheLastMatch()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, true, GameState.FinalPackage);

            feats.NewMatch();

            CollectionAssert.AreEqual(new[] { new Feat(1, Achievement.FirstDelivery) }, feats.Delivered(1, false, GameState.MainLoop));
            CollectionAssert.IsEmpty(feats.Results(Scores(0, 40, 1, 90)), "the golden delivery was last match's");
        }

        [Test]
        public void Results_TwiceInAMatch_EarnsGoldenWinOnce()
        {
            MatchFeats feats = new MatchFeats();
            feats.Delivered(1, true, GameState.FinalPackage);

            CollectionAssert.IsNotEmpty(feats.Results(Scores(1, 90)));
            CollectionAssert.IsEmpty(feats.Results(Scores(1, 90)));
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

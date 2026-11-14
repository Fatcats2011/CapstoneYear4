using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What a mode's state change tells each player (ModeFeedbackRules): the lines worked out from a state before and after,
    /// the goal of each role, the tag's fades, the 30-second warning, the countdown's words, and that the 16 new strings
    /// exist in every language
    /// </summary>
    public class ModeFeedbackRulesTests
    {
        const int NONE = ModeMatch.NONE;
        const int ALL = 0b1111;

        static ModeView View(GameMode mode, int players, int crown = NONE, int potato = NONE, int infected = 0, int outMask = 0)
        {
            return new ModeView { Mode = mode, Players = players, Crown = crown, Potato = potato, Infected = infected, Out = outMask };
        }

        static List<(int, ModeLineKind, int)> Read(ModeView before, ModeView after)
        {
            List<(int, ModeLineKind, int)> result = new List<(int, ModeLineKind, int)>();
            foreach (ModeLine line in ModeFeedbackRules.Lines(before, after))
                result.Add((line.Seat, line.Kind, line.Other));
            return result;
        }

        [Test]
        public void Nothing_IsAnEmptyStandardView()
        {
            ModeView nothing = ModeView.Nothing;

            Assert.AreEqual(GameMode.Standard, nothing.Mode);
            Assert.AreEqual(0, nothing.Players);
            Assert.AreEqual(NONE, nothing.Crown);
            Assert.AreEqual(NONE, nothing.Potato);
            Assert.AreEqual(0, nothing.Infected);
            Assert.AreEqual(0, nothing.Out);
        }

        [Test]
        public void Crown_PickedUpFromNobody_OnlyTheNewHolderIsTold()
        {
            var lines = Read(View(GameMode.KingsCrown, ALL), View(GameMode.KingsCrown, ALL, crown: 2));

            CollectionAssert.AreEqual(new[] { (2, ModeLineKind.CrownYours, NONE) }, lines);
        }

        [Test]
        public void Crown_TakenFromAnotherPlayer_BothAreTold()
        {
            var lines = Read(View(GameMode.KingsCrown, ALL, crown: 2), View(GameMode.KingsCrown, ALL, crown: 0));

            CollectionAssert.AreEqual(new[] { (0, ModeLineKind.CrownYours, NONE), (2, ModeLineKind.CrownTaken, 0) }, lines);
        }

        [Test]
        public void Crown_TheHolderLeaves_NoLines()
        {
            var lines = Read(View(GameMode.KingsCrown, ALL, crown: 2), View(GameMode.KingsCrown, 0b1011));

            Assert.IsEmpty(lines);
        }

        [Test]
        public void Crown_Unchanged_NoLines()
        {
            ModeView view = View(GameMode.KingsCrown, ALL, crown: 1);

            Assert.IsEmpty(Read(view, view));
        }

        [Test]
        public void Infection_TheFirstHunter_IsToldTheyAreInfected()
        {
            var lines = Read(ModeView.Nothing, View(GameMode.Infection, ALL, infected: 0b0001));

            CollectionAssert.AreEqual(new[] { (0, ModeLineKind.Infected, NONE) }, lines);
        }

        [Test]
        public void Infection_ALoneHunterCatches_BothAreTold()
        {
            var lines = Read(View(GameMode.Infection, ALL, infected: 0b0001), View(GameMode.Infection, ALL, infected: 0b0101));

            CollectionAssert.AreEqual(new[] { (2, ModeLineKind.Infected, NONE), (0, ModeLineKind.Caught, 2) }, lines);
        }

        [Test]
        public void Infection_TwoHunters_NobodyIsToldWhoCaught_AndTheLastFreeIsCalled()
        {
            var lines = Read(View(GameMode.Infection, ALL, infected: 0b0101), View(GameMode.Infection, ALL, infected: 0b0111));

            CollectionAssert.AreEqual(new[] { (1, ModeLineKind.Infected, NONE), (3, ModeLineKind.LastFree, NONE) }, lines);
            CollectionAssert.DoesNotContain(lines.ConvertAll(l => l.Item2), ModeLineKind.Caught);
        }

        [Test]
        public void Infection_TheHunterLeavesAndANewOneIsPicked_OnlyTheNewOneIsTold()
        {
            var lines = Read(View(GameMode.Infection, ALL, infected: 0b0001), View(GameMode.Infection, 0b1110, infected: 0b0010));

            CollectionAssert.AreEqual(new[] { (1, ModeLineKind.Infected, NONE) }, lines);
        }

        [Test]
        public void HotPotato_LitFromNothing_TheHolderIsTold()
        {
            var lines = Read(View(GameMode.HotPotato, ALL), View(GameMode.HotPotato, ALL, potato: 1));

            CollectionAssert.AreEqual(new[] { (1, ModeLineKind.PotatoYours, NONE) }, lines);
        }

        [Test]
        public void HotPotato_Passed_BothAreTold()
        {
            var lines = Read(View(GameMode.HotPotato, ALL, potato: 1), View(GameMode.HotPotato, ALL, potato: 3));

            CollectionAssert.AreEqual(new[] { (3, ModeLineKind.PotatoYours, NONE), (1, ModeLineKind.PotatoPassed, 3) }, lines);
        }

        [Test]
        public void HotPotato_AnExplosion_EveryoneElseIsToldWhoIsOut()
        {
            var lines = Read(View(GameMode.HotPotato, ALL, potato: 3), View(GameMode.HotPotato, ALL, outMask: 0b1000));

            CollectionAssert.AreEqual(new[] { (ModeLine.EVERYONE_ELSE, ModeLineKind.PlayerOut, 3) }, lines);
        }

        [Test]
        public void HotPotato_ExplosionAndRelightInOneUpdate_BothAreReported_AndNoPass()
        {
            var lines = Read(View(GameMode.HotPotato, ALL, potato: 3), View(GameMode.HotPotato, ALL, potato: 0, outMask: 0b1000));

            CollectionAssert.AreEqual(new[] { (ModeLine.EVERYONE_ELSE, ModeLineKind.PlayerOut, 3), (0, ModeLineKind.PotatoYours, NONE) }, lines);
        }

        [Test]
        public void ANewMatchInAnotherMode_NothingLeaksFromTheLastOne()
        {
            var lines = Read(View(GameMode.KingsCrown, ALL, crown: 1), View(GameMode.Infection, ALL, infected: 0b0100));

            CollectionAssert.AreEqual(new[] { (2, ModeLineKind.Infected, NONE) }, lines);
        }

        [TestCase(GameMode.Standard)]
        [TestCase(GameMode.GoldRush)]
        public void OtherModes_NoLines(GameMode mode)
        {
            Assert.IsEmpty(Read(View(mode, ALL), View(mode, ALL, crown: 1, potato: 2, infected: 0b0001, outMask: 0b1000)));
        }

        [TestCase(GameMode.KingsCrown, 1, 1, ModeRole.KeepCrown)]
        [TestCase(GameMode.KingsCrown, 1, 2, ModeRole.GetCrown)]
        [TestCase(GameMode.Infection, 0b0010, 1, ModeRole.Catch)]
        [TestCase(GameMode.Infection, 0b0010, 2, ModeRole.DontGetCaught)]
        [TestCase(GameMode.HotPotato, 3, 3, ModeRole.PassIt)]
        [TestCase(GameMode.HotPotato, 3, 0, ModeRole.StayAway)]
        public void GoalFor_EachRole(GameMode mode, int holderOrInfected, int seat, ModeRole expected)
        {
            ModeView view = mode == GameMode.KingsCrown ? View(mode, ALL, crown: holderOrInfected)
                : mode == GameMode.Infection ? View(mode, ALL, infected: holderOrInfected)
                : View(mode, ALL, potato: holderOrInfected);

            Assert.AreEqual(expected, ModeFeedbackRules.GoalFor(view, seat));
        }

        [Test]
        public void GoalFor_AnOutPlayer_HasNone()
        {
            Assert.AreEqual(ModeRole.None, ModeFeedbackRules.GoalFor(View(GameMode.HotPotato, ALL, potato: 0, outMask: 0b1000), 3));
        }

        [Test]
        public void GoalFor_ASeatNotPlaying_HasNone()
        {
            Assert.AreEqual(ModeRole.None, ModeFeedbackRules.GoalFor(View(GameMode.KingsCrown, 0b0011, crown: 0), 2));
        }

        [Test]
        public void GoalFor_StandardAndGoldRush_HaveNone()
        {
            Assert.AreEqual(ModeRole.None, ModeFeedbackRules.GoalFor(View(GameMode.Standard, ALL), 0));
            Assert.AreEqual(ModeRole.None, ModeFeedbackRules.GoalFor(View(GameMode.GoldRush, ALL), 0));
        }

        [Test]
        public void Key_IsTheModeLineKeyOfEachKind()
        {
            Assert.AreEqual("mode.line.crown.yours", ModeFeedbackRules.Key(ModeLineKind.CrownYours));
            Assert.AreEqual("mode.line.crown.taken", ModeFeedbackRules.Key(ModeLineKind.CrownTaken));
            Assert.AreEqual("mode.line.infected", ModeFeedbackRules.Key(ModeLineKind.Infected));
            Assert.AreEqual("mode.line.caught", ModeFeedbackRules.Key(ModeLineKind.Caught));
            Assert.AreEqual("mode.line.lastfree", ModeFeedbackRules.Key(ModeLineKind.LastFree));
            Assert.AreEqual("mode.line.potato.yours", ModeFeedbackRules.Key(ModeLineKind.PotatoYours));
            Assert.AreEqual("mode.line.potato.passed", ModeFeedbackRules.Key(ModeLineKind.PotatoPassed));
            Assert.AreEqual("mode.line.out", ModeFeedbackRules.Key(ModeLineKind.PlayerOut));
        }

        [Test]
        public void GoalKey_IsTheModeGoalKeyOfEachGoal()
        {
            Assert.IsNull(ModeFeedbackRules.GoalKey(ModeRole.None));
            Assert.AreEqual("mode.goal.keepcrown", ModeFeedbackRules.GoalKey(ModeRole.KeepCrown));
            Assert.AreEqual("mode.goal.getcrown", ModeFeedbackRules.GoalKey(ModeRole.GetCrown));
            Assert.AreEqual("mode.goal.catch", ModeFeedbackRules.GoalKey(ModeRole.Catch));
            Assert.AreEqual("mode.goal.dontgetcaught", ModeFeedbackRules.GoalKey(ModeRole.DontGetCaught));
            Assert.AreEqual("mode.goal.passit", ModeFeedbackRules.GoalKey(ModeRole.PassIt));
            Assert.AreEqual("mode.goal.stayaway", ModeFeedbackRules.GoalKey(ModeRole.StayAway));
        }

        [Test]
        public void Tone_GoldForGains_RedForLosses_PlainOtherwise()
        {
            Assert.AreEqual(CalloutTone.Thief, ModeFeedbackRules.Tone(ModeLineKind.CrownYours));
            Assert.AreEqual(CalloutTone.Thief, ModeFeedbackRules.Tone(ModeLineKind.Caught));
            Assert.AreEqual(CalloutTone.Thief, ModeFeedbackRules.Tone(ModeLineKind.PotatoPassed));
            Assert.AreEqual(CalloutTone.Victim, ModeFeedbackRules.Tone(ModeLineKind.CrownTaken));
            Assert.AreEqual(CalloutTone.Victim, ModeFeedbackRules.Tone(ModeLineKind.Infected));
            Assert.AreEqual(CalloutTone.Victim, ModeFeedbackRules.Tone(ModeLineKind.PotatoYours));
            Assert.AreEqual(CalloutTone.Plain, ModeFeedbackRules.Tone(ModeLineKind.LastFree));
            Assert.AreEqual(CalloutTone.Plain, ModeFeedbackRules.Tone(ModeLineKind.PlayerOut));
        }

        [Test]
        public void GoalAlpha_FadesInHoldsFadesOut()
        {
            Assert.AreEqual(0f, ModeFeedbackRules.GoalAlpha(-1f));
            Assert.AreEqual(0.5f, ModeFeedbackRules.GoalAlpha(0.15f), 0.01f);
            Assert.AreEqual(1f, ModeFeedbackRules.GoalAlpha(10f));
            Assert.AreEqual(0.5f, ModeFeedbackRules.GoalAlpha(24.85f), 0.01f);
            Assert.AreEqual(0f, ModeFeedbackRules.GoalAlpha(25f));
        }

        [Test]
        public void WarnsIn_OnlyTheModesWithARoundTimer()
        {
            Assert.IsTrue(ModeFeedbackRules.WarnsIn(GameMode.KingsCrown));
            Assert.IsTrue(ModeFeedbackRules.WarnsIn(GameMode.Infection));
            Assert.IsFalse(ModeFeedbackRules.WarnsIn(GameMode.Standard));
            Assert.IsFalse(ModeFeedbackRules.WarnsIn(GameMode.GoldRush));
            Assert.IsFalse(ModeFeedbackRules.WarnsIn(GameMode.HotPotato));
        }

        [Test]
        public void CrossedWarning_OnlyWhenTheClockPassesThirtyWhileRunning()
        {
            Assert.IsTrue(ModeFeedbackRules.CrossedWarning(31f, 29.9f));
            Assert.IsFalse(ModeFeedbackRules.CrossedWarning(29f, 28f));
            Assert.IsFalse(ModeFeedbackRules.CrossedWarning(31f, 31f));
            Assert.IsFalse(ModeFeedbackRules.CrossedWarning(31f, 0f));
            Assert.IsFalse(ModeFeedbackRules.CrossedWarning(31f, -1f));
        }

        [Test]
        public void CountdownKey_GoldenOrTimesUp()
        {
            Assert.AreEqual("hud.goldenin", ModeFeedbackRules.CountdownKey(true));
            Assert.AreEqual("hud.timesupin", ModeFeedbackRules.CountdownKey(false));
        }

        [Test]
        public void TheSixteenNewStrings_ExistInEveryLanguage()
        {
            StringsCsv csv = StringsCsv.Load();
            string[] keys =
            {
                "mode.goal.keepcrown", "mode.goal.getcrown", "mode.goal.catch", "mode.goal.dontgetcaught", "mode.goal.passit",
                "mode.goal.stayaway", "mode.line.crown.yours", "mode.line.crown.taken", "mode.line.infected", "mode.line.caught",
                "mode.line.lastfree", "mode.line.potato.yours", "mode.line.potato.passed", "mode.line.out", "mode.line.thirty",
                "hud.timesupin"
            };

            foreach (string key in keys)
                foreach (string locale in LocaleCodes.All)
                    Assert.IsNotNull(csv.Get(key, locale), key + " " + locale);
        }

        [Test]
        public void TheTakenLine_ReadsTheName()
        {
            StringsCsv csv = StringsCsv.Load();

            Assert.AreEqual("Ryan TOOK YOUR CROWN", string.Format(csv.Get("mode.line.crown.taken", "en"), "Ryan"));
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// MATCH RULES (RulesPanel): Start in player select opens it on the rules picked, up / down / left / right change
    /// them (each change is the host's rules at once, and kept), A or B close it, the Golden Order off says which
    /// achievements can't be earned, each mode shows only its rows (some locked), and anything but the game as designed
    /// says it wasn't meant to be played that way
    /// </summary>
    public class RulesPanelTests
    {
        readonly TestObjects objects = new TestObjects();
        RulesPanel panel;
        static HouseRules kept;
        static int keeps;
        GameMode[] playable;

        static void Keep(HouseRules rules) { kept = rules; keeps++; }

        [SetUp]
        public void SetUp()
        {
            kept = HouseRules.Normal;
            keeps = 0;
            playable = GameModes.Playable;
            panel = RulesPanel.Build(objects.NewGameObject("Canvas").AddComponent<UnityEngine.RectTransform>());
            panel.Kept = Keep;
        }

        [TearDown]
        public void TearDown()
        {
            GameModes.Playable = playable;
            HouseRules.Current = HouseRules.Normal;
            objects.DestroyAll();
        }

        void Select(int row)
        {
            for (int i = 0; i < row; i++)
                panel.Move(true);
        }

        static HouseRules InMode(GameMode mode) => new HouseRules(MatchLength.Normal, true, true, true, mode);

        [Test]
        public void Open_ShowsTheRulesPicked()
        {
            panel.Open(new HouseRules(MatchLength.Long, false, true));

            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual("90S", panel.ValueOf(panel.RowOf(Rule.WaveLength)));
            Assert.AreEqual("OFF", panel.ValueOf(panel.RowOf(Rule.Steals)));
            Assert.AreEqual("ON", panel.ValueOf(panel.RowOf(Rule.GoldenOrder)));
            Assert.AreEqual("100%", panel.ValueOf(panel.RowOf(Rule.Speed)));
            Assert.AreEqual("3", panel.ValueOf(panel.RowOf(Rule.Waves)));
        }

        [Test]
        public void Standard_ShowsItsRowsInOrder_WithoutModeOrRoundTimer()
        {
            GameModes.Playable = new[] { GameMode.Standard };
            panel.Open(HouseRules.Normal);

            string[] names = { "WAVE LENGTH", "WAVES", "STEALS", "GOLDEN ORDER", "BOTS", "SPEED", "LOW GRAVITY" };
            for (int i = 0; i < names.Length; i++)
                Assert.AreEqual(names[i], panel.NameOf(i));
            Assert.AreEqual(-1, panel.ModeRow, "no Mode row with one mode to play");
            Assert.AreEqual(-1, panel.RowOf(Rule.RoundTimer));
        }

        [Test]
        public void Step_ChangesTheRule_AndKeepsIt()
        {
            panel.Open(HouseRules.Normal);

            Select(panel.RowOf(Rule.WaveLength));
            panel.Step(false);

            Assert.AreEqual(MatchLength.Short, HouseRules.Current.Length);
            Assert.AreEqual(MatchLength.Short, kept.Length);
            Assert.AreEqual(1, keeps);
        }

        [Test]
        public void Move_ThenStep_TheNextRules()
        {
            panel.Open(HouseRules.Normal);

            Select(panel.RowOf(Rule.Steals));
            panel.Step(false);
            panel.Move(true);
            panel.Step(false);

            Assert.AreEqual(new HouseRules(MatchLength.Normal, false, false), HouseRules.Current);
        }

        [Test]
        public void EveryMutatorRow_Steps()
        {
            panel.Open(HouseRules.Normal);

            Select(panel.RowOf(Rule.Waves));
            panel.Step(true);
            Select(panel.RowOf(Rule.Speed) - panel.RowOf(Rule.Waves));
            panel.Step(true);
            panel.Move(true);
            panel.Step(true);

            Assert.AreEqual(4, HouseRules.Current.Waves);
            Assert.AreEqual(125, HouseRules.Current.SpeedPercent);
            Assert.IsTrue(HouseRules.Current.LowGravity);
        }

        [Test]
        public void GoldenOrderOff_SaysWhichAchievementsCantBeEarned()
        {
            panel.Open(HouseRules.Normal);
            StringAssert.DoesNotContain("GOLDEN WIN", panel.Note);

            Select(panel.RowOf(Rule.GoldenOrder));
            panel.Step(false);

            StringAssert.Contains("GOLDEN WIN", panel.Note);
        }

        [Test]
        public void Bots_OnByDefault_AndStepOff()
        {
            panel.Open(HouseRules.Normal);
            Assert.AreEqual("ON", panel.ValueOf(panel.RowOf(Rule.Bots)));

            Select(panel.RowOf(Rule.Bots));
            panel.Step(false);

            Assert.IsFalse(HouseRules.Current.Bots);
            Assert.AreEqual("NO BOTS", HouseRules.Current.Summary);
        }

        [Test]
        public void TheNote_DescribesTheModePicked()
        {
            foreach (GameMode mode in GameModes.Playable)
            {
                panel.Open(new HouseRules(MatchLength.Normal, true, true, true, mode));
                Assert.AreEqual(Loc.Get(GameModes.DescriptionKey(mode)), panel.Note);
                Assert.AreNotEqual(GameModes.DescriptionKey(mode), panel.Note, "the text exists");
            }
        }

        [Test]
        public void ModeRow_LoopsPastTheLastMode_AndBack()
        {
            GameModes.Playable = new[] { GameMode.Standard, GameMode.KingsCrown, GameMode.HotPotato };
            panel.Open(new HouseRules(MatchLength.Normal, true, true, true, GameMode.HotPotato));

            panel.Step(true);
            Assert.AreEqual(GameMode.Standard, HouseRules.Current.Mode, "right from the last");

            panel.Step(false);
            Assert.AreEqual(GameMode.HotPotato, HouseRules.Current.Mode, "left from the first");
        }

        [Test]
        public void InAPublicLobby_SaysTheNormalRulesPlay()
        {
            panel.Open(new HouseRules(MatchLength.Short, true, true), true);

            StringAssert.Contains("NORMAL RULES", panel.Note);
        }

        [Test]
        public void GoldRush_StealsAreLocked_AndDontStep()
        {
            GameModes.Playable = new[] { GameMode.Standard, GameMode.GoldRush };
            panel.Open(new HouseRules(MatchLength.Normal, false, true, true, GameMode.GoldRush));

            int row = panel.RowOf(Rule.Steals);
            Assert.AreEqual("ON (LOCKED)", panel.ValueOf(row));
            Select(row);
            panel.Step(true);
            panel.Step(false);

            Assert.AreEqual(0, keeps, "a locked row doesn't change");
        }

        [Test]
        public void ModeRow_IsFirst_AndChangingModeChangesTheRows()
        {
            GameModes.Playable = new[] { GameMode.Standard, GameMode.KingsCrown };
            panel.Open(HouseRules.Normal);
            Assert.AreEqual(0, panel.ModeRow);
            Assert.AreEqual("STANDARD", panel.ValueOf(0));

            panel.Step(true);

            Assert.AreEqual(GameMode.KingsCrown, HouseRules.Current.Mode);
            Assert.AreEqual("KINGS CROWN", panel.ValueOf(0));
            Assert.AreEqual(-1, panel.RowOf(Rule.Waves));
            Assert.AreEqual(-1, panel.RowOf(Rule.GoldenOrder));
            Assert.AreNotEqual(-1, panel.RowOf(Rule.RoundTimer));
            Assert.AreEqual(Loc.Get(GameModes.DescriptionKey(GameMode.KingsCrown)), panel.Note);
        }

        [Test]
        public void ChangingMode_KeepsHiddenRulesForLater()
        {
            GameModes.Playable = new[] { GameMode.Standard, GameMode.KingsCrown };
            panel.Open(new HouseRules(MatchLength.Normal, true, true, true, GameMode.Standard, 5));

            panel.Step(true);
            panel.Step(false);

            Assert.AreEqual(5, HouseRules.Current.Waves);
            Assert.AreEqual("5", panel.ValueOf(panel.RowOf(Rule.Waves)));
        }

        [Test]
        public void ChangingMode_KeepsTheSelectionOnTheModeRow_AndMovesRoundTheNewRows()
        {
            GameModes.Playable = new[] { GameMode.Standard, GameMode.Infection };
            panel.Open(InMode(GameMode.Standard));

            panel.Step(true); // to Infection
            Assert.AreEqual("INFECTION", panel.ValueOf(panel.ModeRow));
            panel.Move(false); // round to the last row: Low Gravity
            panel.Step(true);

            Assert.IsTrue(HouseRules.Current.LowGravity);
            Assert.AreEqual(-1, panel.RowOf(Rule.Steals), "Infection has no steals");
        }

        [Test]
        public void Close_HidesIt()
        {
            panel.Open(HouseRules.Normal);

            panel.Close();

            Assert.IsFalse(panel.IsOpen);
            Assert.IsFalse(panel.gameObject.activeSelf);
        }

        [Test]
        public void Closed_StepsDoNothing()
        {
            panel.Open(HouseRules.Normal);
            panel.Close();

            panel.Step(false);

            Assert.AreEqual(HouseRules.Normal, HouseRules.Current);
            Assert.AreEqual(0, keeps);
        }
    }
}

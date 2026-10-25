using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// MATCH RULES (RulesPanel): Start in player select opens it on the rules picked, up / down / left / right change
    /// them (each change is the host's rules at once, and kept), A or B close it, and the Golden Order off says which
    /// achievements can't be earned
    /// </summary>
    public class RulesPanelTests
    {
        readonly TestObjects objects = new TestObjects();
        RulesPanel panel;
        static HouseRules kept;
        static int keeps;

        static void Keep(HouseRules rules) { kept = rules; keeps++; }

        [SetUp]
        public void SetUp()
        {
            kept = HouseRules.Normal;
            keeps = 0;
            panel = RulesPanel.Build(objects.NewGameObject("Canvas").AddComponent<UnityEngine.RectTransform>());
            panel.Kept = Keep;
        }

        [TearDown]
        public void TearDown()
        {
            HouseRules.Current = HouseRules.Normal;
            objects.DestroyAll();
        }

        [Test]
        public void Open_ShowsTheRulesPicked()
        {
            panel.Open(new HouseRules(MatchLength.Long, false, true));

            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual("LONG", panel.ValueOf(0));
            Assert.AreEqual("OFF", panel.ValueOf(1));
            Assert.AreEqual("ON", panel.ValueOf(2));
        }

        [Test]
        public void Step_ChangesTheRule_AndKeepsIt()
        {
            panel.Open(HouseRules.Normal);

            panel.Step(false);

            Assert.AreEqual(MatchLength.Short, HouseRules.Current.Length);
            Assert.AreEqual(MatchLength.Short, kept.Length);
            Assert.AreEqual(1, keeps);
        }

        [Test]
        public void Move_ThenStep_TheNextRule()
        {
            panel.Open(HouseRules.Normal);

            panel.Move(true);
            panel.Step(false);
            panel.Move(true);
            panel.Step(false);

            Assert.AreEqual(new HouseRules(MatchLength.Normal, false, false), HouseRules.Current);
        }

        [Test]
        public void GoldenOrderOff_SaysWhichAchievementsCantBeEarned()
        {
            panel.Open(HouseRules.Normal);
            Assert.AreEqual("", panel.Note);

            panel.Move(false); // round to the last row: Golden Order
            panel.Step(false);

            StringAssert.Contains("GOLDEN WIN", panel.Note);
        }

        [Test]
        public void InAPublicLobby_SaysTheNormalRulesPlay()
        {
            panel.Open(new HouseRules(MatchLength.Short, true, true), true);

            StringAssert.Contains("NORMAL RULES", panel.Note);
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

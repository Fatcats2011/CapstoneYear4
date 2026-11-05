using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The modes' playtest round: the fuse's countdown, what the board's money column shows, the crown's shape, who an out
    /// player watches, the info box's five seconds, wide screens, and the bots slowing for corners and backing out
    /// </summary>
    public class ModePlaytestTests
    {
        [Test]
        public void FuseSeconds_CountDownInWholeSeconds_ToZero()
        {
            Assert.AreEqual(5, TimedModeRules.FuseSeconds(10f, 14.2f));
            Assert.AreEqual(1, TimedModeRules.FuseSeconds(10f, 10.1f));
            Assert.AreEqual(0, TimedModeRules.FuseSeconds(10f, 10f));
            Assert.AreEqual(0, TimedModeRules.FuseSeconds(10f, 9f), "never below zero");
        }

        [Test]
        public void HotPotato_HasNoRoundTimer_ItPlaysToTheLastPlayer()
        {
            Assert.AreEqual(RuleShow.Hidden, ModeRules.For(GameMode.HotPotato, Rule.RoundTimer));
            Assert.AreEqual(RuleShow.Shown, ModeRules.For(GameMode.KingsCrown, Rule.RoundTimer));
            Assert.AreEqual(RuleShow.Shown, ModeRules.For(GameMode.Infection, Rule.RoundTimer));
        }

        [Test]
        public void ScoreText_MoneyOnlyWhereMoneyCounts()
        {
            Assert.AreEqual("$120", Scoreboard.ScoreText(GameMode.Standard, 120));
            Assert.AreEqual("$120", Scoreboard.ScoreText(GameMode.GoldRush, 120));
            Assert.AreEqual("", Scoreboard.ScoreText(GameMode.Infection, 120));
            Assert.AreEqual("", Scoreboard.ScoreText(GameMode.HotPotato, 120));
            Assert.AreEqual("1:15", Scoreboard.ScoreText(GameMode.KingsCrown, 750), "seconds held: 10 points a second");
        }

        [Test]
        public void Crown_HasItsPoints_AndAClosedMesh()
        {
            Assert.AreEqual(ModeVisuals.CROWN_TIP, ModeVisuals.CrownTop(0f), 0.001f, "a point");
            Assert.AreEqual(ModeVisuals.CROWN_BAND, ModeVisuals.CrownTop(0.5f / ModeVisuals.CROWN_POINTS), 0.001f, "the dip between two");
            Assert.AreEqual(ModeVisuals.CROWN_TIP, ModeVisuals.CrownTop(1f / ModeVisuals.CROWN_POINTS), 0.001f, "the next point");

            Mesh mesh = ModeVisuals.CrownMesh();
            try
            {
                Assert.Greater(mesh.vertexCount, 0);
                Assert.AreEqual(ModeVisuals.CROWN_TIP, mesh.bounds.max.y, 0.01f);
                Assert.AreEqual(0f, mesh.bounds.min.y, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void Icons_HaveTheirShapes()
        {
            Assert.Less(ModeIcons.CrownDistance(0f, -0.3f), 0f, "the crown's band");
            Assert.Greater(ModeIcons.CrownDistance(0f, 0.9f), 0f, "above its points");
            Assert.Less(ModeIcons.GhostDistance(0f, 0f), 0f);
            Assert.Greater(ModeIcons.GhostDistance(0.95f, 0.95f), 0f);
            Assert.Less(ModeIcons.CrossDistance(0.5f, 0.5f), 0f, "on a stroke");
            Assert.Less(ModeIcons.CrossDistance(0.5f, -0.5f), 0f, "on the other stroke");
            Assert.Greater(ModeIcons.CrossDistance(0.6f, 0f), 0f, "between them");
        }

        [Test]
        public void NextIn_SkipsThoseOut_AndComesRound()
        {
            const int players = 0b1111;
            Assert.AreEqual(2, ModeVisuals.NextIn(players, 0b0010, 0, seat => true), "seat 1 is out");
            Assert.AreEqual(0, ModeVisuals.NextIn(players, 0b0010, 3, seat => true), "round to the first");
            Assert.AreEqual(3, ModeVisuals.NextIn(players, 0b0111, 3, seat => true), "only the one left");
            Assert.AreEqual(ModeMatch.NONE, ModeVisuals.NextIn(players, 0b1111, 0, seat => true));
            Assert.AreEqual(1, ModeVisuals.NextIn(players, 0, -1, seat => seat != 0), "seat 0 has no scooter yet");
        }

        [Test]
        public void InfoBox_IsGoneAfterFiveSeconds_AndOnlyForTheNewModes()
        {
            Assert.AreEqual(5f, ModeInfoBox.SECONDS);
            Assert.AreEqual(0f, ModeInfoBox.AlphaAt(-0.1f));
            Assert.AreEqual(1f, ModeInfoBox.AlphaAt(2.5f), "solid while it holds");
            Assert.AreEqual(0f, ModeInfoBox.AlphaAt(5f));
            Assert.Less(ModeInfoBox.AlphaAt(4.9f), 1f, "fading by the end");

            Assert.IsFalse(ModeInfoBox.HasInstructions(GameMode.Standard));
            foreach (GameMode mode in new[] { GameMode.GoldRush, GameMode.KingsCrown, GameMode.Infection, GameMode.HotPotato })
            {
                Assert.IsTrue(ModeInfoBox.HasInstructions(mode));
                Assert.AreNotEqual(GameModes.InstructionKey(mode), Loc.Get(GameModes.InstructionKey(mode)), mode + " has words");
            }
        }

        [Test]
        public void WideScreens_FitTheHeight_OnlyWiderThanSixteenNine()
        {
            Assert.IsTrue(WideScreens.FitsHeight(3440, 1440), "21:9");
            Assert.IsTrue(WideScreens.FitsHeight(5120, 1440), "32:9");
            Assert.IsFalse(WideScreens.FitsHeight(1920, 1080), "16:9");
            Assert.IsFalse(WideScreens.FitsHeight(2560, 1440), "16:9");
            Assert.IsFalse(WideScreens.FitsHeight(1280, 800), "16:10");
            Assert.IsFalse(WideScreens.FitsHeight(1920, 0));
        }

        [Test]
        public void SpeedFor_SlowsOnlyForSharpCorners_MoreTheCloserTheyAre()
        {
            Assert.AreEqual(float.PositiveInfinity, BotBrain.SpeedFor(5f, 10f), "a gentle bend");
            Assert.Less(BotBrain.SpeedFor(5f, 90f), BotBrain.SpeedFor(40f, 90f), "room to slow");
            Assert.Less(BotBrain.SpeedFor(5f, 120f), BotBrain.SpeedFor(5f, 40f), "sharper is slower");
            Assert.AreEqual(BotBrain.SpeedFor(5f, 90f), BotBrain.SpeedFor(5f, -90f), "either way");
        }

        [Test]
        public void Pedals_HoldTheLimit()
        {
            BotBrain.Pedals(10f, 20f, 0f, out float accelerate, out float brake);
            Assert.AreEqual(1f, accelerate);
            Assert.AreEqual(0f, brake);

            BotBrain.Pedals(21f, 20f, 0f, out accelerate, out brake);
            Assert.AreEqual(0f, accelerate, "lifts");
            Assert.AreEqual(0f, brake);

            BotBrain.Pedals(30f, 20f, 0f, out accelerate, out brake);
            Assert.AreEqual(0f, accelerate);
            Assert.AreEqual(1f, brake, "brakes");
        }

        [Test]
        public void Stuck_AgainAtOnce_BacksFurther_AndSaysSo()
        {
            BotStuck stuck = new BotStuck();
            stuck.Tick(Vector3.zero, 0.1f);
            int ticks = 0;
            while (!stuck.Reversing && ticks++ < 100)
                stuck.Tick(Vector3.zero, 0.1f);
            Assert.AreEqual(1, stuck.Repeats);

            for (int i = 0; i < 11; i++)
                stuck.Tick(Vector3.zero, 0.1f);
            Assert.IsFalse(stuck.Reversing, "a second back");

            ticks = 0;
            while (!stuck.Reversing && ticks++ < 100)
                stuck.Tick(Vector3.zero, 0.1f);
            Assert.AreEqual(2, stuck.Repeats, "the same spot");

            for (int i = 0; i < 11; i++)
                stuck.Tick(Vector3.zero, 0.1f);
            Assert.IsTrue(stuck.Reversing, "further this time");

            // Driving free a while: a fresh start
            for (int i = 0; i < 100; i++)
                stuck.Tick(new Vector3(i * 2f, 0f, 0f), 0.1f);
            Assert.AreEqual(0, stuck.Repeats);
        }

        [Test]
        public void OptionSetting_Wraps_OnlyWhenAsked()
        {
            OptionSetting stops = new OptionSetting("x", new[] { 0, 1 }, c => c.ToString(), 1);
            Assert.IsFalse(stops.Step(true));

            OptionSetting loops = new OptionSetting("x", new[] { 0, 1 }, c => c.ToString(), 1) { Wraps = true };
            Assert.IsTrue(loops.Step(true));
            Assert.AreEqual(0, loops.Value);
            Assert.IsTrue(loops.Step(false));
            Assert.AreEqual(1, loops.Value);
        }
    }
}

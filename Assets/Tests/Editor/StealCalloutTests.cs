using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A player's steal callout (StealCallout): one small line in their own view, replaced by the next, in its tone's
    /// colour, never read as rich text; the line each side of a bump gets; the camera kick's fade; and the six lines in
    /// every language
    /// </summary>
    public class StealCalloutTests
    {
        readonly TestObjects objects = new TestObjects();

        [SetUp]
        public void SetUp()
        {
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            Comfort.ReduceMotion = false;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        // A player in the next free seat with a HUD canvas of their own
        (OrderHandler handler, NumberHandler hud) NewPlayer(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            GameObject canvas = new GameObject("Driving Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(player.transform);
            return (handler, canvas.AddComponent<NumberHandler>());
        }

        [Test]
        public void ANewLine_ReplacesTheOld()
        {
            var player = NewPlayer("Player 1");
            StealCallout callout = StealCallout.AddTo(player.handler, player.hud);

            callout.Show("A", CalloutTone.Thief);
            callout.Show("B", CalloutTone.Victim);

            Assert.AreEqual("B", callout.ShownWords);
            Assert.AreEqual(1, player.hud.transform.root.GetComponentsInChildren<TextMeshProUGUI>(true).Length, "one line, never two");
        }

        [Test]
        public void TheLine_IsItsTonesColour()
        {
            var player = NewPlayer("Player 1");
            StealCallout callout = StealCallout.AddTo(player.handler, player.hud);
            TextMeshProUGUI line = player.hud.transform.root.GetComponentInChildren<TextMeshProUGUI>(true);

            callout.Show("A", CalloutTone.Thief);
            Assert.AreEqual(GameCard.GOLD, line.color);
            callout.Show("B", CalloutTone.Victim);
            Assert.AreEqual(ModeIcons.RED, line.color);
            callout.Show("C", CalloutTone.Plain);
            Assert.AreEqual(Color.white, line.color);
        }

        [Test]
        public void TheLine_IsSmallAndNeverRichText()
        {
            var player = NewPlayer("Player 1");
            StealCallout.AddTo(player.handler, player.hud);
            TextMeshProUGUI line = player.hud.transform.root.GetComponentInChildren<TextMeshProUGUI>(true);

            Assert.IsFalse(line.richText, "a name with tags in it is drawn as it is");
            Assert.IsFalse(line.enableWordWrapping, "one line");
            Assert.LessOrEqual(line.fontSize, 28f);
            Assert.LessOrEqual(line.rectTransform.sizeDelta.x, 300f);
        }

        [Test]
        public void APlayerWithoutAHud_HasNoCallout()
        {
            var player = NewPlayer("Player 1");

            Assert.IsNull(StealCallout.AddTo(player.handler, null));
        }

        [Test]
        public void ASteal_TellsTheThiefAndTheVictim_AndNobodyElse()
        {
            var thief = NewPlayer("Player 1");
            var victim = NewPlayer("Player 2");
            var other = NewPlayer("Player 3");
            StealCallout thiefs = StealCallout.AddTo(thief.handler, thief.hud);
            StealCallout victims = StealCallout.AddTo(victim.handler, victim.hud);
            StealCallout others = StealCallout.AddTo(other.handler, other.hud);
            BumpNotice steal = new BumpNotice(0, 1, BumpOutcome.Stolen, 7);

            thiefs.Hear(steal);
            victims.Hear(steal);
            others.Hear(steal);

            StringAssert.Contains("+$7", thiefs.ShownWords);
            StringAssert.Contains(StealFeedbackRules.Short(Scoreboard.ShownName(1)), thiefs.ShownWords);
            StringAssert.Contains(StealFeedbackRules.Short(Scoreboard.ShownName(0)), victims.ShownWords);
            Assert.AreEqual("", others.ShownWords);
        }

        [Test]
        public void ABumpThatTookNothing_TellsOnlyTheAttacker()
        {
            var attacker = NewPlayer("Player 1");
            var victim = NewPlayer("Player 2");
            StealCallout attackers = StealCallout.AddTo(attacker.handler, attacker.hud);
            StealCallout victims = StealCallout.AddTo(victim.handler, victim.hud);
            BumpNotice bump = new BumpNotice(0, 1, BumpOutcome.SlotsFull, 0);

            attackers.Hear(bump);
            victims.Hear(bump);

            Assert.AreEqual(Loc.Get("steal.full"), attackers.ShownWords);
            Assert.AreEqual("", victims.ShownWords);
        }

        [Test]
        public void AModesHit_ShowsNothing()
        {
            var attacker = NewPlayer("Player 1");
            NewPlayer("Player 2");
            StealCallout callout = StealCallout.AddTo(attacker.handler, attacker.hud);

            callout.Hear(new BumpNotice(0, 1, BumpOutcome.ModeHit, 0));

            Assert.AreEqual("", callout.ShownWords);
        }

        [Test]
        public void TheKick_IsOffWithReduceMotion()
        {
            Assert.AreEqual(6f, Comfort.Kick(6f));
            Comfort.ReduceMotion = true;
            Assert.AreEqual(0f, Comfort.Kick(6f));
        }

        [Test]
        public void TheKick_EasesBackToNothing()
        {
            Assert.AreEqual(6f, OrbitalCamera.KickAt(6f, 0f), 0.001f);
            Assert.AreEqual(3f, OrbitalCamera.KickAt(6f, OrbitalCamera.KICK_SECONDS / 2f), 0.001f);
            Assert.AreEqual(0f, OrbitalCamera.KickAt(6f, OrbitalCamera.KICK_SECONDS), 0.001f);
            Assert.AreEqual(0f, OrbitalCamera.KickAt(6f, 10f), 0.001f);
            Assert.AreEqual(0f, OrbitalCamera.KickAt(6f, -1f), 0.001f);
        }

        [TestCase("steal.stole_paid")]
        [TestCase("steal.stole")]
        [TestCase("steal.robbed")]
        [TestCase("steal.full")]
        [TestCase("steal.nothing")]
        [TestCase("steal.off")]
        public void EveryLine_IsInEveryLanguage(string key)
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
                Assert.IsNotNull(csv.Get(key, locale), key + " " + locale);
        }

        [Test]
        public void TheThiefsLine_InEnglish()
        {
            StringsCsv csv = StringsCsv.Load();

            Assert.AreEqual("STOLE FROM Ryan +$7", string.Format(csv.Get("steal.stole_paid", "en"), "Ryan", 7));
            Assert.AreEqual("Ryan TOOK YOUR ORDER", string.Format(csv.Get("steal.robbed", "en"), "Ryan"));
        }
    }
}

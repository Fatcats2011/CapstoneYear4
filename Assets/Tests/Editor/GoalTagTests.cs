using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A player's goal tag (GoalTag): nothing until a goal is seen, the goal's words for 25 seconds from when it appears
    /// (or from when the mode's info box is gone), the same goal not restarting them, a new goal starting them again,
    /// nothing while paused, and the one small line of text it is made of. Edit mode: nothing starts, so the tests call
    /// See and Alpha directly with a clock they move by hand
    /// </summary>
    public class GoalTagTests
    {
        readonly TestObjects objects = new TestObjects();
        float now;

        [SetUp]
        public void SetUp()
        {
            now = 10f;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        float Now()
        {
            return now;
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

        GoalTag NewTag(out NumberHandler hud)
        {
            var player = NewPlayer("Player 1");
            hud = player.hud;
            GoalTag tag = GoalTag.AddTo(player.handler, player.hud);
            tag.Clock = Now;
            return tag;
        }

        [Test]
        public void AddTo_WithoutAHud_IsNull()
        {
            var player = NewPlayer("Player 1");

            Assert.IsNull(GoalTag.AddTo(player.handler, null));
        }

        [Test]
        public void ANewTag_ShowsNothing()
        {
            GoalTag tag = NewTag(out NumberHandler hud);

            Assert.AreEqual("", tag.ShownWords);
            Assert.AreEqual(ModeRole.None, tag.Shown);
            Assert.AreEqual(0f, tag.Alpha(false));
        }

        [Test]
        public void AGoal_ShowsItsWordsForTwentyFiveSeconds()
        {
            GoalTag tag = NewTag(out NumberHandler hud);

            tag.See(ModeRole.GetCrown, 0f);

            Assert.AreEqual(Loc.Get("mode.goal.getcrown"), tag.ShownWords);
            Assert.AreEqual(ModeRole.GetCrown, tag.Shown);
            now = 11f;
            Assert.AreEqual(1f, tag.Alpha(false), 0.001f);
            now = 35.1f;
            Assert.AreEqual(0f, tag.Alpha(false));
        }

        [Test]
        public void TheSameGoalAgain_DoesNotRestartIt()
        {
            GoalTag tag = NewTag(out NumberHandler hud);
            tag.See(ModeRole.GetCrown, 0f);

            now = 20f;
            tag.See(ModeRole.GetCrown, 0f);

            now = 35.1f;
            Assert.AreEqual(0f, tag.Alpha(false));
        }

        [Test]
        public void ADifferentGoal_ShowsItsWordsAndStartsAgain()
        {
            GoalTag tag = NewTag(out NumberHandler hud);
            tag.See(ModeRole.GetCrown, 0f);

            now = 30f;
            tag.See(ModeRole.KeepCrown, 0f);

            Assert.AreEqual(Loc.Get("mode.goal.keepcrown"), tag.ShownWords);
            now = 54f;
            Assert.AreEqual(1f, tag.Alpha(false), 0.001f);
            now = 55.1f;
            Assert.AreEqual(0f, tag.Alpha(false));
        }

        [Test]
        public void NoGoal_ClearsTheTag()
        {
            GoalTag tag = NewTag(out NumberHandler hud);
            tag.See(ModeRole.PassIt, 0f);

            tag.See(ModeRole.None, 0f);

            Assert.AreEqual("", tag.ShownWords);
            Assert.AreEqual(ModeRole.None, tag.Shown);
            Assert.AreEqual(0f, tag.Alpha(false));
        }

        [Test]
        public void AGoalSeenBeforeTheInfoBoxIsGone_WaitsForIt()
        {
            GoalTag tag = NewTag(out NumberHandler hud);

            now = 40f;
            tag.See(ModeRole.PassIt, 50f);

            now = 45f;
            Assert.AreEqual(0f, tag.Alpha(false));
            now = 51f;
            Assert.AreEqual(1f, tag.Alpha(false), 0.001f);
        }

        [Test]
        public void WhilePaused_TheTagIsHidden()
        {
            GoalTag tag = NewTag(out NumberHandler hud);
            tag.See(ModeRole.StayAway, 0f);

            now = 15f;
            Assert.AreEqual(1f, tag.Alpha(false), 0.001f);
            Assert.AreEqual(0f, tag.Alpha(true));
        }

        [Test]
        public void TheTag_IsOneSmallLineOfPlainText()
        {
            GoalTag tag = NewTag(out NumberHandler hud);

            TextMeshProUGUI text = hud.GetComponentInParent<Canvas>().rootCanvas.GetComponentInChildren<TextMeshProUGUI>(true);

            Assert.IsNotNull(text);
            Assert.IsFalse(text.richText);
            Assert.IsFalse(text.enableWordWrapping);
            Assert.LessOrEqual(text.fontSize, 26f);
            Assert.LessOrEqual(text.rectTransform.sizeDelta.x, 320f);
            Assert.IsFalse(text.raycastTarget);
        }
    }
}

using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// What a player's callout says about a mode (StealCallout.HearMode, HearWarning): a line for the seat it is
    /// addressed to, a line for everyone but the player it names, the name and colour it gives, and the 30-second warning
    /// on every callout. Edit mode: nothing starts, so the callouts hear lines directly
    /// </summary>
    public class ModeCalloutTests
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

        TextMeshProUGUI lastLine;

        StealCallout NewCallout(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            GameObject canvas = new GameObject("Driving Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(player.transform);
            StealCallout callout = StealCallout.AddTo(handler, canvas.AddComponent<NumberHandler>());
            callout.Clock = Now;
            lastLine = canvas.transform.root.GetComponentInChildren<TextMeshProUGUI>(true);
            return callout;
        }

        [Test]
        public void ALineForASeat_ShowsForThatSeatOnly()
        {
            StealCallout first = NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");

            first.HearMode(new ModeLine(0, ModeLineKind.CrownYours, ModeMatch.NONE));
            second.HearMode(new ModeLine(0, ModeLineKind.CrownYours, ModeMatch.NONE));

            Assert.AreEqual(Loc.Get("mode.line.crown.yours"), first.ShownWords);
            Assert.AreEqual("", second.ShownWords);
        }

        [Test]
        public void ALineThatNamesAPlayer_ShowsTheirShortName_InRed()
        {
            NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");
            TextMeshProUGUI line = lastLine;

            second.HearMode(new ModeLine(1, ModeLineKind.CrownTaken, 0));

            Assert.AreEqual(Loc.Get("mode.line.crown.taken", StealFeedbackRules.Short(Scoreboard.ShownName(0))), second.ShownWords);
            Assert.AreEqual(ModeIcons.RED, line.color);
        }

        [Test]
        public void ALineForEveryoneElse_ShowsOnTheOthersAndNotOnThePlayerItNames()
        {
            StealCallout first = NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");
            StealCallout third = NewCallout("Player 3");

            ModeLine line = new ModeLine(ModeLine.EVERYONE_ELSE, ModeLineKind.PlayerOut, 2);
            first.HearMode(line);
            second.HearMode(line);
            third.HearMode(line);

            string words = Loc.Get("mode.line.out", StealFeedbackRules.Short(Scoreboard.ShownName(2)));
            Assert.AreEqual(words, first.ShownWords);
            Assert.AreEqual(words, second.ShownWords);
            Assert.AreEqual("", third.ShownWords);
        }

        [Test]
        public void TheWarning_ShowsOnEveryCallout()
        {
            StealCallout first = NewCallout("Player 1");
            StealCallout second = NewCallout("Player 2");

            first.HearWarning();
            second.HearWarning();

            Assert.AreEqual(Loc.Get("mode.line.thirty"), first.ShownWords);
            Assert.AreEqual(Loc.Get("mode.line.thirty"), second.ShownWords);
        }
    }
}

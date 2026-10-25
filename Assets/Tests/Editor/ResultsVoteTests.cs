using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The results screen's rematch vote (ResultsVote): each player votes with A, leaves or takes the vote back with B,
    /// and the outcome rematches through the scene flow once, or goes back to the lobby. Locally the seats that didn't
    /// vote are dropped; an online client sends its vote to the host instead of deciding
    /// </summary>
    public class ResultsVoteTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<int> dropped = new List<int>();
        FakeSceneFlow flow;
        ResultsVote vote;
        double now;

        static int sentSeat = -1;
        static bool sentRematch;

        [SetUp]
        public void SetUp()
        {
            flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            now = 100;
            vote = objects.Add<ResultsVote>();
            vote.Clock = Now;
            vote.DropSeat = dropped.Add;
            vote.SeatStillThere = Everyone;
            sentSeat = -1;
        }

        [TearDown]
        public void TearDown()
        {
            SceneFlow.Current = null;
            RematchSync.Reset();
            PlayerInstantiate.instance = null;
            ScoreManager.instance = null;
            dropped.Clear();
            objects.DestroyAll();
        }

        double Now() { return now; }

        static bool Everyone(int seat) { return true; }

        static void Sent(int seat, bool rematch)
        {
            sentSeat = seat;
            sentRematch = rematch;
        }

        [Test]
        public void Press_EveryoneSouth_RematchesOnce()
        {
            vote.Open(new[] { 0, 1 }, -1, true);
            vote.Press(0, true);
            vote.Press(1, true);

            vote.Tick(now);
            Assert.AreEqual(1, flow.Rematches);
            vote.Tick(now + 1);
            Assert.AreEqual(1, flow.Rematches, "once");
            Assert.AreEqual(0, flow.MenuReturns);
            CollectionAssert.IsEmpty(dropped);
        }

        [Test]
        public void Tick_TimeRunsOut_DropsTheSeatsThatDidntVote()
        {
            vote.Open(new[] { 0, 1 }, -1, true);
            vote.Press(0, true);

            vote.Tick(now + RematchVote.SECONDS);

            Assert.AreEqual(1, flow.Rematches);
            CollectionAssert.AreEqual(new[] { 1 }, dropped);
        }

        [Test]
        public void Press_EastByEveryone_ReturnsToTheLobby()
        {
            vote.Open(new[] { 0, 1 }, -1, true);
            vote.Press(0, false);
            vote.Press(1, false);

            vote.Tick(now);

            Assert.AreEqual(1, flow.MenuReturns);
            Assert.AreEqual(0, flow.Rematches);
        }

        [Test]
        public void Press_EastAfterSouth_TakesTheVoteBack()
        {
            vote.Open(new[] { 0 }, -1, true);
            vote.Press(0, true);
            vote.Press(0, false);

            vote.Tick(now + 1);
            Assert.AreEqual(0, flow.Rematches + flow.MenuReturns, "still in, still waiting");

            vote.Tick(now + RematchVote.SECONDS);
            Assert.AreEqual(1, flow.MenuReturns, "nobody voted");
        }

        [Test]
        public void Press_BeforeOpen_DoesNothing()
        {
            vote.Press(0, true);
            vote.Tick(now);

            Assert.IsNull(ResultsVote.Current);
            Assert.AreEqual(0, flow.Rematches + flow.MenuReturns);
        }

        [Test]
        public void Press_AfterTheOutcome_DoesNothing()
        {
            vote.Open(new[] { 0 }, -1, true);
            vote.Press(0, true);
            vote.Tick(now);

            vote.Press(0, false);
            vote.Tick(now + 1);

            Assert.AreEqual(1, flow.Rematches);
            Assert.AreEqual(0, flow.MenuReturns);
        }

        [Test]
        public void Press_OnAClient_SendsTheVoteToTheHost()
        {
            RematchSync.VoteSent += Sent;
            vote.Open(new[] { 0, 1 }, 0, false);

            vote.Press(1, true);
            vote.Tick(now + RematchVote.SECONDS);

            Assert.AreEqual(1, sentSeat);
            Assert.IsTrue(sentRematch);
            Assert.AreEqual(0, flow.Rematches + flow.MenuReturns, "the host decides");
        }

        [Test]
        public void Tick_OnTheHost_ASeatGoneFromTheRoster_LeavesTheVote()
        {
            vote.SeatStillThere = seat => seat != 1;
            vote.Open(new[] { 0, 1 }, 0, true);
            vote.Press(0, true);

            vote.Tick(now);

            Assert.AreEqual(1, flow.Rematches, "nobody waits for a machine that left");
        }

        [Test]
        public void Open_Again_StartsAFreshVote()
        {
            vote.Open(new[] { 0 }, -1, true);
            vote.Press(0, true);
            vote.Tick(now);

            vote.Open(new[] { 0 }, -1, true);
            vote.Press(0, true);
            vote.Tick(now);

            Assert.AreEqual(2, flow.Rematches);
        }

        [Test]
        public void View_MarksFollowTheSeat_NotThePlacing()
        {
            // The scores are in finishing order: Player 2 won, so the first score is seat 1's
            TMP_Text winner = objects.Add<TextMeshProUGUI>();
            TMP_Text second = objects.Add<TextMeshProUGUI>();
            ResultsVoteView view = ResultsVoteView.Build(objects.NewGameObject("Canvas").transform, new[] { winner, second });
            view.ShowSeats(new[] { 1, 0 });
            RematchVote votes = new RematchVote(new[] { 0, 1 }, 0);
            votes.Vote(0);

            view.Show(RematchBoard.From(votes, 0));

            Assert.AreEqual("READY", Mark(second), "Player 1 voted: under Player 1's score");
            Assert.AreEqual("", Mark(winner));
        }

        static string Mark(TMP_Text score)
        {
            return score.transform.Find("Rematch Mark").GetComponent<TMP_Text>().text;
        }

        [Test]
        public void ResultsButtons_HoldTheConfirmAndBackArt()
        {
            ButtonArtSet art = ButtonArtSet.Results();

            Assert.IsNotNull(art);
            Assert.AreEqual("MenUI_Buttons_7", art.confirmXbox.name);
            Assert.AreEqual("PS_Cross", art.confirmPlayStation.name);
            Assert.AreEqual("Key_Space", art.confirmKeyboard.name);
            Assert.AreEqual("MenUI_Buttons_4", art.backXbox.name);
            Assert.AreEqual("PS_Circle", art.backPlayStation.name);
            Assert.AreEqual("Key_Backspace", art.backKeyboard.name);
        }

        [Test]
        public void DropSeat_TheHostPlayer_PassesTheRoleOn()
        {
            PlayerInstantiate players = objects.Add<PlayerInstantiate>();
            PlayerInstantiate.instance = players;
            ScoreManager.instance = objects.Add<ScoreManager>();
            MenuInteractions firstMenus = objects.Add<MenuInteractions>();
            MenuInteractions secondMenus = objects.Add<MenuInteractions>();
            PlayerInput first = Player("P1", firstMenus);
            PlayerInput second = Player("P2", secondMenus);
            players.Roster.JoinLocal(first);
            players.Roster.JoinLocal(second);
            firstMenus.hostPlayer = true;
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            players.DropSeat(0);

            Assert.AreEqual(1, players.PlayerCount);
            Assert.IsNull(players.Roster[0]);
            Assert.IsTrue(secondMenus.hostPlayer, "the pause menu still has a host");
            Assert.AreEqual(new Rect(0, 0, 1, 1), second.camera.rect, "the screen is theirs");
        }

        PlayerInput Player(string name, MenuInteractions menus)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInput input = player.AddComponent<PlayerInput>();
            input.camera = objects.Add<Camera>();
            player.AddComponent<PlayerUIHandler>().menuInteractions = menus;
            return input;
        }
    }
}

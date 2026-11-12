using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The players' cards on the results screen (ResultsAwardsView): one per score, for the seat that score shows (the
    /// scores are in finishing order), along the bottom of the screen under its score, with the player's place, name
    /// and score, their award and their rematch vote. The scene's own scores and title are
    /// restyled, and awards that arrive once the cards are up still show
    /// </summary>
    public class ResultsAwardsViewTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            AwardSync.Reset();
        }

        static AwardBoard Board()
        {
            MatchStats stats = new MatchStats();
            stats.Stole(0, GameState.Begin);
            stats.Stole(0, GameState.Begin);
            stats.Stole(0, GameState.Begin);
            stats.Delivered(0, Constants.OrderValue.Easy, GameState.Begin);
            stats.Sank(1, GameState.Begin);
            return AwardBoard.From(stats, new[] { 0, 1 });
        }

        // Scores side by side in a small box in the middle of a 1920 x 1080 canvas, as the results screen has them
        TMP_Text[] Scores(int count, out RectTransform box)
        {
            RectTransform canvas = objects.NewGameObject("Canvas").AddComponent<RectTransform>();
            canvas.sizeDelta = new Vector2(1920, 1080);
            box = new GameObject("Placement Text", typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(canvas, false);
            box.sizeDelta = new Vector2(100, 100);
            TMP_Text[] scores = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                scores[i] = new GameObject("Score " + i, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                scores[i].transform.SetParent(box, false);
                scores[i].alignment = TextAlignmentOptions.Center;
                scores[i].rectTransform.anchoredPosition = new Vector2(-300 + 340 * i, -400);
            }
            return scores;
        }

        [Test]
        public void Show_EachCardIsItsScoresSeats()
        {
            // Player 2 won, so the first score is seat 1's
            TMP_Text[] scores = Scores(2, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);

            view.Show(Board(), new[] { 1, 0 });

            Assert.AreEqual("KLEPTOGHOST", view.AwardName(1), "Player 1's steals: on Player 1's card");
            Assert.AreEqual("3 steals", view.AwardLine(1));
            Assert.AreEqual("FISH FOOD", view.AwardName(0));
            Assert.IsTrue(view.CardOf(0).activeSelf);
        }

        [Test]
        public void Show_AScoreWithNoSeat_HasNoCard()
        {
            TMP_Text[] scores = Scores(2, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);

            view.Show(Board(), new[] { 0, -1 });

            Assert.IsTrue(view.CardOf(0).activeSelf);
            Assert.IsFalse(view.CardOf(1).activeSelf);
        }

        [Test]
        public void ShowPlayer_ThePlaceNameAndScore_BeforeAnyAward()
        {
            TMP_Text[] scores = Scores(2, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);

            view.ShowPlayer(0, 2, 1, "Faty2k", "$975");
            view.ShowPlayer(1, -1, 2, "", "");

            Assert.IsTrue(view.CardOf(0).activeSelf, "a client's card is up before the host's awards reach it");
            Assert.AreEqual("1", view.PlaceText(0));
            Assert.AreEqual("Faty2k", view.PlayerName(0));
            Assert.AreEqual("$975", view.Money(0));
            Assert.IsFalse(view.AwardShown(0));
            Assert.IsFalse(view.CardOf(1).activeSelf, "nobody there");

            view.Show(Board(), new[] { 0, -1 });
            Assert.AreEqual("Faty2k", view.PlayerName(0), "the awards keep the header");
            Assert.IsTrue(view.AwardShown(0));
        }

        [Test]
        public void Hide_HidesEveryCard()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Show(Board(), new[] { 0 });

            view.Hide();

            Assert.IsFalse(view.CardOf(0).activeSelf);
        }

        [Test]
        public void ACard_SitsUnderItsScore_AlongTheBottomOfTheScreen()
        {
            TMP_Text[] scores = Scores(2, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Show(Board(), new[] { 0, 1 });

            view.Settle();

            for (int i = 0; i < 2; i++)
            {
                RectTransform card = (RectTransform)view.CardOf(i).transform;
                Vector2 at = ResultsAwardsView.CanvasPoint(card, Vector2.zero, (RectTransform)box.parent);
                Assert.AreEqual(scores[i].rectTransform.anchoredPosition.x, at.x, 1f, "under its own score");
                Assert.AreEqual(-540f + 20f, at.y, 1f, "its bottom edge just off the bottom of the screen");
                Assert.AreEqual(1f, view.CardOf(i).GetComponent<CanvasGroup>().alpha, 1e-3f, "arrived");
            }
        }

        [Test]
        public void ACard_NearTheScreensSide_StaysOnIt()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            scores[0].rectTransform.anchoredPosition = new Vector2(940, -400);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Show(Board(), new[] { 0 });

            view.Settle();

            RectTransform card = (RectTransform)view.CardOf(0).transform;
            float right = ResultsAwardsView.CanvasPoint(card, Vector2.zero, (RectTransform)box.parent).x + ResultsAwardsView.WIDTH * card.localScale.x / 2f;
            Assert.LessOrEqual(right, 960f);
        }

        [Test]
        public void TheScenesScore_AndItsBox_NoLongerDraw()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            GameObject purple = new GameObject("Box", typeof(RectTransform), typeof(Image));
            purple.transform.SetParent(scores[0].transform, false);
            purple.transform.localPosition = new Vector3(-291, 0);

            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Show(Board(), new[] { 0 });
            view.Settle();

            Assert.IsFalse(scores[0].enabled);
            Assert.IsFalse(purple.activeSelf);
            RectTransform card = (RectTransform)view.CardOf(0).transform;
            float x = ResultsAwardsView.CanvasPoint(card, Vector2.zero, (RectTransform)box.parent).x;
            Assert.AreEqual(scores[0].rectTransform.anchoredPosition.x - 291f, x, 1f, "where the box was: under the ghost");
        }

        [Test]
        public void TheTitle_GoesToTheTopOfTheScreen_OnOneLine()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            TMP_Text title = new GameObject("Title", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            title.transform.SetParent(box, false);
            title.rectTransform.anchoredPosition = new Vector2(0, 200);
            title.text = "Employee of The Month";

            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Settle();

            Assert.AreSame(title, view.Title);
            Assert.IsFalse(title.enableWordWrapping);
            float y = ResultsAwardsView.CanvasPoint(title.rectTransform, Vector2.zero, (RectTransform)box.parent).y;
            Assert.Greater(y, 400f, "clear of the podium");
            Assert.Less(y, 540f);
        }

        [Test]
        public void ShowVote_TagsTheCard_AndFadesAPlayerWhoLeft()
        {
            TMP_Text[] scores = Scores(2, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Show(Board(), new[] { 0, 1 });

            view.ShowVote(0, VoteMark.Ready);
            view.ShowVote(1, VoteMark.Out);
            view.Settle();

            Assert.AreEqual("READY", view.VoteText(0));
            Assert.AreEqual("OUT", view.VoteText(1));
            Assert.Less(view.CardOf(1).GetComponent<CanvasGroup>().alpha, 1f);

            view.ShowVote(0, VoteMark.None);
            Assert.AreEqual("", view.VoteText(0));
        }

        [Test]
        public void Follow_AwardsArrivingLater_StillShow()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);

            view.Follow(new[] { 0 });
            Assert.IsFalse(view.CardOf(0).activeSelf, "no awards yet");
            AwardSync.Arrive(Board());

            Assert.IsTrue(view.CardOf(0).activeSelf);
            Assert.AreEqual("KLEPTOGHOST", view.AwardName(0));
        }

        [Test]
        public void Follow_AwardsAlreadyIn_ShowAtOnce()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            AwardSync.Decide(Board());

            view.Follow(new[] { 0 });

            Assert.IsTrue(view.CardOf(0).activeSelf);
        }

        [Test]
        public void Hide_StopsFollowing()
        {
            TMP_Text[] scores = Scores(1, out RectTransform box);
            ResultsAwardsView view = ResultsAwardsView.Build(box, scores);
            view.Follow(new[] { 0 });

            view.Hide();
            AwardSync.Arrive(Board());

            Assert.IsFalse(view.CardOf(0).activeSelf, "the results are done with");
        }
    }
}

using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The award cards on the results screen (ResultsAwardsView): one above each score, for the seat that score shows
    /// (the scores are in finishing order), with the award's joke name and its count; none without awards. They're sized
    /// in the canvas, not scaled up with the scores, and awards that arrive once the scores are up still show
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
            return AwardBoard.From(stats, new[] { 0, 1 });
        }

        // Scores side by side under one canvas, as the results screen has them
        TMP_Text[] Scores(int count, out Transform canvas)
        {
            canvas = objects.NewGameObject("Canvas").AddComponent<RectTransform>();
            TMP_Text[] scores = new TMP_Text[count];
            for (int i = 0; i < count; i++)
            {
                scores[i] = objects.Add<TextMeshProUGUI>();
                scores[i].transform.SetParent(canvas, false);
            }
            return scores;
        }

        static string Text(ResultsAwardsView view, int score, string part)
        {
            return view.CardOf(score).transform.Find(part).GetComponent<TMP_Text>().text;
        }

        [Test]
        public void Show_EachCardIsItsScoresSeats()
        {
            // Player 2 won, so the first score is seat 1's
            TMP_Text[] scores = Scores(2, out Transform canvas);
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);

            view.Show(Board(), new[] { 1, 0 });

            Assert.AreEqual("KLEPTOGHOST", Text(view, 1, "Name"), "Player 1's steals: over Player 1's score");
            Assert.AreEqual("3 steals", Text(view, 1, "Line"));
            Assert.AreEqual("TOURIST", Text(view, 0, "Name"));
            Assert.IsTrue(view.CardOf(0).activeSelf);
        }

        [Test]
        public void Show_AScoreWithNoSeat_HasNoCard()
        {
            TMP_Text[] scores = Scores(2, out Transform canvas);
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);

            view.Show(Board(), new[] { 0, -1 });

            Assert.IsTrue(view.CardOf(0).activeSelf);
            Assert.IsFalse(view.CardOf(1).activeSelf);
        }

        [Test]
        public void Hide_HidesEveryCard()
        {
            TMP_Text[] scores = Scores(1, out Transform canvas);
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);
            view.Show(Board(), new[] { 0 });

            view.Hide();

            Assert.IsFalse(view.CardOf(0).activeSelf);
        }

        [Test]
        public void ACard_SitsOverItsScoresText_AndNamesItsPlayer()
        {
            // The results' scores are left-aligned in boxes far wider than "$975": cards centred on the box sat over the
            // next player's score (playtest, 2026-10-24: whose award it was wasn't clear)
            TMP_Text[] scores = Scores(1, out Transform canvas);
            RectTransform box = scores[0].rectTransform;
            box.sizeDelta = new Vector2(771, 47);
            box.localScale = Vector3.one * 1.778f;
            box.anchoredPosition = new Vector2(495, -422);
            scores[0].alignment = TextAlignmentOptions.Left;
            scores[0].text = "$975";
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);

            view.Show(Board(), new[] { 0 });

            float textWidth = scores[0].GetPreferredValues("$975").x;
            float textMiddle = box.anchoredPosition.x + (box.rect.xMin + textWidth / 2f) * box.localScale.x;
            RectTransform card = (RectTransform)view.CardOf(0).transform;
            Assert.AreEqual(textMiddle, card.anchoredPosition.x, 1f, "over the text, not the box's middle");
            Assert.Less(card.anchoredPosition.x, box.anchoredPosition.x, "the text is in the box's left part");
            Assert.Greater(card.anchoredPosition.y, box.anchoredPosition.y, "above the score");
            Assert.AreEqual("P1", Text(view, 0, "Player"));
        }

        [Test]
        public void ACard_IsSizedInTheCanvas_NotScaledUpWithItsScore()
        {
            TMP_Text[] scores = Scores(1, out Transform canvas);
            scores[0].transform.localScale = Vector3.one * 1.778f; // the results' scores are drawn bigger

            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);

            Assert.AreEqual(1f, view.CardOf(0).transform.lossyScale.x, 1e-4f);
        }

        [Test]
        public void Follow_AwardsArrivingLater_StillShow()
        {
            TMP_Text[] scores = Scores(1, out Transform canvas);
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);

            view.Follow(new[] { 0 });
            Assert.IsFalse(view.CardOf(0).activeSelf, "no awards yet");
            AwardSync.Arrive(Board());

            Assert.IsTrue(view.CardOf(0).activeSelf);
            Assert.AreEqual("KLEPTOGHOST", Text(view, 0, "Name"));
        }

        [Test]
        public void Follow_AwardsAlreadyIn_ShowAtOnce()
        {
            TMP_Text[] scores = Scores(1, out Transform canvas);
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);
            AwardSync.Decide(Board());

            view.Follow(new[] { 0 });

            Assert.IsTrue(view.CardOf(0).activeSelf);
        }

        [Test]
        public void Hide_StopsFollowing()
        {
            TMP_Text[] scores = Scores(1, out Transform canvas);
            ResultsAwardsView view = ResultsAwardsView.Build(canvas, scores);
            view.Follow(new[] { 0 });

            view.Hide();
            AwardSync.Arrive(Board());

            Assert.IsFalse(view.CardOf(0).activeSelf, "the results are done with");
        }
    }
}

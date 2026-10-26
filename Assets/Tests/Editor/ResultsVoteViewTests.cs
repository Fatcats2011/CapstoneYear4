using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The rematch vote on the results screen sits along the bottom of the whole screen
    /// </summary>
    public class ResultsVoteViewTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void TheView_CoversTheWholeCanvas_NotItsSmallParent()
        {
            // The results' elements sit in a 100 x 100 box mid-screen: the view stretched over it put "the bottom of the
            // screen" under the title, and the prompts over EMPLOYEE OF THE MONTH (playtest, 2026-10-24)
            RectTransform canvas = objects.NewGameObject("Canvas").AddComponent<RectTransform>();
            canvas.gameObject.AddComponent<Canvas>();
            canvas.sizeDelta = new Vector2(1920, 1080);
            RectTransform elements = new GameObject("CanvasElements", typeof(RectTransform)).GetComponent<RectTransform>();
            elements.SetParent(canvas, false);
            elements.sizeDelta = new Vector2(100, 100);

            ResultsVoteView view = ResultsVoteView.Build(elements, new TMP_Text[0]);

            RectTransform rect = (RectTransform)view.transform;
            Assert.AreEqual(canvas.rect.size, rect.rect.size);
            Assert.Greater(rect.rect.height, 100f);
            Assert.AreEqual(Vector2.zero, (Vector2)rect.localPosition);
        }
    }
}

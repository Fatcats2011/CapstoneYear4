using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The mouse in the menus (MouseMenuRules): which row the pointer is over, which way a click on a row's value steps,
    /// a row's area on screen (its own rect and its children's, as a drawn row's art and text overhang it), and when the
    /// mouse moves the selector (only when it moved: a still pointer never fights the pad)
    /// </summary>
    public class MouseMenuRulesTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        static readonly List<Rect> ROWS = new List<Rect>
        {
            new Rect(100, 500, 400, 80),
            new Rect(100, 400, 400, 80),
            new Rect(100, 300, 400, 80),
        };

        [Test]
        public void Hovered_TheRowUnderThePointer()
        {
            Assert.AreEqual(0, MouseMenuRules.Hovered(ROWS, new Vector2(200, 540)));
            Assert.AreEqual(2, MouseMenuRules.Hovered(ROWS, new Vector2(499, 301)));
        }

        [Test]
        public void Hovered_NoRow_IsMinusOne()
        {
            Assert.AreEqual(-1, MouseMenuRules.Hovered(ROWS, new Vector2(50, 540)));
            Assert.AreEqual(-1, MouseMenuRules.Hovered(ROWS, new Vector2(200, 390)), "between rows");
            Assert.AreEqual(-1, MouseMenuRules.Hovered(new List<Rect>(), new Vector2(200, 540)));
        }

        [Test]
        public void StepsRight_RightOfTheValue_LeftOfItLeft()
        {
            Assert.IsTrue(MouseMenuRules.StepsRight(700f, 650f));
            Assert.IsFalse(MouseMenuRules.StepsRight(600f, 650f));
        }

        [Test]
        public void Selects_OnlyWhenTheMouseMoved_OntoAnotherRow()
        {
            Assert.IsTrue(MouseMenuRules.Selects(true, 1, 0));
            Assert.IsFalse(MouseMenuRules.Selects(false, 1, 0), "the pointer sat still: the pad's choice stands");
            Assert.IsFalse(MouseMenuRules.Selects(true, 0, 0), "already selected");
            Assert.IsFalse(MouseMenuRules.Selects(true, -1, 0), "over no row");
        }

        [TestCase(0.5f, true)]
        [TestCase(1.9f, true)]
        [TestCase(2.5f, false)]
        public void AClick_CountsOnlyWhileTheCursorShows(float sinceMoved, bool counts)
        {
            Assert.AreEqual(counts, MouseMenuRules.ClickCounts(sinceMoved), "a click to focus the window, the cursor hidden, does nothing");
        }

        [Test]
        public void Nearest_TheChoiceClosestToTheClick()
        {
            float[] choices = { -186f, 138f }; // OFF and ON
            Assert.AreEqual(1, MouseMenuRules.Nearest(choices, 120f), "on ON, left of its marker: still ON");
            Assert.AreEqual(0, MouseMenuRules.Nearest(choices, -40f));
            Assert.AreEqual(-1, MouseMenuRules.Nearest(new float[0], 5f));
        }

        [TestCase(500f, 0)]
        [TestCase(500f + MouseMenuRules.APPLY_HALF_WIDTH - 1f, 0)]
        [TestCase(500f + MouseMenuRules.APPLY_HALF_WIDTH + 1f, 1)]
        [TestCase(500f - MouseMenuRules.APPLY_HALF_WIDTH - 1f, -1)]
        public void Zone_OnTheValueApplies_EitherSideSteps(float clickX, int zone)
        {
            Assert.AreEqual(zone, MouseMenuRules.Zone(clickX, 500f));
        }

        [Test]
        public void ScreenRect_TakesInTheChildren()
        {
            RectTransform row = objects.NewGameObject("Row").AddComponent<RectTransform>();
            row.sizeDelta = new Vector2(100, 100);
            row.position = new Vector3(500, 500, 0);
            RectTransform text = new GameObject("Text", typeof(RectTransform)).GetComponent<RectTransform>();
            text.SetParent(row, false);
            text.sizeDelta = new Vector2(100, 40);
            text.anchoredPosition = new Vector2(300, 0);

            Rect area = MouseMenuRules.ScreenRect(row, null);

            Assert.AreEqual(450f, area.xMin, 0.01f);
            Assert.AreEqual(850f, area.xMax, 0.01f, "the text to its right counts");
            Assert.AreEqual(450f, area.yMin, 0.01f);
            Assert.AreEqual(550f, area.yMax, 0.01f);
        }

        [Test]
        public void ScreenRect_HiddenChildrenDontCount()
        {
            RectTransform row = objects.NewGameObject("Row").AddComponent<RectTransform>();
            row.sizeDelta = new Vector2(100, 100);
            row.position = new Vector3(500, 500, 0);
            RectTransform hidden = new GameObject("Hidden", typeof(RectTransform)).GetComponent<RectTransform>();
            hidden.SetParent(row, false);
            hidden.sizeDelta = new Vector2(100, 40);
            hidden.anchoredPosition = new Vector2(900, 0);
            hidden.gameObject.SetActive(false);

            Assert.AreEqual(550f, MouseMenuRules.ScreenRect(row, null).xMax, 0.01f);
        }
    }
}

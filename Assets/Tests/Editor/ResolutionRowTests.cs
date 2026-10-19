using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The Resolution row the Options screen builds under its last row (ResolutionRow): where it goes, its text in the
    /// screen's own font, left and right picking a size, A applying it, and leaving without A keeping the old one
    /// </summary>
    public class ResolutionRowTests
    {
        readonly TestObjects objects = new TestObjects();
        RectTransform rowAbove;
        TextMeshProUGUI style;
        ResolutionRow row;

        static readonly ScreenSize HD = new ScreenSize(1280, 720);
        static readonly ScreenSize FULL_HD = new ScreenSize(1920, 1080);

        [SetUp]
        public void SetUp()
        {
            GameObject positions = objects.NewGameObject("Positions");
            positions.AddComponent<RectTransform>();
            rowAbove = objects.NewGameObject("Fullscreen").AddComponent<RectTransform>();
            rowAbove.SetParent(positions.transform, false);
            rowAbove.anchoredPosition = new Vector2(-559f, -21f);
            style = objects.NewGameObject("Off").AddComponent<TextMeshProUGUI>();
            style.color = Color.yellow;

            List<ScreenSize> listed = new List<ScreenSize> { HD, FULL_HD, new ScreenSize(800, 600) };
            row = ResolutionRow.Create(rowAbove, style, listed, HD, FULL_HD);
        }

        [TearDown]
        public void TearDown()
        {
            if (row != null)
                Object.DestroyImmediate(row.gameObject);
            objects.DestroyAll();
        }

        [Test]
        public void Create_SitsUnderTheRowAbove_InTheScreensOwnLook()
        {
            RectTransform placed = (RectTransform)row.transform;
            Assert.AreSame(rowAbove.parent, placed.parent);
            Assert.AreEqual(rowAbove.anchoredPosition.y - ResolutionRow.ROW_SPACING, placed.anchoredPosition.y, 0.01f);
            Assert.AreEqual("RESOLUTION", row.LabelText);
            Assert.AreEqual(Color.yellow, row.Value.color, "the screen's text colour");
            Assert.AreSame(style.font, row.Value.font, "the screen's font");
        }

        [Test]
        public void Starts_OnTheSavedSize_Applied()
        {
            Assert.AreEqual(HD, row.Shown);
            Assert.AreEqual(HD, row.Applied);
            Assert.AreEqual(ResolutionRules.Label(HD, false), row.Value.text);
        }

        [Test]
        public void LeftAndRight_PickASize_ThatWaitsForA()
        {
            row.Step(true);

            Assert.AreEqual(FULL_HD, row.Shown);
            Assert.AreEqual(HD, row.Applied, "not applied yet");
            Assert.AreEqual(ResolutionRules.Label(FULL_HD, true), row.Value.text);
        }

        [Test]
        public void A_AppliesTheShownSize()
        {
            row.Step(true);

            Assert.AreEqual(FULL_HD, row.Apply());
            Assert.AreEqual(FULL_HD, row.Applied);
            Assert.AreEqual(ResolutionRules.Label(FULL_HD, false), row.Value.text);
        }

        [Test]
        public void LeavingWithoutA_GoesBackToTheAppliedSize()
        {
            row.Step(false);

            row.Revert();

            Assert.AreEqual(HD, row.Shown);
            Assert.AreEqual(ResolutionRules.Label(HD, false), row.Value.text);
        }
    }
}

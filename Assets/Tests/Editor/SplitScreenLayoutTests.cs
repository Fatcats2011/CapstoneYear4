using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class SplitScreenLayoutTests
    {
        [Test]
        public void NoPlayers_GetNoViewports()
        {
            Assert.AreEqual(0, SplitScreenLayout.CalculateRects(0).Length);
        }

        [Test]
        public void OnePlayer_GetsTheWholeScreen()
        {
            CollectionAssert.AreEqual(new[] { new Rect(0f, 0f, 1f, 1f) }, SplitScreenLayout.CalculateRects(1));
        }

        [Test]
        public void TwoPlayers_GetCenteredTopAndBottomViewports()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0.25f, 0.5f, 0.5f, 0.5f),
                new Rect(0.25f, 0f, 0.5f, 0.5f),
            }, SplitScreenLayout.CalculateRects(2));
        }

        [Test]
        public void ThreePlayers_FillTheTopRowAndCenterTheThird()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                new Rect(0.25f, 0f, 0.5f, 0.5f),
            }, SplitScreenLayout.CalculateRects(3));
        }

        [Test]
        public void FourPlayers_GetOneQuadrantEach()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                new Rect(0f, 0f, 0.5f, 0.5f),
                new Rect(0.5f, 0f, 0.5f, 0.5f),
            }, SplitScreenLayout.CalculateRects(4));
        }

        [Test]
        public void TwoPlayers_Stacked_FullWidthTopAndBottom()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0f, 0.5f, 1f, 0.5f),
                new Rect(0f, 0f, 1f, 0.5f),
            }, SplitScreenLayout.CalculateRects(2, SplitLayout.Stacked));
        }

        [Test]
        public void TwoPlayers_SideBySide_FullHeightLeftAndRight()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0f, 0f, 0.5f, 1f),
                new Rect(0.5f, 0f, 0.5f, 1f),
            }, SplitScreenLayout.CalculateRects(2, SplitLayout.SideBySide));
        }

        [Test]
        public void TwoPlayers_Boxed_IsTodays()
        {
            CollectionAssert.AreEqual(SplitScreenLayout.CalculateRects(2), SplitScreenLayout.CalculateRects(2, SplitLayout.Boxed));
        }

        [Test]
        public void ThreeAndFourPlayers_KeepTheirLayout_WhateverIsPicked()
        {
            CollectionAssert.AreEqual(SplitScreenLayout.CalculateRects(3), SplitScreenLayout.CalculateRects(3, SplitLayout.SideBySide));
            CollectionAssert.AreEqual(SplitScreenLayout.CalculateRects(4), SplitScreenLayout.CalculateRects(4, SplitLayout.Stacked));
            CollectionAssert.AreEqual(SplitScreenLayout.CalculateRects(1), SplitScreenLayout.CalculateRects(1, SplitLayout.Stacked));
        }

        [Test]
        public void TheSavedLayout_IsWhatTheGameUses()
        {
            try
            {
                SplitScreenLayout.TwoPlayers = SplitLayout.Stacked;
                CollectionAssert.AreEqual(SplitScreenLayout.CalculateRects(2, SplitLayout.Stacked), SplitScreenLayout.ForPlayers(2));
            }
            finally
            {
                SplitScreenLayout.TwoPlayers = SplitLayout.Boxed;
            }
        }
    }
}

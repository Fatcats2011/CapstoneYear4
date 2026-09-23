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
    }
}

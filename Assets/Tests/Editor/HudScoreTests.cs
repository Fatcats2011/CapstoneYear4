using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// A player's score in the match HUD (NumberHandler): room for four digits, where the dollar sign and the whole
    /// counter sit for each count of digits, and a score above 9999 reading 9999. The results screen shows the true score
    /// </summary>
    public class HudScoreTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [TestCase("0", "0")]
        [TestCase("55", "55")]
        [TestCase("999", "999")]
        [TestCase("1000", "1000")]
        [TestCase("1280", "1280")]
        [TestCase("9999", "9999")]
        [TestCase("10000", "9999")]
        [TestCase("52000", "9999")]
        public void HudScoreText_NeverNeedsMoreThanFourDigits(string score, string shown)
        {
            Assert.AreEqual(shown, NumberHandler.HudScoreText(score));
        }

        [Test]
        public void HudScoreText_NotANumber_IsLeftAlone()
        {
            Assert.AreEqual("abc", NumberHandler.HudScoreText("abc"));
        }

        [TestCase(1, 0f, -50f, 1.45f)]
        [TestCase(2, -65f, -16f, 1.2f)]
        [TestCase(3, -115f, 0f, 1f)]
        [TestCase(4, -175f, 18f, 0.82f)]
        public void Layout_PutsTheCounterWhereEachDigitCountNeedsIt(int digits, float dollarX, float counterX, float counterScale)
        {
            NumberHandler.ScoreLayout layout = NumberHandler.LayoutFor(digits);

            Assert.AreEqual(dollarX, layout.DollarX, 0.001f);
            Assert.AreEqual(counterX, layout.CounterX, 0.001f);
            Assert.AreEqual(counterScale, layout.CounterScale, 0.001f);
        }

        // A counter like the HUD's: three digit holders 60 apart (each with its Image) under one counter object
        NumberHandler NewCounter(out RectTransform counter)
        {
            GameObject root = objects.NewGameObject("Counter");
            counter = root.AddComponent<RectTransform>();
            NumberHandler handler = root.AddComponent<NumberHandler>();
            Image[] digits = new Image[3];
            float[] xs = { -50.5f, 9.5f, 69.5f };
            for (int i = 0; i < 3; i++)
            {
                GameObject holder = new GameObject("Image Holder", typeof(RectTransform));
                holder.transform.SetParent(counter, false);
                ((RectTransform)holder.transform).anchoredPosition = new Vector2(xs[i], 0f);
                GameObject image = new GameObject("Image", typeof(RectTransform), typeof(Image));
                image.transform.SetParent(holder.transform, false);
                digits[i] = image.GetComponent<Image>();
            }
            handler.moneyImages = digits;
            return handler;
        }

        [Test]
        public void ThousandsImage_IsACopyOfTheFirstDigitOneSlotFurtherLeft_MadeOnce()
        {
            NumberHandler handler = NewCounter(out RectTransform counter);

            Image thousands = handler.ThousandsImage();

            Assert.IsNotNull(thousands);
            Assert.AreSame(counter, thousands.transform.parent.parent, "beside the other digits");
            Assert.AreEqual(-110.5f, ((RectTransform)thousands.transform.parent).anchoredPosition.x, 0.001f);
            Assert.AreEqual(4, counter.childCount);
            Assert.AreSame(thousands, handler.ThousandsImage(), "the same one again");
            Assert.AreEqual(4, counter.childCount);
        }

        [Test]
        public void Layout_AnotherCount_UsesTheNearestOne()
        {
            Assert.AreEqual(NumberHandler.LayoutFor(1).DollarX, NumberHandler.LayoutFor(0).DollarX, 0.001f);
            Assert.AreEqual(NumberHandler.LayoutFor(4).DollarX, NumberHandler.LayoutFor(7).DollarX, 0.001f);
        }
    }
}

using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Options → Accessibility's rumble, colourblind mode and text size (Comfort, Colourblind, TextSize): saved and read
    /// back, what each does, and the shapes and colours colourblind mode draws
    /// </summary>
    public class AccessibilityTests
    {
        static readonly GameSettings Defaults = new GameSettings(5, 5, 1);

        [TearDown]
        public void TearDown()
        {
            Comfort.Rumble = true;
            Colourblind.On = false;
            TextSize.Set(0);
        }

        [Test]
        public void Settings_RoundTrip_AndClamp()
        {
            GameSettings saved = new GameSettings(5, 5, 1) { rumble = 0, colourblind = 1, textSize = 2 };
            GameSettings loaded = GameSettings.Parse(saved.ToText(), Defaults);
            Assert.AreEqual(0, loaded.rumble);
            Assert.AreEqual(1, loaded.colourblind);
            Assert.AreEqual(2, loaded.textSize);

            GameSettings fresh = GameSettings.Parse("", Defaults);
            Assert.AreEqual(1, fresh.rumble, "pads shake unless turned off");
            Assert.AreEqual(0, fresh.colourblind);
            Assert.AreEqual(0, fresh.textSize, "100%");

            GameSettings wild = GameSettings.Parse("rumble=7\ncolourblind=-2\ntextsize=9", Defaults);
            Assert.AreEqual(1, wild.rumble);
            Assert.AreEqual(0, wild.colourblind);
            Assert.AreEqual(TextSize.SCALES.Length - 1, wild.textSize);
        }

        [Test]
        public void Rumble_Off_NoMotorSpeed()
        {
            Assert.AreEqual(0.6f, Comfort.Motor(0.6f));
            Comfort.Rumble = false;
            Assert.AreEqual(0f, Comfort.Motor(0.6f));
        }

        [Test]
        public void TextSize_ScalesFixedAndAutoSizedText_AndBack()
        {
            GameObject holder = new GameObject("Texts", typeof(RectTransform));
            try
            {
                TextMeshProUGUI fixedSize = new GameObject("Fixed", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                fixedSize.transform.SetParent(holder.transform);
                fixedSize.fontSize = 40f;
                TextMeshProUGUI auto = new GameObject("Auto", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
                auto.transform.SetParent(holder.transform);
                auto.enableAutoSizing = true;
                auto.fontSizeMin = 10f;
                auto.fontSizeMax = 30f;

                TextSize.Set(2);
                Assert.AreEqual(60f, fixedSize.fontSize, 0.001f, "150%");
                Assert.AreEqual(15f, auto.fontSizeMin, 0.001f);
                Assert.AreEqual(45f, auto.fontSizeMax, 0.001f);

                TextSize.Set(1);
                Assert.AreEqual(50f, fixedSize.fontSize, 0.001f, "from its own size, not the last shown");

                // The code sets its own size: that's its size now
                fixedSize.fontSize = 20f;
                TextSize.Fit(fixedSize);
                Assert.AreEqual(25f, fixedSize.fontSize, 0.001f);
                TextSize.Fit(fixedSize);
                Assert.AreEqual(25f, fixedSize.fontSize, 0.001f, "never scaled twice");

                TextSize.Set(0);
                Assert.AreEqual(20f, fixedSize.fontSize, 0.001f);
                Assert.AreEqual(30f, auto.fontSizeMax, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void TextSize_SetSize_ShowsScaledAtOnce()
        {
            TextMeshProUGUI text = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            try
            {
                TextSize.Set(2);
                TextSize.SetSize(text, 40f);
                Assert.AreEqual(60f, text.fontSize, 0.001f, "measured as it shows");
                TextSize.Fit(text);
                Assert.AreEqual(60f, text.fontSize, 0.001f, "not scaled again");
                TextSize.Set(0);
                Assert.AreEqual(40f, text.fontSize, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(text.gameObject);
            }
        }

        [Test]
        public void TextSize_BigText_GrowsNoFurtherThanLargest()
        {
            Assert.AreEqual(36f, TextSize.Grown(24f, 1.5f), 0.001f);
            Assert.AreEqual(TextSize.LARGEST, TextSize.Grown(64f, 1.5f), 0.001f, "a menu row");
            Assert.AreEqual(90f, TextSize.Grown(90f, 1.5f), 0.001f, "never smaller than it was");
        }

        [Test]
        public void TextSize_Labels()
        {
            Assert.AreEqual("100%", TextSize.Label(0));
            Assert.AreEqual("125%", TextSize.Label(1));
            Assert.AreEqual("150%", TextSize.Label(2));
        }

        [Test]
        public void Colourblind_FourSeats_FourShapes()
        {
            Assert.AreEqual(Colourblind.Shape.Circle, Colourblind.ShapeOf(0));
            Assert.AreEqual(Colourblind.Shape.Triangle, Colourblind.ShapeOf(1));
            Assert.AreEqual(Colourblind.Shape.Square, Colourblind.ShapeOf(2));
            Assert.AreEqual(Colourblind.Shape.Diamond, Colourblind.ShapeOf(3));

            foreach (Colourblind.Shape shape in System.Enum.GetValues(typeof(Colourblind.Shape)))
            {
                Assert.Less(Colourblind.Distance(shape, 0f, 0f), 0f, shape + ": its middle is inside");
                Assert.Greater(Colourblind.Distance(shape, 0.98f, 0.98f), 0f, shape + ": its corner is outside");
            }
            // Told apart by outline, not colour: the square fills a corner the circle and diamond don't
            Assert.Less(Colourblind.Distance(Colourblind.Shape.Square, 0.65f, 0.65f), 0f);
            Assert.Greater(Colourblind.Distance(Colourblind.Shape.Circle, 0.65f, 0.65f), 0f);
            Assert.Greater(Colourblind.Distance(Colourblind.Shape.Diamond, 0.65f, 0.65f), 0f);
        }

        [Test]
        public void Colourblind_Badge_WhiteInside_BlackEdge_ClearOutside()
        {
            Assert.AreEqual(Color.white, Colourblind.Pixel(-0.5f));
            Assert.Less(Colourblind.Pixel(-0.05f).r, 0.1f, "the edge");
            Assert.AreEqual(0f, Colourblind.Pixel(0.1f).a);
        }

        [Test]
        public void Colourblind_SafeColours_InAMatch_NotWhilePicking()
        {
            Assert.IsTrue(Colourblind.SafeColoursIn(GameState.Begin));
            Assert.IsTrue(Colourblind.SafeColoursIn(GameState.Results));
            Assert.IsFalse(Colourblind.SafeColoursIn(GameState.PlayerSelect), "the colour picked shows while picking it");
            Assert.IsFalse(Colourblind.SafeColoursIn(GameState.Menu));
        }

        [Test]
        public void Colourblind_SafeCopy_TintsTheGhostMaterial()
        {
            Material original = AssetDatabaseLoad("Assets/Materials/Ghost Materials/Ghost Color 1.mat");
            Material copy = Colourblind.SafeCopy(original, 1);
            Assert.AreNotSame(original, copy);
            Assert.AreSame(copy, Colourblind.SafeCopy(original, 1), "one copy per seat");
            Color baseColour = copy.GetColor("_BaseColor");
            Assert.AreEqual(Colourblind.SAFE[1].r, baseColour.r, 0.001f);
            Assert.AreEqual(Colourblind.SAFE[1].b, baseColour.b, 0.001f);
            Assert.AreEqual(original.GetColor("_RimColor").a, copy.GetColor("_RimColor").a, 0.001f, "strengths kept");
            Assert.IsTrue(Colourblind.IsSafeCopy(copy));
            Assert.IsFalse(Colourblind.IsSafeCopy(original));
        }

        static Material AssetDatabaseLoad(string path)
        {
            Material material = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, path);
            return material;
        }
    }
}

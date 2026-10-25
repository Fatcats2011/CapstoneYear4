using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The drawn accents on screen (AccentOverlay), in a text in the Bangers font: a letter the font lacks (Č) shows as
    /// its base with a mark placed on it, the mark follows the letter when the line re-lays out, text without such
    /// letters has no marks, and every character of every translation can be shown in each of the game's fonts
    /// </summary>
    public class AccentOverlayTests
    {
        readonly TestObjects objects = new TestObjects();
        TextMeshProUGUI text;

        [SetUp]
        public void SetUp()
        {
            AccentOverlay.Listen();
            RectTransform canvas = objects.NewGameObject("Canvas").AddComponent<RectTransform>();
            canvas.gameObject.AddComponent<Canvas>(); // a UI text lays out only under a canvas
            text = objects.NewGameObject("Text").AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(canvas, false);
            text.rectTransform.sizeDelta = new Vector2(800, 200);
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
            text.fontSize = 60;
            text.enableWordWrapping = true;
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            AccentOverlay.StopListening();
        }

        static List<Image> Marks(TMP_Text text)
        {
            List<Image> shown = new List<Image>();
            foreach (Image image in text.GetComponentsInChildren<Image>(false))
                shown.Add(image);
            return shown;
        }

        // The laid-out C's left and right edges
        static Vector2 SpanOf(TMP_Text text, char letter, int which = 0)
        {
            foreach (TMP_CharacterInfo info in text.textInfo.characterInfo)
            {
                if (info.character == letter && which-- == 0)
                    return new Vector2(info.bottomLeft.x, info.bottomRight.x);
            }
            Assert.Fail("no " + letter);
            return default;
        }

        [Test]
        public void Overlay_PlacesAMarkOverTheBase()
        {
            text.text = "ČAS";
            text.ForceMeshUpdate();

            Assert.AreEqual("CAS", text.text, "the font lacks Č: its base letter");
            List<Image> marks = Marks(text);
            Assert.AreEqual(1, marks.Count);
            Vector2 c = SpanOf(text, 'C');
            float x = marks[0].rectTransform.localPosition.x;
            Assert.That(x, Is.InRange(c.x - 10f, c.y + 10f), "over the C");
            Assert.AreSame(AccentMarkSet.Game.Of(AccentMark.Caron), marks[0].sprite);
        }

        [Test]
        public void Overlay_FollowsTheCharacterAfterRelayout()
        {
            text.text = "AAAA AAAA ŐAAA";
            text.ForceMeshUpdate();

            text.rectTransform.sizeDelta = new Vector2(260, 400); // wraps: the Ő moves to another line
            text.ForceMeshUpdate();

            TMP_CharacterInfo o = default;
            foreach (TMP_CharacterInfo info in text.textInfo.characterInfo)
                if (info.character == 'O')
                    o = info;
            Assert.Greater(o.lineNumber, 0, "it wrapped");
            Image mark = Marks(text)[0];
            Assert.That(mark.rectTransform.localPosition.x, Is.InRange(o.bottomLeft.x - 10f, o.bottomRight.x + 10f));
            float cap = o.ascender - o.baseLine;
            Assert.That(mark.rectTransform.localPosition.y, Is.InRange(o.ascender, o.ascender + cap), "just above the letter, on its new line");
        }

        [Test]
        public void Overlay_TextWithoutThemHasNoMarks()
        {
            text.text = "ČAS";
            text.ForceMeshUpdate();

            text.text = "PLAIN";
            text.ForceMeshUpdate();

            Assert.IsEmpty(Marks(text));
            Assert.AreEqual("PLAIN", text.text);
        }

        [Test]
        public void Overlay_SharpS_IsSS()
        {
            text.text = "STRAẞE";
            text.ForceMeshUpdate();

            Assert.AreEqual("STRASSE", text.text);
        }

        [Test]
        public void EveryTranslation_IsShowableInEachFont()
        {
            StringsCsv csv = StringsCsv.Load();
            List<TMP_FontAsset> fonts = new List<TMP_FontAsset>
            {
                Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/jcandlestick norm.asset"),
            };
            HashSet<string> missing = new HashSet<string>();
            foreach (TMP_FontAsset font in fonts)
            {
                Assert.IsNotNull(font);
                foreach (string key in csv.Keys)
                {
                    // Jack's Candlestick shows only the drawn words (DrawnWords), all capitals
                    if (font.name.StartsWith("jcandlestick") && !key.StartsWith("word."))
                        continue;
                    foreach (string locale in LocaleCodes.All)
                        foreach (char c in csv.Get(key, locale))
                            // Letters only: punctuation the fonts lack (…) shows in TextMesh Pro's fallback, as it always has
                            if (char.IsLetter(c) && c != 'ẞ' && !AccentRules.TryGet(c, out _) && !AccentOverlay.FontHas(font, c))
                                missing.Add(font.name + " " + c + " (" + key + " " + locale + ")");
                }
            }

            Assert.IsEmpty(missing);
        }
    }
}

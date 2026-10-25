using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The drawn button prompts follow the device without an editor step: the game gives each a ButtonGlyph as it loads
    /// (playtest, 2026-10-24: keyboard players saw Xbox buttons)
    /// </summary>
    public class ButtonGlyphsTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void TheArtSet_MapsA_B_X_Y_LB_RB()
        {
            ButtonArtSet art = ButtonArtSet.Results();
            Assert.AreEqual(6, art.glyphs.Length);
            string[] keys = { "Key_Space", "Key_Backspace", "Key_Q", "Key_Tab", "Key_F", "Key_R" };
            for (int i = 0; i < keys.Length; i++)
            {
                Assert.IsNotNull(art.glyphs[i].xbox, "Xbox " + i);
                Assert.IsNotNull(art.glyphs[i].playStation, "PlayStation " + i);
                Assert.AreEqual(keys[i], art.glyphs[i].keyboard.name);
            }
            Assert.AreEqual("MenUI_Buttons_7", art.glyphs[0].xbox.name, "A");
        }

        [Test]
        public void AttachUnder_GivesEachDrawnPromptAGlyph_Once()
        {
            ButtonArtSet art = ButtonArtSet.Results();
            GameObject root = objects.NewGameObject("Menu");
            Image a = new GameObject("A", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            a.transform.SetParent(root.transform);
            a.sprite = art.glyphs[0].xbox;
            a.gameObject.SetActive(false); // hidden prompts too
            Image other = new GameObject("Picture", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            other.transform.SetParent(root.transform);

            Assert.AreEqual(1, ButtonGlyphs.AttachUnder(root));
            Assert.AreEqual(0, ButtonGlyphs.AttachUnder(root), "once");

            ButtonGlyph glyph = a.GetComponent<ButtonGlyph>();
            Assert.AreSame(art.glyphs[0].keyboard, glyph.keyboard);
            Assert.AreSame(art.glyphs[0].playStation, glyph.playStation);
            Assert.IsNull(other.GetComponent<ButtonGlyph>());
        }
    }
}

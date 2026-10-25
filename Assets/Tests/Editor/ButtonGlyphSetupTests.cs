using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The editor's button glyph setup: an Image showing one of the game's Xbox buttons (MenUI_Buttons) gets a ButtonGlyph
    /// with that button's PlayStation and keyboard art (tools/button-art.py), so it follows the device in use
    /// </summary>
    public class ButtonGlyphSetupTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        static Sprite Xbox(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/UI/MenUI_Buttons.png").OfType<Sprite>().Single(s => s.name == name);
        }

        Image Showing(Sprite sprite)
        {
            Image image = objects.Add<Image>();
            image.sprite = sprite;
            return image;
        }

        [TestCase("MenUI_Buttons_7", "PS_Cross", "Key_Space")]     // A
        [TestCase("MenUI_Buttons_4", "PS_Circle", "Key_Backspace")] // B
        [TestCase("MenUI_Buttons_2", "PS_Square", "Key_Q")]        // X
        [TestCase("MenUI_Buttons_3", "PS_Triangle", "Key_Tab")]    // Y
        [TestCase("MenUI_Buttons_5", "PS_L1", "Key_F")]            // LB
        [TestCase("MenUI_Buttons_6", "PS_R1", "Key_R")]            // RB
        public void Attach_AnXboxButton_GetsItsPlayStationAndKeyboardArt(string xbox, string playStation, string keyboard)
        {
            Image image = Showing(Xbox(xbox));

            Assert.IsTrue(ButtonGlyphSetup.Attach(image));

            ButtonGlyph glyph = image.GetComponent<ButtonGlyph>();
            Assert.IsNotNull(glyph);
            Assert.AreSame(image.sprite, glyph.xbox);
            Assert.AreEqual(playStation, glyph.playStation.name);
            Assert.AreEqual(keyboard, glyph.keyboard.name);
            Assert.IsTrue(image.preserveAspect, "a wide key keeps its shape");
        }

        [Test]
        public void Attach_AnythingElse_IsLeftAlone()
        {
            Image image = Showing(Xbox("MenUI_Buttons_8")); // a stick: no prompt

            Assert.IsFalse(ButtonGlyphSetup.Attach(image));
            Assert.IsNull(image.GetComponent<ButtonGlyph>());
            Assert.IsFalse(ButtonGlyphSetup.Attach(Showing(null)));
        }

        [Test]
        public void TheGlyph_SizesItsLayoutToTheArt()
        {
            // A prompt lays out by its button's width: a wide key pushes the word along, a round button doesn't leave a gap
            Image image = Showing(Xbox("MenUI_Buttons_7"));
            UnityEngine.UI.LayoutElement size = image.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            size.preferredHeight = 56;
            ButtonGlyphSetup.Attach(image);
            ButtonGlyph glyph = image.GetComponent<ButtonGlyph>();

            UnityEngine.InputSystem.Keyboard keys = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                glyph.Show(keys);
                Assert.AreEqual(56 * 384f / 256f, size.preferredWidth, 0.5f, "Space: a wide key");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keys);
            }
            glyph.Show(null);
            Rect xbox = image.sprite.rect;
            Assert.AreEqual(56 * xbox.width / xbox.height, size.preferredWidth, 0.5f, "Xbox: as round as the button");
        }

        [Test]
        public void Attach_Twice_KeepsOneGlyph()
        {
            Image image = Showing(Xbox("MenUI_Buttons_7"));

            ButtonGlyphSetup.Attach(image);
            ButtonGlyphSetup.Attach(image);

            Assert.AreEqual(1, image.GetComponents<ButtonGlyph>().Length);
        }

        [Test]
        public void TheGlyph_FollowsTheDevice()
        {
            Image image = Showing(Xbox("MenUI_Buttons_7"));
            ButtonGlyphSetup.Attach(image);
            ButtonGlyph glyph = image.GetComponent<ButtonGlyph>();

            UnityEngine.InputSystem.Keyboard keys = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                glyph.Show(keys);
                Assert.AreEqual("Key_Space", image.sprite.name, "PC: the key");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(keys);
            }
            glyph.Show(null);
            Assert.AreEqual("MenUI_Buttons_7", image.sprite.name, "a pad that isn't PlayStation: Xbox");
        }
    }
}

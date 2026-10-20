using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Switch;
using UnityEngine.InputSystem.XInput;
using UnityEngine.UI;

namespace DoA.Tests
{
    public class ButtonGlyphTests
    {
        readonly TestObjects objects = new TestObjects();
        Sprite xbox, playStation, nintendo, keyboard;

        [SetUp]
        public void SetUp()
        {
            xbox = NewSprite("xbox");
            playStation = NewSprite("ps");
            nintendo = NewSprite("nintendo");
            keyboard = NewSprite("keyboard");
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            foreach (Sprite s in new[] { xbox, playStation, nintendo, keyboard })
            {
                Object.DestroyImmediate(s.texture);
                Object.DestroyImmediate(s);
            }
        }

        static Sprite NewSprite(string name)
        {
            Sprite s = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
            s.name = name;
            return s;
        }

        static GlyphStyle StyleOf<T>() where T : InputDevice
        {
            T device = InputSystem.AddDevice<T>();
            try
            {
                return GlyphRules.StyleFor(device);
            }
            finally
            {
                InputSystem.RemoveDevice(device);
            }
        }

        [Test]
        public void StyleFor_PlayStationPads_ShowPlayStation()
        {
            Assert.AreEqual(GlyphStyle.PlayStation, StyleOf<DualShock4GamepadHID>());
            Assert.AreEqual(GlyphStyle.PlayStation, StyleOf<DualSenseGamepadHID>());
        }

        [Test]
        public void StyleFor_SwitchPro_ShowsNintendo()
        {
            Assert.AreEqual(GlyphStyle.Nintendo, StyleOf<SwitchProControllerHID>());
        }

        [Test]
        public void StyleFor_KeyboardAndMouse_ShowKeys()
        {
            Assert.AreEqual(GlyphStyle.Keyboard, StyleOf<Keyboard>());
            Assert.AreEqual(GlyphStyle.Keyboard, StyleOf<Mouse>());
        }

        [Test]
        public void StyleFor_XboxOtherPadsAndNothing_ShowXbox()
        {
            Assert.AreEqual(GlyphStyle.Xbox, StyleOf<XInputControllerWindows>());
            Assert.AreEqual(GlyphStyle.Xbox, StyleOf<Gamepad>());
            Assert.AreEqual(GlyphStyle.Xbox, GlyphRules.StyleFor(null));
        }

        [Test]
        public void Pick_UsesTheStylesSprite()
        {
            Assert.AreSame(playStation, GlyphRules.Pick(GlyphStyle.PlayStation, xbox, playStation, nintendo, keyboard));
            Assert.AreSame(nintendo, GlyphRules.Pick(GlyphStyle.Nintendo, xbox, playStation, nintendo, keyboard));
            Assert.AreSame(keyboard, GlyphRules.Pick(GlyphStyle.Keyboard, xbox, playStation, nintendo, keyboard));
            Assert.AreSame(xbox, GlyphRules.Pick(GlyphStyle.Xbox, xbox, playStation, nintendo, keyboard));
        }

        // Until a style's art exists the prompt keeps showing what it shows today
        [Test]
        public void Pick_StyleWithNoArt_FallsBackToXbox()
        {
            Assert.AreSame(xbox, GlyphRules.Pick(GlyphStyle.PlayStation, xbox, null, null, null));
            Assert.AreSame(xbox, GlyphRules.Pick(GlyphStyle.Keyboard, xbox, null, null, null));
        }

        [Test]
        public void Show_SwapsTheImagesSprite_AndKeepsTheSceneSpriteAsXbox()
        {
            Image image = objects.Add<Image>();
            image.sprite = xbox; // what the scene has today
            ButtonGlyph glyph = image.gameObject.AddComponent<ButtonGlyph>();
            glyph.playStation = playStation;

            DualShock4GamepadHID pad = InputSystem.AddDevice<DualShock4GamepadHID>();
            Keyboard keys = InputSystem.AddDevice<Keyboard>();
            try
            {
                glyph.Show(pad);
                Assert.AreSame(playStation, image.sprite);
                Assert.AreEqual(GlyphStyle.PlayStation, glyph.Shown);

                glyph.Show(keys); // no keyboard art yet
                Assert.AreSame(xbox, image.sprite);
                Assert.AreEqual(GlyphStyle.Keyboard, glyph.Shown);
            }
            finally
            {
                InputSystem.RemoveDevice(pad);
                InputSystem.RemoveDevice(keys);
            }
        }

        [Test]
        public void LastDevice_Use_RaisesChangedOncePerNewDevice()
        {
            Gamepad pad = InputSystem.AddDevice<Gamepad>();
            ChangeRecorder recorder = new ChangeRecorder();
            LastDevice.Changed += recorder.Record;
            try
            {
                LastDevice.Use(pad);
                LastDevice.Use(pad);
                LastDevice.Use(null);

                Assert.AreEqual(1, recorder.Count);
                Assert.AreSame(pad, LastDevice.Current);
            }
            finally
            {
                LastDevice.Changed -= recorder.Record;
                InputSystem.RemoveDevice(pad);
            }
        }

        class ChangeRecorder
        {
            public int Count;
            public void Record(InputDevice device) => Count++;
        }
    }
}

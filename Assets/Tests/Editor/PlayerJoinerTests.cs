using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace DoA.Tests
{
    /// <summary>
    /// Which press joins a player (PlayerJoiner.SchemeFor): any button on a controller, and only Space or Enter on a
    /// keyboard. The game joins players itself, so other keys and devices create no player at all
    /// </summary>
    public class PlayerJoinerTests
    {
        Gamepad pad;
        Keyboard keyboard;

        [SetUp]
        public void SetUp()
        {
            pad = InputSystem.AddDevice<Gamepad>("DoA Joiner Test Pad");
            keyboard = InputSystem.AddDevice<Keyboard>("DoA Joiner Test Keyboard");
        }

        [TearDown]
        public void TearDown()
        {
            InputSystem.RemoveDevice(pad);
            InputSystem.RemoveDevice(keyboard);
        }

        // The scheme a press of one control would join with
        static string SchemeForPress(InputDevice device, ButtonControl control)
        {
            using (StateEvent.From(device, out InputEventPtr eventPtr))
            {
                control.WriteValueIntoEvent(1f, eventPtr);
                return PlayerJoiner.SchemeFor(control, eventPtr);
            }
        }

        [Test]
        public void SchemeFor_AnyControllerButton_JoinsWithTheController()
        {
            Assert.AreEqual(PlayerJoiner.GAMEPAD_SCHEME, SchemeForPress(pad, pad.buttonSouth));
            Assert.AreEqual(PlayerJoiner.GAMEPAD_SCHEME, SchemeForPress(pad, pad.startButton));
            Assert.AreEqual(PlayerJoiner.GAMEPAD_SCHEME, SchemeForPress(pad, pad.leftStickButton));
        }

        [Test]
        public void SchemeFor_SpaceOrEnter_JoinsTheKeyboard_OtherKeysDont()
        {
            Assert.AreEqual(KeyboardControls.SCHEME, SchemeForPress(keyboard, keyboard.spaceKey));
            Assert.AreEqual(KeyboardControls.SCHEME, SchemeForPress(keyboard, keyboard.enterKey));
            Assert.AreEqual(KeyboardControls.SCHEME, SchemeForPress(keyboard, keyboard.numpadEnterKey));
            Assert.IsNull(SchemeForPress(keyboard, keyboard.xKey), "X");
            Assert.IsNull(SchemeForPress(keyboard, keyboard.f1Key), "F1, the dev key that adds a test pad");
        }

        [Test]
        public void SchemeFor_AReleaseInTheEvent_JoinsNothing()
        {
            using (StateEvent.From(keyboard, out InputEventPtr eventPtr))
            {
                keyboard.spaceKey.WriteValueIntoEvent(0f, eventPtr);
                Assert.IsNull(PlayerJoiner.SchemeFor(keyboard.spaceKey, eventPtr));
            }
        }
    }
}

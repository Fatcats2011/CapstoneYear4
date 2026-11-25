using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace DoA.Tests
{
    /// <summary>
    /// The players' controls while the highlight reel plays (ReelControls): the skip is read from each player's own
    /// devices, as the match has the UI map off (playtest, 2026-11-25: the skip did nothing), and boost and pause wait
    /// meanwhile so the A held to skip does nothing else
    /// </summary>
    public class ReelControlsTests
    {
        Gamepad pad;
        Keyboard keys;
        InputActionAsset controls;

        [TearDown]
        public void TearDown()
        {
            if (pad != null && pad.added)
                InputSystem.RemoveDevice(pad);
            if (keys != null && keys.added)
                InputSystem.RemoveDevice(keys);
            pad = null;
            keys = null;
            if (controls != null)
                Object.DestroyImmediate(controls);
        }

        [Test]
        public void SkipHeld_APadsSouthButton()
        {
            pad = InputSystem.AddDevice<Gamepad>();

            Assert.IsFalse(ReelControls.SkipHeld(new InputDevice[] { pad }), "nothing held");
            InputState.Change(pad, new GamepadState().WithButton(GamepadButton.East));
            Assert.IsFalse(ReelControls.SkipHeld(new InputDevice[] { pad }), "B isn't the skip");
            InputState.Change(pad, new GamepadState().WithButton(GamepadButton.South));
            Assert.IsTrue(ReelControls.SkipHeld(new InputDevice[] { pad }), "A");
        }

        [Test]
        public void SkipHeld_SpaceOrEnter_OnTheirKeyboard()
        {
            keys = InputSystem.AddDevice<Keyboard>();

            Assert.IsFalse(ReelControls.SkipHeld(new InputDevice[] { keys }));
            InputState.Change(keys, new KeyboardState(Key.Enter));
            Assert.IsTrue(ReelControls.SkipHeld(new InputDevice[] { keys }), "Enter");
            InputState.Change(keys, new KeyboardState(Key.Space));
            Assert.IsTrue(ReelControls.SkipHeld(new InputDevice[] { keys }), "Space");
        }

        [Test]
        public void SkipHeld_OnlyThePlayersOwnDevices()
        {
            pad = InputSystem.AddDevice<Gamepad>();
            InputState.Change(pad, new GamepadState().WithButton(GamepadButton.South));

            Assert.IsFalse(ReelControls.SkipHeld(new InputDevice[0]), "another player's pad doesn't vote for them");
        }

        [Test]
        public void Take_BoostAndPauseWait_DrivingDoesnt_ThenGiveBringsThemBack()
        {
            controls = Controls();
            controls.FindActionMap("Player").Enable();
            var reel = new ReelControls();

            reel.Take(new[] { controls });
            Assert.IsFalse(controls.FindAction("Player/Boost").enabled, "A doesn't boost the hidden scooter");
            Assert.IsFalse(controls.FindAction("Player/Pause").enabled, "nor pause");
            Assert.IsTrue(controls.FindAction("Player/Steer").enabled, "the rest is left as it was");

            reel.Give();
            Assert.IsTrue(controls.FindAction("Player/Boost").enabled);
            Assert.IsTrue(controls.FindAction("Player/Pause").enabled);
        }

        [Test]
        public void Give_AfterTheGameSwitchedTheMapOff_LeavesItOff()
        {
            controls = Controls();
            controls.FindActionMap("Player").Enable();
            var reel = new ReelControls();

            reel.Take(new[] { controls });
            controls.FindActionMap("Player").Disable(); // a scene load, the menus
            reel.Give();

            Assert.IsFalse(controls.FindAction("Player/Boost").enabled);
            Assert.IsFalse(controls.FindActionMap("Player").enabled);
        }

        [Test]
        public void Take_ActionsAlreadyOff_StayOffAfterGive()
        {
            controls = Controls();
            controls.FindAction("Player/Steer").Enable(); // the map is on, but Boost wasn't
            var reel = new ReelControls();

            reel.Take(new[] { controls });
            reel.Give();

            Assert.IsFalse(controls.FindAction("Player/Boost").enabled, "only what Take switched off comes back");
        }

        // The game's Player map, as far as the reel cares
        static InputActionAsset Controls()
        {
            InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap player = asset.AddActionMap("Player");
            player.AddAction("Steer", InputActionType.Value, "<Gamepad>/leftStick/x");
            player.AddAction("Boost", InputActionType.Button, "<Gamepad>/buttonSouth");
            player.AddAction("Pause", InputActionType.Button, "<Gamepad>/start");
            return asset;
        }
    }
}

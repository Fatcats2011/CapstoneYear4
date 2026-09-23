using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

namespace DoA.Tests
{
    /// <summary>
    /// The Input System reports a lost or regained controller before it updates the player's control scheme match,
    /// so "is this player missing a controller" has to be right at the moment those events fire
    /// </summary>
    public class MissingControllerTests
    {
        Gamepad pad;
        InputActionAsset actions;
        InputUser user;

        [SetUp]
        public void SetUp()
        {
            // A user set up like a PlayerInput's: controller paired, actions associated, Gamepad scheme active
            pad = InputSystem.AddDevice<Gamepad>();
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            actions.AddActionMap("Player").AddAction("Boost", binding: "<Gamepad>/buttonSouth");
            actions.AddControlScheme("Gamepad").WithRequiredDevice("<Gamepad>");
            user = InputUser.PerformPairingWithDevice(pad);
            user.AssociateActionsWithUser(actions);
            user.ActivateControlScheme("Gamepad");
        }

        [TearDown]
        public void TearDown()
        {
            user.UnpairDevicesAndRemoveUser();
            if (pad.added)
                InputSystem.RemoveDevice(pad);
            UnityEngine.Object.DestroyImmediate(actions);
        }

        /// <summary>
        /// Whether the player counts as missing a controller while the Input System reports the given change
        /// </summary>
        bool MissingWhileReporting(InputUserChange change, Action trigger)
        {
            bool? missing = null;
            Action<InputUser, InputUserChange, InputDevice> onChange = (changedUser, userChange, device) =>
            {
                if (changedUser == user && userChange == change)
                    missing = PlayerInstantiate.IsMissingController(changedUser);
            };

            InputUser.onChange += onChange;
            try
            {
                trigger();
            }
            finally
            {
                InputUser.onChange -= onChange;
            }

            Assert.IsTrue(missing.HasValue, change + " was never reported");
            return missing.Value;
        }

        [Test]
        public void WhenItsControllerIsLost_ThePlayerIsMissingIt()
        {
            Assert.IsTrue(MissingWhileReporting(InputUserChange.DeviceLost, () => InputSystem.RemoveDevice(pad)));
        }

        [Test]
        public void WhenItsControllerComesBack_ThePlayerIsNoLongerMissingIt()
        {
            InputSystem.RemoveDevice(pad);

            Assert.IsFalse(MissingWhileReporting(InputUserChange.DeviceRegained, () => InputSystem.AddDevice(pad)));
        }
    }
}

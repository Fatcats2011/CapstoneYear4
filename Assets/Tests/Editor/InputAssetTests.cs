using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The game's input asset has the menu buttons the code wires by name (PlayerUIHandler): without one, its button
    /// silently does nothing (X's Customize on Dispatch did, before UI/West Face was added)
    /// </summary>
    public class InputAssetTests
    {
        static InputActionAsset Asset()
        {
            InputActionAsset asset = Resources.Load<InputActionAsset>(KeyboardControls.ASSET);
            Assert.IsNotNull(asset, "Resources/" + KeyboardControls.ASSET);
            return asset;
        }

        [TestCase(PlayerUIHandler.WEST_ACTION, "<Gamepad>/buttonWest")]
        [TestCase(PlayerUIHandler.LEFT_SHOULDER_ACTION, "<Gamepad>/leftShoulder")]
        [TestCase(PlayerUIHandler.RIGHT_SHOULDER_ACTION, "<Gamepad>/rightShoulder")]
        public void MenuButton_ExistsAsAButton_OnItsPadButton(string actionName, string padPath)
        {
            InputAction action = Asset().FindAction(actionName);

            Assert.IsNotNull(action, actionName);
            Assert.AreEqual(InputActionType.Button, action.type);
            Assert.IsTrue(action.bindings.Any(b => b.path == padPath), actionName + " on " + padPath);
        }

        [Test]
        public void Customize_HasItsKeyboardKey_Q()
        {
            // The asset carries the keyboard scheme itself, so KeyboardControls adds nothing at launch
            InputAction west = Asset().FindAction(PlayerUIHandler.WEST_ACTION);

            Assert.IsNotNull(west);
            Assert.IsTrue(west.bindings.Any(b => b.path == "<Keyboard>/q"), "Q customizes on the keyboard");
        }
    }
}

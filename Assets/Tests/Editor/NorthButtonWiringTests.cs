using NUnit.Framework;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// Y (triangle) is wired to the menus in code (PlayerUIHandler), unless the player's PlayerInput already sends it there
    /// through its own events: then a code wiring would make Y fire twice. Edit mode: nothing starts
    /// </summary>
    public class NorthButtonWiringTests
    {
        readonly TestObjects objects = new TestObjects();
        InputActionAsset actions;

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            if (actions != null)
                Object.DestroyImmediate(actions);
        }

        PlayerInput InputWithNorth(out InputAction north)
        {
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            north = actions.AddActionMap("UI").AddAction("North Face", binding: "<Gamepad>/buttonNorth");
            PlayerInput input = objects.Add<PlayerInput>();
            input.actions = actions;
            input.notificationBehavior = PlayerNotifications.InvokeUnityEvents;
            return input;
        }

        [Test]
        public void WiredByPlayerInput_OnlyWithAPersistentListenerOnThatAction()
        {
            PlayerInput input = InputWithNorth(out InputAction north);
            PlayerUIHandler menus = objects.Add<PlayerUIHandler>();

            PlayerInput.ActionEvent onNorth = new PlayerInput.ActionEvent(north);
            input.actionEvents = new[] { onNorth };
            Assert.IsFalse(PlayerUIHandler.WiredByPlayerInput(input, north), "an event with no listener");

            UnityEventTools.AddPersistentListener(onNorth, new UnityAction<InputAction.CallbackContext>(menus.NorthFaceTrigger));
            Assert.IsTrue(PlayerUIHandler.WiredByPlayerInput(input, north), "the prefab sends Y to the menus itself");

            input.notificationBehavior = PlayerNotifications.SendMessages;
            Assert.IsFalse(PlayerUIHandler.WiredByPlayerInput(input, north), "events that are never invoked don't count");
        }
    }
}

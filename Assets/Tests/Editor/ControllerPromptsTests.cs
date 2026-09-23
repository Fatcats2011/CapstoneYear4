using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class ControllerPromptsTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            if (ControllerPrompts.Exists)
                Object.DestroyImmediate(ControllerPrompts.Instance.gameObject);
        }

        [Test]
        public void ShowReconnect_CoversOnlyThatPlayersViewport()
        {
            Rect secondPlayersView = new Rect(0.5f, 0.5f, 0.5f, 0.5f);

            ControllerPrompts.Instance.ShowReconnect(1, secondPlayersView);

            Assert.IsTrue(ControllerPrompts.Instance.IsReconnectShown(1));
            Assert.AreEqual(secondPlayersView, ControllerPrompts.Instance.ReconnectArea(1));
            Assert.IsFalse(ControllerPrompts.Instance.IsReconnectShown(0));
        }

        [Test]
        public void HideReconnect_HidesTheMessage()
        {
            ControllerPrompts.Instance.ShowReconnect(2, new Rect(0f, 0f, 0.5f, 0.5f));

            ControllerPrompts.Instance.HideReconnect(2);

            Assert.IsFalse(ControllerPrompts.Instance.IsReconnectShown(2));
        }

        [Test]
        public void ReconnectMessage_NamesThePlayer()
        {
            StringAssert.StartsWith("P3 ", ControllerPrompts.ReconnectMessage(2));
        }

        [Test]
        public void Hint_DisappearsWhenItsTimeIsUp()
        {
            ControllerPrompts prompts = ControllerPrompts.Instance;
            prompts.ShowHint(ControllerPrompts.CONNECT_CONTROLLER_HINT, 0f);

            Reflect.Invoke(prompts, "Update");

            Assert.IsFalse(prompts.IsHintShown);
        }

        [Test]
        public void Hint_StaysUntilItsTimeIsUp()
        {
            ControllerPrompts prompts = ControllerPrompts.Instance;
            prompts.ShowHint(ControllerPrompts.CONNECT_CONTROLLER_HINT, 60f);

            Reflect.Invoke(prompts, "Update");

            Assert.IsTrue(prompts.IsHintShown);
        }

        [Test]
        public void HintForDevice_KeyboardAsksForAController()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.AreEqual(ControllerPrompts.CONNECT_CONTROLLER_HINT, ControllerPrompts.HintForDevice(keyboard));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [Test]
        public void HintForDevice_UnknownControllerIsUnsupported()
        {
            Joystick joystick = InputSystem.AddDevice<Joystick>();
            try
            {
                Assert.AreEqual(ControllerPrompts.UNSUPPORTED_CONTROLLER_HINT, ControllerPrompts.HintForDevice(joystick));
            }
            finally
            {
                InputSystem.RemoveDevice(joystick);
            }
        }

        [Test]
        public void AddPlayerReference_BeforeAnyoneJoins_ShowsTheConnectHint()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerInput playerWithoutController = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.AddPlayerReference(playerWithoutController);

            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown);
            Assert.AreEqual(ControllerPrompts.CONNECT_CONTROLLER_HINT, ControllerPrompts.Instance.HintText);
            Assert.AreEqual(0, instantiate.PlayerCount);
        }

        [Test]
        public void AddPlayerReference_KeyboardDuringAMatch_ShowsNoHint()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            Reflect.SetField(instantiate, "allowPlayerSpawn", false); // spawning is off from the loading screen on
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            PlayerInput playerWithoutController = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.AddPlayerReference(playerWithoutController);

            Assert.IsFalse(ControllerPrompts.Exists && ControllerPrompts.Instance.IsHintShown);
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The menus' buttons: Y (triangle) reaches the menus through the UI map's "North Face" action, which each player's
    /// menus subscribe to in code. The Play Mode test uses the real menu scene (about 10 s)
    /// </summary>
    public class MenuButtonsTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        static bool IsGamepadNorth(InputBinding binding)
        {
            return binding.path == "<Gamepad>/buttonNorth" && binding.groups == "Gamepad";
        }

        [Test]
        public void UIMap_HasNorthFace_OnTheGamepadsNorthButton()
        {
            InputActionAsset actions = Resources.Load<InputActionAsset>("CapstoneYear4");
            InputAction north = actions.FindAction(PlayerUIHandler.NORTH_ACTION);

            Assert.IsNotNull(north);
            Assert.AreEqual(InputActionType.Button, north.type);
            Assert.IsTrue(north.bindings.Any(IsGamepadNorth), "on the gamepad's north button (Y, triangle)");
        }

        [UnityTest]
        public IEnumerator NorthButton_InPlayerSelect_ReachesTheMenus()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);

            PlayerUIHandler handler = PlayerInstantiate.Instance.Roster[0].Input.GetComponent<PlayerUIHandler>();
            NorthRecorder recorder = new NorthRecorder();
            handler.NorthFaceEvent.AddListener(recorder.Press);

            // The menus ignore buttons for a moment after they open
            yield return WaitSeconds(0.5f);
            Gamepad pad = TestPlayers.Pads[0];
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.North));
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return WaitSeconds(0.3f);

            Assert.AreEqual(1, recorder.Presses, "Y reaches player select's menus");
        }

        static void Set(MenuInteractions menu, string field, object value)
        {
            typeof(MenuInteractions).GetField(field, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(menu, value);
        }

        // Components added in Edit Mode don't get their serialized UnityEvents made
        static void GiveEvents(PlayerUIHandler ui, InputManager driving)
        {
            ui.NorthFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.EastFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.SouthFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.WestFaceEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.LeftPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.RightPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.UpPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.DownPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            ui.StartPadEvent = new UnityEngine.Events.UnityEvent<bool>();
            driving.StartPadEvent = new UnityEngine.Events.UnityEvent<bool>();
        }

        [Test]
        public void Online_PlayerSelect_SlidersHiddenUntilWest()
        {
            TestObjects objects = new TestObjects();
            try
            {
                GameAuthority.Role = NetworkRole.Host;
                MenuInteractions menu = objects.Add<MenuInteractions>();
                PlayerUIHandler ui = objects.Add<PlayerUIHandler>();
                InputManager driving = objects.Add<InputManager>();
                CustomizationSelector sliders = objects.Add<CustomizationSelector>();
                Set(menu, "uiHandler", ui);
                Set(menu, "drivingHandler", driving);
                Set(menu, "customizationSelector", sliders);
                GiveEvents(ui, driving);

                menu.SwapMenuType(MenuType.PlayerSelect);
                Assert.IsFalse(sliders.gameObject.activeSelf, "hidden when player select opens");

                ui.WestFaceEvent.Invoke(true);
                Assert.IsTrue(sliders.gameObject.activeSelf, "X opens them");

                ui.EastFaceEvent.Invoke(true);
                Assert.IsFalse(sliders.gameObject.activeSelf, "B closes them");

                ui.WestFaceEvent.Invoke(true);
                Assert.IsTrue(sliders.gameObject.activeSelf);
                menu.SwapToPlayerSelect();
                Assert.IsFalse(sliders.gameObject.activeSelf, "back in player select they start closed");
            }
            finally
            {
                GameAuthority.Role = NetworkRole.Offline;
                objects.DestroyAll();
            }
        }

        [Test]
        public void Offline_PlayerSelect_SlidersAlwaysShown_AndXDoesNothing()
        {
            TestObjects objects = new TestObjects();
            try
            {
                MenuInteractions menu = objects.Add<MenuInteractions>();
                PlayerUIHandler ui = objects.Add<PlayerUIHandler>();
                InputManager driving = objects.Add<InputManager>();
                CustomizationSelector sliders = objects.Add<CustomizationSelector>();
                Set(menu, "uiHandler", ui);
                Set(menu, "drivingHandler", driving);
                Set(menu, "customizationSelector", sliders);
                GiveEvents(ui, driving);

                menu.SwapMenuType(MenuType.PlayerSelect);
                Assert.IsTrue(sliders.gameObject.activeSelf);

                ui.WestFaceEvent.Invoke(true);
                Assert.IsTrue(sliders.gameObject.activeSelf, "X changes nothing offline");
            }
            finally
            {
                objects.DestroyAll();
            }
        }

        class NorthRecorder
        {
            public int Presses;

            public void Press(bool pressed)
            {
                Presses++;
            }
        }
    }
}

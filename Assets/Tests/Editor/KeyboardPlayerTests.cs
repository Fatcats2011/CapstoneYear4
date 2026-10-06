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
    /// The keyboard as a player, in the real menu scene: it joins on Space (never on another key, such as the F1 dev key,
    /// which says how to join instead), readies up with Space, and online moves to the seat the host gave with the
    /// keyboard still its own. Each test enters Play Mode (10-20 s). No lambda here captures a local: after EnterPlayMode
    /// even assigning a captured local throws
    /// </summary>
    public class KeyboardPlayerTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        static Keyboard testKeyboard;
        static InputSettings.EditorInputBehaviorInPlayMode focusBehaviour;
        static InputSettings.BackgroundBehavior backgroundBehaviour;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            if (testKeyboard != null && testKeyboard.added)
                InputSystem.RemoveDevice(testKeyboard);
            if (testKeyboard != null)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = focusBehaviour;
                InputSystem.settings.backgroundBehavior = backgroundBehaviour;
            }
            testKeyboard = null;
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

        static IEnumerator ToTheTitleScreen()
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            // In the editor a keyboard reaches the game only while the Game view has focus, and batch-mode tests never have
            // focus (a keyboard, unlike the test controllers, can't run in the background): the test lets every device
            // through, and puts the settings back after
            focusBehaviour = InputSystem.settings.editorInputBehaviorInPlayMode;
            backgroundBehaviour = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testKeyboard = InputSystem.AddDevice<Keyboard>("DoA Test Keyboard");
        }

        static void Press(Key key)
        {
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState(key));
            InputSystem.QueueStateEvent(testKeyboard, new KeyboardState());
        }

        static bool KeyboardIn(int slot)
        {
            PlayerSlot player = PlayerInstantiate.Instance.Roster[slot];
            return player != null && player.IsLocal && player.Input.currentControlScheme == KeyboardControls.SCHEME;
        }

        static bool AnyKeyboardPlayer()
        {
            for (int slot = 0; slot < Constants.MAX_PLAYERS; slot++)
            {
                if (KeyboardIn(slot))
                    return true;
            }
            return false;
        }

        static IEnumerator JoinWithSpace()
        {
            float deadline = Time.realtimeSinceStartup + 10;
            float nextPress = 0;
            while (!KeyboardIn(0) && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(Key.Space);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AKeyboard_JoinsOnSpace_AsPlayerOne()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheTitleScreen();

            yield return JoinWithSpace();

            PlayerInstantiate players = PlayerInstantiate.Instance;
            Assert.AreEqual(1, players.PlayerCount);
            Assert.IsTrue(KeyboardIn(0), "player 1 plays with the keyboard");
            Assert.IsTrue(players.Roster[0].Input.devices.Contains(testKeyboard));
            Assert.IsNull(players.PlayerGamepads[0], "no pad to rumble");
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AKeyboard_DoesntJoinOnOtherKeys()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            yield return ToTheTitleScreen();

            // F1 is the dev key that adds a test pad: the pad may join, the keyboard mustn't
            Press(Key.F1);
            yield return WaitSeconds(1f);
            Assert.IsFalse(AnyKeyboardPlayer(), "F1 doesn't join the keyboard");

            Press(Key.X);
            yield return WaitSeconds(1f);
            Assert.IsFalse(AnyKeyboardPlayer(), "X doesn't join the keyboard");
            Assert.IsTrue(ControllerPrompts.Instance.IsHintShown, "a hint says how to join");
            Assert.AreEqual(ControllerPrompts.KEYBOARD_JOIN_HINT, ControllerPrompts.Instance.HintText);
        }

        [UnityTest]
        public IEnumerator AKeyboardPlayer_ReadiesUp_WithSpace()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            yield return ToTheTitleScreen();
            yield return JoinWithSpace();
            Assert.IsTrue(KeyboardIn(0), "joined");

            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            yield return WaitSeconds(0.5f); // the menus ignore keys for a moment after they open
            Press(Key.Space);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!PlayerInstantiate.Instance.IsReady(0) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(PlayerInstantiate.Instance.IsReady(0), "Space readies up");
        }

        [UnityTest]
        public IEnumerator ThisMachinesKeyboardPlayer_MovesToTheSeatTheHostGave()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            yield return ToTheTitleScreen();
            yield return JoinWithSpace();
            PlayerInstantiate players = PlayerInstantiate.Instance;

            players.SetOnlineSeat(2);
            float deadline = Time.realtimeSinceStartup + 10;
            while (!KeyboardIn(2) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(KeyboardIn(2), "in seat 3, with the keyboard");
            Assert.IsTrue(players.Roster[2].Input.devices.Contains(testKeyboard));
            Assert.IsNull(players.Roster[0], "seat 1 is free");
            Assert.AreEqual(1, players.Roster.LocalCount);

            players.SetOnlineSeat(-1);
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

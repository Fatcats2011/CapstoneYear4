using System.Collections;
using System.Collections.Generic;
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
    /// Plays a real 4-player local match with virtual controllers and fails on any error or exception: main menu,
    /// player select (where a player leaves and joins again), loading screen, opening cutscene, tutorial (skipped the way
    /// the S hotkey skips it), driving, drifting, boosting and the match clock, pausing, and a controller that dies
    /// mid-race, comes back and still drives.
    /// Enters Play Mode and takes a minute or two. Needs a graphics device (tools/run-tests.sh runs Unity with one).
    /// </summary>
    [Category("Smoke")]
    public class LocalMatchSmokeTest
    {
        // Starts in the menu scene, where every manager lives. The splash screen only shows the logo, and loading it
        // from disk in a test replays a stale saved link that sends it back to itself
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME_SCENE = "Design Scene(Main)";
        const float PRESS_EVERY = 0.5f;

        [UnityTest]
        public IEnumerator FourPlayers_PlayFromTheMenuIntoTheFirstWave_WithoutErrors()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();

            // Entering Play Mode reloaded every script: everything below runs in the match
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end, not just the first
            LogCollector log = new LogCollector();
            float deadline;
            float nextPress;

            deadline = Deadline(60);
            while (State() != GameState.Menu)
            {
                FailIfLate(log, deadline, "the main menu");
                yield return null;
            }

            // Player 1's controller joins on the title screen and runs the menus
            TestPlayers.Add();
            deadline = Deadline(10);
            while (Players() < 1)
            {
                FailIfLate(log, deadline, "player 1 to join");
                yield return null;
            }
            Gamepad host = TestPlayers.Pads[0];

            // Player 1 picks Play (menus ignore buttons for a moment after they open, so keep pressing)
            deadline = Deadline(15);
            nextPress = 0;
            while (State() != GameState.PlayerSelect)
            {
                FailIfLate(log, deadline, "player select");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(host, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // Players 2-4 join in player select (the title screen only lets player 1 in)
            for (int joined = 2; joined <= Constants.MAX_PLAYERS; joined++)
            {
                TestPlayers.Add();
                deadline = Deadline(10);
                while (Players() < joined)
                {
                    FailIfLate(log, deadline, "player " + joined + " to join");
                    yield return null;
                }
            }

            // Player 4 leaves player select with B (a player who hasn't readied up may leave), then joins again with A
            Gamepad leaver = TestPlayers.Pads[Constants.MAX_PLAYERS - 1];
            deadline = Deadline(10);
            nextPress = 0;
            while (Players() == Constants.MAX_PLAYERS)
            {
                FailIfLate(log, deadline, "player 4 to leave player select");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(leaver, GamepadButton.East);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }
            deadline = Deadline(10);
            nextPress = 0;
            while (Players() < Constants.MAX_PLAYERS)
            {
                FailIfLate(log, deadline, "player 4 to join again");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(leaver, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // The golden round's leaderboard and the results screen name each player after their scooter's top object
            for (int slot = 0; slot < Constants.MAX_PLAYERS; slot++)
                Assert.AreEqual("P" + (slot + 1), ScooterOf(slot).transform.parent.name, "player " + (slot + 1) + "'s name on the leaderboard");
            Gamepad[] pads = TestPlayers.Pads.ToArray();

            // Everyone readies up; after a 3 second countdown the game scene starts loading
            deadline = Deadline(30);
            nextPress = 0;
            while (State() != GameState.Loading)
            {
                FailIfLate(log, deadline, "the loading screen");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressOnAll(pads, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // Everyone confirms once the loading screen asks, and the game scene opens
            deadline = Deadline(240);
            nextPress = 0;
            while (State() == GameState.Loading || ActiveScene() != GAME_SCENE)
            {
                FailIfLate(log, deadline, "the game scene");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressOnAll(pads, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // The opening cutscene plays by itself, then the tutorial starts
            deadline = Deadline(60);
            while (State() != GameState.Tutorial)
            {
                FailIfLate(log, deadline, "the tutorial");
                yield return null;
            }

            // Skip the tutorial the way the S hotkey does
            foreach (TutorialHandler handler in UnityEngine.Object.FindObjectsOfType<TutorialHandler>())
                handler.TeachHandler(TutorialType.Final);
            OrderManager.Instance.DeleteActiveOrders();

            deadline = Deadline(15);
            while (State() != GameState.Begin)
            {
                FailIfLate(log, deadline, "the first wave");
                yield return null;
            }

            // Driving controls switch on 3 seconds after the tutorial starts
            PlayerInput[] players = UnityEngine.Object.FindObjectsOfType<PlayerInput>();
            Assert.AreEqual(Constants.MAX_PLAYERS, players.Length, "players in the match");
            deadline = Deadline(10);
            while (!players.All(p => p.actions.FindActionMap("Player").enabled))
            {
                FailIfLate(log, deadline, "the driving controls");
                yield return null;
            }

            // Everyone holds the throttle for 4 seconds while the match clock runs
            BallDriving[] scooters = UnityEngine.Object.FindObjectsOfType<BallDriving>();
            Vector3[] startPositions = scooters.Select(s => s.transform.position).ToArray();
            float clockAtStart = OrderManager.Instance.GameTimer;
            float driveUntil = Time.realtimeSinceStartup + 4f;
            while (Time.realtimeSinceStartup < driveUntil)
            {
                foreach (Gamepad pad in pads)
                    InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f, leftStick = new Vector2(0.2f, 0f) });
                yield return null;
            }
            foreach (Gamepad pad in pads)
                InputSystem.QueueStateEvent(pad, new GamepadState());
            for (int i = 0; i < scooters.Length; i++)
                Assert.Greater(Vector3.Distance(startPositions[i], scooters[i].transform.position), 1f, scooters[i].name + " didn't move");
            Assert.Less(OrderManager.Instance.GameTimer, clockAtStart - 2f, "the match clock didn't run");

            // Player 1 drifts (X while steering), then boosts (A)
            BallDriving hostScooter = ScooterOf(0);
            deadline = Deadline(5);
            while (!hostScooter.Drifting)
            {
                FailIfLate(log, deadline, "player 1 to drift");
                InputSystem.QueueStateEvent(host, new GamepadState { rightTrigger = 1f, leftStick = new Vector2(1f, 0f) }.WithButton(GamepadButton.West));
                yield return null;
            }
            InputSystem.QueueStateEvent(host, new GamepadState());
            deadline = Deadline(5);
            nextPress = 0;
            while (!hostScooter.Boosting)
            {
                FailIfLate(log, deadline, "player 1 to boost");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(host, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // Player 1 pauses, then resumes
            Press(host, GamepadButton.Start);
            deadline = Deadline(5);
            while (Time.timeScale != 0f)
            {
                FailIfLate(log, deadline, "the pause menu");
                yield return null;
            }
            deadline = Deadline(10);
            nextPress = 0;
            while (Time.timeScale != 1f)
            {
                FailIfLate(log, deadline, "the game to resume");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(host, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // Player 4's controller dies mid-race: the match pauses and their view asks for a controller.
            // It comes back, the message goes, and player 1 resumes
            Gamepad lost = pads[pads.Length - 1];
            InputSystem.RemoveDevice(lost);
            deadline = Deadline(5);
            while (Time.timeScale != 0f || !ControllerPrompts.Exists || !ControllerPrompts.Instance.IsReconnectShown(3))
            {
                FailIfLate(log, deadline, "the reconnect message for player 4");
                yield return null;
            }
            InputSystem.AddDevice(lost);
            deadline = Deadline(5);
            while (ControllerPrompts.Instance.IsReconnectShown(3))
            {
                FailIfLate(log, deadline, "the reconnect message to clear");
                yield return null;
            }
            deadline = Deadline(10);
            nextPress = 0;
            while (Time.timeScale != 1f)
            {
                FailIfLate(log, deadline, "the game to resume after the reconnect");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(host, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            // Player 4 drives on with the controller that came back
            BallDriving lostScooter = ScooterOf(3);
            deadline = Deadline(5);
            nextPress = 0;
            while (!lostScooter.Boosting)
            {
                FailIfLate(log, deadline, "player 4 to boost after reconnecting");
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    Press(lost, GamepadButton.South);
                    nextPress = Time.realtimeSinceStartup + PRESS_EVERY;
                }
                yield return null;
            }

            Assert.AreEqual(Constants.MAX_PLAYERS, Players(), "players still in the match");
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors during the match:\n\n" + string.Join("\n\n", log.Problems));

            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static float Deadline(float seconds)
        {
            return Time.realtimeSinceStartup + seconds;
        }

        /// <summary>
        /// Fails the test when the match hasn't reached a step in time, saying where it got stuck and what went wrong on the way
        /// </summary>
        static void FailIfLate(LogCollector log, float deadline, string step)
        {
            if (Time.realtimeSinceStartup <= deadline)
                return;

            Assert.Fail("Timed out waiting for " + step + " (game state " + State() + ", scene " + ActiveScene() + ", players " + Players() + ")"
                + (log.Problems.Count > 0 ? "\n\nErrors so far:\n\n" + string.Join("\n\n", log.Problems) : ""));
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static string ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        static int Players()
        {
            return PlayerInstantiate.Instance == null ? 0 : PlayerInstantiate.Instance.PlayerCount;
        }

        static BallDriving ScooterOf(int slot)
        {
            return PlayerInstantiate.Instance.Roster[slot].Player.GetComponentInChildren<BallDriving>();
        }

        /// <summary>
        /// Queues a press and release; the next input update sees a full button press
        /// </summary>
        static void Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        static void PressOnAll(IEnumerable<Gamepad> pads, GamepadButton button)
        {
            foreach (Gamepad pad in pads)
                Press(pad, button);
        }
    }
}

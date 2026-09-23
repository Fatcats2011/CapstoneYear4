# Phase 2B — Input, Authority and Scene-Flow Seams Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The same local game, with the three seams Phase 3 plugs netcode into: scooters, emotes and cameras read a driver interface instead of the controller component; one switch says whether this machine decides the match rules (and the rules only run there); scene changes go through a replaceable scene flow.

**Architecture:**
- `IDriveInput` (steer, throttle, brake, camera X/Y, look behind, plus drift / boost / emote button events) is implemented by `InputManager`. `BallDriving`, `EmoteHandler` and `OrbitalCamera` read their new `DriveInput` property: the prefab's `InputManager` unless something else is set, and it can change at any time (the button listeners move with it). Task 2.2's prefab split and Phase 3 connect an avatar to its driver through it.
- `GameAuthority.Role` (`Offline` / `Host` / `Client`) gives `IsAuthority` and `IsOnline`. `GameManager.SetGameState` becomes a request that only the authority applies; `ApplyGameState` switches the state and fires the existing `OnSwap*` events (Phase 3 clients call it with the host's state). The match rules return early where `!IsAuthority`: waves and order spawns, the golden order's value, pickups and deliveries, steals and clashes, the golden bonus. Every `Time.timeScale` write goes through `GameAuthority.SetTimeScale`, which does nothing online.
- `ISceneFlow` (load the match, load the golden-order scene, return to the menu and its event, the loading screen's confirm) is implemented by the custom `SceneManager`. `SceneFlow.Current` is that loader unless something else is set, and every caller goes through `SceneFlow.Current`.
- Offline, `Role` is `Offline` (authority, not online) and `SceneFlow.Current` is the local loader, so the game plays exactly as before. The smoke test gains checks for drift, boost, the match clock and a reconnected controller's boost to show it.

**Tech Stack:** Unity 2022.3.62f3 · Input System 1.14.0 · Cinemachine 2.x (embedded in `Assets/Cinemachine`) · Unity Test Framework 1.1.33 (EditMode tests) · bash + robocopy test runner.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` — Phase 2 goal ("identical game, but a player's identity and authority no longer live in `PlayerInput`"), Tasks 2.3 (input abstraction), 2.4 (authority seam), 2.5 (scene flow seam) and the §R checklist.

**Not in this plan:** Task 2.2 (split the player prefab): prefab edits need the Unity editor.

**Scope decisions** (where this plan differs from the roadmap's Task 2.4 list, after reading the code on 2026-09-23):
- `OrderHandler.DeliverOrder` gets no gate of its own: its only caller is the dropoff beacon's trigger (`OrderBeacon.OnTriggerStay`), which is gated.
- Spawns (`SpawnManager`) aren't gated. Spawn points are fixed per slot, so there's nothing to decide, and Phase 3 moves scooters on the owner's machine (Task 3.3, owner-authoritative movement), so each machine places its own players. Task 6 adds this to roadmap Task 3.3.
- The respawn point choice (`RespawnManager.GetRespawnPoint`) and the order drop height (`Order.Drop`'s `Random.Range`) stay local until Phase 3. A client can't respawn or drop an order until the host's answer can reach it (Tasks 3.5–3.6: the owner asks, the host picks the point and drops the orders). A gate now would leave a client's scooter in the water. Task 6 adds notes to roadmap Tasks 3.5 and 3.6.
- The other random call the roadmap names, the order shuffle, runs inside `OrderManager.InitGame`, which is gated. `InitGame` and `InitTutorial` are gated as well as `Update` / `InitWave`, because they hand out orders.
- `PlayerInstantiate` keeps listening to the loader's `OnConfirmToLoad` through its scene reference. It subscribes in `OnEnable`, possibly before `SceneManager.Instance` is set, and an online flow has no loading-screen confirm.

**Findings behind the tasks (2026-09-23):**
- Baseline: `bash tools/run-tests.sh` → 97 passed on `d8bc2567` ("Phase 2"). The next commit, `ff4d4b6a`, only changes `docs/steam/steampipe/README.md`.
- The player prefab's `PlayerInput` events call `InputManager`'s callbacks by name (`LeftStickControl`, `LeftTriggerControl`, `RightTriggerControl` ×6 each, `SouthFaceTrigger` ×10, `WestFaceTrigger` ×6, `StartPadTrigger` ×4, `DPadPress`, `RightStickPress`, `RightStickXControl`, `RightStickYControl`). They keep their names; `IDriveInput` is implemented explicitly on top.
- No scene or prefab `UnityEvent` calls the loader's or `GameManager`'s methods (searched every `m_MethodName`), so interface members can be added without renaming anything.
- `BallDriving` subscribes to the drift and boost buttons in `Start` and never unsubscribes. The subscription stays in `Start`: in `OnEnable` it could catch the button press that joins the player before `Start` sets up rumble and sound.
- `OrderManager.Update` reads keys through `DevTools.GetKeyDown` → `Input.GetKeyDown`. Active Input Handling is *Both*, so that returns false in Edit Mode tests instead of throwing.

## Global Constraints

- Unity 2022.3.62f3 LTS; no package upgrades or additions.
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`). Offline play must stay identical and keep passing the roadmap's §R checklist: offline, `GameAuthority.IsAuthority` is true, `IsOnline` is false and `SceneFlow.Current` is the local `SceneManager`.
- Do not edit scenes, prefabs or `ProjectSettings/` — the Unity editor has the project open. Scripts, tests, tools and docs only.
- Don't rename serialized fields, or methods that scenes and prefabs call by name (`InputManager`'s input callbacks). Unity can't serialize interface fields, so the new seams are properties next to the existing serialized references.
- No git commits without explicit approval from your human partner. Work on branch `steam-phase1a` (at `ff4d4b6a`): for now main holds only the Steam API and every other change goes on `steam-phase1a` (the partner's rule). Never check out main in this folder. Each task ends with a checkpoint listing its files.
- EditMode tests live in `Assets/Tests/Editor/` (compiled into `Assembly-CSharp-Editor`), namespace `DoA.Tests`; reuse `TestObjects` / `Reflect` from `TestSupport.cs`.
- Every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`.
- Run tests only through `bash tools/run-tests.sh [filter]` (it syncs the project into the mirror and runs Unity in batch mode with graphics). A compile error prints `NO RESULTS` and the `error CS…` lines.
- Change existing files with the Edit tool only (never `sed -i`: it rewrites CRLF files as LF). These are committed with CRLF (`i/crlf`) and must stay CRLF: `BallDriving.cs`, `GameManager.cs`, `SceneManager.cs`, `PlayerInstantiate.cs`, `MenuInteractions.cs`, `ResultsMenu.cs`, `Order.cs`. New files may be LF. Check with `git ls-files --eol` or byte counts, not `grep $'\r'`.
- Tests that change `GameAuthority.Role`, `SceneFlow.Current`, `Time.timeScale` or a singleton put them back in `[TearDown]`. A leftover `Client` role makes every later test behave like an online client.

## Review Focus

1. Results → Return to menu → a second match: `OnReturnToMenu` now reaches the loader and every `OrderHandler` through `SceneFlow.Current`, so scores reset and the menu loads. (Task 5 `ResultsScreen_ReturnButton_GoesBackToTheMenuThroughTheSceneFlow` and `OrderHandler_EnabledThenDisabled_LeavesTheFlowItListenedTo`. The whole loop has no automated test: the smoke test stops in wave 1.)
2. The last wave ends → half speed for 1.5 s → the golden-order scene loads, back at full speed. (Task 3 `EndOfTheLastWave_SlowsTheClock_OnlyInALocalMatch`, Task 5 `EndOfTheLastWave_LoadsTheGoldenOrderSceneThroughTheSceneFlow`.)
3. A controller dies mid-race and comes back → that player still drives, drifts and boosts. (Task 1: player 4 boosts after reconnecting.)
4. Pause menu → Main Menu → the clock runs again (`PlayerPlay` → `GameAuthority.SetTimeScale(1)`) and the menu loads through the scene flow. (Task 5 `PauseMenu_MainMenu_RestartsTheClockAndGoesBackThroughTheSceneFlow`; the smoke test only resumes.)
5. Two players boost into each other at the same moment offline → both bounce off (one clash each), as before the gate. (Task 4 `OrderHandler_BoostingIntoABoostingPlayer_Clashes_OnlyWhereTheRulesAreDecided`, `Offline` case.)

---

### Task 1: Smoke test checks for drift, boost, the match clock and a reconnected controller

The safety net for Tasks 2–5. These checks pin how the game plays today, so they pass before any refactor; Step 3 proves the boost check can fail.

**Files:**
- Modify: `Assets/Tests/Editor/LocalMatchSmokeTest.cs`

**Interfaces:**
- Consumes: `BallDriving.Drifting`, `BallDriving.Boosting`, `OrderManager.Instance.GameTimer`, `PlayerInstantiate.Instance.Roster[int].Player`.
- Produces: no API. `LocalMatchSmokeTest.FourPlayers_PlayFromTheMenuIntoTheFirstWave_WithoutErrors` now also fails when the match clock stands still, when player 1 can't drift or boost, or when player 4 can't boost after their controller comes back.

- [ ] **Step 1: Add the checks**

The class summary — replace the "driving, pausing, and a controller that dies mid-race and comes back." line with:

```csharp
    /// <summary>
    /// Plays a real 4-player local match with virtual controllers and fails on any error or exception: main menu,
    /// player select, loading screen, opening cutscene, tutorial (skipped the way the S hotkey skips it), driving,
    /// drifting, boosting and the match clock, pausing, and a controller that dies mid-race, comes back and still drives.
    /// Enters Play Mode and takes a minute or two. Needs a graphics device (tools/run-tests.sh runs Unity with one).
    /// </summary>
```

Replace the "Everyone holds the throttle for 4 seconds" block (from that comment through the `Assert.Greater` loop) with:

```csharp
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
```

(The drift boost that follows a long drift adds speed without setting `Boosting`, so only the A button can pass the boost check.)

After the "the game to resume after the reconnect" loop and before `Assert.AreEqual(Constants.MAX_PLAYERS, Players(), "players still in the match");`, add:

```csharp
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
```

After the `Players()` helper, add:

```csharp
        static BallDriving ScooterOf(int slot)
        {
            return PlayerInstantiate.Instance.Roster[slot].Player.GetComponentInChildren<BallDriving>();
        }
```

The waits stay inline rather than in a helper `IEnumerator`: a failed assert inside a nested enumerator can skip the test's error handling.

- [ ] **Step 2: Run the smoke test**

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed, 0 failed, 0 skipped`. If it fails on something the game already does today, ledger the message and rule on it. Never loosen a check as a whole.

- [ ] **Step 3: Prove the boost check catches a broken boost button**

In `Assets/Scripts/Player/BallDriving.cs` `Start`, delete the line `        inp.SouthFaceEvent += BoostFlag; //subscribes to SouthFaceEvent` (Edit tool). Then:

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `FAIL DoA.Tests.LocalMatchSmokeTest.FourPlayers_PlayFromTheMenuIntoTheFirstWave_WithoutErrors: Timed out waiting for player 1 to boost ...`, then `tests: 1 total, 0 passed, 1 failed`.

Put it back and confirm:

```bash
git checkout -- Assets/Scripts/Player/BallDriving.cs
git status --short Assets/Scripts
```

Expected: no output (no script changed).

- [ ] **Step 4: Checkpoint (no commit)**

Changed: `Assets/Tests/Editor/LocalMatchSmokeTest.cs`. Test command for the task: `bash tools/run-tests.sh LocalMatchSmokeTest` → 1 passed.

---

### Task 2: `IDriveInput` — scooter, emotes and camera read a driver, not the controller component

**Files:**
- Create: `Assets/Scripts/Player/IDriveInput.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/InputManager.cs` (implements `IDriveInput`)
- Modify: `Assets/Scripts/Player/BallDriving.cs` (CRLF — `DriveInput`, listeners, `OnDestroy`, `Update`)
- Modify: `Assets/Scripts/Player/EmoteHandler.cs` (`DriveInput`, listeners)
- Modify: `Assets/Scripts/Player/OrbitalCamera.cs` (`DriveInput`, `Update`)
- Create: `Assets/Tests/Editor/DriveInputTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `public interface IDriveInput { float Steer { get; } float Accelerate { get; } float Brake { get; } float CameraX { get; } float CameraY { get; } bool LookBehind { get; } event Action<bool> DriftButton; event Action<bool> BoostButton; event Action<Vector2> EmotePad; }` (global namespace).
  - `InputManager : MonoBehaviour, IDriveInput` (explicit implementation: Steer = left stick, Accelerate = right trigger, Brake = left trigger, CameraX/Y = right stick, LookBehind = right stick press, DriftButton = `WestFaceEvent`, BoostButton = `SouthFaceEvent`, EmotePad = `DPadEvent`).
  - `public IDriveInput DriveInput { get; set; }` on `BallDriving`, `EmoteHandler` and `OrbitalCamera`: the serialized `InputManager` unless set. Setting it moves the button listeners: `BallDriving` listens from `Start` until `OnDestroy`, `EmoteHandler` while enabled.
  - `DoA.Tests.FakeDriveInput : IDriveInput` (settable properties, raisable events) for later tests.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/DriveInputTests.cs`:

```csharp
using System;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A driver whose sticks and triggers a test sets by hand
    /// </summary>
    class FakeDriveInput : IDriveInput
    {
#pragma warning disable 0067 // tests count the subscribers, they never raise these
        public event Action<bool> DriftButton;
        public event Action<bool> BoostButton;
        public event Action<Vector2> EmotePad;
#pragma warning restore 0067

        public float Steer { get; set; }
        public float Accelerate { get; set; }
        public float Brake { get; set; }
        public float CameraX { get; set; }
        public float CameraY { get; set; }
        public bool LookBehind { get; set; }
    }

    public class DriveInputTests
    {
        /// <summary>
        /// Something that listens to a driver's buttons, so a test can count its subscriptions
        /// </summary>
        class Listener
        {
            public void Button(bool pressed) { }
            public void Pad(Vector2 direction) { }
        }

        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
        }

        [Test]
        public void InputManager_AsADriver_ReportsItsSticksAndTriggers()
        {
            InputManager controller = objects.Add<InputManager>();
            Reflect.SetField(controller, "leftStickValue", -0.5f);
            Reflect.SetField(controller, "rightTriggerValue", 0.75f);
            Reflect.SetField(controller, "leftTriggerValue", 0.25f);
            Reflect.SetField(controller, "rightStickXValue", 0.3f);
            Reflect.SetField(controller, "rightStickYValue", -0.4f);
            Reflect.SetField(controller, "rightStickValue", true);

            IDriveInput driver = controller;

            Assert.AreEqual(-0.5f, driver.Steer, "steer = left stick");
            Assert.AreEqual(0.75f, driver.Accelerate, "accelerate = right trigger");
            Assert.AreEqual(0.25f, driver.Brake, "brake = left trigger");
            Assert.AreEqual(0.3f, driver.CameraX, "camera X = right stick X");
            Assert.AreEqual(-0.4f, driver.CameraY, "camera Y = right stick Y");
            Assert.IsTrue(driver.LookBehind, "look behind = right stick press");
        }

        [Test]
        public void InputManager_AsADriver_PassesOnItsDriftBoostAndEmoteButtons()
        {
            InputManager controller = objects.Add<InputManager>();
            IDriveInput driver = controller;
            Listener listener = new Listener();

            driver.DriftButton += listener.Button;
            driver.BoostButton += listener.Button;
            driver.EmotePad += listener.Pad;
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "WestFaceEvent", listener), "drift = X");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "SouthFaceEvent", listener), "boost = A");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "DPadEvent", listener), "emotes = d-pad");

            driver.DriftButton -= listener.Button;
            driver.BoostButton -= listener.Button;
            driver.EmotePad -= listener.Pad;
            Assert.AreEqual(0, Reflect.HandlerCount(controller, "WestFaceEvent", listener)
                + Reflect.HandlerCount(controller, "SouthFaceEvent", listener)
                + Reflect.HandlerCount(controller, "DPadEvent", listener), "after unsubscribing");
        }

        /// <summary>
        /// A scooter with just enough around it to run Start
        /// </summary>
        BallDriving StartedScooter(InputManager controller, IDriveInput driverSetBeforeStart = null)
        {
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>()); // Start looks up this player's gamepad for rumble
            BallDriving scooter = objects.Add<BallDriving>();
            scooter.playerIndex = 1;
            GameObject sphere = objects.NewGameObject("Ball Of Fun");
            sphere.AddComponent<Rigidbody>();
            sphere.AddComponent<SphereCollider>();
            GameObject sparks = objects.NewGameObject("Particle Basket");
            for (int i = 0; i < 6; i++)
                new GameObject("Spark " + i).transform.SetParent(sparks.transform);
            Reflect.SetField(scooter, "sphere", sphere);
            Reflect.SetField(scooter, "particleBasket", sparks.transform);
            Reflect.SetField(scooter, "orderHandler", objects.Add<OrderHandler>());
            Reflect.SetField(scooter, "inp", controller);
            if (driverSetBeforeStart != null)
                scooter.DriveInput = driverSetBeforeStart;

            Reflect.Invoke(scooter, "Start");
            return scooter;
        }

        [Test]
        public void BallDriving_Start_ListensToItsControllersDriftAndBoostButtons()
        {
            InputManager controller = objects.Add<InputManager>();

            BallDriving scooter = StartedScooter(controller);

            Assert.AreSame(controller, scooter.DriveInput);
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "WestFaceEvent", scooter), "drift (X)");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "SouthFaceEvent", scooter), "boost (A)");
        }

        [Test]
        public void BallDriving_DriverChangedWhileDriving_ListensOnlyToTheNewDriver()
        {
            InputManager controller = objects.Add<InputManager>();
            BallDriving scooter = StartedScooter(controller);
            FakeDriveInput online = new FakeDriveInput();

            scooter.DriveInput = online;

            Assert.AreEqual(0, Reflect.HandlerCount(controller, "WestFaceEvent", scooter)
                + Reflect.HandlerCount(controller, "SouthFaceEvent", scooter), "old driver");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "DriftButton", scooter), "new driver's drift");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "BoostButton", scooter), "new driver's boost");
        }

        [Test]
        public void BallDriving_DriverSetBeforeStart_IsTheOneItListensTo()
        {
            FakeDriveInput online = new FakeDriveInput();

            BallDriving scooter = StartedScooter(null, online); // a scooter with no controller of its own (after Task 2.2)

            Assert.AreEqual(1, Reflect.HandlerCount(online, "DriftButton", scooter), "drift");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "BoostButton", scooter), "boost");
        }

        [Test]
        public void BallDriving_Destroyed_StopsListeningToItsDriver()
        {
            FakeDriveInput online = new FakeDriveInput();
            BallDriving scooter = StartedScooter(null, online);

            Reflect.Invoke(scooter, "OnDestroy");

            Assert.AreEqual(0, Reflect.HandlerCount(online, "DriftButton", scooter)
                + Reflect.HandlerCount(online, "BoostButton", scooter));
        }

        [Test]
        public void EmoteHandler_DriverChangedWhileEnabled_ListensOnlyToTheNewDriver()
        {
            InputManager controller = objects.Add<InputManager>();
            EmoteHandler emotes = objects.Add<EmoteHandler>();
            Reflect.SetField(emotes, "input", controller);
            Reflect.Invoke(emotes, "OnEnable");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "DPadEvent", emotes), "its controller at first");

            FakeDriveInput online = new FakeDriveInput();
            emotes.DriveInput = online;
            Assert.AreEqual(0, Reflect.HandlerCount(controller, "DPadEvent", emotes), "old driver");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "EmotePad", emotes), "new driver");

            Reflect.Invoke(emotes, "OnDisable");
            Assert.AreEqual(0, Reflect.HandlerCount(online, "EmotePad", emotes), "after disabling");
        }

        [Test]
        public void EmoteHandler_DriverSetBeforeEnable_IsTheOneItListensTo()
        {
            EmoteHandler emotes = objects.Add<EmoteHandler>();
            FakeDriveInput online = new FakeDriveInput();

            emotes.DriveInput = online;
            Reflect.Invoke(emotes, "OnEnable");

            Assert.AreEqual(1, Reflect.HandlerCount(online, "EmotePad", emotes));
        }

        /// <summary>
        /// An orbital camera with its main and icon virtual cameras, driven by a test driver
        /// </summary>
        OrbitalCamera CameraRig(FakeDriveInput driver, out CinemachineOrbitalTransposer mainOrbit, out CinemachineOrbitalTransposer iconOrbit)
        {
            OrbitalCamera rig = objects.Add<OrbitalCamera>();
            CinemachineVirtualCamera main = objects.Add<CinemachineVirtualCamera>();
            CinemachineVirtualCamera icon = objects.Add<CinemachineVirtualCamera>();
            mainOrbit = main.AddCinemachineComponent<CinemachineOrbitalTransposer>();
            iconOrbit = icon.AddCinemachineComponent<CinemachineOrbitalTransposer>();
            Reflect.SetField(rig, "virtualCameraMain", main);
            Reflect.SetField(rig, "virtualCameraIcon", icon);
            Reflect.SetField(rig, "mainOrb", mainOrbit);
            Reflect.SetField(rig, "iconOrb", iconOrbit);
            Reflect.SetField(rig, "CameraFocus", objects.NewGameObject("Camera Focus"));
            rig.DriveInput = driver;
            return rig;
        }

        [Test]
        public void OrbitalCamera_DriverPushesTheRightStickRight_TurnsBothCameras()
        {
            OrbitalCamera rig = CameraRig(new FakeDriveInput { CameraX = 1f }, out CinemachineOrbitalTransposer mainOrbit, out CinemachineOrbitalTransposer iconOrbit);

            Reflect.Invoke(rig, "Update");

            Assert.Greater(mainOrbit.m_XAxis.Value, 0f, "main camera");
            Assert.AreEqual(mainOrbit.m_XAxis.Value, iconOrbit.m_XAxis.Value, "icon camera follows");
        }

        [Test]
        public void OrbitalCamera_DriverPressesTheRightStick_LooksBehind()
        {
            OrbitalCamera rig = CameraRig(new FakeDriveInput { LookBehind = true }, out CinemachineOrbitalTransposer mainOrbit, out CinemachineOrbitalTransposer iconOrbit);

            Reflect.Invoke(rig, "Update");

            Assert.AreEqual(-180f, mainOrbit.m_XAxis.Value, "main camera");
            Assert.AreEqual(-180f, iconOrbit.m_XAxis.Value, "icon camera");
        }
    }
}
```

```bash
bash tools/newmeta.sh Assets/Tests/Editor/DriveInputTests.cs
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `bash tools/run-tests.sh DriveInputTests`
Expected: `NO RESULTS`, with `error CS0246: The type or namespace name 'IDriveInput' could not be found`.

- [ ] **Step 3: Add the interface**

`Assets/Scripts/Player/IDriveInput.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// What a player's scooter, emotes and camera read from whoever drives them. InputManager implements it for a controller
/// on this machine. Online play (Phase 3) connects each avatar to its driver at runtime through the components' DriveInput
/// </summary>
public interface IDriveInput
{
    /// <summary>Steering, -1 (left) to 1 (right): the left stick</summary>
    float Steer { get; }

    /// <summary>Throttle, 0 to 1: the right trigger</summary>
    float Accelerate { get; }

    /// <summary>Brake, then reverse, 0 to 1: the left trigger</summary>
    float Brake { get; }

    /// <summary>Camera turn, -1 to 1: the right stick's X</summary>
    float CameraX { get; }

    /// <summary>Camera height, -1 to 1: the right stick's Y</summary>
    float CameraY { get; }

    /// <summary>Looking behind: the right stick pressed in</summary>
    bool LookBehind { get; }

    /// <summary>The drift button (X on Xbox): true when pressed, false when released</summary>
    event Action<bool> DriftButton;

    /// <summary>The boost button (A on Xbox): true when pressed, false when released</summary>
    event Action<bool> BoostButton;

    /// <summary>The d-pad pushed one way, for emotes: (0, 1) up, (1, 0) right, (0, -1) down, (-1, 0) left</summary>
    event Action<Vector2> EmotePad;
}
```

```bash
bash tools/newmeta.sh Assets/Scripts/Player/IDriveInput.cs
```

- [ ] **Step 4: `InputManager` implements it**

`Assets/Scripts/Player/InputManager.cs` — the class line becomes:

```csharp
public class InputManager : MonoBehaviour, IDriveInput
```

and after `StartPadTrigger`, before the class's closing brace, add:

```csharp

    // What the scooter, emotes and camera read (IDriveInput). The callbacks above keep their names: the player prefab's
    // PlayerInput events call them
    float IDriveInput.Steer { get { return leftStickValue; } }
    float IDriveInput.Accelerate { get { return rightTriggerValue; } }
    float IDriveInput.Brake { get { return leftTriggerValue; } }
    float IDriveInput.CameraX { get { return rightStickXValue; } }
    float IDriveInput.CameraY { get { return rightStickYValue; } }
    bool IDriveInput.LookBehind { get { return rightStickValue; } }

    event Action<bool> IDriveInput.DriftButton { add { WestFaceEvent += value; } remove { WestFaceEvent -= value; } }
    event Action<bool> IDriveInput.BoostButton { add { SouthFaceEvent += value; } remove { SouthFaceEvent -= value; } }
    event Action<Vector2> IDriveInput.EmotePad { add { DPadEvent += value; } remove { DPadEvent -= value; } }
```

- [ ] **Step 5: `BallDriving` reads its driver**

`Assets/Scripts/Player/BallDriving.cs` (CRLF). After `    [SerializeField] private InputManager inp;` add:

```csharp
    private IDriveInput driveInput; // set when something other than this prefab's controller drives the scooter
    private bool listening; // from Start until the scooter is destroyed: drift and boost follow the driver's buttons
```

After the `SetGamepad` method add:

```csharp

    /// <summary>
    /// Who drives this scooter: the controller on this machine unless something else is set. Can change at any time
    /// </summary>
    public IDriveInput DriveInput
    {
        get { return driveInput ?? inp; }
        set
        {
            if (listening)
                StopListening();
            driveInput = value;
            if (listening)
                StartListening();
        }
    }

    private void StartListening()
    {
        IDriveInput driver = DriveInput;
        if (driver == null)
            return;

        driver.DriftButton += DriftFlag;
        driver.BoostButton += BoostFlag;
    }

    private void StopListening()
    {
        IDriveInput driver = DriveInput;
        if (driver == null)
            return;

        driver.DriftButton -= DriftFlag;
        driver.BoostButton -= BoostFlag;
    }

    private void OnDestroy()
    {
        if (listening)
            StopListening();
        listening = false;
    }
```

In `Start`, replace

```csharp
        inp.WestFaceEvent += DriftFlag; //subscribes to WestFaceEvent
        inp.SouthFaceEvent += BoostFlag; //subscribes to SouthFaceEvent
```

with

```csharp
        listening = true;
        StartListening(); // drift on the drift button, boost on the boost button
```

In `Update`, replace

```csharp
        leftStick = inp.LeftStickValue;
        leftTrig = inp.LeftTriggerValue;
        rightTrig = inp.RightTriggerValue;
```

with

```csharp
        IDriveInput driver = DriveInput;
        leftStick = driver.Steer;
        leftTrig = driver.Brake;
        rightTrig = driver.Accelerate;
```

- [ ] **Step 6: `EmoteHandler` reads its driver**

`Assets/Scripts/Player/EmoteHandler.cs`. After `    [SerializeField] private InputManager input;` add:

```csharp
    private IDriveInput driveInput; // set when something other than this prefab's controller drives the player
    private bool listening; // while enabled: emotes follow the driver's d-pad
```

Replace `OnEnable` and `OnDisable` with:

```csharp
    private void OnEnable()
    {
        listening = true;
        StartListening();
    }
    private void OnDisable()
    {
        StopListening();
        listening = false;
    }

    /// <summary>
    /// Whose d-pad shows emotes: the controller on this machine unless something else is set. Can change at any time
    /// </summary>
    public IDriveInput DriveInput
    {
        get { return driveInput ?? input; }
        set
        {
            if (listening)
                StopListening();
            driveInput = value;
            if (listening)
                StartListening();
        }
    }

    private void StartListening()
    {
        IDriveInput driver = DriveInput;
        if (driver != null)
            driver.EmotePad += Emote;
    }

    private void StopListening()
    {
        IDriveInput driver = DriveInput;
        if (driver != null)
            driver.EmotePad -= Emote;
    }
```

- [ ] **Step 7: `OrbitalCamera` reads its driver**

`Assets/Scripts/Player/OrbitalCamera.cs`. After `    [SerializeField] InputManager inputManager;` add:

```csharp
    IDriveInput driveInput; // set when something other than this prefab's controller turns the camera

    /// <summary>
    /// Whose right stick turns this camera: the controller on this machine unless something else is set
    /// </summary>
    public IDriveInput DriveInput { get { return driveInput ?? inputManager; } set { driveInput = value; } }
```

`Update` reads the driver. Replace its start, from `void Update()` through `else if(inputManager.RightStickValue)`, with (the look-behind block under it stays as it is):

```csharp
    void Update()
    {
        IDriveInput driver = DriveInput;
        fovValue = Mathf.Lerp(fovValue, passInFOV, changeSpeed);

        // Custom joystick camera aim
        if (!driver.LookBehind && reverseCamera == false)
        {
            realXAxis = RangeMutations.Map_Linear(driver.CameraX, -1, 1, -maxXAngle, maxXAngle);
            realYAxis = RangeMutations.Map_Linear(driver.CameraY, -1, 1, yAngleMinMax.x, yAngleMinMax.y);

            smoothXAxis = Mathf.Lerp(smoothXAxis, realXAxis, smoothSpeedValue);
            smoothYAxis = Mathf.Lerp(smoothYAxis, realYAxis, smoothSpeedValue);

            mainOrb.m_XAxis.Value = smoothXAxis;
            iconOrb.m_XAxis.Value = smoothXAxis;

            CameraFocus.transform.localPosition = new Vector3(CameraFocus.transform.localPosition.x, smoothYAxis, CameraFocus.transform.localPosition.z);

            virtualCameraMain.m_Lens.FieldOfView = fovValue;
            virtualCameraIcon.m_Lens.FieldOfView = fovValue;

        }
        // Reset look behind
        if (!driver.LookBehind && reverseCamera == true)
        {
            reverseCamera = false;
            smoothXAxis = 0f;
        }
        // Static look behind
        else if(driver.LookBehind)
```

```bash
grep -n "inputManager\.\|inp\.\|input\.DPad" Assets/Scripts/Player/OrbitalCamera.cs Assets/Scripts/Player/BallDriving.cs Assets/Scripts/Player/EmoteHandler.cs
```

Expected: no output.

- [ ] **Step 8: Run the tests to see them pass**

Run: `bash tools/run-tests.sh DriveInputTests`
Expected: `tests: 10 total, 10 passed, 0 failed, 0 skipped`.

- [ ] **Step 9: Run the whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 107 total, 107 passed, 0 failed, 0 skipped`. The smoke test drives, drifts, boosts and reconnects through `DriveInput` now.

- [ ] **Step 10: Checkpoint (no commit)**

Created: `IDriveInput.cs`, `DriveInputTests.cs` (+ metas). Changed: `InputManager.cs`, `BallDriving.cs` (still CRLF: `git ls-files --eol` shows `w/crlf`, and a byte count shows no bare LF), `EmoteHandler.cs`, `OrbitalCamera.cs`.

---

### Task 3: `GameAuthority` — who decides, game-state requests and a clock only local matches change

**Files:**
- Create: `Assets/Scripts/Management/GameAuthority.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Management/GameManager.cs` (CRLF — `SetGameState` / `ApplyGameState`)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF — pause / resume clock)
- Modify: `Assets/Scripts/Management/OrderManager.cs` (end-of-round slow motion)
- Create: `Assets/Tests/Editor/GameAuthorityTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `public enum NetworkRole { Offline, Host, Client }` (global namespace).
  - `public static class GameAuthority { static NetworkRole Role { get; set; } /* Offline by default */ static bool IsAuthority /* Role != Client */; static bool IsOnline /* Role != Offline */; static void SetTimeScale(float scale) /* only when !IsOnline */ }`.
  - `GameManager.SetGameState(GameState)` does nothing where `!GameAuthority.IsAuthority`; new `public void ApplyGameState(GameState)` holds the old body (clock back to 1 through `GameAuthority.SetTimeScale`, state, events).
  - No script writes `Time.timeScale` except `GameAuthority.cs` (a test scans for it).

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/GameAuthorityTests.cs`:

```csharp
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class GameAuthorityTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            Time.timeScale = 1f;
            objects.DestroyAll();
        }

        [TestCase(NetworkRole.Offline, true, false)]
        [TestCase(NetworkRole.Host, true, true)]
        [TestCase(NetworkRole.Client, false, true)]
        public void Role_SaysWhoDecidesTheRulesAndWhetherTheMatchIsOnline(NetworkRole role, bool decidesRules, bool online)
        {
            GameAuthority.Role = role;

            Assert.AreEqual(decidesRules, GameAuthority.IsAuthority, "decides the rules");
            Assert.AreEqual(online, GameAuthority.IsOnline, "online");
        }

        [Test]
        public void SetTimeScale_InALocalMatch_ChangesTheClock()
        {
            GameAuthority.SetTimeScale(0f);

            Assert.AreEqual(0f, Time.timeScale);
        }

        [TestCase(NetworkRole.Host)]
        [TestCase(NetworkRole.Client)]
        public void SetTimeScale_Online_LeavesTheSharedClockRunning(NetworkRole role)
        {
            GameAuthority.Role = role;

            GameAuthority.SetTimeScale(0f);

            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void GameplayScripts_ChangeTheClockOnlyThroughGameAuthority()
        {
            string[] offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/OUTDATED/") && Path.GetFileName(f) != "GameAuthority.cs")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"Time\.timeScale\s*=(?!=)"))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void SetGameState_InALocalMatch_SwitchesTheStateAndTellsListeners()
        {
            GameManager game = objects.Add<GameManager>();
            int optionsOpened = 0;
            game.OnSwapOptions += () => optionsOpened++;

            game.SetGameState(GameState.Options);

            Assert.AreEqual(GameState.Options, game.MainState);
            Assert.AreEqual(1, optionsOpened);
        }

        [Test]
        public void SetGameState_OnAnOnlineClient_ChangesNothing()
        {
            GameAuthority.Role = NetworkRole.Client;
            GameManager game = objects.Add<GameManager>();
            int optionsOpened = 0;
            game.OnSwapOptions += () => optionsOpened++;

            game.SetGameState(GameState.Options);

            Assert.AreEqual(GameState.Default, game.MainState);
            Assert.AreEqual(0, optionsOpened);
        }

        [Test]
        public void ApplyGameState_OnAnOnlineClient_SwitchesToTheHostsState()
        {
            GameAuthority.Role = NetworkRole.Client;
            GameManager game = objects.Add<GameManager>();
            int optionsOpened = 0;
            game.OnSwapOptions += () => optionsOpened++;

            game.ApplyGameState(GameState.Options);

            Assert.AreEqual(GameState.Options, game.MainState);
            Assert.AreEqual(1, optionsOpened);
        }

        [TestCase(NetworkRole.Offline, 0.5f)]
        [TestCase(NetworkRole.Host, 1f)]
        public void EndOfTheLastWave_SlowsTheClock_OnlyInALocalMatch(NetworkRole role, float expectedTimeScale)
        {
            GameAuthority.Role = role;
            OrderManager orders = objects.Add<OrderManager>();

            IEnumerator linger = (IEnumerator)Reflect.Invoke(orders, "PostGameClarity", false);
            linger.MoveNext(); // up to the half-speed pause

            Assert.AreEqual(expectedTimeScale, Time.timeScale);
        }
    }
}
```

```bash
bash tools/newmeta.sh Assets/Tests/Editor/GameAuthorityTests.cs
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `bash tools/run-tests.sh GameAuthorityTests`
Expected: `NO RESULTS`, with `error CS0103: The name 'GameAuthority' does not exist in the current context` (and `NetworkRole`).

- [ ] **Step 3: Add `GameAuthority`**

`Assets/Scripts/Management/GameAuthority.cs`:

```csharp
using UnityEngine;

/// <summary>
/// This machine's part in a match: a local (split-screen) match, or the host or a client of an online one (Phase 3)
/// </summary>
public enum NetworkRole { Offline, Host, Client }

/// <summary>
/// Who decides a match's rules: game states, waves and order spawns, pickups and deliveries, steals and clashes, scores.
/// In a local match this machine does. Online (Phase 3) the host does, and clients show what the host decided
/// </summary>
public static class GameAuthority
{
    /// <summary>
    /// This machine's part in the match. Offline until online play starts a session, and again after it ends
    /// </summary>
    public static NetworkRole Role { get; set; } = NetworkRole.Offline;

    /// <summary>
    /// Whether this machine decides the rules
    /// </summary>
    public static bool IsAuthority { get { return Role != NetworkRole.Client; } }

    /// <summary>
    /// Whether the match is played over the network
    /// </summary>
    public static bool IsOnline { get { return Role != NetworkRole.Offline; } }

    /// <summary>
    /// Pauses or slows the game clock in a local match. Online, everyone shares one clock, so this does nothing
    /// </summary>
    public static void SetTimeScale(float scale)
    {
        if (IsOnline)
            return;

        Time.timeScale = scale;
    }
}
```

```bash
bash tools/newmeta.sh Assets/Scripts/Management/GameAuthority.cs
```

- [ ] **Step 4: Game states — the authority applies requests, clients apply the host's**

`Assets/Scripts/Management/GameManager.cs` (CRLF). Replace

```csharp
    ///<summary>
    /// Allows swapping of game states, and also invokes right event when swapping
    ///</summary>
    public void SetGameState(GameState state)
    {
        Time.timeScale = 1f;

        mainState = state;
```

with

```csharp
    ///<summary>
    /// Asks for a game state. The authority (this machine in a local match, the host online) switches to it; an online
    /// client ignores the request and follows the host's state instead
    ///</summary>
    public void SetGameState(GameState state)
    {
        if (!GameAuthority.IsAuthority)
            return;

        ApplyGameState(state);
    }

    ///<summary>
    /// Switches to a game state and invokes its events. Online clients call this with the state the host sent
    ///</summary>
    public void ApplyGameState(GameState state)
    {
        GameAuthority.SetTimeScale(1f);

        mainState = state;
```

The rest of the old body (the `OnSwapAnything` call and the `switch`) stays as it is, now inside `ApplyGameState`.

- [ ] **Step 5: Pause, resume and slow motion go through `GameAuthority`**

`Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF). In `PlayerPause`, replace `        Time.timeScale = 0f;` with `        GameAuthority.SetTimeScale(0f);`. In `PlayerPlay`, replace `        Time.timeScale = 1f;` with `        GameAuthority.SetTimeScale(1f);`. (The `Time.timeScale == 0f` read in `OnPlayerControllerLost` stays.)

`Assets/Scripts/Management/OrderManager.cs`, `PostGameClarity`: replace

```csharp
        Time.timeScale = 0.5f;
        yield return new WaitForSeconds(postGameLinger/2);
        Time.timeScale = 1.0f;
```

with

```csharp
        GameAuthority.SetTimeScale(0.5f);
        yield return new WaitForSeconds(postGameLinger/2);
        GameAuthority.SetTimeScale(1.0f);
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh GameAuthorityTests`
Expected: `tests: 12 total, 12 passed, 0 failed, 0 skipped`.

- [ ] **Step 7: Run the whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 119 total, 119 passed, 0 failed, 0 skipped`. Every state change in the smoke test now goes through the authority check, and pausing still stops the clock offline.

- [ ] **Step 8: Checkpoint (no commit)**

Created: `GameAuthority.cs`, `GameAuthorityTests.cs` (+ metas). Changed: `GameManager.cs`, `PlayerInstantiate.cs` (both still CRLF), `OrderManager.cs`.

---

### Task 4: The match rules run only where they're decided

**Files:**
- Modify: `Assets/Scripts/Management/OrderManager.cs` (`Update`, `InitTutorial`, `InitGame`, `InitWave`)
- Modify: `Assets/Scripts/World/OrderBeacon.cs` (`OnTriggerStay`)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`OnTriggerEnter`, `AttemptSteal`, new `AwardGoldenBonus`)
- Modify: `Assets/Scripts/World/Order.cs` (CRLF — `EraseOrder` uses `AwardGoldenBonus`)
- Create: `Assets/Tests/Editor/AuthorityGateTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `GameAuthority.Role`, `GameAuthority.IsAuthority`, `NetworkRole` (Task 3).
- Produces: `public void OrderHandler.AwardGoldenBonus(int finalOrderValue)` (adds `finalOrderValue - Golden` to the score, authority only). Each gate is the same first statement, `if (!GameAuthority.IsAuthority) return;`, with a one-line comment saying who decides online.

Every test below runs in 3 roles: `Offline` and `Host` must behave as today, `Client` must change nothing.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/AuthorityGateTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The match rules run only where GameAuthority says this machine decides them: always in a local match, on the host online
    /// </summary>
    public class AuthorityGateTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            Reflect.SetSingleton<TutorialManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
        }

        /// <summary>
        /// An order manager whose match has one wave and no scene orders
        /// </summary>
        OrderManager OrdersWithOneWave()
        {
            OrderManager orders = objects.Add<OrderManager>();
            Reflect.SetField(orders, "maxEasy", new[] { 1 });
            Reflect.SetField(orders, "maxMedium", new[] { 1 });
            Reflect.SetField(orders, "maxHard", new[] { 1 });
            Reflect.SetField(orders, "normalOrders", new List<Order>());
            return orders;
        }

        /// <summary>
        /// A player as the triggers see them: a root with the orders handler (and its scooter) and the ball as children
        /// </summary>
        OrderHandler NewPlayer(string name, out Collider ball)
        {
            GameObject player = objects.NewGameObject(name);
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            Reflect.SetField(handler, "ball", control.AddComponent<BallDriving>());
            GameObject sphere = new GameObject("Ball Of Fun");
            sphere.transform.SetParent(player.transform);
            ball = sphere.AddComponent<SphereCollider>();
            return handler;
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_InitWave_StartsTheWaveClock_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderManager orders = OrdersWithOneWave();

            orders.InitWave();

            Assert.AreEqual(decides ? 20f : 0f, orders.WaveTimer); // 20 s is the component's default wave length
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_InitGame_StartsTheGame_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderManager orders = OrdersWithOneWave();

            Reflect.Invoke(orders, "InitGame");

            Assert.AreEqual(decides, orders.GameStarted);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_InitTutorial_StartsTheTutorial_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            TutorialManager tutorial = objects.Add<TutorialManager>();
            Reflect.SetSingleton(tutorial);
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>()); // nobody has joined, so no tutorial orders are handed out
            tutorial.ShouldTutorialize = false;
            OrderManager orders = OrdersWithOneWave();

            Reflect.Invoke(orders, "InitTutorial");

            Assert.AreEqual(decides, tutorial.ShouldTutorialize);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderManager_Update_GrowsTheHeldGoldenOrdersValue_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderManager orders = OrdersWithOneWave();
            Order golden = objects.Add<Order>();
            Reflect.SetField(golden, "playerHolding", objects.Add<OrderHandler>());
            Reflect.SetField(orders, "finalOrder", golden);
            Reflect.SetField(orders, "finalOrderActive", true);
            Reflect.SetField(orders, "goldTimer", 1f); // a second has passed since the last raise
            orders.FinalOrderValue = 50;

            Reflect.Invoke(orders, "Update");

            Assert.AreEqual(decides ? 51 : 50, orders.FinalOrderValue);
        }

        [Test]
        public void OrderBeacon_PlayerInTheLight_OnAnOnlineClient_NobodyPicksUpTheOrder()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler player = NewPlayer("Player 1", out Collider ball);
            Reflect.SetField(player, "canTakeOrder", true);
            OrderBeacon beacon = objects.Add<OrderBeacon>();
            Reflect.SetField(beacon, "order", objects.Add<Order>());
            Reflect.SetField(beacon, "canInteract", true);

            // Offline this is a pickup, which goes on to the order's scene objects that a test doesn't build
            Assert.DoesNotThrow(() => Reflect.Invoke(beacon, "OnTriggerStay", ball));
            Assert.IsFalse(player.HasOrder);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderHandler_TouchingAnotherPlayer_IsNoted_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderHandler me = NewPlayer("Player 1", out _);
            OrderHandler other = NewPlayer("Player 2", out Collider otherBall);

            Reflect.Invoke(me, "OnTriggerEnter", otherBall);

            Assert.AreEqual(decides ? me : null, other.PlayerTouching);
        }

        [TestCase(NetworkRole.Offline, 2)]
        [TestCase(NetworkRole.Host, 2)]
        [TestCase(NetworkRole.Client, 0)]
        public void OrderHandler_BoostingIntoABoostingPlayer_Clashes_OnlyWhereTheRulesAreDecided(NetworkRole role, int expectedClashes)
        {
            GameAuthority.Role = role;
            OrderHandler me = NewPlayer("Player 1", out _);
            OrderHandler other = NewPlayer("Player 2", out _);
            Reflect.SetField(other.GetComponent<BallDriving>(), "boosting", true);
            me.PlayerTouching = other;
            int clashes = 0;
            me.Clash += _ => clashes++;
            other.Clash += _ => clashes++;

            me.AttemptSteal();

            Assert.AreEqual(expectedClashes, clashes);
        }

        [TestCase(NetworkRole.Offline, true)]
        [TestCase(NetworkRole.Host, true)]
        [TestCase(NetworkRole.Client, false)]
        public void OrderHandler_AwardGoldenBonus_AddsWhatTheGoldenOrderEarned_OnlyWhereTheRulesAreDecided(NetworkRole role, bool decides)
        {
            GameAuthority.Role = role;
            OrderHandler holder = NewPlayer("Player 1", out _);
            holder.Score = 200;

            holder.AwardGoldenBonus(80); // the golden order grew from 50 to 80 while it was held

            Assert.AreEqual(decides ? 230 : 200, holder.Score);
        }
    }
}
```

```bash
bash tools/newmeta.sh Assets/Tests/Editor/AuthorityGateTests.cs
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `bash tools/run-tests.sh AuthorityGateTests`
Expected: `NO RESULTS`, with `error CS1061: 'OrderHandler' does not contain a definition for 'AwardGoldenBonus'`.

- [ ] **Step 3: Add `AwardGoldenBonus` and use it**

`Assets/Scripts/Player/OrderHandler.cs`, after `DeliverOrder`:

```csharp

    /// <summary>
    /// Adds what the golden order earned while it was held, on top of its base value (delivering it already scored that)
    /// </summary>
    /// <param name="finalOrderValue">What the golden order was worth when it was delivered</param>
    public void AwardGoldenBonus(int finalOrderValue)
    {
        score += finalOrderValue - (int)Constants.OrderValue.Golden;
    }
```

`Assets/Scripts/World/Order.cs` (CRLF), in `EraseOrder`, replace

```csharp
                playerHolding.Score += OrderManager.Instance.FinalOrderValue - (int)Constants.OrderValue.Golden; // beacon code already adds base gold value
```

with

```csharp
                playerHolding.AwardGoldenBonus(OrderManager.Instance.FinalOrderValue); // delivering already added the base gold value
```

- [ ] **Step 4: Run the tests to see the gates are missing**

Run: `bash tools/run-tests.sh AuthorityGateTests`
Expected: the 8 `Client` cases fail, e.g. `OrderManager_InitWave_StartsTheWaveClock_OnlyWhereTheRulesAreDecided(Client,False): Expected: 0.0f But was: 20.0f` and `OrderBeacon_PlayerInTheLight_OnAnOnlineClient_NobodyPicksUpTheOrder: Expected: No Exception to be thrown But was: <System.NullReferenceException>`; the 14 `Offline` / `Host` cases pass. `tests: 22 total, 14 passed, 8 failed, 0 skipped`.

- [ ] **Step 5: Add the gates**

Each gate is the first statement of its method.

`Assets/Scripts/Management/OrderManager.cs`:

```csharp
    private void Update()
    {
        // Online, the host runs the waves, the order spawns and the golden order's value; clients show what it sends
        if (!GameAuthority.IsAuthority)
            return;

```

```csharp
    private void InitTutorial()
    {
        // Online, the host hands out the tutorial orders
        if (!GameAuthority.IsAuthority)
            return;

```

```csharp
    private void InitGame()
    {
        // Online, the host picks and spawns the orders
        if (!GameAuthority.IsAuthority)
            return;

```

```csharp
    public void InitWave()
    {
        // Online, the host runs the waves
        if (!GameAuthority.IsAuthority)
            return;

```

`Assets/Scripts/World/OrderBeacon.cs`:

```csharp
    private void OnTriggerStay(Collider other)
    {
        // Online, only the host decides pickups and deliveries
        if (!GameAuthority.IsAuthority)
            return;

```

`Assets/Scripts/Player/OrderHandler.cs`, in `AttemptSteal` and `OnTriggerEnter`:

```csharp
        // Online, only the host decides steals and clashes
        if (!GameAuthority.IsAuthority)
            return;

```

and in `AwardGoldenBonus`, before the `score +=` line:

```csharp
        // Online, only the host changes scores
        if (!GameAuthority.IsAuthority)
            return;

```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh AuthorityGateTests`
Expected: `tests: 22 total, 22 passed, 0 failed, 0 skipped`.

- [ ] **Step 7: Run the whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 141 total, 141 passed, 0 failed, 0 skipped`. The smoke test's tutorial, first wave and match clock run through the gates offline.

- [ ] **Step 8: Checkpoint (no commit)**

Created: `AuthorityGateTests.cs` (+ meta). Changed: `OrderManager.cs`, `OrderBeacon.cs`, `OrderHandler.cs`, `Order.cs` (still CRLF).

---

### Task 5: `ISceneFlow` — scene changes through a replaceable flow

**Files:**
- Create: `Assets/Scripts/Menu/ISceneFlow.cs` (+ `.meta`) — `ISceneFlow` and `SceneFlow`
- Modify: `Assets/Scripts/Menu/SceneManager.cs` (CRLF — implements `ISceneFlow`)
- Modify (callers): `Assets/Scripts/Management/HotKeys.cs`, `Assets/Scripts/Management/OrderManager.cs`, `Assets/Scripts/Menu/MenuInteractions.cs` (CRLF), `Assets/Scripts/Menu/Pause/PauseMenu.cs`, `Assets/Scripts/Menu/ResultsMenu.cs` (CRLF), `Assets/Scripts/UI/ResultsUI.cs`, `Assets/Scripts/Player/OrderHandler.cs`, `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF)
- Create: `Assets/Tests/Editor/SceneFlowTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `GameAuthority.SetTimeScale` (Task 3; `PostGameClarity` calls it before loading the golden-order scene).
- Produces:
  - `public interface ISceneFlow { void LoadGameScene(); void LoadFinalOrderScene(); void ReturnToMenu(); event Action OnReturnToMenu; bool WaitingForConfirm { get; } void ConfirmLoad(); }`.
  - `public static class SceneFlow { static ISceneFlow Current { get; set; } }`: the set flow, else `SceneManager.Instance`; setting `null` goes back to the local loader.
  - `SceneManager : …, ISceneFlow` (`ReturnToMenu` = `InvokeMenuSceneEvent`, `WaitingForConfirm` = `EnableConfirm`; the other members already match).
  - No script outside `SceneManager.cs` and `ISceneFlow.cs` uses `SceneManager.Instance` (a test scans for it).
  - `DoA.Tests.FakeSceneFlow : ISceneFlow` (counts calls).

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/SceneFlowTests.cs`:

```csharp
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A scene flow that only counts what the game asked of it
    /// </summary>
    class FakeSceneFlow : ISceneFlow
    {
        public int GameLoads, FinalOrderLoads, MenuReturns, Confirms;

        public event Action OnReturnToMenu;
        public bool WaitingForConfirm { get; set; }

        public void LoadGameScene() { GameLoads++; }
        public void LoadFinalOrderScene() { FinalOrderLoads++; }
        public void ReturnToMenu() { MenuReturns++; OnReturnToMenu?.Invoke(); }
        public void ConfirmLoad() { Confirms++; }
    }

    public class SceneFlowTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            SceneFlow.Current = null;
            Time.timeScale = 1f;
            Reflect.SetSingleton<SceneManager>(null);
            Reflect.SetSingleton<GameManager>(null);
            Reflect.SetSingleton<SoundManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
        }

        [Test]
        public void Current_WhenNothingElseIsSet_IsTheLocalLoader()
        {
            SceneManager loader = objects.Add<SceneManager>();
            Reflect.SetSingleton(loader);

            Assert.AreSame(loader, SceneFlow.Current);
        }

        [Test]
        public void Current_SetToAnotherFlow_IsThatFlowUntilCleared()
        {
            SceneManager loader = objects.Add<SceneManager>();
            Reflect.SetSingleton(loader);
            FakeSceneFlow online = new FakeSceneFlow();

            SceneFlow.Current = online;
            Assert.AreSame(online, SceneFlow.Current, "while set");

            SceneFlow.Current = null;
            Assert.AreSame(loader, SceneFlow.Current, "after clearing");
        }

        [Test]
        public void LocalLoader_ReturnToMenu_TellsListenersTheGameIsGoingBackToTheMenu()
        {
            ISceneFlow loader = objects.Add<SceneManager>();
            int returns = 0;
            loader.OnReturnToMenu += () => returns++;

            loader.ReturnToMenu();

            Assert.AreEqual(1, returns);
        }

        [Test]
        public void LocalLoader_WaitingForConfirm_IsTheLoadingScreenAskingForA()
        {
            SceneManager loader = objects.Add<SceneManager>();
            ISceneFlow flow = loader;

            Assert.IsFalse(flow.WaitingForConfirm, "before the loading screen asks");
            Reflect.SetField(loader, "enableConfirm", true);
            Assert.IsTrue(flow.WaitingForConfirm, "while it asks");
        }

        [Test]
        public void GameplayScripts_ReachTheSceneLoaderOnlyThroughSceneFlow()
        {
            string[] offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/OUTDATED/"))
                .Where(f => Path.GetFileName(f) != "SceneManager.cs" && Path.GetFileName(f) != "ISceneFlow.cs")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bSceneManager\.Instance\b"))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void ResultsScreen_ReturnButton_GoesBackToTheMenuThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            ResultsUI results = objects.Add<ResultsUI>();

            Reflect.Invoke(results, "ResetGame");

            Assert.AreEqual(1, flow.MenuReturns);
        }

        [Test]
        public void PauseMenu_MainMenu_RestartsTheClockAndGoesBackThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            Reflect.SetSingleton(objects.Add<SoundManager>()); // no mixer snapshots, so the music change does nothing
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>());
            PauseMenu pause = objects.Add<PauseMenu>();
            Time.timeScale = 0f; // paused

            Reflect.Invoke(pause, "ReturnToMenu");

            Assert.AreEqual(1f, Time.timeScale, "clock running again");
            Assert.AreEqual(1, flow.MenuReturns);
        }

        [Test]
        public void EndOfTheLastWave_LoadsTheGoldenOrderSceneThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            OrderManager orders = objects.Add<OrderManager>();

            IEnumerator linger = (IEnumerator)Reflect.Invoke(orders, "PostGameClarity", false);
            linger.MoveNext(); // the half-speed pause
            linger.MoveNext(); // then the golden-order scene

            Assert.AreEqual(1, flow.FinalOrderLoads);
        }

        [Test]
        public void LoadingScreen_EveryoneConfirmed_ConfirmsThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            PlayerInstantiate players = objects.Add<PlayerInstantiate>();
            players.Roster.JoinLocal(objects.Add<PlayerInput>());
            Reflect.SetField(players, "playerLoadingConfirm", new[] { true, false, false, false });

            players.CheckLoadingConfirmCount();

            Assert.AreEqual(1, flow.Confirms);
        }

        [Test]
        public void OrderHandler_EnabledThenDisabled_LeavesTheFlowItListenedTo()
        {
            Reflect.SetSingleton(objects.Add<GameManager>());
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            OrderHandler handler = objects.Add<OrderHandler>();
            Reflect.SetField(handler, "ball", objects.Add<BallDriving>());

            Reflect.Invoke(handler, "OnEnable");
            Assert.AreEqual(1, Reflect.HandlerCount(flow, "OnReturnToMenu", handler), "while enabled");

            SceneFlow.Current = null; // e.g. an online session ended while the handler was enabled
            Reflect.Invoke(handler, "OnDisable");
            Assert.AreEqual(0, Reflect.HandlerCount(flow, "OnReturnToMenu", handler), "after disabling");
        }
    }
}
```

```bash
bash tools/newmeta.sh Assets/Tests/Editor/SceneFlowTests.cs
```

- [ ] **Step 2: Run the tests to see them fail**

Run: `bash tools/run-tests.sh SceneFlowTests`
Expected: `NO RESULTS`, with `error CS0246: The type or namespace name 'ISceneFlow' could not be found`.

- [ ] **Step 3: Add `ISceneFlow` and `SceneFlow`**

`Assets/Scripts/Menu/ISceneFlow.cs`:

```csharp
using System;

/// <summary>
/// Moves the game between the menu, the match and the golden-order scene. In a local match SceneManager loads each scene
/// behind the loading screen, which waits for every player to press A. Online play (Phase 3) swaps in a flow that
/// follows the host's scene loads
/// </summary>
public interface ISceneFlow
{
    /// <summary>Loads the match once everyone is ready in player select</summary>
    void LoadGameScene();

    /// <summary>Loads the golden-order scene after the last wave</summary>
    void LoadFinalOrderScene();

    /// <summary>Takes everyone back to the menu (from the pause menu or the results)</summary>
    void ReturnToMenu();

    /// <summary>Raised when the game heads back to the menu</summary>
    event Action OnReturnToMenu;

    /// <summary>Whether the loading screen is waiting for every player to press A</summary>
    bool WaitingForConfirm { get; }

    /// <summary>Everyone pressed A: shows the loaded scene</summary>
    void ConfirmLoad();
}

/// <summary>
/// The scene flow the game uses: the local loader (SceneManager) unless online play sets another
/// </summary>
public static class SceneFlow
{
    static ISceneFlow current;

    /// <summary>
    /// The flow every script loads scenes through. Setting null goes back to the local loader
    /// </summary>
    public static ISceneFlow Current
    {
        get { return current ?? SceneManager.Instance; }
        set { current = value; }
    }
}
```

```bash
bash tools/newmeta.sh Assets/Scripts/Menu/ISceneFlow.cs
```

- [ ] **Step 4: The local loader is a scene flow**

`Assets/Scripts/Menu/SceneManager.cs` (CRLF). The class line becomes:

```csharp
public class SceneManager : SingletonMonobehaviour<SceneManager>, ISceneFlow
```

After `InvokeMenuSceneEvent`, add:

```csharp

    // The local scene flow (ISceneFlow): LoadGameScene, LoadFinalOrderScene, ConfirmLoad and OnReturnToMenu are this
    // class's own public members
    void ISceneFlow.ReturnToMenu() { InvokeMenuSceneEvent(); }
    bool ISceneFlow.WaitingForConfirm { get { return enableConfirm; } }
```

- [ ] **Step 5: Callers go through `SceneFlow.Current`**

- `Management/HotKeys.cs`: `SceneManager.Instance.LoadGameScene();` → `SceneFlow.Current.LoadGameScene();`
- `Management/OrderManager.cs` (`PostGameClarity`): `SceneManager.Instance.LoadFinalOrderScene();` → `SceneFlow.Current.LoadFinalOrderScene();`
- `Menu/MenuInteractions.cs` (`PlayerConfirmLoad`): `if (!SceneManager.Instance.EnableConfirm && !loadReady)` → `if (!SceneFlow.Current.WaitingForConfirm && !loadReady)`
- `Menu/Pause/PauseMenu.cs`, `Menu/ResultsMenu.cs`, `UI/ResultsUI.cs`: `SceneManager.Instance.InvokeMenuSceneEvent();` → `SceneFlow.Current.ReturnToMenu();`
- `Player/PlayerInstantiate.cs`: in `LoadingConfirm`, `if (sceneManager.EnableConfirm == false)` → `if (SceneFlow.Current.WaitingForConfirm == false)`; in `CheckLoadingConfirmCount`, `SceneManager.Instance.ConfirmLoad();` → `SceneFlow.Current.ConfirmLoad();`. The `sceneManager.OnConfirmToLoad` subscription in `OnEnable` / `OnDisable` stays (see Scope decisions).
- `Player/OrderHandler.cs`: after `    [SerializeField] Animator playerAnimator;` add

  ```csharp

    private ISceneFlow sceneFlow; // the scene flow this handler listens to while enabled
  ```

  In `OnEnable`, replace `        SceneManager.Instance.OnReturnToMenu += ResetHandler;` with

  ```csharp
        sceneFlow = SceneFlow.Current;
        sceneFlow.OnReturnToMenu += ResetHandler;
  ```

  In `OnDisable`, replace `        SceneManager.Instance.OnReturnToMenu -= ResetHandler;` with `        sceneFlow.OnReturnToMenu -= ResetHandler;`.

```bash
grep -rn "SceneManager\.Instance" Assets/Scripts --include=*.cs
```

Expected: only `Menu/ISceneFlow.cs` (the default flow).

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh SceneFlowTests`
Expected: `tests: 10 total, 10 passed, 0 failed, 0 skipped`.

- [ ] **Step 7: Run the whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 151 total, 151 passed, 0 failed, 0 skipped`. The smoke test's loading-screen confirm now goes through `SceneFlow.Current`, and `EventSubscriptionTests.OrderHandler_EnabledThenDisabled_LeavesNoHandlers` still passes with the local loader as the flow.

- [ ] **Step 8: Checkpoint (no commit)**

Created: `ISceneFlow.cs`, `SceneFlowTests.cs` (+ metas). Changed: `SceneManager.cs`, `MenuInteractions.cs`, `ResultsMenu.cs`, `PlayerInstantiate.cs` (all still CRLF), `HotKeys.cs`, `OrderManager.cs`, `PauseMenu.cs`, `ResultsUI.cs`, `OrderHandler.cs`.

---

### Task 6: Roadmap and docs

**Files:**
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`
- Modify: `docs/testing.md`

**Interfaces:**
- Consumes: everything above.
- Produces: a roadmap that shows Tasks 2.3–2.5 done, the Phase 3 notes from the scope decisions, and the new test count.

- [ ] **Step 1: Roadmap**

- Phase 2 **Progress** line → `Progress (2026-09-23): Phase 2A (`2026-09-23-phase2a-roster-and-smoke-test.md`) — automated 4-player match test and the player roster (Task 2.1). Phase 2B (`2026-09-23-phase2b-input-authority-scene-flow.md`) — Tasks 2.3–2.5. Left: Task 2.2 (needs the editor).`
- Task 2.3 → `[x]`, note: `*(`Assets/Scripts/Player/IDriveInput.cs`. Each of the three has a `DriveInput` property: the prefab's `InputManager` unless something else is set. The button listeners move with it, so Task 2.2 can connect an avatar to its view at runtime.)*`
- Task 2.4 → all four `[x]`, with notes:
  - `GameAuthority.IsAuthority`: `*(`GameAuthority.Role` = `Offline` / `Host` / `Client`; `IsOnline` too. Phase 3 sets the role when a session starts and ends.)*`
  - Gates: `*(Gated: `OrderManager.Update/InitWave/InitGame/InitTutorial` (the shuffle runs in `InitGame`), `OrderBeacon.OnTriggerStay`, `OrderHandler.OnTriggerEnter/AttemptSteal`, the golden bonus (`OrderHandler.AwardGoldenBonus`). `DeliverOrder` needs no gate of its own (only the gated beacon calls it). Not gated, see Tasks 3.3, 3.5 and 3.6: spawns (fixed per slot, placed by each scooter's owner), the respawn point and the drop height (they wait for the host's answer).)*`
  - `Time.timeScale`: `*(Every write goes through `GameAuthority.SetTimeScale`, which does nothing online; a test fails if a script writes `Time.timeScale` directly.)*`
- Task 2.5 → both `[x]`, note: `*(`ISceneFlow` + `SceneFlow.Current`: the local loader unless online play sets another. A test fails if a script uses `SceneManager.Instance` directly. `PlayerInstantiate` still hears the local loader's `OnConfirmToLoad` directly — online has no loading-screen confirm.)*`
- Task 3.3, new bullet: `- [ ] Spawns: each owner puts its own scooter on its slot's spawn point (`SpawnManager.SpawnPoint`); the host doesn't place other machines' scooters.`
- Task 3.4, append to the timers bullet: `*(Phase 2B already stops `OrderManager.Update` on clients, so their clocks wait for this.)*`
- Task 3.5, new bullet: `- [ ] Dropped orders: the host picks the drop height (`Order.Drop`'s `Random.Range`) and clients get the landing spot.`
- Task 3.6, append to the respawn bullet: `*(Moves `RespawnManager.GetRespawnPoint` and `RespawnPoint.InUse` to the host.)*`
- Task 3.7, append to the online pause bullet: `*(`GameAuthority.SetTimeScale` already ignores pauses online. `ControllerDisconnectPolicy.ShouldPause` still reads `Time.timeScale == 0` to tell whether the game is paused — change that here.)*`
- §R: the smoke test description → `(`LocalMatchSmokeTest`, 1–2 minutes: menus, loading, cutscene, driving, drifting, boosting, the match clock, pause, controller unplug and replug)`.
- Partner steps → checks: `Test Runner → EditMode → Run All: 97 passed` → `151 passed`.

- [ ] **Step 2: `docs/testing.md`**

Replace the smoke test's "It drives, pauses, unplugs player 4's controller and plugs it back in." bullet with:

```markdown
  - It drives, drifts and boosts, checks the match clock runs, pauses, then unplugs player 4's controller, plugs it back in and boosts with it.
```

- [ ] **Step 3: Final run**

Run: `bash tools/run-tests.sh`
Expected: `tests: 151 total, 151 passed, 0 failed, 0 skipped`.

- [ ] **Step 4: Checkpoint (no commit)**

Changed: the roadmap, `docs/testing.md`.

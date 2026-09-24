# Phase 2C — Player Prefab Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The same local game, with the player prefab split in two: `PlayerAvatar.prefab` is the scooter every machine sees, and the local player's prefab (`Player - Cinemachine.prefab`: controller, cameras, menus) nests one avatar. Phase 3 can then show another machine's player as the avatar alone.

**Architecture:**
- `PlayerAvatar.prefab` = a `PlayerAvatar` root with `Ball Of Fun` (the rolling sphere) and `Control`. `Control` keeps the scooter's scripts: `BallDriving`, `OrderHandler`, `SoundPool`, `PhaseIndicator`, `DrivingIndicators`, `CompassMarker`, trails, headlight, models, particles.
- `Player - Cinemachine.prefab` keeps its asset, GUID and root, so the menu scene's Player Input Manager still spawns it. Its root keeps `PlayerInput`, `InputManager`, `PlayerUIHandler` and `PlayerCameraResizer`, and its first child is a nested `PlayerAvatar`. The view puts its parts back on the nested avatar's `Control` as prefab overrides:
  - children: `Camera Holder`, `Driving Canvas`, `Menu Canvas`, `Compass Calc`;
  - components: `OrbitalCamera`, `Compass`, `DynamicNumberUI`, `TutorialHandler`, `Rumbler`.
  Every reference between the two halves stays serialized, and the running hierarchy is today's plus one level: view root → `PlayerAvatar` → `Ball Of Fun` / `Control`.
- A one-time editor migration (`PlayerPrefabSplit`) does the split with `PrefabUtility`. It fails unless every serialized reference in the player prefab points at the same object afterwards. It runs in batch mode on the test mirror, and the three resulting files are copied into the project while no Unity editor is running. The tool is deleted after use; its source stays in this plan.
- The three scripts that climbed from the scooter to the root with `transform.parent` now climb one level further: the lobby's leave button, the results screen, and player names. Each gets fixed. `InputManager` raises its button events with `?.Invoke`, as roadmap Task 2.2 asks.

**Tech Stack:** Unity 2022.3.62f3 (`PrefabUtility`, batch mode `-executeMethod`) · Input System 1.14.0 · Unity Test Framework 1.1.33 (EditMode tests) · bash + robocopy test runner.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` — Phase 2 goal ("identical game, but a player's identity and authority no longer live in `PlayerInput`, so Phase 3 can plug in netcode"), Task 2.2 (split the player prefab) and the §R checklist.

**Not in this plan:**
- Making a lone avatar run on its own. It needs Phase 3's replication; Task 5 lists the view fields a remote avatar lacks for roadmap Task 3.3.
- Moving the company look (`PlayerCameraResizer.InitalizeCompanyScooter`) onto the avatar.
- Renaming `Player - Cinemachine.prefab` to `PlayerView`: cosmetic.

**Scope decisions** (after reading the code and a trial split on 2026-09-23):
- **Nested prefab, not assembled at runtime.**
  - Nesting keeps every one of the prefab's 824 references serialized, and the running hierarchy only gains a level.
  - Assembling the two halves at runtime would mean wiring about 20 fields in code.
  - In Phase 3, another machine's player is `PlayerAvatar.prefab` alone. A local online player can keep its nested avatar, for example by handing it to NGO through a prefab instance handler.
- **View components stay on the `Control` GameObject** (as added components) instead of moving to a view object. Every `GetComponent` between them and the scooter's scripts keeps working unchanged: `OrderHandler` → `TutorialHandler`, `Order` / `OrderBeacon` → `Compass`, `BallDriving` → `Rumbler`, `TutorialHandler` → `BallDriving`.
- **`PhaseIndicator` and `DrivingIndicators` stay in the avatar.** The horn glow and a player's indicator sprites are what other players see. Their UI and camera fields are filled by the view; a remote avatar's are empty (Phase 3).
- **The avatar's top object is renamed `P1`–`P4` at join,** like the view. The golden-round leaderboard (`SceneManager.cs:239`), the results list (`ResultsUI.cs:19`) and `QAHandler.cs:84` print the scooter's parent's name.
- **No runtime `DriveInput` hookup.** The roadmap's "connect each avatar to its view's controller at runtime through `DriveInput`" isn't needed: the nested avatar's controller fields (`BallDriving.inp`, `OrbitalCamera.inputManager`) are overrides pointing at the view's `InputManager`. `DriveInput` stays the seam for other drivers.
- **No emote bubble to move.** The roadmap's avatar list names one, but `EmoteHandler` isn't on the live player prefab (only in `NewDrive Test.unity`).

**Findings behind the tasks (2026-09-23):**
- Baseline: `bash tools/run-tests.sh` → 151 passed on `1b896cf3` ("Phase2B").
- The Unity editor is closed (no `Unity.exe`, no `Temp/UnityLockfile`), so the prefab can change.
- The trial split (the Task 4 tool, run in the mirror; the next sync discards it):
  - 824 references before and after, 0 missing or changed.
  - All 151 tests passed with no code changes, the match smoke test included.
  - `Control`'s children kept their order.
  - A lone avatar's view fields are empty, as expected.
- The extra level breaks three lookups, none on the smoke test's path:
  - `MenuInteractions.cs:256` finds the leaving player's `PlayerInput` two parents up, and would pass `null`: the lobby's B button would throw.
  - `ResultsMenu.cs:82` and `:110` find `PlayerCameraResizer` one parent up: the results screen would throw.
  - Player names would read "PlayerAvatar".
- These lookups keep working:
  - Everything that climbs from the sphere or `Control` with `transform.parent` and then searches down now lands on the avatar root, which still holds both `Ball Of Fun` and `Control`: `OrderHandler.cs:64/403/442`, `OrderBeacon.cs:114/221`, `CutoutHandler.cs:66-67`, `FreezeBox.cs:17`, `TutorialTrigger.cs:14`, `TutorialHandler.cs:55`, `ResultsMovementController.cs:31/52`.
  - `slot.Player.GetComponentInChildren<…>()` from the view root still reaches the nested avatar.
  - `Respawn`'s and `BallDriving`'s `parent.parent` chains stay inside the avatar.
  - `GetComponentInParent<BallDriving / SoundPool>` from `PhaseChecker` (Camera Holder) and `MenuInteractions` (Menu Canvas) still finds `Control`.
- Scene references:
  - The only build scene that references the player prefab is the menu scene, through its root: Player Input Manager's `m_PlayerPrefab`, fileID `1867894801688539398`, which the split keeps.
  - `Shader Work.unity` (not in the build) has an instance with overrides on objects that move into the avatar. Those overrides stop applying.
- Controller buttons:
  - The live prefab binds only X (`WestFaceTrigger`, drift) and A (`SouthFaceTrigger`, boost) to `InputManager`'s button callbacks, and `BallDriving` always listens to both.
  - Nothing listens to `NorthFaceEvent` or `DPadEvent`. The missing null checks start to matter once a scooter listens to another driver or is gone.
- The three `playerAnimator` fields (`BallDriving`, `OrderHandler`, `PlayerCameraResizer`) point at the same `Animator` (fileID `4753950194951505273`).

## Global Constraints

- Unity 2022.3.62f3 LTS; no package upgrades or additions.
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`). Offline play must stay identical and keep passing the roadmap's §R checklist.
- Prefab files change only through the migration tool, run in batch mode on the test mirror. The result is copied into the project only while no Unity editor is running (`tasklist`). Never edit scenes or `ProjectSettings/`.
- Don't rename serialized fields, or methods that prefabs call by name. Don't rename or move `Player - Cinemachine.prefab`: the menu scene spawns it by GUID and root fileID `1867894801688539398`.
- No git commits without explicit approval from your human partner.
  - Work on branch `steam-phase1a` (at `1b896cf3`). For now main holds only the Steam API, and every other change goes on `steam-phase1a` (the partner's rule).
  - Never check out main in this folder.
  - Each task ends with a checkpoint listing its files.
- EditMode tests live in `Assets/Tests/Editor/` (compiled into `Assembly-CSharp-Editor`), namespace `DoA.Tests`; reuse `TestObjects` / `Reflect` from `TestSupport.cs` and `FakeSceneFlow` from `SceneFlowTests.cs`.
- Every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`. `PlayerAvatar.prefab.meta` is the exception: Unity writes it when the tool saves the prefab.
- Run tests only through `bash tools/run-tests.sh [filter]`. It syncs the project into the mirror and runs Unity in batch mode with graphics. `DOA_NO_SYNC=1` tests the mirror as it is, which Task 4 needs after running the tool there. A compile error prints `NO RESULTS` and the `error CS…` lines.
- Change existing files with the Edit tool only (never `sed -i`).
  - `MenuInteractions.cs`, `ResultsMenu.cs` and `PlayerInstantiate.cs` are committed with CRLF (`i/crlf`) and must stay CRLF. Check with `git ls-files --eol`.
  - Every working file is CRLF on disk (`core.autocrlf=true`). New files may be LF.
- Tests that change `SceneFlow.Current` or a singleton put it back in `[TearDown]`.
- A second local Claude session ("Steam SDK integration") works in this folder. It agreed not to use the mirror, switch branches or stash without messaging first.

## Review Focus

1. **End of a match:** the results screen plays each scooter's placement animation, and Return resets them and goes back to the menu. Both `ResultsMenu.UpdateResults` and `ConfirmMenu` now read `OrderHandler.PlayerAnimator`.
   - `ConfirmMenu` is covered by Task 3's `Return_WithScootersNestedInTheirViews_ResetsTheirResultsAnimation`.
   - `UpdateResults` is a coroutine with waits and has no test: read it.
   - The whole loop (last wave → golden round → results → menu → second match) is manual (§R).
2. **Player names:** the golden-round leaderboard and the results list show `P1`–`P4`. Covered by Task 2's smoke-test name check, which Task 4 watches fail before the fix.
3. **Sphere triggers:** pickups and drop-offs, steals and clashes, cutouts, freeze boxes and tutorial triggers go from the sphere's collider to `other.transform.parent`, then search down.
   - With the split, that parent is the avatar root, which must hold both `Ball Of Fun` and `Control`, and `TutorialHandler` must sit on `Control`.
   - Task 4's `Avatar_HoldsTheWholeScooter` and `LocalPlayer_IsTheViewAroundOneAvatar` pin that structure. No test drives these triggers.
4. **Phasing through buildings and the camera's phase effect:** `PhaseChecker` on `Camera Holder` finds `BallDriving` and `SoundPool` with `GetComponentInParent`, so `Camera Holder` must stay under `Control`. Pinned by `LocalPlayer_IsTheViewAroundOneAvatar`; the smoke test never phases.
5. **Designer edits:** a designer applies the view's added parts to `PlayerAvatar.prefab`, or reverts the nested avatar. Every avatar would then carry cameras and menus, or the local player would lose them. `Avatar_HoldsNothingOfTheLocalView` and `LocalPlayer_IsTheViewAroundOneAvatar` fail; `docs/player-prefab.md` (Task 5) warns.

---

### Task 1: The controller ignores button presses nobody listens to

**Files:**
- Modify: `Assets/Scripts/Player/InputManager.cs:125-162` (`NorthFaceTrigger`, `WestFaceTrigger`, `SouthFaceTrigger`, `DPadPress`)
- Test: `Assets/Tests/Editor/DriveInputTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: no API change. `InputManager` raises `NorthFaceEvent`, `WestFaceEvent`, `SouthFaceEvent` and `DPadEvent` only when something listens.

- [ ] **Step 1: Write the failing test**

Add this test to `DriveInputTests`, after `InputManager_AsADriver_PassesOnItsDriftBoostAndEmoteButtons`:

```csharp
        // A controller whose scooter is gone, or whose scooter listens to another driver, has nobody on its buttons
        [Test]
        public void InputManager_ButtonPressesNobodyListensTo_AreIgnored()
        {
            InputManager controller = objects.Add<InputManager>();

            Assert.DoesNotThrow(() => controller.SouthFaceTrigger(default), "A");
            Assert.DoesNotThrow(() => controller.WestFaceTrigger(default), "X");
            Assert.DoesNotThrow(() => controller.NorthFaceTrigger(default), "Y");
        }
```

(`default(InputAction.CallbackContext)` is safe to read: with no action state its phase is `Disabled`, `ReadValueAsButton()` returns false and `ReadValue<T>()` returns `default` — see `InputAction.cs` in the Input System package.)

- [ ] **Step 2: Run the tests to verify it fails**

Run: `bash tools/run-tests.sh DriveInputTests`
Expected: `tests: 11 total, 10 passed, 1 failed`: `InputManager_ButtonPressesNobodyListensTo_AreIgnored` fails with a `NullReferenceException` for "A".

- [ ] **Step 3: Raise the events through null checks**

In `InputManager.cs`, change the four event calls:

```csharp
        NorthFaceEvent?.Invoke(northFaceValue);
```
```csharp
        WestFaceEvent?.Invoke(westFaceValue);
```
```csharp
        SouthFaceEvent?.Invoke(southFaceValue);
```
```csharp
            DPadEvent?.Invoke(dpadValue);
```

(They replace `NorthFaceEvent(northFaceValue);`, `WestFaceEvent(westFaceValue);`, `SouthFaceEvent(southFaceValue);` and `DPadEvent(dpadValue);`. `StartPadEvent` already uses `?.Invoke`. The d-pad line has no test: a blank context reads (0, 0), which raises nothing, Input System actions don't run in Edit Mode, and the live prefab doesn't bind the d-pad to `InputManager` at all.)

- [ ] **Step 4: Run the tests to verify they pass**

Run: `bash tools/run-tests.sh DriveInputTests`
Expected: `tests: 11 total, 11 passed, 0 failed`.

- [ ] **Step 5: Checkpoint**

Files: `Assets/Scripts/Player/InputManager.cs`, `Assets/Tests/Editor/DriveInputTests.cs`. No commit.

---

### Task 2: Smoke test — a player leaves player select and joins again, and players keep their names

These checks pin how the game plays today, so they pass before the split. Task 4 watches both fail for real after the split and before its fixes, so no planted failure is needed here.

**Files:**
- Modify: `Assets/Tests/Editor/LocalMatchSmokeTest.cs`

**Interfaces:**
- Consumes: `PlayerInstantiate.Instance.PlayerCount`, `PlayerInstantiate.Instance.Roster[int].Player`, `TestPlayers.Pads`.
- Produces: no API. `LocalMatchSmokeTest.FourPlayers_PlayFromTheMenuIntoTheFirstWave_WithoutErrors` now also fails when a player can't leave player select with B and join again, or when a scooter's top object isn't named after its player.

- [ ] **Step 1: Add the checks**

Replace the class summary with:

```csharp
    /// <summary>
    /// Plays a real 4-player local match with virtual controllers and fails on any error or exception: main menu,
    /// player select (where a player leaves and joins again), loading screen, opening cutscene, tutorial (skipped the way
    /// the S hotkey skips it), driving, drifting, boosting and the match clock, pausing, and a controller that dies
    /// mid-race, comes back and still drives.
    /// Enters Play Mode and takes a minute or two. Needs a graphics device (tools/run-tests.sh runs Unity with one).
    /// </summary>
```

Insert between the "Players 2-4 join in player select" loop and `Gamepad[] pads = TestPlayers.Pads.ToArray();`:

```csharp
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
```

(Leaving unpairs the pad. Its release arrives in the same input update as the press, while it's still paired, so it can't rejoin by itself. The next press is an unpaired button press, which the Player Input Manager joins as the lowest free slot: player 4 again.)

- [ ] **Step 2: Run the smoke test**

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed, 0 failed`.

- [ ] **Step 3: Checkpoint**

Files: `Assets/Tests/Editor/LocalMatchSmokeTest.cs`. No commit.

---

### Task 3: The results screen reaches each scooter's animator through its order handler

`ResultsMenu` climbs from a player's `OrderHandler` (on `Control`) one parent up to `PlayerCameraResizer` (on the player's root) for the scooter's animator. After the split that parent is the avatar, and the results screen throws. `OrderHandler` already holds the same animator.

**Files:**
- Modify: `Assets/Scripts/Player/OrderHandler.cs:54`
- Modify: `Assets/Scripts/Menu/ResultsMenu.cs:82`, `:110` (CRLF)
- Create: `Assets/Tests/Editor/ResultsMenuTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `FakeSceneFlow` (`SceneFlowTests.cs`), `ScoreManager.GetHandlerOfIndex(int)`, `PlayerInstantiate.PlayerCount`.
- Produces: `public Animator OrderHandler.PlayerAnimator { get; }`: the scooter's animator (Task 4's `LocalPlayer_ScooterUsesTheViewsControllerAndAnimator` checks it's the one `PlayerCameraResizer.playerAnimator` holds).

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/ResultsMenuTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class ResultsMenuTests
    {
        const string END_STATUS = "End Status"; // the results screen's animator parameter: 0 = racing, 1-4 = finishing place

        readonly TestObjects objects = new TestObjects();
        AnimatorController controller;

        [TearDown]
        public void TearDown()
        {
            SceneFlow.Current = null;
            Reflect.SetSingleton<ScoreManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
            if (controller != null)
                Object.DestroyImmediate(controller);
        }

        Animator ScooterAnimator()
        {
            controller = new AnimatorController();
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Racing");
            controller.AddParameter(END_STATUS, AnimatorControllerParameterType.Int);
            Animator animator = objects.Add<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            return animator;
        }

        /// <summary>
        /// A local player shaped like the split prefab: view root (controller, cameras) → PlayerAvatar → Control (the scooter's scripts)
        /// </summary>
        OrderHandler NestedScooter(Animator animator)
        {
            GameObject view = objects.NewGameObject("P1");
            view.AddComponent<PlayerCameraResizer>().playerAnimator = animator;
            GameObject avatar = new GameObject("P1");
            avatar.transform.SetParent(view.transform);
            GameObject control = new GameObject("Control");
            control.transform.SetParent(avatar.transform);
            OrderHandler orders = control.AddComponent<OrderHandler>();
            Reflect.SetField(orders, "playerAnimator", animator);
            return orders;
        }

        [Test]
        public void Return_WithScootersNestedInTheirViews_ResetsTheirResultsAnimation()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            Animator animator = ScooterAnimator();
            animator.SetInteger(END_STATUS, 2); // finished second
            Assert.AreEqual(2, animator.GetInteger(END_STATUS), "the test animator holds its parameter in Edit Mode");
            OrderHandler orders = NestedScooter(animator);
            ScoreManager scores = objects.Add<ScoreManager>();
            Reflect.SetSingleton(scores);
            Reflect.SetField(scores, "orderHandlers", new List<OrderHandler> { orders });
            PlayerInstantiate players = objects.Add<PlayerInstantiate>();
            Reflect.SetSingleton(players);
            players.Roster.JoinLocal(objects.Add<PlayerInput>());
            ResultsMenu results = objects.Add<ResultsMenu>();
            Reflect.SetField(results, "displayText", new TMP_Text[0]);
            Reflect.SetField(results, "canQuit", true);

            results.ConfirmMenu();

            Assert.AreEqual(0, animator.GetInteger(END_STATUS), "the scooter's results animation is back to racing");
            Assert.AreEqual(1, flow.MenuReturns, "back to the menu");
        }
    }
}
```

Then: `bash tools/newmeta.sh Assets/Tests/Editor/ResultsMenuTests.cs`

(If the guard assert fails — an Edit Mode animator that won't hold its parameter — drop the two `End Status` asserts and keep the menu check. Then the test fails only on the exception, and that fallback gets a ledger ruling.)

- [ ] **Step 2: Run the test to verify it fails**

Run: `bash tools/run-tests.sh ResultsMenuTests`
Expected: `tests: 1 total, 0 passed, 1 failed`, with a `NullReferenceException` from `ResultsMenu.ConfirmMenu`: one parent up from `Control` is the avatar, which has no `PlayerCameraResizer`.

- [ ] **Step 3: Read the animator from the order handler**

In `OrderHandler.cs`, after `[SerializeField] Animator playerAnimator;`:

```csharp
    [SerializeField] Animator playerAnimator;
    public Animator PlayerAnimator { get { return playerAnimator; } } // the scooter's animator (the results screen plays placements on it)
```

In `ResultsMenu.cs`, `UpdateResults` (line 82) and `ConfirmMenu` (line 110), replace
`Animator playerAnim = currHandler.transform.parent.GetComponent<PlayerCameraResizer>().playerAnimator;` with:

```csharp
            Animator playerAnim = currHandler.PlayerAnimator;
```

(Both are indented 12 spaces.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `bash tools/run-tests.sh ResultsMenuTests`
Expected: `tests: 1 total, 1 passed, 0 failed`.

- [ ] **Step 5: Checkpoint**

Files: `Assets/Scripts/Player/OrderHandler.cs`, `Assets/Scripts/Menu/ResultsMenu.cs` (still CRLF: `git ls-files --eol` shows `w/crlf`), `Assets/Tests/Editor/ResultsMenuTests.cs` + `.meta`. No commit.

---

### Task 4: Split the player prefab

**Files:**
- Create: `Assets/Tests/Editor/PlayerPrefabTests.cs` (+ `.meta`)
- Create, then delete in Step 13: `Assets/Scripts/Editor/PlayerPrefabSplit.cs` (+ `.meta`)
- Create (by the tool): `Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab` (+ `.meta`)
- Modify (by the tool): `Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab`
- Modify: `Assets/Scripts/Menu/MenuInteractions.cs:256` (CRLF)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs:220-222` (CRLF)

**Interfaces:**
- Consumes: `OrderHandler.PlayerAnimator` (Task 3); the smoke test's leave-and-rejoin and name checks (Task 2).
- Produces:
  - `Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab`: root `PlayerAvatar` → children `Ball Of Fun` (index 0), `Control` (index 1).
  - `Player - Cinemachine.prefab`: root child 0 is a `PlayerAvatar` instance.
  - At runtime, after a join, the avatar's top object is named `P1`–`P4`, like the view's root.

- [ ] **Step 1: Write the prefab tests**

Create `Assets/Tests/Editor/PlayerPrefabTests.cs`:

```csharp
using System;
using System.Linq;
using Cinemachine;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The player prefab is split: PlayerAvatar.prefab is the scooter every machine sees, and the local player's prefab
    /// (Player - Cinemachine: controller, cameras, menus) nests one avatar and adds its view parts to the avatar's Control
    /// as prefab overrides. See docs/player-prefab.md.
    /// </summary>
    public class PlayerPrefabTests
    {
        const string VIEW = "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab";
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        static readonly Type[] ViewComponentsOnControl = { typeof(OrbitalCamera), typeof(Compass), typeof(DynamicNumberUI), typeof(TutorialHandler), typeof(Rumbler) };

        static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing");
            return prefab;
        }

        static UnityEngine.Object Link(Component owner, string field)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, owner.GetType().Name + " has no serialized field " + field);
            return property.objectReferenceValue;
        }

        [Test]
        public void Avatar_HoldsTheWholeScooter()
        {
            GameObject avatar = Load(AVATAR);

            // Sphere triggers climb to this root and search down; players' GetComponentInChildren<Rigidbody>() must reach the sphere first
            Assert.AreEqual("Ball Of Fun", avatar.transform.GetChild(0).name, "first child");
            Assert.AreEqual("Control", avatar.transform.GetChild(1).name, "second child");
            string[] missing = new[] { typeof(BallDriving), typeof(OrderHandler), typeof(Respawn), typeof(CanKicker), typeof(SoundPool), typeof(CompassMarker),
                    typeof(PhaseIndicator), typeof(DrivingIndicators), typeof(TrailHandler), typeof(SkideeSkidoo), typeof(HeadlightController) }
                .Where(part => avatar.GetComponentInChildren(part, true) == null).Select(part => part.Name).ToArray();
            CollectionAssert.IsEmpty(missing, "scooter parts missing from the avatar");
        }

        [Test]
        public void Avatar_HoldsNothingOfTheLocalView()
        {
            GameObject avatar = Load(AVATAR);

            string[] viewParts = new[] { typeof(PlayerInput), typeof(InputManager), typeof(PlayerUIHandler), typeof(PlayerCameraResizer), typeof(MenuInteractions),
                    typeof(Camera), typeof(Canvas), typeof(AudioListener), typeof(CinemachineBrain), typeof(CinemachineVirtualCamera), typeof(PhaseChecker) }
                .Concat(ViewComponentsOnControl)
                .Where(part => avatar.GetComponentInChildren(part, true) != null).Select(part => part.Name).ToArray();
            CollectionAssert.IsEmpty(viewParts, "the local view's parts don't belong in the scooter every machine sees");
        }

        [Test]
        public void LocalPlayer_IsTheViewAroundOneAvatar()
        {
            GameObject view = Load(VIEW);
            GameObject avatar = view.transform.GetChild(0).gameObject;

            Assert.AreSame(Load(AVATAR), PrefabUtility.GetCorrespondingObjectFromSource(avatar), "the view's first child is a PlayerAvatar");
            Assert.AreEqual(1, view.GetComponentsInChildren<BallDriving>(true).Length, "scooters in the local player");
            foreach (Type controls in new[] { typeof(PlayerInput), typeof(InputManager), typeof(PlayerUIHandler), typeof(PlayerCameraResizer) })
                Assert.IsNotNull(view.GetComponent(controls), controls.Name + " on the view's root");
            Transform control = avatar.transform.Find("Control");
            foreach (string viewChild in new[] { "Camera Holder", "Driving Canvas", "Menu Canvas", "Compass Calc" })
                Assert.IsNotNull(control.Find(viewChild), viewChild + " rides on the scooter's Control");
            foreach (Type viewPart in ViewComponentsOnControl)
                Assert.IsNotNull(control.GetComponent(viewPart), viewPart.Name + " sits on the scooter's Control, where the scooter's scripts find it");
        }

        // The fields the other half fills: prefab overrides on the nested avatar, or view fields pointing into it
        [TestCase(typeof(BallDriving), new[] { "inp", "cameraResizer", "orbitalCamera" })]
        [TestCase(typeof(OrderHandler), new[] { "numberHandler", "playerAnimator" })]
        [TestCase(typeof(PhaseIndicator), new[] { "hornSliderLeft", "hornSliderRight" })]
        [TestCase(typeof(DrivingIndicators), new[] { "iconCamera", "thisPlayer" })]
        [TestCase(typeof(OrbitalCamera), new[] { "inputManager", "virtualCameraMain", "virtualCameraIcon", "CameraFocus" })]
        [TestCase(typeof(Compass), new[] { "compassImage", "compassCalc", "player", "orbitalCamera", "playerCam" })]
        [TestCase(typeof(DynamicNumberUI), new[] { "numHandler", "centerText" })]
        [TestCase(typeof(TutorialHandler), new[] { "tutorialText", "tutorialImage" })]
        [TestCase(typeof(PlayerCameraResizer), new[] { "ballDriving", "drivingIndicators", "scooterModel", "playerAnimator" })]
        [TestCase(typeof(PhaseChecker), new[] { "target" })]
        public void LocalPlayer_LinksBetweenViewAndScooter_AreSet(Type component, string[] fields)
        {
            Component owner = Load(VIEW).GetComponentInChildren(component, true);
            Assert.IsNotNull(owner, component.Name + " in the local player");

            string[] empty = fields.Where(field => Link(owner, field) == null).ToArray();
            CollectionAssert.IsEmpty(empty, component.Name + " fields left empty");
        }

        [Test]
        public void LocalPlayer_ScooterUsesTheViewsControllerAndAnimator()
        {
            GameObject view = Load(VIEW);
            InputManager controller = view.GetComponent<InputManager>();
            BallDriving scooter = view.GetComponentInChildren<BallDriving>(true);

            Assert.AreSame(controller, Link(scooter, "inp"), "the scooter drives with the view's controller");
            Assert.AreSame(controller, Link(scooter.GetComponent<OrbitalCamera>(), "inputManager"), "the camera turns with it");
            Assert.AreSame(view.GetComponent<PlayerInput>(), Link(scooter.GetComponent<DrivingIndicators>(), "thisPlayer"), "the indicator skips its own view");
            Assert.AreSame(view.GetComponent<PlayerCameraResizer>().playerAnimator, scooter.GetComponent<OrderHandler>().PlayerAnimator,
                "the results screen animates the scooter through its order handler");
        }
    }
}
```

Then: `bash tools/newmeta.sh Assets/Tests/Editor/PlayerPrefabTests.cs`

- [ ] **Step 2: Run the tests to verify the split tests fail**

Run: `bash tools/run-tests.sh PlayerPrefabTests`
Expected: `tests: 14 total, 11 passed, 3 failed`.
- `Avatar_HoldsTheWholeScooter` and `Avatar_HoldsNothingOfTheLocalView` fail: "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab is missing".
- `LocalPlayer_IsTheViewAroundOneAvatar` fails the same way.
- The 10 link cases and `LocalPlayer_ScooterUsesTheViewsControllerAndAnimator` pass on today's prefab. They pin links the split must keep.

- [ ] **Step 3: Write the migration tool**

Create `Assets/Scripts/Editor/PlayerPrefabSplit.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// One-time migration (Phase 2C, roadmap Task 2.2). Splits the player prefab: PlayerAvatar.prefab gets the scooter every
/// machine sees (Ball Of Fun + Control), and Player - Cinemachine.prefab keeps the controller, nests the avatar and puts its
/// cameras, canvases and view components back on the nested Control as prefab overrides. Fails if any serialized
/// reference in the player prefab points somewhere else afterwards. Batch mode:
/// Unity -batchmode -projectPath &lt;project&gt; -executeMethod PlayerPrefabSplit.RunFromCommandLine -quit
/// Writes its report to Logs/player-prefab-split.txt.
/// </summary>
public static class PlayerPrefabSplit
{
    const string VIEW_PATH = "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab";
    const string AVATAR_PATH = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";
    const string AVATAR_NAME = "PlayerAvatar";
    const string REPORT_PATH = "Logs/player-prefab-split.txt";

    // Control's children and components that belong to the local player's view
    static readonly string[] ViewChildren = { "Driving Canvas", "Menu Canvas", "Camera Holder", "Compass Calc" };
    static readonly Type[] ViewComponents = { typeof(OrbitalCamera), typeof(Compass), typeof(DynamicNumberUI), typeof(TutorialHandler), typeof(Rumbler) };

    // Serialized bookkeeping, not references between the prefab's objects
    static readonly HashSet<string> Bookkeeping = new HashSet<string>
        { "m_GameObject", "m_Script", "m_PrefabInstance", "m_PrefabAsset", "m_CorrespondingSourceObject", "m_Father", "m_ObjectHideFlags" };

    public static void RunFromCommandLine()
    {
        StringBuilder report = new StringBuilder();
        bool ok;
        try
        {
            ok = Run(report);
        }
        catch (Exception e)
        {
            report.AppendLine("FAILED: " + e);
            ok = false;
        }
        File.WriteAllText(REPORT_PATH, report.ToString());
        EditorApplication.Exit(ok ? 0 : 1);
    }

    static bool Run(StringBuilder report)
    {
        if (File.Exists(AVATAR_PATH))
        {
            report.AppendLine("FAILED: " + AVATAR_PATH + " exists: the player prefab is already split");
            return false;
        }

        GameObject view = PrefabUtility.LoadPrefabContents(VIEW_PATH);
        Dictionary<string, string> before = References(view);
        Split(view, report);
        PrefabUtility.SaveAsPrefabAsset(view, VIEW_PATH, out bool viewSaved);
        PrefabUtility.UnloadPrefabContents(view);
        report.AppendLine("view saved: " + viewSaved);

        GameObject reloaded = PrefabUtility.LoadPrefabContents(VIEW_PATH);
        Dictionary<string, string> after = References(reloaded);
        PrefabUtility.UnloadPrefabContents(reloaded);

        int problems = 0;
        foreach (KeyValuePair<string, string> reference in before)
        {
            if (!after.TryGetValue(reference.Key, out string now))
            {
                report.AppendLine("MISSING " + reference.Key + " (was " + reference.Value + ")");
                problems++;
            }
            else if (now != reference.Value)
            {
                report.AppendLine("CHANGED " + reference.Key + ": " + reference.Value + " -> " + now);
                problems++;
            }
        }
        foreach (string added in after.Keys.Except(before.Keys))
            report.AppendLine("new: " + added + " => " + after[added]);
        report.AppendLine("references: " + before.Count + " before, " + after.Count + " after, " + problems + " missing or changed");
        return viewSaved && problems == 0;
    }

    static void Split(GameObject view, StringBuilder report)
    {
        Transform ball = Child(view.transform, "Ball Of Fun");
        Transform control = Child(view.transform, "Control");

        // 1. The view's children and components come off Control; the components wait on a holder
        Dictionary<Transform, int> viewChildren = ViewChildren.Select(name => Child(control, name)).ToDictionary(child => child, child => child.GetSiblingIndex());
        foreach (Transform child in viewChildren.Keys)
            child.SetParent(view.transform, false);
        GameObject holder = new GameObject("view components (temporary)");
        holder.transform.SetParent(view.transform, false);
        foreach (Type type in ViewComponents)
        {
            Component original = control.GetComponent(type);
            if (original == null)
                throw new InvalidOperationException("Control has no " + type.Name);
            Move(view, original, holder);
        }

        // 2. Ball Of Fun and Control, now scooter only, become PlayerAvatar.prefab, nested in the view
        GameObject avatar = new GameObject(AVATAR_NAME);
        avatar.layer = view.layer;
        avatar.transform.SetParent(view.transform, false);
        avatar.transform.SetAsFirstSibling();
        ball.SetParent(avatar.transform, false);
        control.SetParent(avatar.transform, false);
        PrefabUtility.SaveAsPrefabAssetAndConnect(avatar, AVATAR_PATH, InteractionMode.AutomatedAction, out bool avatarSaved);
        if (!avatarSaved)
            throw new InvalidOperationException("couldn't save " + AVATAR_PATH);
        report.AppendLine("avatar saved: " + AVATAR_PATH);

        // 3. The view's children and components go back on the nested Control, as the view prefab's overrides
        Transform nestedControl = Child(avatar.transform, "Control");
        foreach (KeyValuePair<Transform, int> child in viewChildren.OrderBy(c => c.Value))
        {
            child.Key.SetParent(nestedControl, false);
            child.Key.SetSiblingIndex(child.Value);
        }
        foreach (Type type in ViewComponents)
            Move(view, holder.GetComponent(type), nestedControl.gameObject);
        UnityEngine.Object.DestroyImmediate(holder);

        report.AppendLine("Control's children: " + string.Join(", ", nestedControl.Cast<Transform>().Select(t => t.name)));
        report.AppendLine("Control's components: " + string.Join(", ", nestedControl.GetComponents<Component>().Select(c => c.GetType().Name)));
    }

    static Transform Child(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child == null)
            throw new InvalidOperationException(parent.name + " has no child " + name);
        return child;
    }

    /// <summary>
    /// Recreates a component on another GameObject with the same serialized values, points every reference to it at the copy, and removes the original
    /// </summary>
    static void Move(GameObject root, Component original, GameObject destination)
    {
        Component copy = destination.AddComponent(original.GetType());
        SerializedObject to = new SerializedObject(copy);
        SerializedProperty property = new SerializedObject(original).GetIterator();
        bool enterChildren = true;
        while (property.Next(enterChildren))
        {
            enterChildren = false;
            if (!Bookkeeping.Contains(property.name))
                to.CopyFromSerializedProperty(property);
        }
        to.ApplyModifiedPropertiesWithoutUndo();

        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty reference = serialized.GetIterator();
            bool changed = false;
            while (reference.Next(true))
            {
                if (reference.propertyType == SerializedPropertyType.ObjectReference && reference.objectReferenceValue == original)
                {
                    reference.objectReferenceValue = copy;
                    changed = true;
                }
            }
            if (changed)
                serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        UnityEngine.Object.DestroyImmediate(original);
    }

    /// <summary>
    /// Every serialized reference in the prefab, "path#Component[i].field" → what it points at. Paths leave out the avatar's
    /// level, so a reference that survived the split reads the same before and after
    /// </summary>
    static Dictionary<string, string> References(GameObject root)
    {
        Dictionary<string, string> references = new Dictionary<string, string>();
        foreach (Component component in root.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
                continue;
            string owner = Describe(component);
            SerializedProperty property = new SerializedObject(component).GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || Bookkeeping.Contains(property.name) || property.propertyPath.StartsWith("m_Children"))
                    continue;
                references[owner + "." + property.propertyPath] = Target(property.objectReferenceValue, property.objectReferenceInstanceIDValue);
            }
        }
        return references;
    }

    static string PathOf(Transform transform)
    {
        List<string> names = new List<string>();
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t.name != AVATAR_NAME)
                names.Insert(0, t.name);
        }
        return string.Join("/", names);
    }

    static string Describe(Component component)
    {
        Component[] same = component.GetComponents(component.GetType());
        return PathOf(component.transform) + "#" + component.GetType().Name + "[" + Array.IndexOf(same, component) + "]";
    }

    static string Target(UnityEngine.Object target, int instanceId)
    {
        if (target == null)
            return instanceId != 0 ? "missing object " + instanceId : "none";
        if (EditorUtility.IsPersistent(target))
            return "asset " + AssetDatabase.GetAssetPath(target) + ":" + target.name + ":" + target.GetType().Name;
        if (target is GameObject gameObject)
            return PathOf(gameObject.transform) + "#GameObject";
        if (target is Component component)
            return Describe(component);
        return target.GetType().Name + ":" + target.name;
    }
}
```

Then: `bash tools/newmeta.sh Assets/Scripts/Editor/PlayerPrefabSplit.cs`

- [ ] **Step 4: Sync the tool into the mirror and check it compiles**

Run: `bash tools/run-tests.sh PlayerPrefabTests`
Expected: the same `tests: 14 total, 11 passed, 3 failed` as Step 2. The run proves the tool compiles, and the mirror now holds it.

- [ ] **Step 5: Run the tool on the mirror**

Run:
```bash
MIRROR="$(dirname "$(pwd)")/_doa_test_mirror/$(basename "$(pwd)")"
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -projectPath "$(cygpath -w "$MIRROR")" -executeMethod PlayerPrefabSplit.RunFromCommandLine -logFile "$(cygpath -w "$MIRROR/Logs/player-prefab-split-unity.log")" -quit
echo "exit $?"; cat "$MIRROR/Logs/player-prefab-split.txt"
```
Expected: `exit 0`, and a report with:
- `avatar saved: Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab` and `view saved: True`
- `Control's children: Menu Canvas, Driving Canvas, Player Dependent Objects, Camera Holder, Groundcheck, Compass Calc, CanKicker, Camera Focus, Front Tire Trail, Back Tire Trail`
- `Control's components: Transform, BallDriving, PhaseIndicator, DrivingIndicators, OrderHandler, CompassMarker, TrailHandler, SoundPool, SkideeSkidoo, HeadlightController, CapsuleCollider, OrbitalCamera, Compass, DynamicNumberUI, TutorialHandler, Rumbler`
- `references: 824 before, 824 after, 0 missing or changed`, and no `MISSING` or `CHANGED` lines.

Any `MISSING`/`CHANGED` line or a nonzero exit means stop and investigate. The next synced test run puts the unsplit prefab back in the mirror.

- [ ] **Step 6: Run the prefab tests on the split mirror**

Run: `DOA_NO_SYNC=1 bash tools/run-tests.sh PlayerPrefabTests`
Expected: `tests: 14 total, 14 passed, 0 failed`.

- [ ] **Step 7: Copy the split into the project (only with no Unity editor running)**

Run:
```bash
if MSYS_NO_PATHCONV=1 tasklist /FI "IMAGENAME eq Unity.exe" | grep -qi "unity.exe"; then echo "UNITY IS RUNNING - stop"; else
  SPLIT="$(dirname "$(pwd)")/_doa_test_mirror/$(basename "$(pwd)")/Assets/Prefabs/Player Prefabs"
  cp "$SPLIT/Player - Cinemachine.prefab" "$SPLIT/PlayerAvatar.prefab" "$SPLIT/PlayerAvatar.prefab.meta" "Assets/Prefabs/Player Prefabs/"
  git status --short "Assets/Prefabs/Player Prefabs"
fi
```
Expected:
```
 M "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab"
?? "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab"
?? "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab.meta"
```
If Unity is running, stop and ask your human partner to close the editor, then rerun this step.

- [ ] **Step 8: Watch the smoke test catch the lobby's leave button**

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `1 failed` with "Timed out waiting for player 4 to leave player select". The errors so far include a `NullReferenceException` from `PlayerInstantiate.RemovePlayerRef`: `MenuInteractions` looks two parents up from the Menu Canvas, reaches the avatar, and passes a null `PlayerInput`.

- [ ] **Step 9: Find the leaving player's controls however deep the menu sits**

In `MenuInteractions.cs` (`PlayerUnreadyDespawn`), replace
`PlayerInstantiate.Instance.RemovePlayerRef(transform.parent.gameObject.transform.parent.GetComponent<PlayerInput>());` with:

```csharp
                PlayerInstantiate.Instance.RemovePlayerRef(GetComponentInParent<PlayerInput>());
```

- [ ] **Step 10: Watch the smoke test catch the players' names**

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `1 failed` with `player 1's name on the leaderboard` / `Expected: "P1"` / `But was: "PlayerAvatar"`.

- [ ] **Step 11: Name the avatar after its player at join**

In `PlayerInstantiate.cs` (`AddPlayerReference`), replace:

```csharp
        // Update the naming scheme of the input reciever
        playerInput.gameObject.name = "P" + nextFillSlot.ToString();
```

with:

```csharp
        // Update the naming scheme of the input reciever
        playerInput.gameObject.name = "P" + nextFillSlot.ToString();
        // Its scooter's top object (PlayerAvatar) gets the same name: the leaderboard and the results screen show it
        ballDriving.transform.parent.name = playerInput.gameObject.name;
```

- [ ] **Step 12: Run the smoke test to verify it passes**

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed, 0 failed`.

- [ ] **Step 13: Delete the migration tool and run everything**

Run:
```bash
rm Assets/Scripts/Editor/PlayerPrefabSplit.cs Assets/Scripts/Editor/PlayerPrefabSplit.cs.meta
bash tools/run-tests.sh
```
Expected: `tests: 167 total, 167 passed, 0 failed`. That's 151, plus 1 from Task 1, 1 from Task 3 and 14 from `PlayerPrefabTests`.

- [ ] **Step 14: Checkpoint**

Files:
- `Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab`
- `Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab` + `.meta`
- `Assets/Scripts/Menu/MenuInteractions.cs` (CRLF)
- `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF)
- `Assets/Tests/Editor/PlayerPrefabTests.cs` + `.meta`

`PlayerPrefabSplit.cs` is gone. No commit.

---

### Task 5: Docs

**Files:**
- Create: `docs/player-prefab.md`
- Modify: `docs/testing.md:8`
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`: §R, the Phase 1 partner check, Phase 2 progress, Tasks 2.2, 3.3 and 3.7

**Interfaces:**
- Consumes: Task 4's prefab layout.
- Produces: docs only.

- [ ] **Step 1: Write `docs/player-prefab.md`**

```markdown
# The player prefab

A player is two prefabs in `Assets/Prefabs/Player Prefabs/` (since Phase 2C, 2026-09-23):

- **`PlayerAvatar.prefab`** is the scooter every machine sees.
  - `Ball Of Fun`: the rolling sphere (`Rigidbody`, `Respawn`, `CanKicker`).
  - `Control`: `BallDriving`, `OrderHandler`, `SoundPool`, `PhaseIndicator`, `DrivingIndicators`, `CompassMarker`, trails, headlight, the scooter and ghost models, particles.
  - Online (Phase 3), another machine's player will be this prefab alone.
- **`Player - Cinemachine.prefab`** is a player on this machine. The menu scene's Player Input Manager spawns it when a controller joins.
  - Its root holds the controller (`PlayerInput`, `InputManager`, `PlayerUIHandler`) and `PlayerCameraResizer`.
  - Its first child is a `PlayerAvatar`. It adds its own parts to that avatar's `Control` as prefab overrides:
    - children: `Camera Holder` (cameras, Cinemachine rigs), `Driving Canvas`, `Menu Canvas`, `Compass Calc`;
    - components: `OrbitalCamera`, `Compass`, `DynamicNumberUI`, `TutorialHandler`, `Rumbler`.
  - They sit on the scooter's `Control`, so the scooter's scripts find them as before.

At runtime both top objects are named `P1`–`P4`: the golden round's leaderboard and the results screen show the scooter's top object's name.

## Editing

- Scooter look, physics, sounds and effects: open `PlayerAvatar.prefab`.
- Cameras, HUD, menus and controls: open `Player - Cinemachine.prefab`. The view's parts show as overrides (a green +) on the nested `PlayerAvatar`.
- Never apply the view's added parts to `PlayerAvatar.prefab` (Overrides → Apply All, or Apply to Prefab 'PlayerAvatar'): every avatar would get cameras and menus.
- Never revert the nested `PlayerAvatar` (Overrides → Revert All): the local player would lose its cameras and menus.
- `PlayerPrefabTests` (Test Runner → EditMode) fails if either happens.
```

- [ ] **Step 2: Update `docs/testing.md`**

Line 8 becomes:

```markdown
  - It plays through player select (player 4 leaves with B and joins again), the loading screen, the opening cutscene and a skipped tutorial.
```

- [ ] **Step 3: Update the roadmap** (`docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`)

1. In §R's last checklist item, change the smoke test description to:
   `(`LocalMatchSmokeTest`, 1–2 minutes: menus, a player leaving and rejoining player select, loading, cutscene, driving, drifting, boosting, the match clock, pause, controller unplug and replug)`.
2. In the Phase 1 partner checks, change "Test Runner → EditMode → Run All: 151 passed" to "167 passed".
3. Replace the Phase 2 progress line with:
   `Progress (2026-09-23): Phase 2A (`2026-09-23-phase2a-roster-and-smoke-test.md`) — automated 4-player match test and the player roster (Task 2.1). Phase 2B (`2026-09-23-phase2b-input-authority-scene-flow.md`) — Tasks 2.3–2.5. Phase 2C (`2026-09-23-phase2c-player-prefab-split.md`) — Task 2.2. Phase 2 is done.`
4. Replace Task 2.2's three items with:
   ```markdown
   - [x] `Player - Cinemachine.prefab` → **PlayerAvatar** (sphere Rigidbody, scooter/ghost model, `BallDriving`, `OrderHandler`, `Respawn`, `SoundPool`, emote bubble, compass marker) + **PlayerView** (6 cameras, Cinemachine rigs, Menu/Driving canvases, `PlayerInput`, `PlayerUIHandler`, `MenuInteractions`, `OrbitalCamera`). *(Phase 2C, see `docs/player-prefab.md`. `PlayerAvatar.prefab` = `Ball Of Fun` + `Control` with the scooter's scripts, including `PhaseIndicator` and `DrivingIndicators` (what others see). The view stays `Player - Cinemachine.prefab`, which the menu scene spawns: it nests one avatar and adds its cameras, canvases, `OrbitalCamera`, `Compass`, `DynamicNumberUI`, `TutorialHandler` and `Rumbler` to the avatar's `Control` as prefab overrides. There's no emote bubble to move: `EmoteHandler` isn't on the live prefab, only in `NewDrive Test.unity`.)*
   - [x] Local player = Avatar + View; remote player (Phase 3) = Avatar only.
   - [x] Connect each avatar to its view's controller. *(Serialized, not at runtime: the nested avatar's `BallDriving.inp` and `OrbitalCamera.inputManager` are prefab overrides pointing at the view's `InputManager`. `DriveInput` stays the seam for other drivers. `InputManager` raises its button events with `?.Invoke`, so a controller nobody listens to no longer throws.)*
   ```
5. Append to Task 3.3:
   ```markdown
   - [ ] A remote avatar (`PlayerAvatar.prefab` alone) has no view:
     - Its view fields are empty: `BallDriving.inp` / `cameraResizer` / `orbitalCamera`, `OrderHandler.numberHandler`, `PhaseIndicator.hornSliderLeft` / `hornSliderRight`, `DrivingIndicators.iconCamera` / `thisPlayer`.
     - Its `Control` has no `Compass`, `TutorialHandler` or `Rumbler`, which `OrderHandler`, `Order.EraseOrder`, `OrderBeacon` and `BallDriving` fetch with `GetComponent`.
     - Guard those uses, or keep those scripts off on remote avatars.
     - The company look (scooter material, logo decals, indicator sprites) comes from the view (`PlayerCameraResizer.InitalizeCompanyScooter`): move it to the avatar so remote players get their colours.
     - A local online player can keep its nested avatar, e.g. handed to NGO through a prefab instance handler.
   ```
6. In Task 3.7, after "emotes (`EmoteHandler`)", add: ` — not on the live player prefab yet (only in `NewDrive Test.unity`)`.

- [ ] **Step 4: Run the prefab tests once more**

Run: `bash tools/run-tests.sh PlayerPrefabTests`
Expected: `tests: 14 total, 14 passed, 0 failed`.

- [ ] **Step 5: Checkpoint**

Files: `docs/player-prefab.md`, `docs/testing.md`, `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`. No commit.

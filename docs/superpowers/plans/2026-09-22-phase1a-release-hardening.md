# Phase 1A — Release Hardening (no Steam App ID needed) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the pre-existing stability bugs and release-build leaks from the 2026-09-22 audit, with EditMode tests, while the Steam App ID is still pending.

**Architecture:** Small, test-first changes to existing scripts. Pure decisions (split-screen rects, settings parsing, disconnect policy) move into small static/plain classes that EditMode tests call directly. MonoBehaviour fixes are tested by creating inert components in EditMode and invoking their private lifecycle methods by reflection.

**Tech Stack:** Unity 2022.3.62f3, C# 9, Unity Test Framework 1.1.33 (NUnit 3.5), Input System 1.14.0.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (Phase 1, Tasks 1.1–1.3, and §0 audit).

## Global Constraints

- Unity 2022.3.62f3 LTS; no package upgrades or additions.
- Do not edit scenes or prefabs — the Unity editor has the project open. Scripts, tests, docs and file moves only.
- No git commits without explicit approval from your human partner; each task ends with a checkpoint listing its files.
- EditMode tests live in `Assets/Tests/Editor/` (compiles into `Assembly-CSharp-Editor`, which references game code, NUnit and the Test Runner), namespace `DoA.Tests`.
- Every new asset (script or folder) gets a hand-written `.meta` file with a fresh random 32-hex GUID, so the open editor and the test mirror agree.
- Run tests only through the runner from Task 1 (it syncs the project into `D:\.PersonalWork\DoA\_doa_test_mirror\CapstoneYear4` and runs Unity in batch mode there).

## Review Focus

1. A controller disconnects during the 3-2-1 countdown, a cutscene or a menu → nothing pauses (pausing there leaves nobody able to unpause). Test: `ControllerDisconnectPolicyTests.ShouldPause_OutsideDriving_DoesNothing` (Task 6).
2. Players 3 and 4 join after players 1 and 2 → earlier players' follower cameras take the new viewport. Test: `PlayerCameraResizerTests` (Task 2).
3. A player without a gamepad (future keyboard player) → rumble calls with no pad don't throw. Test: `RumblerTests` (Task 2).
4. A damaged or old binary `settings.dat` → defaults load, nothing throws. Test: `GameSettingsTests.UnreadableFile_FallsBackToDefaults` (Task 5).
5. A release build → debug keys ignored, no playtest files written. Tests: `DevToolsTests` (Task 4).

---

## Task 1: EditMode test runner + split-screen layout

**Files:**
- Create: `.superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh` (git-ignored tooling)
- Create: `Assets/Tests.meta`, `Assets/Tests/Editor.meta`
- Create: `Assets/Tests/Editor/SplitScreenLayoutTests.cs` (+ `.meta`)
- Create: `Assets/Scripts/Management/SplitScreenLayout.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs:262-322`

**Interfaces:**
- Produces: `run-tests.sh [filter]` (exit 0 only when ≥1 test ran and none failed); `public static Rect[] SplitScreenLayout.CalculateRects(int playerCount)`.

- [ ] **Step 1: Write the runner**

```bash
#!/usr/bin/env bash
# Runs the project's EditMode tests in a mirror copy of the project, so the Unity
# editor that is open on the real project keeps its lock.
# Usage: run-tests.sh [test-filter]   (filter = NUnit class/test name or regex)
# Exit: 0 only when at least one test ran and none failed.
set -uo pipefail

REAL="D:/.PersonalWork/DoA/CapstoneYear4"
MIRROR="D:/.PersonalWork/DoA/_doa_test_mirror/CapstoneYear4"
UNITY="C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe"
OUT="$(cd "$(dirname "$0")" && pwd)"
filter="${1:-}"

for d in Assets Packages ProjectSettings; do
  MSYS_NO_PATHCONV=1 robocopy "$(cygpath -w "$REAL/$d")" "$(cygpath -w "$MIRROR/$d")" /MIR /R:1 /W:1 /NFL /NDL /NP /NJH /NJS > /dev/null
  rc=$?
  if [ "$rc" -ge 8 ]; then echo "sync of $d failed (robocopy exit $rc)"; exit 1; fi
done

results="$OUT/results.xml"
log="$OUT/unity.log"
rm -f "$results"
args=(-batchmode -nographics -projectPath "$(cygpath -w "$MIRROR")" -runTests -testPlatform EditMode
      -testResults "$(cygpath -w "$results")" -logFile "$(cygpath -w "$log")")
[ -n "$filter" ] && args+=(-testFilter "$filter")
"$UNITY" "${args[@]}"
unity_rc=$?

if [ ! -f "$results" ]; then
  grep -E "error CS[0-9]+" "$log" | sort -u | head -20
  echo "NO RESULTS (Unity exit $unity_rc) - see $log"
  exit 1
fi

powershell.exe -NoProfile -Command "
  [xml]\$x = Get-Content -Raw '$(cygpath -w "$results")'
  foreach (\$tc in \$x.SelectNodes(\"//test-case[@result='Failed']\")) {
    \$m = \$tc.SelectSingleNode('failure/message')
    'FAIL ' + \$tc.fullname + ': ' + ((\$m.InnerText -replace '\s+',' ').Trim())
  }
  \$r = \$x.'test-run'
  'tests: ' + \$r.total + ' total, ' + \$r.passed + ' passed, ' + \$r.failed + ' failed, ' + \$r.skipped + ' skipped'
  if ([int]\$r.failed -gt 0 -or [int]\$r.total -eq 0) { exit 1 } else { exit 0 }
"
```

- [ ] **Step 2: Write the failing tests** — `Assets/Tests/Editor/SplitScreenLayoutTests.cs`

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class SplitScreenLayoutTests
    {
        [Test]
        public void NoPlayers_GetNoViewports()
        {
            Assert.AreEqual(0, SplitScreenLayout.CalculateRects(0).Length);
        }

        [Test]
        public void OnePlayer_GetsTheWholeScreen()
        {
            CollectionAssert.AreEqual(new[] { new Rect(0f, 0f, 1f, 1f) }, SplitScreenLayout.CalculateRects(1));
        }

        [Test]
        public void TwoPlayers_GetCenteredTopAndBottomViewports()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0.25f, 0.5f, 0.5f, 0.5f),
                new Rect(0.25f, 0f, 0.5f, 0.5f),
            }, SplitScreenLayout.CalculateRects(2));
        }

        [Test]
        public void ThreePlayers_FillTheTopRowAndCenterTheThird()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                new Rect(0.25f, 0f, 0.5f, 0.5f),
            }, SplitScreenLayout.CalculateRects(3));
        }

        [Test]
        public void FourPlayers_GetOneQuadrantEach()
        {
            CollectionAssert.AreEqual(new[]
            {
                new Rect(0f, 0.5f, 0.5f, 0.5f),
                new Rect(0.5f, 0.5f, 0.5f, 0.5f),
                new Rect(0f, 0f, 0.5f, 0.5f),
                new Rect(0.5f, 0f, 0.5f, 0.5f),
            }, SplitScreenLayout.CalculateRects(4));
        }
    }
}
```

- [ ] **Step 3: Add a stub so the tests compile** — `Assets/Scripts/Management/SplitScreenLayout.cs`

```csharp
using UnityEngine;

public static class SplitScreenLayout
{
    public static Rect[] CalculateRects(int playerCount)
    {
        throw new System.NotImplementedException();
    }
}
```

- [ ] **Step 4: Run the tests to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh SplitScreenLayoutTests`
Expected: `5 total, 0 passed, 5 failed`, each `System.NotImplementedException`.

- [ ] **Step 5: Implement** — replace the stub with the logic moved out of `PlayerInstantiate.CalculateRects`

```csharp
using UnityEngine;

/// <summary>
/// Viewport rects for local split-screen, in join order (index 0 is the first player to join)
/// </summary>
public static class SplitScreenLayout
{
    ///<summary>
    /// Calculates the camera rects for when there are 1 - 4 players
    ///</summary>
    public static Rect[] CalculateRects(int playerCount)
    {
        Rect[] viewportRects = new Rect[playerCount];

        // 1 Player
        if (playerCount == 1)
        {
            viewportRects[0] = new Rect(0, 0, 1, 1);
        }
        else if (playerCount == 2)
        {
            viewportRects[0] = new Rect(0.25f, 0.5f, 0.5f, 0.5f);
            viewportRects[1] = new Rect(0.25f, 0, 0.5f, 0.5f);
        }
        else if (playerCount == 3)
        {
            viewportRects[0] = new Rect(0, 0.5f, 0.5f, 0.5f);
            viewportRects[1] = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            viewportRects[2] = new Rect(0.25f, 0, 0.5f, 0.5f);
        }
        else if (playerCount == 4)
        {
            viewportRects[0] = new Rect(0, 0.5f, 0.5f, 0.5f);
            viewportRects[1] = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            viewportRects[2] = new Rect(0, 0, 0.5f, 0.5f);
            viewportRects[3] = new Rect(0.5f, 0, 0.5f, 0.5f);
        }

        return viewportRects;
    }
}
```

In `PlayerInstantiate.cs`: in `UpdatePlayerCameraRects` replace `cameraRects = CalculateRects();` with `cameraRects = SplitScreenLayout.CalculateRects(playerCount);` and delete the private `CalculateRects()` method (lines 290-322).

- [ ] **Step 6: Run the tests to watch them pass**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh SplitScreenLayoutTests`
Expected: `5 total, 5 passed, 0 failed`.

- [ ] **Step 7: Checkpoint** — files: `Assets/Tests*`, `SplitScreenLayout.cs(.meta)`, `PlayerInstantiate.cs`.

---

## Task 2: Player slot, follower-camera and rumble fixes

**Files:**
- Create: `Assets/Tests/Editor/TestSupport.cs`, `PlayerSlotTests.cs`, `PlayerCameraResizerTests.cs`, `RumblerTests.cs` (+ `.meta`s)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs:327-399`, `Assets/Scripts/Player/PlayerCameraResizer.cs:76-124`, `Assets/Scripts/Player/Rumbler.cs`

**Interfaces:**
- Consumes: Task 1 runner.
- Produces: `DoA.Tests.TestObjects` (`NewGameObject(string)`, `Add<T>()`, `DestroyAll()`), `DoA.Tests.Reflect` (`SetField`, `Invoke`, `SetSingleton<T>`, `HandlerCount`); `PlayerInstantiate.RemoveFromPlayerArray` returns `-1` for unknown players.

- [ ] **Step 1: Test support** — `Assets/Tests/Editor/TestSupport.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Creates throwaway GameObjects for EditMode tests and destroys them afterwards.
    /// Components added in Edit Mode don't get Awake/OnEnable/Start, so tests call those through Reflect.
    /// </summary>
    public class TestObjects
    {
        readonly List<GameObject> created = new List<GameObject>();

        public GameObject NewGameObject(string name = "Test Object")
        {
            GameObject go = new GameObject(name);
            created.Add(go);
            return go;
        }

        public T Add<T>() where T : Component
        {
            return NewGameObject(typeof(T).Name).AddComponent<T>();
        }

        public void DestroyAll()
        {
            foreach (GameObject go in created)
            {
                if (go != null)
                    UnityEngine.Object.DestroyImmediate(go);
            }
            created.Clear();
        }
    }

    /// <summary>
    /// Reaches private fields, lifecycle methods, singleton instances and event subscriber lists for tests.
    /// </summary>
    public static class Reflect
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static void SetField(object target, string name, object value)
        {
            FindField(target.GetType(), name).SetValue(target, value);
        }

        public static object Invoke(object target, string methodName, params object[] args)
        {
            MethodInfo method = FindMethod(target.GetType(), methodName, args.Length);
            try
            {
                return method.Invoke(target, args);
            }
            catch (TargetInvocationException e)
            {
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
                throw;
            }
        }

        public static void SetSingleton<T>(T instance) where T : MonoBehaviour
        {
            typeof(SingletonMonobehaviour<T>).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, instance);
        }

        /// <summary>
        /// How many handlers on an event (or delegate field) belong to the subscriber
        /// </summary>
        public static int HandlerCount(object source, string eventName, object subscriber)
        {
            Delegate handlers = FindField(source.GetType(), eventName).GetValue(source) as Delegate;
            return handlers == null ? 0 : handlers.GetInvocationList().Count(h => ReferenceEquals(h.Target, subscriber));
        }

        static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, Members);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.Name, name);
        }

        static MethodInfo FindMethod(Type type, string name, int argumentCount)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo method = t.GetMethods(Members).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == argumentCount);
                if (method != null)
                    return method;
            }
            throw new MissingMethodException(type.Name, name);
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`Assets/Tests/Editor/PlayerSlotTests.cs`:

```csharp
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class PlayerSlotTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<PlayerSelectCanvas>(null);
            Reflect.SetSingleton<ScoreManager>(null);
            objects.DestroyAll();
        }

        [Test]
        public void RemoveFromPlayerArray_PlayerNotInLobby_ReturnsMinusOne()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerInput stranger = objects.Add<PlayerInput>();

            Assert.AreEqual(-1, instantiate.RemoveFromPlayerArray(stranger));
        }

        [Test]
        public void RemoveFromPlayerArray_JoinedPlayer_FreesOnlyTheirSlot()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerInput first = objects.Add<PlayerInput>();
            PlayerInput second = objects.Add<PlayerInput>();
            instantiate.AddToPlayerArray(first);
            instantiate.AddToPlayerArray(second);

            Assert.AreEqual(1, instantiate.RemoveFromPlayerArray(second));
            Assert.AreSame(first, instantiate.PlayerInputs[0]);
            Assert.IsNull(instantiate.PlayerInputs[1]);
        }

        [Test]
        public void RemovePlayerRef_PlayerNotInLobby_LeavesPlayerOnesJoinPromptAlone()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] joinPrompts = new GameObject[Constants.MAX_PLAYERS];
            for (int i = 0; i < joinPrompts.Length; i++)
            {
                joinPrompts[i] = objects.NewGameObject("Join Prompt " + i);
                joinPrompts[i].SetActive(false);
            }
            Reflect.SetField(canvas, "pressButtonTexts", joinPrompts);
            Reflect.SetSingleton(canvas);
            Reflect.SetSingleton(objects.Add<ScoreManager>());
            PlayerInput stranger = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.RemovePlayerRef(stranger);

            Assert.IsFalse(joinPrompts[0].activeSelf);
        }
    }
}
```

`Assets/Tests/Editor/PlayerCameraResizerTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class PlayerCameraResizerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        PlayerCameraResizer CreateResizer(out Camera reference, out Camera follower)
        {
            PlayerCameraResizer resizer = objects.Add<PlayerCameraResizer>();
            reference = objects.Add<Camera>();
            follower = objects.Add<Camera>();
            Reflect.SetField(resizer, "referenceCam", reference);
            Reflect.SetField(resizer, "camerasToFollow", new[] { follower });
            Reflect.SetField(resizer, "viewPortRectDefault", new Vector4(0f, 0f, 1f, 1f));
            return resizer;
        }

        [Test]
        public void FollowerCameras_TakeTheNewViewport_WhenMorePlayersJoin()
        {
            PlayerCameraResizer resizer = CreateResizer(out Camera reference, out Camera follower);
            reference.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f); // 2 players
            Reflect.Invoke(resizer, "Update");

            reference.rect = new Rect(0f, 0.5f, 0.5f, 0.5f); // a 3rd player joined
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Rect(0f, 0.5f, 0.5f, 0.5f), follower.rect);
        }

        [Test]
        public void FollowerCameras_ReturnToFullScreen_WhenTheOtherPlayersLeave()
        {
            PlayerCameraResizer resizer = CreateResizer(out Camera reference, out Camera follower);
            reference.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f);
            Reflect.Invoke(resizer, "Update");

            reference.rect = new Rect(0f, 0f, 1f, 1f);
            Reflect.Invoke(resizer, "Update");

            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), follower.rect);
        }
    }
}
```

`Assets/Tests/Editor/RumblerTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;

namespace DoA.Tests
{
    public class RumblerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void SuspendedRumble_WithNoGamepad_DoesNothing()
        {
            Rumbler rumbler = objects.Add<Rumbler>();
            Assert.DoesNotThrow(() => rumbler.SuspendedRumble(null, 0.1f, 0.2f));
        }

        [Test]
        public void EndSuspension_WithNoGamepad_DoesNothing()
        {
            Rumbler rumbler = objects.Add<Rumbler>();
            Assert.DoesNotThrow(() => rumbler.EndSuspension(null));
        }

        [Test]
        public void RumblePulse_WithNoGamepad_FinishesWithoutError()
        {
            Rumbler rumbler = objects.Add<Rumbler>();
            IEnumerator pulse = (IEnumerator)Reflect.Invoke(rumbler, "PulseTime", 0f, null, false);

            Assert.DoesNotThrow(() => { while (pulse.MoveNext()) { } });
        }
    }
}
```

- [ ] **Step 3: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh "PlayerSlotTests|PlayerCameraResizerTests|RumblerTests"`
Expected: `8 total, 1 passed, 7 failed` — `RemoveFromPlayerArray_JoinedPlayer_FreesOnlyTheirSlot` already passes (guard for existing behaviour); the rest fail on the asserted values or `NullReferenceException`.

- [ ] **Step 4: Implement**

`PlayerInstantiate.cs` — `RemoveFromPlayerArray` ends with `return -1;` (doc: "returns position where it was removed, or -1 if the player was not in the array"), and in `RemovePlayerRef`:

```csharp
        int position = RemoveFromPlayerArray(playerInput);

        //Enabled text for fillslot text based on player's removed position
        if (position >= 0)
            PlayerSelectCanvas.Instance.TogglePressButtonTexts(position, true);
```

`PlayerCameraResizer.cs` — add field `Rect followedRect;` next to `bool initalized = false;` and replace the rect-copying part of `Update` (from `// Returns if updated already`):

```csharp
        // Waits until the player is given their first split-screen viewport
        if (!initalized && referenceCam.rect.x == viewPortRectDefault.x && referenceCam.rect.y == viewPortRectDefault.y
            && referenceCam.rect.width == viewPortRectDefault.z && referenceCam.rect.height == viewPortRectDefault.w)
            return;

        // Returns if the other cameras already match
        if (initalized && referenceCam.rect == followedRect)
            return;

        // Players joining or leaving resize the reference cam, so the other cameras follow every change
        foreach (Camera cam in camerasToFollow)
        {
            cam.rect = referenceCam.rect;
        }

        followedRect = referenceCam.rect;
        initalized = true;
```

`Rumbler.cs` — `Start` and `OnApplicationQuit` keep only `InputSystem.ResetHaptics();` (it stops every pad); every `pad.SetMotorSpeeds(...)` in `SuspendedRumble`, `EndSuspension` and `PulseTime` becomes `pad?.SetMotorSpeeds(...)`.

- [ ] **Step 5: Run to watch them pass**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`
Expected: `13 total, 13 passed, 0 failed`.

- [ ] **Step 6: Checkpoint** — files: test files, `PlayerInstantiate.cs`, `PlayerCameraResizer.cs`, `Rumbler.cs`.

---

## Task 3: Event subscriptions that never unsubscribe

**Files:**
- Create: `Assets/Tests/Editor/EventSubscriptionTests.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/BallDriving.cs:279-305`, `Assets/Scripts/Management/SoundManager.cs:97-116`, `Assets/Scripts/Player/OrderHandler.cs:77-83`

**Interfaces:**
- Consumes: `TestObjects`, `Reflect` (Task 2).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Linq;
using NUnit.Framework;

namespace DoA.Tests
{
    public class EventSubscriptionTests
    {
        static readonly string[] GameStateEvents =
        {
            "OnSwapMenu", "OnSwapOptions", "OnSwapCredits", "OnSwapPlayerSelect", "OnSwapLoading",
            "OnSwapStartingCutscene", "OnSwapTutorial", "OnSwapBegin", "OnSwapMainLoop",
            "OnSwapGoldenCutscene", "OnSwapFinalPackage", "OnSwapResults", "OnSwapAnything",
        };

        readonly TestObjects objects = new TestObjects();
        GameManager gameManager;

        [SetUp]
        public void SetUp()
        {
            gameManager = objects.Add<GameManager>();
            Reflect.SetSingleton(gameManager);
        }

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<GameManager>(null);
            Reflect.SetSingleton<SceneManager>(null);
            objects.DestroyAll();
        }

        int GameStateHandlersOf(object subscriber)
        {
            return GameStateEvents.Sum(e => Reflect.HandlerCount(gameManager, e, subscriber));
        }

        [Test]
        public void BallDriving_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            BallDriving ball = objects.Add<BallDriving>();

            Reflect.Invoke(ball, "OnEnable");
            Reflect.Invoke(ball, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(ball));
        }

        [Test]
        public void SoundManager_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            SoundManager sound = objects.Add<SoundManager>();

            Reflect.Invoke(sound, "OnEnable");
            Reflect.Invoke(sound, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(sound));
        }

        [Test]
        public void OrderHandler_EnabledThenDisabled_LeavesNoHandlers()
        {
            SceneManager scene = objects.Add<SceneManager>();
            Reflect.SetSingleton(scene);
            BallDriving ball = objects.Add<BallDriving>();
            OrderHandler handler = objects.Add<OrderHandler>();
            Reflect.SetField(handler, "ball", ball);

            Reflect.Invoke(handler, "OnEnable");
            Reflect.Invoke(handler, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(handler));
            Assert.AreEqual(0, Reflect.HandlerCount(scene, "OnReturnToMenu", handler));
            Assert.AreEqual(0, Reflect.HandlerCount(ball, "OnBoostStart", handler));
        }
    }
}
```

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh EventSubscriptionTests`
Expected: `3 total, 0 passed, 3 failed` — BallDriving `Expected: 0 But was: 5`, SoundManager `But was: 1`, OrderHandler `But was: 2`.

- [ ] **Step 3: Implement**

`BallDriving.cs` — the five lambdas become two named methods, subscribed and unsubscribed by name:

```csharp
        GameManager.Instance.OnSwapStartingCutscene += FreezeBallForGameState;
        GameManager.Instance.OnSwapGoldenCutscene += FreezeBallForGameState;
        GameManager.Instance.OnSwapResults += FreezeBallForGameState;

        GameManager.Instance.OnSwapTutorial += UnfreezeBallForGameState;
        GameManager.Instance.OnSwapFinalPackage += UnfreezeBallForGameState;
```

(same five lines with `-=` in `OnDisable`), plus:

```csharp
    /// <summary>
    /// Freezes the ball when the game state takes control away from the player (cutscenes, results)
    /// </summary>
    private void FreezeBallForGameState()
    {
        FreezeBall(true);
    }

    /// <summary>
    /// Unfreezes the ball when the game state hands control back to the player
    /// </summary>
    private void UnfreezeBallForGameState()
    {
        FreezeBall(false);
    }
```

`SoundManager.cs` — `GameManager.Instance.OnSwapAnything += ResetSnapshotToGameplay;` / `-= ResetSnapshotToGameplay;` plus:

```csharp
    /// <summary>
    /// Returns the mix to the gameplay snapshot whenever the game state changes
    /// </summary>
    private void ResetSnapshotToGameplay()
    {
        ChangeSnapshot("gameplay");
    }
```

`OrderHandler.cs:82` — `GameManager.Instance.OnSwapStartingCutscene += InitHandler;` → `-= InitHandler;`.

- [ ] **Step 4: Run to watch them pass**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`
Expected: `16 total, 16 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: `EventSubscriptionTests.cs`, `BallDriving.cs`, `SoundManager.cs`, `OrderHandler.cs`.

---

## Task 4: Developer tools off in release builds

**Files:**
- Create: `Assets/Scripts/Management/DevTools.cs`, `Assets/Tests/Editor/DevToolsTests.cs` (+ `.meta`s)
- Modify: `HotKeys.cs:12-24`, `OrderManager.cs:123-133,202-205`, `TutorialManager.cs:38`, `CutsceneManager.cs:40`, `MainMenu.cs:39`, `BallDriving.cs:358`, `OrderHandler.cs:87`, `Respawn.cs:102`, `TutorialHandler.cs:51`, `QAManager.cs:86-160`
- Move: `Assets/StreamingAssets/QAData.csv` → `docs/playtest-data/QAData.csv`; `Assets/StreamingAssets/HeatMaps/` → `docs/playtest-data/HeatMaps/` (drop their `.meta` files); delete `Assets/StreamingAssets/` + `Assets/StreamingAssets.meta`

**Interfaces:**
- Produces: `public static class DevTools { public static bool Enabled; public static bool GetKeyDown(KeyCode key); }`; `public static string QAManager.DataDirectory { get; set; }` (null → default `persistentDataPath/Playtest`).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class DevToolsTests
    {
        readonly TestObjects objects = new TestObjects();
        bool devToolsWereEnabled;
        string playtestFolder;

        [SetUp]
        public void SetUp()
        {
            devToolsWereEnabled = DevTools.Enabled;
            playtestFolder = Path.Combine(Path.GetTempPath(), "doa-playtest-" + System.Guid.NewGuid().ToString("N"));
            QAManager.DataDirectory = playtestFolder;
        }

        [TearDown]
        public void TearDown()
        {
            DevTools.Enabled = devToolsWereEnabled;
            QAManager.DataDirectory = null;
            if (Directory.Exists(playtestFolder))
                Directory.Delete(playtestFolder, true);
            objects.DestroyAll();
        }

        [Test]
        public void GetKeyDown_WhenDevToolsAreOff_IgnoresEveryKey()
        {
            DevTools.Enabled = false;

            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
                Assert.IsFalse(DevTools.GetKeyDown(key), key.ToString());
        }

        [Test]
        public void GameplayScripts_ReadDebugKeysOnlyThroughDevTools()
        {
            string[] offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/OUTDATED/") && Path.GetFileName(f) != "DevTools.cs")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"Input\.GetKeyDown\(\s*KeyCode\."))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void QAManager_RecordsPlaytestDataInItsDataDirectory()
        {
            DevTools.Enabled = true;
            QAManager qa = objects.Add<QAManager>();

            Reflect.Invoke(qa, "SendData");

            Assert.IsTrue(File.Exists(Path.Combine(playtestFolder, "QAData.csv")));
        }

        [Test]
        public void QAManager_WhenDevToolsAreOff_RecordsNothing()
        {
            DevTools.Enabled = false;
            QAManager qa = objects.Add<QAManager>();

            Reflect.Invoke(qa, "SendData");

            Assert.IsFalse(Directory.Exists(playtestFolder));
        }

        [Test]
        public void StreamingAssets_ShipNoPlaytestData()
        {
            Assert.IsFalse(File.Exists("Assets/StreamingAssets/QAData.csv"), "QAData.csv would ship inside the build");
            Assert.IsFalse(Directory.Exists("Assets/StreamingAssets/HeatMaps"), "HeatMaps would ship inside the build");
        }
    }
}
```

Stubs so it compiles: `DevTools.cs` with `public static bool Enabled = true;` and `GetKeyDown` throwing `NotImplementedException`; in `QAManager.cs` add the `DataDirectory` property (below) without using it yet.

```csharp
    static string dataDirectory;

    /// <summary>
    /// Folder the playtest CSV is written to. Never StreamingAssets, which ships inside builds.
    /// </summary>
    public static string DataDirectory
    {
        get { return dataDirectory ?? Path.Combine(Application.persistentDataPath, "Playtest"); }
        set { dataDirectory = value; }
    }
```

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh DevToolsTests`
Expected: `5 total, 1 passed, 4 failed` — `GetKeyDown…` (NotImplementedException), `GameplayScripts…` (9 offending files listed), `QAManager_Records…` (file missing), `StreamingAssets…` (QAData.csv found). `QAManager_WhenDevToolsAreOff…` passes only because the CSV still goes to StreamingAssets — it must fail after Step 3.

- [ ] **Step 3: Implement the key gate, the data path and the move (not the QA gate yet)**

`DevTools.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Gate for developer-only features: debug hotkeys and playtest data recording.
/// On in the editor and development builds, off in release builds.
/// </summary>
public static class DevTools
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool Enabled = true;
#else
    public static bool Enabled = false;
#endif

    /// <summary>
    /// Input.GetKeyDown that only reports presses while developer tools are enabled
    /// </summary>
    public static bool GetKeyDown(KeyCode key)
    {
        return Enabled && Input.GetKeyDown(key);
    }
}
```

In the 9 hotkey files replace every `Input.GetKeyDown(KeyCode.` with `DevTools.GetKeyDown(KeyCode.` (13 call sites). In `QAManager.WriteCSV`/`WriteEmptyLine` use `string filePath = Path.Combine(DataDirectory, fileName);` preceded by `Directory.CreateDirectory(DataDirectory);`. Move the StreamingAssets playtest data as listed under **Files**.

- [ ] **Step 4: Run — only the QA gate test should fail now**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh DevToolsTests`
Expected: `5 total, 4 passed, 1 failed` — `QAManager_WhenDevToolsAreOff_RecordsNothing` (the folder gets created).

- [ ] **Step 5: Implement the QA gate** — first line of `QAManager.SendData`:

```csharp
        // Playtest data is only recorded in the editor and development builds
        if (!DevTools.Enabled) { return; }
```

- [ ] **Step 6: Run the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`
Expected: `21 total, 21 passed, 0 failed`.

- [ ] **Step 7: Checkpoint** — files: `DevTools.cs`, `DevToolsTests.cs`, 9 hotkey scripts, `QAManager.cs`, StreamingAssets removal, `docs/playtest-data/`.

---

## Task 5: Settings saved without BinaryFormatter

**Files:**
- Create: `Assets/Scripts/Menu/GameSettings.cs`, `Assets/Tests/Editor/GameSettingsTests.cs` (+ `.meta`s)
- Modify: `Assets/Scripts/Menu/OptionsMenu.cs:1-111` and remove `OptionsSave` (bottom of file)

**Interfaces:**
- Produces: `public class GameSettings { int bgmPosition, sfxPosition, fullscreenPosition; GameSettings(int,int,int); string ToText(); static GameSettings Parse(string text, GameSettings defaults); const int MAX_VOLUME_POSITION = 11; }`

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    public class GameSettingsTests
    {
        static readonly GameSettings Defaults = new GameSettings(5, 6, 1);

        static void AssertSettings(int bgm, int sfx, int fullscreen, GameSettings actual)
        {
            Assert.AreEqual(bgm, actual.bgmPosition, "bgm");
            Assert.AreEqual(sfx, actual.sfxPosition, "sfx");
            Assert.AreEqual(fullscreen, actual.fullscreenPosition, "fullscreen");
        }

        [Test]
        public void SavedSettings_LoadBackUnchanged()
        {
            GameSettings saved = new GameSettings(3, 9, 0);
            AssertSettings(3, 9, 0, GameSettings.Parse(saved.ToText(), Defaults));
        }

        [Test]
        public void UnreadableFile_FallsBackToDefaults()
        {
            AssertSettings(5, 6, 1, GameSettings.Parse("\0\u0001\u0002 binary settings from the old BinaryFormatter save", Defaults));
        }

        [Test]
        public void OutOfRangeValues_AreClamped()
        {
            AssertSettings(11, 0, 1, GameSettings.Parse("bgm=99\nsfx=-4\nfullscreen=7", Defaults));
        }

        [Test]
        public void MissingValues_KeepTheirDefaults()
        {
            AssertSettings(2, 6, 1, GameSettings.Parse("bgm=2", Defaults));
        }

        [Test]
        public void WindowsLineEndings_AreRead()
        {
            AssertSettings(1, 2, 0, GameSettings.Parse("bgm=1\r\nsfx=2\r\nfullscreen=0\r\n", Defaults));
        }
    }
}
```

Stub: `GameSettings` with the three fields and constructor; `ToText()` and `Parse(...)` throw `NotImplementedException`.

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh GameSettingsTests`
Expected: `5 total, 0 passed, 5 failed` (NotImplementedException).

- [ ] **Step 3: Implement** — `GameSettings.cs`:

```csharp
using System.Globalization;
using UnityEngine;

/// <summary>
/// Values saved by the options menu, stored as plain "key=value" lines so a damaged
/// or outdated file only loses the lines that can't be read.
/// </summary>
public class GameSettings
{
    public const int MAX_VOLUME_POSITION = 11;

    public int bgmPosition;
    public int sfxPosition;
    public int fullscreenPosition;

    public GameSettings(int bgmPosition, int sfxPosition, int fullscreenPosition)
    {
        this.bgmPosition = bgmPosition;
        this.sfxPosition = sfxPosition;
        this.fullscreenPosition = fullscreenPosition;
    }

    ///<summary>
    /// Converts the settings to the text saved on disk
    ///</summary>
    public string ToText()
    {
        return $"bgm={bgmPosition}\nsfx={sfxPosition}\nfullscreen={fullscreenPosition}\n";
    }

    ///<summary>
    /// Reads saved settings on top of the defaults. Unreadable lines are skipped and values are
    /// clamped, in case the file was edited or damaged
    ///</summary>
    public static GameSettings Parse(string text, GameSettings defaults)
    {
        GameSettings settings = new GameSettings(defaults.bgmPosition, defaults.sfxPosition, defaults.fullscreenPosition);

        foreach (string line in (text ?? "").Split('\n'))
        {
            string[] pair = line.Split('=');
            if (pair.Length != 2 || !int.TryParse(pair[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
                continue;

            switch (pair[0].Trim())
            {
                case "bgm":
                    settings.bgmPosition = Mathf.Clamp(value, 0, MAX_VOLUME_POSITION);
                    break;
                case "sfx":
                    settings.sfxPosition = Mathf.Clamp(value, 0, MAX_VOLUME_POSITION);
                    break;
                case "fullscreen":
                    settings.fullscreenPosition = Mathf.Clamp(value, 0, 1);
                    break;
            }
        }

        return settings;
    }
}
```

`OptionsMenu.cs` — drop `using System.Runtime.Serialization.Formatters.Binary;`, delete the `OptionsSave` class, add `const string SETTINGS_FILE_NAME = "settings.cfg";` and replace `SaveOptions`/the load part of `LoadOptions`:

```csharp
    string SettingsFilePath => Path.Combine(Application.persistentDataPath, SETTINGS_FILE_NAME);

    ///<summary>
    /// Saves the game's option settings
    ///</summary>
    public void SaveOptions()
    {
        GameSettings settings = new GameSettings(bgmPosition, sfxPosition, fullscreenPosition);

        try
        {
            File.WriteAllText(SettingsFilePath, settings.ToText());
        }
        catch (Exception e) // a full disk or read-only folder shouldn't break the menu
        {
            Debug.LogWarning($"Could not save settings to {SettingsFilePath}: {e.Message}");
        }
    }

    ///<summary>
    /// Loads the game's option settings
    ///</summary>
    public void LoadOptions()
    {
        // The values set in the inspector are the defaults
        GameSettings settings = new GameSettings(bgmPosition, sfxPosition, fullscreenPosition);

        try
        {
            if (File.Exists(SettingsFilePath))
                settings = GameSettings.Parse(File.ReadAllText(SettingsFilePath), settings);
        }
        catch (Exception e) // an unreadable file falls back to the defaults
        {
            Debug.LogWarning($"Could not load settings from {SettingsFilePath}: {e.Message}");
        }

        bgmPosition = settings.bgmPosition;
        sfxPosition = settings.sfxPosition;
        fullscreenPosition = settings.fullscreenPosition;

        // (unchanged from here: apply volumes and fullscreen)
```

- [ ] **Step 4: Run the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`
Expected: `26 total, 26 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: `GameSettings.cs`, `GameSettingsTests.cs`, `OptionsMenu.cs`.

---

## Task 6: Pause when a controller disconnects mid-race

**Files:**
- Create: `Assets/Scripts/Player/ControllerDisconnectPolicy.cs`, `Assets/Tests/Editor/ControllerDisconnectPolicyTests.cs` (+ `.meta`s)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (`AddPlayerReference` + new handler)

**Interfaces:**
- Produces: `ControllerDisconnectPolicy.ShouldPause(bool pauseMenuIsActive, bool gameIsPaused)`, `ControllerDisconnectPolicy.PickPauseHost(bool[] slotHasController, int disconnectedSlot)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    public class ControllerDisconnectPolicyTests
    {
        [Test]
        public void ShouldPause_WhileDriving_Pauses()
        {
            Assert.IsTrue(ControllerDisconnectPolicy.ShouldPause(pauseMenuIsActive: true, gameIsPaused: false));
        }

        [Test]
        public void ShouldPause_WhenAlreadyPaused_DoesNothing()
        {
            Assert.IsFalse(ControllerDisconnectPolicy.ShouldPause(pauseMenuIsActive: true, gameIsPaused: true));
        }

        [Test]
        public void ShouldPause_OutsideDriving_DoesNothing()
        {
            // Menus, cutscenes and the 3-2-1 countdown have no pause menu to resume from
            Assert.IsFalse(ControllerDisconnectPolicy.ShouldPause(pauseMenuIsActive: false, gameIsPaused: false));
        }

        [Test]
        public void PickPauseHost_FirstPlayerWithAControllerHostsThePause()
        {
            Assert.AreEqual(0, ControllerDisconnectPolicy.PickPauseHost(new[] { true, false, true, true }, 1));
        }

        [Test]
        public void PickPauseHost_SkipsTheDisconnectedPlayer()
        {
            Assert.AreEqual(1, ControllerDisconnectPolicy.PickPauseHost(new[] { false, true, true, false }, 0));
        }

        [Test]
        public void PickPauseHost_NobodyHasAController_DisconnectedPlayerHosts()
        {
            Assert.AreEqual(2, ControllerDisconnectPolicy.PickPauseHost(new[] { false, false, false, false }, 2));
        }
    }
}
```

Stub: both methods throw `NotImplementedException`.

- [ ] **Step 2: Run to watch them fail**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh ControllerDisconnectPolicyTests`
Expected: `6 total, 0 passed, 6 failed`.

- [ ] **Step 3: Implement** — `ControllerDisconnectPolicy.cs`:

```csharp
/// <summary>
/// Decides what happens when a player's controller disconnects mid-match
/// </summary>
public static class ControllerDisconnectPolicy
{
    ///<summary>
    /// Pauses only while players are driving (their pause menu is active) and the game isn't already paused
    ///</summary>
    public static bool ShouldPause(bool pauseMenuIsActive, bool gameIsPaused)
    {
        return pauseMenuIsActive && !gameIsPaused;
    }

    ///<summary>
    /// The first player who still has a controller runs the pause menu; if nobody does, the disconnected player
    ///</summary>
    public static int PickPauseHost(bool[] slotHasController, int disconnectedSlot)
    {
        for (int i = 0; i < slotHasController.Length; i++)
        {
            if (slotHasController[i] && i != disconnectedSlot)
                return i;
        }

        return disconnectedSlot;
    }
}
```

`PlayerInstantiate.cs` — in `AddPlayerReference`, right after `AddToPlayerArray(playerInput);`:

```csharp
        // Pauses the match if this player's controller disconnects
        playerInput.deviceLostEvent.AddListener(OnPlayerControllerLost);
```

and the handler:

```csharp
    ///<summary>
    /// Pauses for everyone when a player's controller disconnects mid-race, with a player who still has a controller running the pause menu
    ///</summary>
    private void OnPlayerControllerLost(PlayerInput lostPlayer)
    {
        int lostSlot = Array.IndexOf(availiblePlayerInputs, lostPlayer);
        if (lostSlot < 0)
            return;

        MenuInteractions lostPlayerMenu = lostPlayer.gameObject.GetComponent<PlayerUIHandler>().menuInteractions;
        if (!ControllerDisconnectPolicy.ShouldPause(lostPlayerMenu.curentMenuType == MenuType.PauseMenu, Time.timeScale == 0f))
            return;

        bool[] slotHasController = new bool[availiblePlayerInputs.Length];
        for (int i = 0; i < availiblePlayerInputs.Length; i++)
        {
            slotHasController[i] = availiblePlayerInputs[i] != null && !availiblePlayerInputs[i].hasMissingRequiredDevices;
        }

        int hostSlot = ControllerDisconnectPolicy.PickPauseHost(slotHasController, lostSlot);
        availiblePlayerInputs[hostSlot].gameObject.GetComponent<PlayerUIHandler>().menuInteractions.PauseGame(true);
    }
```

- [ ] **Step 4: Run the whole suite**

Run: `bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`
Expected: `32 total, 32 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: `ControllerDisconnectPolicy.cs`, its tests, `PlayerInstantiate.cs`. Manual check for your human partner: unplug a controller mid-race → everyone pauses; the first connected player can Resume or return to the menu; re-plugging lets the player continue.

---

## Task 7: License inventory and Steam store page draft

**Files:**
- Create: `LICENSES.md`, `docs/steam/store-page-draft.md`

No code; no tests (documentation).

- [ ] **Step 1:** Inventory third-party code/assets from `Assets/` and `Packages/` (license files, headers, package.json licenses) into `LICENSES.md`; mark anything without a found license as **UNKNOWN — confirm with the team**.
- [ ] **Step 2:** Draft `docs/steam/store-page-draft.md` from what the code and data actually contain (mechanics, modes, player count, controller support): short description (≤ 300 characters), long description, feature bullets, suggested tags, content survey notes, open legal flags (company names parody real delivery apps).
- [ ] **Step 3: Checkpoint** — files: `LICENSES.md`, `docs/steam/`.

---

## Task 8: Steamworks.NET bootstrap (approved 2026-09-22)

Steam starts when the game launches, using Valve's test App ID 480 (Spacewar) until the Dead on Arrival App ID is approved. No scene edits: the manager creates itself before the first scene loads. If Steam isn't running, the game carries on without it (local split-screen needs nothing from Steam).

**Files:**
- Modify: `Packages/manifest.json` — add `"com.rlabrecque.steamworks.net": "https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net#2025.164.1"`
- Create: `Assets/Scripts/Management/SteamStartup.cs`, `Assets/Scripts/Management/SteamManager.cs`, `Assets/Tests/Editor/SteamStartupTests.cs` (+ `.meta`s)
- Modify: `docs/steam/steampipe/app_build.vdf` — exclude `steam_appid.txt` from uploads
- Modify: `run-tests.sh` — `DOA_MIRROR` env var picks the mirror, so tests can run while a build holds the first one

**Interfaces:**
- Produces: `SteamStartup.TEST_APP_ID` (480), `SteamStartup.APP_ID`, `SteamStartup.ShouldRestartThroughSteam(uint appId, bool isEditor)`; `SteamManager.Initialized` (static bool — check before any Steamworks call).

- [ ] **Step 1: Write the failing tests** — `Assets/Tests/Editor/SteamStartupTests.cs`

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    public class SteamStartupTests
    {
        const uint SomeRealAppId = 1234560;

        [Test]
        public void TestAppId_NeverRelaunchesThroughSteam()
        {
            // Relaunching with 480 would start Valve's Spacewar instead of the game
            Assert.IsFalse(SteamStartup.ShouldRestartThroughSteam(SteamStartup.TEST_APP_ID, isEditor: false));
        }

        [Test]
        public void Editor_NeverRelaunchesThroughSteam()
        {
            Assert.IsFalse(SteamStartup.ShouldRestartThroughSteam(SomeRealAppId, isEditor: true));
        }

        [Test]
        public void RealAppIdInABuild_RelaunchesThroughSteam()
        {
            Assert.IsTrue(SteamStartup.ShouldRestartThroughSteam(SomeRealAppId, isEditor: false));
        }
    }
}
```

Stub: `SteamStartup` with both constants and `ShouldRestartThroughSteam` throwing `NotImplementedException`.

- [ ] **Step 2: Run to watch them fail**

Run: `DOA_MIRROR=D:/.PersonalWork/DoA/_doa_test_mirror2/CapstoneYear4 bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh SteamStartupTests`
Expected: `3 total, 0 passed, 3 failed` (NotImplementedException).

- [ ] **Step 3: Implement** — `SteamStartup.cs`:

```csharp
/// <summary>
/// Steam App ID and launch rules
/// </summary>
public static class SteamStartup
{
    /// <summary>
    /// Valve's public test app (Spacewar), used until the Dead on Arrival App ID is approved
    /// </summary>
    public const uint TEST_APP_ID = 480;

    /// <summary>
    /// The game's Steam App ID. Replace with the real one once Steamworks approves the app (and update steam_appid.txt)
    /// </summary>
    public const uint APP_ID = TEST_APP_ID;

    ///<summary>
    /// Only a real App ID in a player build relaunches the game through the Steam client
    ///</summary>
    public static bool ShouldRestartThroughSteam(uint appId, bool isEditor)
    {
        return !isEditor && appId != TEST_APP_ID;
    }
}
```

Add the package to `Packages/manifest.json` and create `SteamManager.cs`: guarded by `DISABLESTEAMWORKS` for non-desktop platforms; `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` resets statics; `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` creates a `DontDestroyOnLoad` GameObject with the manager; `Awake` runs `Packsize.Test()`/`DllCheck.Test()`, relaunches through Steam only when `ShouldRestartThroughSteam` says so, then `SteamAPI.Init()` inside a `try` (a missing `steam_api64.dll` or Steam not running logs a warning and the game continues); `OnEnable` hooks `SteamClient.SetWarningMessageHook`; `Update` calls `SteamAPI.RunCallbacks()`; `OnDestroy` calls `SteamAPI.Shutdown()`.

- [ ] **Step 4: Run the whole suite** (this also proves the package resolves and `SteamManager` compiles against it)

Run: `DOA_MIRROR=D:/.PersonalWork/DoA/_doa_test_mirror2/CapstoneYear4 bash .superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`
Expected: `35 total, 35 passed, 0 failed`.

- [ ] **Step 5: Checkpoint** — files: manifest, `SteamStartup.cs`, `SteamManager.cs`, `SteamStartupTests.cs` (+ metas), `app_build.vdf`. Manual check for your human partner: with Steam running, press Play from SplashScreen → Steam shows you "playing Spacewar" and Shift+Tab opens the overlay in a build.

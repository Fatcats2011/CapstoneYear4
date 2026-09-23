# Phase 2A — Match Smoke Test and Player Roster Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An automated 4-player local match test that fails on any error, then one player roster (who is in each of the 4 slots) that every system reads instead of `PlayerInstantiate.PlayerInputs`, ready for online players who have no controller or camera on this machine.

**Architecture:** The smoke test is an EditMode `[UnityTest]` that enters Play Mode in the menu scene, joins 4 virtual gamepads and plays through the menus, loading screen, opening cutscene, a skipped tutorial, driving, pausing and a controller unplug/replug, collecting every error Unity logs. `PlayerRoster` (a plain C# class owned by `PlayerInstantiate`) holds 4 `PlayerSlot`s; `PlayerCount` comes from it. Systems loop over `Players` (everyone: spawns, scores, cutouts, tutorial orders, lobby prompts, loading buttons) or `LocalPlayers` (things that need this machine's cameras, menus or controllers). Offline every player is local, so the game plays exactly as before.

**Tech Stack:** Unity 2022.3.62f3 · Input System 1.14.0 · Unity Test Framework 1.1.33 (EditMode tests, `EnterPlayMode`) · bash + robocopy test runner.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` — Phase 2 goal and Task 2.1 (Player roster), the Task 1.1 `SpawnManager` item that was moved into Task 2.1, and the §R checklist.

**Not in this plan (and why):** Task 2.2 (split the player prefab — prefab edits need the Unity editor), Tasks 2.3 input abstraction, 2.4 authority seam and 2.5 scene flow seam (next plan, Phase 2B). Colour and hat stay in `CustomizationSelector`: nothing outside the lobby reads them until Phase 3's `NetworkPlayer` sends them over the network, so the slot only gets the company (which the slot decides).

**Findings behind two tasks (2026-09-23):**
- A test can press Play and run the game unattended (`EnterPlayMode` in a batch-mode EditMode test), but only with a graphics device: with `-nographics`, URP crashes Unity when the menu scene's off-screen cameras render. The current runner passes `-nographics`, so Task 1 replaces it.
- `SplashScreen.unity` saved its link to the menu scene as build index 0 (itself). The editor fixes the link whenever it saves or plays the open scene, and builds do the same, so players never see it; a test that loads the splash screen from disk loops forever. The smoke test starts in the menu scene instead (the splash scene is only a logo).
- `FinalAreaScene` (and `Design Scene(Main)`) list 3 golden-round spawn points. With 4 players, `SpawnPlayersFinalPackage` throws for player 4 inside an empty `catch`, so player 4 is never moved to the start of the golden round. `gameSpawnPositions[3]` ("Spawn 4") sits in line with the 3 golden points, so it is the fallback (Task 4).

## Global Constraints

- Unity 2022.3.62f3 LTS; no package upgrades or additions.
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`). Offline play must stay identical and keep passing the roadmap's §R checklist.
- Do not edit scenes, prefabs or `ProjectSettings/` — the Unity editor has the project open. Scripts, tests, tools and docs only; anything that needs the editor goes to the partner as short bullet steps.
- Don't rename serialized fields on scene or prefab components (their saved references would break). Removing a serialized field is fine: Unity ignores the leftover data.
- No git commits without explicit approval from your human partner. Work on branch `steam-phase2`, created from `steam-phase1a` at setup (no file changes); each task ends with a checkpoint listing its files.
- EditMode tests live in `Assets/Tests/Editor/` (compiles into `Assembly-CSharp-Editor`), namespace `DoA.Tests`; reuse `TestObjects` / `Reflect` from `TestSupport.cs`.
- Every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>` (Task 1 creates it).
- Run tests only through `bash tools/run-tests.sh [filter]` (Task 1 creates it; it syncs the project into the mirror and runs Unity in batch mode with graphics). Before Task 1 Step 1, the old runner is `.superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`.
- Change existing files with the Edit tool only (never `sed -i`: it rewrites CRLF files as LF). These are committed with CRLF (`i/crlf`) and must stay CRLF: `PlayerInstantiate.cs`, `MenuInteractions.cs`, `MainMenu.cs`, `PlayerSelectCanvas.cs`, `SceneManager.cs`, `LoadingScreenManager.cs`, `BeconIndicator.cs`, `DrivingIndicators.cs`, `BoostPadManager.cs`. New files may be LF. Check with `git ls-files --eol` or byte counts, not `grep $'\r'`.
- Developer-only features go through `DevTools`.

## Review Focus

1. A player leaves in the lobby (B) while others stay, then a new controller joins → the newcomer takes the freed slot (lowest free), with that slot's company, layers and viewport; everyone else keeps theirs. (Roster rule: Task 2 `JoinLocal_AfterSomeoneLeft_FillsTheLowestFreeSlot`. The lobby path through `MenuInteractions` and `AddPlayerReference` has no test.)
2. Four players reach the golden round → player 4 starts next to the others, although the scene lists 3 golden spawn points. (Task 4 `SpawnPlayersFinalPackage_FourPlayersThreeGoldenPoints_EveryoneStartsOnASpawnPoint`.)
3. Results → back to the menu → a second match: players, join prompts, scores, cutouts and loading buttons reset, and nobody is counted twice now that `PlayerCount` has no counter of its own. (No test: the smoke test stops in wave 1.)
4. The L developer key on the title screen clears everyone → `PlayerCount` 0, every join prompt back, and the next controller becomes player 1 and runs the menus. (Task 3 `ClearPlayerArray_EmptiesTheRoster`; the `MainMenu` L path has no test.)
5. A controller disconnects on the title screen, in player select or on the loading screen → no pause, only the reconnect message, and another controller can take over (Phase 1B behaviour, now looping over `LocalPlayers`). (No test: the smoke test disconnects mid-race only.)

---

### Task 1: Match smoke test and a test runner with graphics

**Files:**
- Create: `tools/run-tests.sh` (replaces `.superpowers/sdd/2026-09-22-phase1a-release-hardening/run-tests.sh`: graphics on, paths from its own location, results in `Logs/`, `DOA_NO_SYNC`)
- Create: `tools/newmeta.sh` (copy of `.superpowers/sdd/2026-09-22-phase1a-release-hardening/newmeta.sh`)
- Create: `Assets/Tests/Editor/LogCollector.cs` (+ `.meta`)
- Create: `Assets/Tests/Editor/LogCollectorTests.cs` (+ `.meta`)
- Create: `Assets/Tests/Editor/LocalMatchSmokeTest.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `TestPlayers.Add()`, `TestPlayers.Pads`, `TestPlayers.RemoveAll()`, `PlayerInstantiate.Instance.PlayerCount`, `GameManager.Instance.MainState`, `TutorialHandler.TeachHandler(TutorialType.Final)`, `OrderManager.Instance.DeleteActiveOrders()`, `ControllerPrompts.Exists`, `ControllerPrompts.Instance.IsReconnectShown(int)`.
- Produces: `bash tools/run-tests.sh [filter]` (exit 0 only when ≥ 1 test ran and none failed; results `Logs/test-results.xml`, log `Logs/test-unity.log`; env `DOA_MIRROR`, `DOA_UNITY`, `DOA_NO_SYNC=1`); `bash tools/newmeta.sh <path> [folder]`; `DoA.Tests.LogCollector : IDisposable` with `IReadOnlyList<string> Problems`; test `DoA.Tests.LocalMatchSmokeTest.FourPlayers_PlayFromTheMenuIntoTheFirstWave_WithoutErrors` (category `Smoke`). The smoke test reads players only through `PlayerCount` and `FindObjectsOfType`, so later tasks don't have to change it.

- [ ] **Step 1: Create the tools**

`tools/newmeta.sh` — copy it unchanged:

```bash
mkdir -p tools
cp .superpowers/sdd/2026-09-22-phase1a-release-hardening/newmeta.sh tools/newmeta.sh
```

`tools/run-tests.sh`:

```bash
#!/usr/bin/env bash
# Runs the project's EditMode tests in a mirror copy of the project, so a Unity editor open on the real project
# keeps its lock. Unity runs with a graphics device: the match smoke test renders real scenes, and without one URP
# crashes Unity as soon as the menu scene's cameras render.
#
# Usage:  tools/run-tests.sh [test-filter]   test-filter = NUnit class or test name, or a regex; default: every test
# Env:    DOA_MIRROR   mirror project (default: _doa_test_mirror/<project folder> next to the project; keep it at a
#                      short path - Windows paths over 260 characters break the import)
#         DOA_UNITY    Unity.exe (default: Unity Hub's 2022.3.62f3)
#         DOA_NO_SYNC  1 = test the mirror as it is, without copying the project into it first
# Output: Logs/test-results.xml and Logs/test-unity.log in the project; failures and a summary line on stdout.
# Exit:   0 only when at least one test ran and none failed.
set -uo pipefail

REAL="$(cd "$(dirname "$0")/.." && pwd)"
MIRROR="${DOA_MIRROR:-$(dirname "$REAL")/_doa_test_mirror/$(basename "$REAL")}"
UNITY="${DOA_UNITY:-C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe}"
OUT="$REAL/Logs"
filter="${1:-}"

mkdir -p "$OUT"
if [ "${DOA_NO_SYNC:-}" != "1" ]; then
  for d in Assets Packages ProjectSettings; do
    MSYS_NO_PATHCONV=1 robocopy "$(cygpath -w "$REAL/$d")" "$(cygpath -w "$MIRROR/$d")" /MIR /R:1 /W:1 /NFL /NDL /NP /NJH /NJS > /dev/null
    rc=$?
    if [ "$rc" -ge 8 ]; then echo "sync of $d failed (robocopy exit $rc)"; exit 1; fi
  done
fi

results="$OUT/test-results.xml"
log="$OUT/test-unity.log"
rm -f "$results"
args=(-batchmode -projectPath "$(cygpath -w "$MIRROR")" -runTests -testPlatform EditMode
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

Then `chmod +x tools/run-tests.sh tools/newmeta.sh`.

- [ ] **Step 2: Run the existing suite through the new runner**

Run: `bash tools/run-tests.sh`
Expected: `tests: 75 total, 75 passed, 0 failed, 0 skipped` (same suite, now with a graphics device).

- [ ] **Step 3: Write the failing LogCollector tests**

`Assets/Tests/Editor/LogCollectorTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/LogCollectorTests.cs`):

```csharp
using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class LogCollectorTests
    {
        [Test]
        public void RecordsErrorsAndExceptions_NotMessagesOrWarnings()
        {
            using (LogCollector collector = new LogCollector())
            {
                Debug.Log("just information");
                Debug.LogWarning("just a warning");
                LogAssert.Expect(LogType.Error, "something broke");
                Debug.LogError("something broke");
                LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException: it threw"));
                Debug.LogException(new InvalidOperationException("it threw"));

                Assert.AreEqual(2, collector.Problems.Count);
                StringAssert.Contains("something broke", collector.Problems[0]);
                StringAssert.Contains("it threw", collector.Problems[1]);
            }
        }

        [Test]
        public void StopsListeningOnceDisposed()
        {
            LogCollector collector = new LogCollector();
            collector.Dispose();

            LogAssert.Expect(LogType.Error, "after dispose");
            Debug.LogError("after dispose");

            Assert.AreEqual(0, collector.Problems.Count);
        }
    }
}
```

- [ ] **Step 4: Run it to see it fail**

Run: `bash tools/run-tests.sh LogCollectorTests`
Expected: `NO RESULTS`, with `error CS0246: The type or namespace name 'LogCollector' could not be found`.

- [ ] **Step 5: Write LogCollector**

`Assets/Tests/Editor/LogCollector.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/LogCollector.cs`):

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Records every error, exception and failed assert Unity logs while it's listening, so a long test can report
    /// all of them at once instead of stopping at the first
    /// </summary>
    public sealed class LogCollector : IDisposable
    {
        readonly List<string> problems = new List<string>();

        public LogCollector()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        /// <summary>
        /// Everything that went wrong so far, oldest first: the log type, the message and where it came from
        /// </summary>
        public IReadOnlyList<string> Problems => problems;

        public void Dispose()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        void OnLogMessage(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                problems.Add(type + ": " + message + "\n" + FirstLines(stackTrace, 4));
        }

        static string FirstLines(string text, int count)
        {
            string[] lines = (text ?? "").Split('\n');
            return string.Join("\n", lines, 0, Math.Min(count, lines.Length)).TrimEnd();
        }
    }
}
```

- [ ] **Step 6: Run it to see it pass**

Run: `bash tools/run-tests.sh LogCollectorTests`
Expected: `tests: 2 total, 2 passed, 0 failed, 0 skipped`.

- [ ] **Step 7: Write the smoke test**

`Assets/Tests/Editor/LocalMatchSmokeTest.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/LocalMatchSmokeTest.cs`):

```csharp
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
    /// player select, loading screen, opening cutscene, tutorial (skipped the way the S hotkey skips it), driving,
    /// pausing, and a controller that dies mid-race and comes back.
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

            // Four controllers join; the first is player 1, who runs the menus
            for (int joined = 1; joined <= Constants.MAX_PLAYERS; joined++)
            {
                TestPlayers.Add();
                deadline = Deadline(10);
                while (Players() < joined)
                {
                    FailIfLate(log, deadline, "player " + joined + " to join");
                    yield return null;
                }
            }
            Gamepad[] pads = TestPlayers.Pads.ToArray();
            Gamepad host = pads[0];

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

            // Everyone holds the throttle for 4 seconds
            BallDriving[] scooters = UnityEngine.Object.FindObjectsOfType<BallDriving>();
            Vector3[] startPositions = scooters.Select(s => s.transform.position).ToArray();
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
```

- [ ] **Step 8: Run the smoke test on today's game**

Run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed, 0 failed, 0 skipped`. It pins how the game plays today, so it must pass before any refactor. If it fails on an error the game already logs today, ledger the exact message and rule on it (fix it if it's a one-line bug, otherwise let the collector skip that one message with a comment saying why) — never loosen the check as a whole.

- [ ] **Step 9: Prove the smoke test catches a broken game**

Break the mirror copy only (the next sync restores it) and run it without syncing:

```bash
MIRROR="$(dirname "$PWD")/_doa_test_mirror/$(basename "$PWD")"
python - "$MIRROR/Assets/Scripts/Player/SpawnManager.cs" <<'EOF'
import sys
path = sys.argv[1]
text = open(path, encoding="utf-8", newline="").read()
text = text.replace("GameObject[] spawnPoints = gameSpawnPositions;",
                    'Debug.LogError("smoke test self-check");\n        GameObject[] spawnPoints = gameSpawnPositions;', 1)
open(path, "w", encoding="utf-8", newline="").write(text)
EOF
DOA_NO_SYNC=1 bash tools/run-tests.sh LocalMatchSmokeTest
```

Expected: `FAIL DoA.Tests.LocalMatchSmokeTest.FourPlayers_PlayFromTheMenuIntoTheFirstWave_WithoutErrors: Errors during the match: ... Error: smoke test self-check ...`, then `tests: 1 total, 0 passed, 1 failed`.

Then run: `bash tools/run-tests.sh LocalMatchSmokeTest`
Expected: `tests: 1 total, 1 passed` (the sync put the real `SpawnManager.cs` back).

- [ ] **Step 10: Run the whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 78 total, 78 passed, 0 failed, 0 skipped`.

- [ ] **Step 11: Checkpoint**

Files: `tools/run-tests.sh`, `tools/newmeta.sh`, `Assets/Tests/Editor/LogCollector.cs(.meta)`, `Assets/Tests/Editor/LogCollectorTests.cs(.meta)`, `Assets/Tests/Editor/LocalMatchSmokeTest.cs(.meta)`. No commit.

---

### Task 2: PlayerRoster and PlayerSlot

**Files:**
- Create: `Assets/Scripts/Player/PlayerSlot.cs` (+ `.meta`)
- Create: `Assets/Scripts/Player/PlayerRoster.cs` (+ `.meta`)
- Test: `Assets/Tests/Editor/PlayerRosterTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `Constants.MAX_PLAYERS`, `CompanyInformation`.
- Produces:
  - `public class PlayerSlot` — `PlayerSlot(int index, GameObject player, PlayerInput input, ulong ownerClientId)`; `int Index { get; }` (0–3); `GameObject Player { get; }` (the player's top object); `PlayerInput Input { get; }` (null for an online player on another machine); `bool IsLocal { get; }` (= `Input != null`); `ulong OwnerClientId { get; }` (0 offline); `CompanyInformation Company { get; set; }`.
  - `public class PlayerRoster` — `int Count { get; }`; `PlayerSlot this[int index] { get; }` (null when free); `IEnumerable<PlayerSlot> Players { get; }` (taken slots, slot order); `IEnumerable<PlayerSlot> LocalPlayers { get; }`; `int LocalCount { get; }`; `PlayerSlot JoinLocal(PlayerInput input)` (lowest free slot; null when full, already in, or `input` is null); `PlayerSlot JoinRemote(GameObject player, ulong ownerClientId)` (same rules, for Phase 3); `int Leave(PlayerInput input)` (freed slot or -1; null never matches an online player); `int IndexOf(PlayerInput input)` (-1 when not in); `void Clear()`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/PlayerRosterTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/PlayerRosterTests.cs`):

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class PlayerRosterTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        PlayerInput NewController(string name)
        {
            return objects.NewGameObject(name).AddComponent<PlayerInput>();
        }

        [Test]
        public void JoinLocal_FirstPlayer_TakesSlotZeroAsALocalPlayer()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("P1");

            PlayerSlot slot = roster.JoinLocal(first);

            Assert.AreEqual(0, slot.Index);
            Assert.AreSame(first, slot.Input);
            Assert.AreSame(first.gameObject, slot.Player);
            Assert.IsTrue(slot.IsLocal);
            Assert.AreEqual(0UL, slot.OwnerClientId);
            Assert.AreSame(slot, roster[0]);
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void JoinLocal_AfterSomeoneLeft_FillsTheLowestFreeSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            PlayerInput second = NewController("P2");
            roster.JoinLocal(second);
            roster.JoinLocal(NewController("P3"));
            roster.Leave(second);

            PlayerSlot newcomer = roster.JoinLocal(NewController("Newcomer"));

            Assert.AreEqual(1, newcomer.Index);
            Assert.AreEqual(3, roster.Count);
        }

        [Test]
        public void JoinLocal_WhenAllFourSlotsAreTaken_ReturnsNull()
        {
            PlayerRoster roster = new PlayerRoster();
            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
                roster.JoinLocal(NewController("P" + (i + 1)));

            Assert.IsNull(roster.JoinLocal(NewController("Fifth")));
            Assert.AreEqual(Constants.MAX_PLAYERS, roster.Count);
        }

        [Test]
        public void JoinLocal_SameControllerTwice_KeepsOneSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("P1");
            roster.JoinLocal(first);

            Assert.IsNull(roster.JoinLocal(first));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Leave_FreesOnlyThatPlayersSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("P1");
            PlayerInput second = NewController("P2");
            roster.JoinLocal(first);
            roster.JoinLocal(second);

            Assert.AreEqual(1, roster.Leave(second));
            Assert.AreSame(first, roster[0].Input);
            Assert.IsNull(roster[1]);
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Leave_PlayerNotInTheRoster_ReturnsMinusOne()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));

            Assert.AreEqual(-1, roster.Leave(NewController("Stranger")));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Leave_NoController_NeverRemovesAnOnlinePlayer()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinRemote(objects.NewGameObject("Online player"), 7);

            Assert.AreEqual(-1, roster.Leave(null));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Players_ListsTakenSlotsInSlotOrder()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            PlayerInput second = NewController("P2");
            roster.JoinLocal(second);
            roster.JoinLocal(NewController("P3"));
            roster.Leave(second);

            CollectionAssert.AreEqual(new[] { 0, 2 }, roster.Players.Select(p => p.Index).ToArray());
        }

        [Test]
        public void JoinRemote_OnlinePlayer_HasNoControllerAndIsLeftOutOfLocalPlayers()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            GameObject online = objects.NewGameObject("Online player");

            PlayerSlot slot = roster.JoinRemote(online, 7);

            Assert.AreEqual(1, slot.Index);
            Assert.IsFalse(slot.IsLocal);
            Assert.IsNull(slot.Input);
            Assert.AreSame(online, slot.Player);
            Assert.AreEqual(7UL, slot.OwnerClientId);
            Assert.AreEqual(2, roster.Count);
            Assert.AreEqual(1, roster.LocalCount);
            CollectionAssert.AreEqual(new[] { 0 }, roster.LocalPlayers.Select(p => p.Index).ToArray());
        }

        [Test]
        public void JoinRemote_SamePlayerTwice_KeepsOneSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            GameObject online = objects.NewGameObject("Online player");
            roster.JoinRemote(online, 7);

            Assert.IsNull(roster.JoinRemote(online, 7));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void IndexOf_FindsLocalPlayersBySlot()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            PlayerInput second = NewController("P2");
            roster.JoinLocal(second);

            Assert.AreEqual(1, roster.IndexOf(second));
            Assert.AreEqual(-1, roster.IndexOf(NewController("Stranger")));
        }

        [Test]
        public void Clear_FreesEverySlot()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            roster.JoinRemote(objects.NewGameObject("Online player"), 7);

            roster.Clear();

            Assert.AreEqual(0, roster.Count);
            Assert.IsEmpty(roster.Players);
            Assert.AreEqual(0, roster.JoinLocal(NewController("Next")).Index);
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh PlayerRosterTests`
Expected: `NO RESULTS`, with `error CS0246: The type or namespace name 'PlayerRoster' could not be found` (and `PlayerSlot`).

- [ ] **Step 3: Write PlayerSlot and PlayerRoster**

`Assets/Scripts/Player/PlayerSlot.cs` (then `bash tools/newmeta.sh Assets/Scripts/Player/PlayerSlot.cs`):

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A taken player slot. The slot number decides the player's layers, renderers, company and split-screen order
/// </summary>
public class PlayerSlot
{
    public PlayerSlot(int index, GameObject player, PlayerInput input, ulong ownerClientId)
    {
        Index = index;
        Player = player;
        Input = input;
        OwnerClientId = ownerClientId;
    }

    /// <summary>
    /// Slot number, 0-3 (player 1 is slot 0)
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The player's top object: their scooter and orders, plus cameras and menus for a player on this machine
    /// </summary>
    public GameObject Player { get; }

    /// <summary>
    /// Controls of a player on this machine (their PlayerInput also holds their camera and menus); null for an online player on another machine
    /// </summary>
    public PlayerInput Input { get; }

    /// <summary>
    /// Plays on this machine: has a controller, a split-screen view and menus here
    /// </summary>
    public bool IsLocal => Input != null;

    /// <summary>
    /// The online client that owns this player (0 when playing offline)
    /// </summary>
    public ulong OwnerClientId { get; }

    /// <summary>
    /// The delivery company this slot plays for
    /// </summary>
    public CompanyInformation Company { get; set; }
}
```

`Assets/Scripts/Player/PlayerRoster.cs` (then `bash tools/newmeta.sh Assets/Scripts/Player/PlayerRoster.cs`):

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Who is in each of the 4 player slots: the one list of players every system reads. A player who joins takes the
/// lowest free slot, and leaving frees it. Offline every player is local (controller, split-screen view and menus on
/// this machine); online players on other machines are remote
/// </summary>
public class PlayerRoster
{
    readonly PlayerSlot[] slots = new PlayerSlot[Constants.MAX_PLAYERS];

    /// <summary>
    /// How many slots are taken
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// The player in a slot (0-3), or null when the slot is free
    /// </summary>
    public PlayerSlot this[int index] => slots[index];

    /// <summary>
    /// Every player, in slot order
    /// </summary>
    public IEnumerable<PlayerSlot> Players
    {
        get
        {
            foreach (PlayerSlot slot in slots)
            {
                if (slot != null)
                    yield return slot;
            }
        }
    }

    /// <summary>
    /// Players on this machine, in slot order (the order of the split-screen views)
    /// </summary>
    public IEnumerable<PlayerSlot> LocalPlayers
    {
        get
        {
            foreach (PlayerSlot slot in slots)
            {
                if (slot != null && slot.IsLocal)
                    yield return slot;
            }
        }
    }

    /// <summary>
    /// How many players are on this machine
    /// </summary>
    public int LocalCount
    {
        get
        {
            int count = 0;
            foreach (PlayerSlot slot in LocalPlayers)
                count++;
            return count;
        }
    }

    ///<summary>
    /// Puts a player on this machine in the lowest free slot. Returns their slot, or null when all 4 are taken or they already have one
    ///</summary>
    public PlayerSlot JoinLocal(PlayerInput input)
    {
        int index = FirstFreeSlot();
        if (input == null || index < 0 || IndexOf(input) >= 0)
            return null;

        return Take(new PlayerSlot(index, input.gameObject, input, 0));
    }

    ///<summary>
    /// Puts an online player from another machine in the lowest free slot. Returns their slot, or null when all 4 are taken or they already have one
    ///</summary>
    public PlayerSlot JoinRemote(GameObject player, ulong ownerClientId)
    {
        int index = FirstFreeSlot();
        if (player == null || index < 0 || SlotOfPlayer(player) >= 0)
            return null;

        return Take(new PlayerSlot(index, player, null, ownerClientId));
    }

    ///<summary>
    /// Frees the slot of a player on this machine. Returns the freed slot, or -1 if they weren't in the roster
    ///</summary>
    public int Leave(PlayerInput input)
    {
        int index = IndexOf(input);
        if (index >= 0)
        {
            slots[index] = null;
            Count--;
        }
        return index;
    }

    ///<summary>
    /// The slot of a player on this machine, or -1 if they aren't in the roster
    ///</summary>
    public int IndexOf(PlayerInput input)
    {
        if (input == null)
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].Input == input)
                return i;
        }
        return -1;
    }

    ///<summary>
    /// Frees every slot
    ///</summary>
    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;
        Count = 0;
    }

    int FirstFreeSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                return i;
        }
        return -1;
    }

    int SlotOfPlayer(GameObject player)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].Player == player)
                return i;
        }
        return -1;
    }

    PlayerSlot Take(PlayerSlot slot)
    {
        slots[slot.Index] = slot;
        Count++;
        return slot;
    }
}
```

- [ ] **Step 4: Run them to see them pass**

Run: `bash tools/run-tests.sh PlayerRosterTests`
Expected: `tests: 12 total, 12 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: Run the whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 90 total, 90 passed, 0 failed, 0 skipped`.

- [ ] **Step 6: Checkpoint**

Files: `Assets/Scripts/Player/PlayerSlot.cs(.meta)`, `Assets/Scripts/Player/PlayerRoster.cs(.meta)`, `Assets/Tests/Editor/PlayerRosterTests.cs(.meta)`. No commit.

---

### Task 3: PlayerInstantiate keeps its players in the roster

**Files:**
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF) — fields `:21-27`, `AddPlayerReference :125-248`, `SetAllPlayerSpawn :253-271`, `UpdatePlayerCameraRects :276-302`, `RemovePlayerRef :307-331`, `SubtractPlayerCount :333-336`, `ClearPlayerArray :341-354`, `AddToPlayerArray`/`RemoveFromPlayerArray :359-385`, `ReadyUp`/`UnreadyUp :390-410`, `SwapMenuTypeForAllPlayers :559-579`, the three `SwapPlayerInputControlSchemeTo*` `:584-631`, `ResetPlayerCanvas :667-676`, `PlayerPause :681-704`, `OnPlayerControllerLost :717-738`, `GiveControllerToMissingPlayer :751-767`, `SlotsMissingController :781-789`, `UpdateControllerPrompts :794-815`, `PlayerPlay :820-833`, `PlayerUpdateDrivingIndicators :838-846`
- Modify: `Assets/Scripts/Menu/MenuInteractions.cs:256` (CRLF) — drop the `SubtractPlayerCount()` call
- Modify: `Assets/Tests/Editor/PlayerSlotTests.cs` (LF)
- Modify: `Assets/Tests/Editor/ControllerPromptsTests.cs:118` — it set the removed `playerCount` field

**Interfaces:**
- Consumes: `PlayerRoster`, `PlayerSlot` (Task 2).
- Produces: `PlayerRoster PlayerInstantiate.Roster { get; }`; `int PlayerCount` (now `Roster.Count`); temporary `PlayerInput[] PlayerInputs` (local players by slot, built from the roster; deleted in Task 5). Removed: `SubtractPlayerCount()`, `AddToPlayerArray(PlayerInput)`, `RemoveFromPlayerArray(PlayerInput)`, the serialized `playerCount` and `availiblePlayerInputs` fields.

- [ ] **Step 1: Write the failing tests**

Replace `Assets/Tests/Editor/PlayerSlotTests.cs` with this (the two `RemoveFromPlayerArray` tests moved into `PlayerRosterTests` as `Leave_*`):

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

        /// <summary>
        /// The lobby's "press a button to join" prompts, one per slot, all hidden
        /// </summary>
        GameObject[] SetUpLobby()
        {
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
            return joinPrompts;
        }

        PlayerInput NewPlayerWithCamera(string name)
        {
            PlayerInput player = objects.NewGameObject(name).AddComponent<PlayerInput>();
            player.camera = objects.Add<Camera>();
            return player;
        }

        [Test]
        public void PlayerCount_IsHowManyPlayersAreInTheRoster()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            instantiate.Roster.JoinRemote(objects.NewGameObject("Online player"), 2);

            Assert.AreEqual(2, instantiate.PlayerCount);
        }

        [Test]
        public void RemovePlayerRef_PlayerNotInLobby_LeavesPlayerOnesJoinPromptAlone()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] joinPrompts = SetUpLobby();
            PlayerInput stranger = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.RemovePlayerRef(stranger);

            Assert.IsFalse(joinPrompts[0].activeSelf);
        }

        [Test]
        public void RemovePlayerRef_JoinedPlayer_FreesTheirSlotAndGivesTheOthersTheScreen()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] joinPrompts = SetUpLobby();
            PlayerInput first = NewPlayerWithCamera("P1");
            PlayerInput second = NewPlayerWithCamera("P2");
            instantiate.Roster.JoinLocal(first);
            instantiate.Roster.JoinLocal(second);
            first.camera.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f);
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.RemovePlayerRef(second);

            Assert.AreEqual(1, instantiate.PlayerCount);
            Assert.IsNull(instantiate.Roster[1]);
            Assert.IsTrue(joinPrompts[1].activeSelf);
            Assert.AreEqual(new Rect(0, 0, 1, 1), first.camera.rect);
        }

        [Test]
        public void ClearPlayerArray_EmptiesTheRoster()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.ClearPlayerArray();

            Assert.AreEqual(0, instantiate.PlayerCount);
            Assert.IsEmpty(instantiate.Roster.Players);
        }
    }
}
```

In `Assets/Tests/Editor/ControllerPromptsTests.cs` (`AddPlayerReference_KeyboardDuringAMatch_ShowsNoHint`), the match's two players join through the roster instead of a hand-set counter — replace

```csharp
            Reflect.SetField(instantiate, "playerCount", 2);
```

with

```csharp
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "PlayerSlotTests|ControllerPromptsTests"`
Expected: `NO RESULTS`, with `error CS1061: 'PlayerInstantiate' does not contain a definition for 'Roster'`.

- [ ] **Step 3: Put PlayerInstantiate's players in the roster**

In `Assets/Scripts/Player/PlayerInstantiate.cs` (Edit tool, CRLF stays):

Fields — replace

```csharp
    [Tooltip("This value is our current player count, ie the # of players in match")]
    [SerializeField] int playerCount = 0;
    public int PlayerCount { get { return playerCount; } }

    [Tooltip("the list of availible player input class objects")]
    [SerializeField] PlayerInput[] availiblePlayerInputs = new PlayerInput[Constants.MAX_PLAYERS];
    public PlayerInput[] PlayerInputs { get { return availiblePlayerInputs; } }
```

with

```csharp
    // Who is in each of the 4 player slots
    readonly PlayerRoster roster = new PlayerRoster();
    public PlayerRoster Roster { get { return roster; } }

    // How many players have joined
    public int PlayerCount { get { return roster.Count; } }

    ///<summary>
    /// Players on this machine by slot, null where a slot is free. Temporary: the code still reading it moves to Roster, then it goes
    ///</summary>
    public PlayerInput[] PlayerInputs
    {
        get
        {
            PlayerInput[] inputs = new PlayerInput[Constants.MAX_PLAYERS];
            foreach (PlayerSlot slot in roster.LocalPlayers)
                inputs[slot.Index] = slot.Input;
            return inputs;
        }
    }
```

`AddPlayerReference` — the two early exits read `PlayerCount` instead of `playerCount`:

```csharp
            if (allowPlayerSpawn || PlayerCount == 0)
```

```csharp
        if(allowPlayerSpawn == false && PlayerCount >= 1)
```

then replace everything from `if (playerCount <= 0)` down to the end of the `for` loop that finds `nextFillSlot` with

```csharp
        bool isFirstPlayer = PlayerCount == 0;

        // Takes the lowest free slot
        PlayerSlot slot = roster.JoinLocal(playerInput);
        if (slot == null)
        {
            Destroy(playerInput.gameObject);
            return;
        }

        if (isFirstPlayer)
        {

            playerInput.gameObject.GetComponent<PlayerUIHandler>().menuInteractions.hostPlayer = true;
            playerInput.gameObject.GetComponent<PlayerUIHandler>().menuInteractions.SwapMenuType(MenuType.MainMenu);

            MainMenu.Instance.Player1ControllerConnected(playerInput);
        }
        else
        {
            playerInput.gameObject.GetComponent<PlayerUIHandler>().menuInteractions.SwapToPlayerSelect();
        }

        GameObject ColliderObject = playerInput.gameObject.GetComponentInChildren<SphereCollider>().gameObject;
        BallDriving ballDriving = playerInput.gameObject.GetComponentInChildren<BallDriving>();
        Camera baseCam = playerInput.camera;
        PlayerCameraResizer playerCameraResizer = playerInput.gameObject.GetComponentInChildren<PlayerCameraResizer>();

        int nextFillSlot = slot.Index + 1;
        slot.Company = companies[slot.Index];
```

(the `switch (nextFillSlot)` block stays as it is). After the switch, `companies[nextFillSlot - 1]` becomes `slot.Company` in both places:

```csharp
        playerCameraResizer.InitalizeCompanyScooter(slot.Company);
```

```csharp
        playerInput.gameObject.GetComponentInChildren<OrderHandler>().CompanyInfo = slot.Company;
```

and delete the `AddToPlayerArray(playerInput);` line (the roster join above did it) with its blank line.

`SetAllPlayerSpawn` — replace the loop with

```csharp
        // Loops for all spawned players
        foreach (PlayerSlot slot in roster.Players)
        {
            int i = slot.Index;

            // Resets the velocity of the players
            slot.Player.GetComponentInChildren<Rigidbody>().velocity = Vector3.zero;

            // reset position and rotation of ball and controller
            slot.Player.GetComponentInChildren<Rigidbody>().transform.position = menuSpawnPositions[i].transform.position;
            slot.Player.GetComponentInChildren<Rigidbody>().transform.rotation = menuSpawnPositions[i].transform.rotation;

            slot.Player.GetComponentInChildren<BallDriving>().transform.position = menuSpawnPositions[i].transform.position;
            slot.Player.GetComponentInChildren<BallDriving>().transform.rotation = menuSpawnPositions[i].transform.rotation;
        }
```

`UpdatePlayerCameraRects` — replace the body with

```csharp
        // Only players on this machine have a split-screen view
        cameraRects = SplitScreenLayout.CalculateRects(roster.LocalCount);

        int cameraRectCounter = 0;

        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.camera.rect = cameraRects[cameraRectCounter];
            cameraRectCounter++;
        }
```

`RemovePlayerRef` — `int position = RemoveFromPlayerArray(playerInput);` becomes

```csharp
        int position = roster.Leave(playerInput);
```

and `ScoreManager.Instance.UpdateOrderHandlers(availiblePlayerInputs);` becomes

```csharp
        ScoreManager.Instance.UpdateOrderHandlers(PlayerInputs);
```

Delete `SubtractPlayerCount()` (the count now comes from the roster). In `CheckReadyUpCount` and `CheckLoadingConfirmCount`, `playerCount` becomes `PlayerCount` (both `readyUpCounter >= PlayerCount && PlayerCount >= 1`). Replace `ClearPlayerArray`'s loop with

```csharp
        foreach (PlayerSlot slot in roster.Players)
            Destroy(slot.Player);
        roster.Clear();
```

Delete `AddToPlayerArray` and `RemoveFromPlayerArray` with their summaries.

`ReadyUp` / `UnreadyUp` — `availiblePlayerInputs[playerIndexToReadyUp].gameObject.GetComponent<PlayerUIHandler>()` becomes `roster[playerIndexToReadyUp].Input.GetComponent<PlayerUIHandler>()` in both.

`SwapMenuTypeForAllPlayers` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.GetComponent<PlayerUIHandler>().menuInteractions.SwapMenuType(menuType);

            // If swapping to pause menu, reparent menu ui to game-camera
            if (menuType == MenuType.PauseMenu)
            {
                slot.Input.GetComponent<PlayerCameraResizer>().ReparentMenuCameraStack(true);
            }
            // If swapping to other menu, reparent menu ui to player-camera on main menu
            else if (menuType == MenuType.MainMenu || menuType == MenuType.ResultsMenu)
            {
                slot.Input.GetComponent<PlayerCameraResizer>().ReparentMenuCameraStack(false);
            }
        }
```

`SwapPlayerInputControlSchemeToUI` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.GetComponent<PlayerCameraResizer>().SwapCanvas(true);

            slot.Input.actions.FindActionMap("UI").Enable();
            slot.Input.actions.FindActionMap("Player").Disable();
            slot.Input.actions.FindActionMap("Load").Disable();
        }
```

`SwapPlayerInputControlSchemeToDrive` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.GetComponent<PlayerCameraResizer>().SwapCanvas(false);

            slot.Input.actions.FindActionMap("UI").Disable();
            slot.Input.actions.FindActionMap("Player").Enable();
            slot.Input.actions.FindActionMap("Load").Disable();
        }
```

`SwapPlayerInputControlSchemeToLoad` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.actions.FindActionMap("UI").Disable();
            slot.Input.actions.FindActionMap("Player").Disable();
            slot.Input.actions.FindActionMap("Load").Enable();
        }
```

`ResetPlayerCanvas` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
            slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>().ResetCanvas();
```

`PlayerPause` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            MenuInteractions menu = slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>();

            // For one who paused
            if (playerInput == slot.Input)
            {
                menu.hostPause = true;
                menu.pauseMenu.OnPause(PauseMenu.PauseType.Host);
            }
            else
            {
                menu.hostPause = false;
                menu.pauseMenu.OnPause(PauseMenu.PauseType.Sub);
            }
        }
```

`OnPlayerControllerLost` — `int lostSlot = Array.IndexOf(availiblePlayerInputs, lostPlayer);` becomes

```csharp
        int lostSlot = roster.IndexOf(lostPlayer);
```

and the controller check and pause call become

```csharp
        bool[] slotHasController = new bool[Constants.MAX_PLAYERS];
        for (int i = 0; i < slotHasController.Length; i++)
        {
            PlayerSlot slot = roster[i];
            slotHasController[i] = slot != null && slot.IsLocal && !IsMissingController(slot.Input.user);
        }

        int hostSlot = ControllerDisconnectPolicy.PickPauseHost(slotHasController, lostSlot);
        roster[hostSlot].Input.GetComponent<PlayerUIHandler>().menuInteractions.PauseGame(true);
```

`GiveControllerToMissingPlayer` — `PlayerInput player = availiblePlayerInputs[slot];` becomes

```csharp
        PlayerInput player = roster[slot].Input;
```

`SlotsMissingController` — replace the body with

```csharp
        bool[] missing = new bool[Constants.MAX_PLAYERS];
        for (int i = 0; i < missing.Length; i++)
        {
            PlayerSlot slot = roster[i];
            missing[i] = slot != null && slot.IsLocal && IsMissingController(slot.Input.user);
        }
        return missing;
```

`UpdateControllerPrompts` — `availiblePlayerInputs[i].camera.rect` becomes `roster[i].Input.camera.rect`.

`PlayerPlay` — replace the loop with

```csharp
        foreach (PlayerSlot slot in roster.LocalPlayers)
            slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>().pauseMenu.OnPlay();
```

`PlayerUpdateDrivingIndicators` — replace the loop with

```csharp
        // Every player's scooter carries the indicators that the views on this machine see
        foreach (PlayerSlot slot in roster.Players)
            slot.Player.GetComponentInChildren<DrivingIndicators>().UpdatePlayerReferencesForObjects();
```

In `Assets/Scripts/Menu/MenuInteractions.cs` (`PlayerUnreadyDespawn`), delete the line

```csharp
                PlayerInstantiate.Instance.SubtractPlayerCount();
```

- [ ] **Step 4: Run them to see them pass**

Run: `bash tools/run-tests.sh "PlayerSlotTests|ControllerPromptsTests"`
Expected: `tests: 13 total, 13 passed, 0 failed, 0 skipped` (4 `PlayerSlotTests` + 9 `ControllerPromptsTests`).

- [ ] **Step 5: Check nothing else used what was removed**

Run: `grep -rnE "availiblePlayerInputs|SubtractPlayerCount|AddToPlayerArray|RemoveFromPlayerArray" Assets/Scripts Assets/Tests; grep -n "playerCount\b" Assets/Scripts/Player/PlayerInstantiate.cs Assets/Tests/Editor/*.cs`
Expected: no output. (`SplitScreenLayout.CalculateRects` has a parameter called `playerCount`; that one stays.)

- [ ] **Step 6: Run the whole suite, smoke test included**

Run: `bash tools/run-tests.sh`
Expected: `tests: 91 total, 91 passed, 0 failed, 0 skipped` (90 − the 2 `RemoveFromPlayerArray` tests that moved into `PlayerRosterTests` + 3 new).

- [ ] **Step 7: Checkpoint**

Files: `Assets/Scripts/Player/PlayerInstantiate.cs`, `Assets/Scripts/Menu/MenuInteractions.cs`, `Assets/Tests/Editor/PlayerSlotTests.cs`, `Assets/Tests/Editor/ControllerPromptsTests.cs`. Line endings: `git ls-files --eol` still shows `w/crlf` for both scripts. No commit.

---

### Task 4: Gameplay systems read the roster

**Files:**
- Modify: `Assets/Scripts/Player/SpawnManager.cs:52-109` (LF in git, CRLF on disk) — roster loop, `i < MAX_PLAYERS` and no empty `catch` (roadmap Task 1.1 item), golden-round fallback
- Modify: `Assets/Scripts/Management/ScoreManager.cs:43-55` — `UpdateOrderHandlers(PlayerRoster)`
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF) and `Assets/Scripts/Menu/MainMenu.cs:45` (CRLF) — the two `UpdateOrderHandlers` callers
- Modify: `Assets/Scripts/Management/CutoutManager.cs:24-33`, `Assets/Scripts/Management/OrderManager.cs:246-258` (`InitTutorial`)
- Modify: `Assets/Scripts/UI/CompassMarker.cs:33-84`, `Assets/Scripts/BeconIndicator.cs:92-107` (CRLF), `Assets/Scripts/DrivingIndicators.cs:83-107` (CRLF), `Assets/Scripts/Management/BoostPadManager.cs:39-54` (CRLF)
- Test: `Assets/Tests/Editor/SpawnManagerTests.cs`, `Assets/Tests/Editor/ScoreManagerTests.cs` (+ `.meta`s)

**Interfaces:**
- Consumes: `PlayerInstantiate.Roster`, `PlayerRoster.Players` / `LocalPlayers`, `PlayerSlot.Index` / `Player` / `Input` / `IsLocal`, `PlayerRoster.JoinLocal` / `JoinRemote` (tests).
- Produces: `public static GameObject SpawnManager.SpawnPoint(int slot, GameObject[] preferred, GameObject[] fallback)`; `ScoreManager.UpdateOrderHandlers(PlayerRoster roster)` (was `PlayerInput[]`).
- Rule for every loop: `Players` for anything every player has (scooter, ball, orders, cutout, tutorial order, compass marker); `LocalPlayers` for anything that belongs to a split-screen view on this machine (camera, compass UI, per-view indicator and boost-pad copies).

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/SpawnManagerTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/SpawnManagerTests.cs`):

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class SpawnManagerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        GameObject Point(string name, float x)
        {
            GameObject point = objects.NewGameObject(name);
            point.transform.position = new Vector3(x, 0f, 0f);
            return point;
        }

        /// <summary>
        /// An online player (no controller or camera needed): a ball and a scooter under one top object. Returns the ball
        /// </summary>
        Rigidbody AddOnlinePlayer(PlayerRoster roster, string name)
        {
            GameObject player = objects.NewGameObject(name);
            GameObject ball = new GameObject("Ball");
            ball.transform.SetParent(player.transform);
            GameObject scooter = new GameObject("Scooter");
            scooter.transform.SetParent(player.transform);
            scooter.AddComponent<BallDriving>();
            roster.JoinRemote(player, 1);
            return ball.AddComponent<Rigidbody>();
        }

        [Test]
        public void SpawnPoint_SlotWithoutAGoldenPoint_UsesItsNormalSpawnPoint()
        {
            GameObject[] golden = { Point("Golden 1", 1), Point("Golden 2", 2), Point("Golden 3", 3) };
            GameObject[] normal = { Point("Spawn 1", 11), Point("Spawn 2", 12), Point("Spawn 3", 13), Point("Spawn 4", 14) };

            Assert.AreSame(golden[2], SpawnManager.SpawnPoint(2, golden, normal));
            Assert.AreSame(normal[3], SpawnManager.SpawnPoint(3, golden, normal));
        }

        [Test]
        public void SpawnPoint_NoPointAnywhere_IsNull()
        {
            Assert.IsNull(SpawnManager.SpawnPoint(3, new GameObject[3], null));
        }

        [Test]
        public void SpawnPlayersFinalPackage_FourPlayersThreeGoldenPoints_EveryoneStartsOnASpawnPoint()
        {
            SpawnManager spawns = objects.Add<SpawnManager>();
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] golden = { Point("Golden 1", 1), Point("Golden 2", 2), Point("Golden 3", 3) };
            GameObject[] normal = { Point("Spawn 1", 11), Point("Spawn 2", 12), Point("Spawn 3", 13), Point("Spawn 4", 14) };
            Reflect.SetField(spawns, "goldenPackageSpawnPositions", golden);
            Reflect.SetField(spawns, "gameSpawnPositions", normal);
            Reflect.SetField(spawns, "playerInstantiate", instantiate);
            Rigidbody[] balls = new Rigidbody[Constants.MAX_PLAYERS];
            for (int i = 0; i < balls.Length; i++)
                balls[i] = AddOnlinePlayer(instantiate.Roster, "P" + (i + 1));

            spawns.SpawnPlayersFinalPackage();

            Assert.AreEqual(golden[0].transform.position, balls[0].transform.position);
            Assert.AreEqual(golden[2].transform.position, balls[2].transform.position);
            Assert.AreEqual(normal[3].transform.position, balls[3].transform.position);
        }
    }
}
```

`Assets/Tests/Editor/ScoreManagerTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/ScoreManagerTests.cs`):

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class ScoreManagerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        static OrderHandler AddOrders(GameObject player)
        {
            GameObject orders = new GameObject("Orders");
            orders.transform.SetParent(player.transform);
            return orders.AddComponent<OrderHandler>();
        }

        [Test]
        public void UpdateOrderHandlers_ListsEveryPlayersOrders_IncludingOnlinePlayers()
        {
            ScoreManager scores = objects.Add<ScoreManager>();
            PlayerRoster roster = new PlayerRoster();
            OrderHandler local = AddOrders(roster.JoinLocal(objects.Add<PlayerInput>()).Player);
            OrderHandler online = AddOrders(roster.JoinRemote(objects.NewGameObject("Online player"), 3).Player);

            scores.UpdateOrderHandlers(roster);

            Assert.AreSame(local, scores.GetHandlerOfIndex(0));
            Assert.AreSame(online, scores.GetHandlerOfIndex(1));
            Assert.IsNull(scores.GetHandlerOfIndex(2));
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "SpawnManagerTests|ScoreManagerTests"`
Expected: `NO RESULTS`, with `error CS0117: 'SpawnManager' does not contain a definition for 'SpawnPoint'` and `error CS1503` (a `PlayerRoster` can't be passed as `PlayerInput[]`).

- [ ] **Step 3: SpawnManager places every player from the roster**

In `Assets/Scripts/Player/SpawnManager.cs`, replace `SpawnPlayersStartOfGame` and `SpawnPlayersFinalPackage` (from the `///<summary>` above `SpawnPlayersStartOfGame` to the end of `SpawnPlayersFinalPackage`) with

```csharp
    ///<summary>
    /// This is executed when the OnSwapStartingCutscene event is called
    ///</summary>
    private void SpawnPlayersStartOfGame()
    {
        foreach (PlayerSlot player in playerInstantiate.Roster.Players)
        {
            PlacePlayer(player, SpawnPoint(player.Index, gameSpawnPositions, null));

            // Initalize the compass ui on each of the players
            player.Player.GetComponentInChildren<CompassMarker>().InitalizeCompassUIOnAllPlayers();
        }

        // After players have been placed, begin main loop
        gameManager.SetGameState(GameState.MainLoop);
    }

    ///<summary>
    /// This is executed when the OnSwapGoldenCutscene event is called
    ///</summary>
    public void SpawnPlayersFinalPackage()
    {
        // The golden round lists 3 spawn points; a 4th player starts on their normal spawn point, in line with the others
        foreach (PlayerSlot player in playerInstantiate.Roster.Players)
            PlacePlayer(player, SpawnPoint(player.Index, goldenPackageSpawnPositions, gameSpawnPositions));
    }

    ///<summary>
    /// A slot's spawn point from the preferred list, or from the fallback list when the preferred one has none for it; null when neither has one
    ///</summary>
    public static GameObject SpawnPoint(int slot, GameObject[] preferred, GameObject[] fallback)
    {
        if (preferred != null && slot < preferred.Length && preferred[slot] != null)
            return preferred[slot];

        if (fallback != null && slot < fallback.Length && fallback[slot] != null)
            return fallback[slot];

        return null;
    }

    ///<summary>
    /// Stops a player and puts their ball and scooter on a spawn point
    ///</summary>
    private static void PlacePlayer(PlayerSlot player, GameObject spawnPoint)
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning("No spawn point for player " + (player.Index + 1));
            return;
        }

        Rigidbody ball = player.Player.GetComponentInChildren<Rigidbody>();
        Transform scooter = player.Player.GetComponentInChildren<BallDriving>().transform;

        // Resets the velocity of the players
        ball.velocity = Vector3.zero;

        // reset position and rotation of ball and controller
        ball.transform.position = spawnPoint.transform.position;
        ball.transform.rotation = spawnPoint.transform.rotation;

        scooter.position = spawnPoint.transform.position;
        scooter.rotation = spawnPoint.transform.rotation;
    }
```

- [ ] **Step 4: ScoreManager counts every player in the roster**

In `Assets/Scripts/Management/ScoreManager.cs`, replace `UpdateOrderHandlers` with

```csharp
    /// <summary>
    /// Recounts the order handlers from the roster, to resize the list in case a player left
    /// </summary>
    public void UpdateOrderHandlers(PlayerRoster roster)
    {
        orderHandlers.Clear();

        foreach (PlayerSlot player in roster.Players)
            orderHandlers.Add(player.Player.GetComponentInChildren<OrderHandler>());
    }
```

and delete `using UnityEngine.InputSystem;` (nothing else in the file uses it). Callers: in `PlayerInstantiate.RemovePlayerRef`,

```csharp
        ScoreManager.Instance.UpdateOrderHandlers(roster);
```

and in `MainMenu.Update` (the L hotkey),

```csharp
            ScoreManager.Instance.UpdateOrderHandlers(playerInstantiate.Roster);
```

- [ ] **Step 5: Run the new tests to see them pass**

Run: `bash tools/run-tests.sh "SpawnManagerTests|ScoreManagerTests"`
Expected: `tests: 4 total, 4 passed, 0 failed, 0 skipped`.

- [ ] **Step 6: The other gameplay systems read the roster**

`Assets/Scripts/Management/CutoutManager.cs` — `SpawnCutouts` body:

```csharp
        foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
        {
            cutouts[player.Index].gameObject.SetActive(true);
            cutouts[player.Index].InitCutout();
        }
```

`Assets/Scripts/Management/OrderManager.cs` — `InitTutorial`'s loop becomes

```csharp
        foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
        {
            tutorialOrders[player.Index].InitOrder(false);
            OnDeleteActiveOrders += tutorialOrders[player.Index].EraseOrder;
            IncrementCounters(tutorialOrders[player.Index].Value, 1);
        }
```

`Assets/Scripts/UI/CompassMarker.cs` — the three loops go over the compasses on this machine:

```csharp
    ///<summary>
    /// Initalizes the compass ui on all players
    ///</summary>
    public void InitalizeCompassUIOnAllPlayers()
    {
        if(playerInstantiate == null)
            playerInstantiate = PlayerInstantiate.Instance;

        // Every split-screen view on this machine has a compass
        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            player.Player.GetComponentInChildren<Compass>().AddCompassMarker(this);
    }

    ///<summary>
    /// Removes the icon for all players
    ///</summary>
    public void RemoveCompassUIFromAllPlayers()
    {
        if (playerInstantiate == null)
            playerInstantiate = PlayerInstantiate.Instance;

        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            player.Player.GetComponentInChildren<Compass>().RemoveCompassMarker(this);
    }

    ///<summary>
    /// Switches the icon on the compass ui
    /// isCarried indicates a scooter icon
    /// !isCarried indicates a floor icon
    ///</summary>
    public void SwitchCompassUIForPlayers(bool isCarried)
    {
        if (playerInstantiate == null)
            playerInstantiate = PlayerInstantiate.Instance;

        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            player.Player.GetComponentInChildren<Compass>().ChangeCompassMarkerIcon(this, isCarried);
    }
```

and delete `using UnityEngine.InputSystem;`.

`Assets/Scripts/BeconIndicator.cs` — in `InitalizeBeconIndicator`, the loop after `playerCameraTransforms = new Transform[4];` becomes

```csharp
        // Each split-screen view on this machine sees its own copy of the beacon, turned towards its camera
        foreach (PlayerSlot viewer in PlayerInstantiate.Instance.Roster.LocalPlayers)
        {
            playersToKeepTrackOf[viewer.Index] = viewer.Player.GetComponentInChildren<BallDriving>().gameObject;
            playerCameraTransforms[viewer.Index] = viewer.Input.GetComponent<PlayerCameraResizer>().PlayerReferenceCamera.transform;
            beconRotationObjects[viewer.Index].SetActive(true);
        }
```

`Assets/Scripts/DrivingIndicators.cs` — in `UpdatePlayerReferencesForObjects`, the loop after `playerCameraTransforms = new Transform[4];` becomes

```csharp
        // Every other split-screen view on this machine sees this player's indicator, turned towards its camera
        foreach (PlayerSlot viewer in playerInstantiate.Roster.LocalPlayers)
        {
            if (viewer.Input == thisPlayer)
                continue;

            int i = viewer.Index;
            playersToKeepTrackOf[i] = viewer.Player.GetComponentInChildren<BallDriving>().gameObject;

            playerCameraTransforms[i] = viewer.Input.GetComponent<PlayerCameraResizer>().PlayerReferenceCamera.transform;

            playersRotationObjects[i].SetActive(true);

            List<GameObject> needToSwitch = new List<GameObject>
            {
                playersRotationObjects[i],
                playersRotationObjects[i].transform.GetChild(0).gameObject
            };

            PlayerCameraResizer.UpdatePlayerObjectLayer(needToSwitch, i, iconCamera);
        }
```

`Assets/Scripts/Management/BoostPadManager.cs` — `UpdateBoostPads`' loop becomes

```csharp
        // Each split-screen view on this machine has its own copy of every pad, pointed at that view's scooter
        foreach (PlayerSlot viewer in playerInstantiate.Roster.LocalPlayers)
            playersToKeepTrackOf[viewer.Index] = viewer.Player.GetComponentInChildren<BallDriving>().gameObject;
```

(keep `using UnityEngine.InputSystem;` in `BeconIndicator.cs` and `DrivingIndicators.cs` — `thisPlayer` is a `PlayerInput`; delete it from `BoostPadManager.cs` if nothing else there uses it.)

- [ ] **Step 7: Check that only the menus still read PlayerInputs**

Run: `grep -rn "PlayerInputs" Assets/Scripts`
Expected: only `Menu/PlayerSelectCanvas.cs`, `Menu/SceneManager.cs` and the property itself in `Player/PlayerInstantiate.cs` (Task 5 moves them).

- [ ] **Step 8: Run the whole suite, smoke test included**

Run: `bash tools/run-tests.sh`
Expected: `tests: 95 total, 95 passed, 0 failed, 0 skipped`. The smoke test is the check that spawns, compasses, cutouts, tutorial orders, beacons, indicators and boost pads still work for 4 local players.

- [ ] **Step 9: Checkpoint**

Files: `SpawnManager.cs`, `ScoreManager.cs`, `PlayerInstantiate.cs`, `MainMenu.cs`, `CutoutManager.cs`, `OrderManager.cs`, `CompassMarker.cs`, `BeconIndicator.cs`, `DrivingIndicators.cs`, `BoostPadManager.cs`, `SpawnManagerTests.cs(.meta)`, `ScoreManagerTests.cs(.meta)`. CRLF files still `w/crlf`. No commit.

---

### Task 5: Menus read the roster; PlayerInputs goes

**Files:**
- Modify: `Assets/Scripts/Menu/PlayerSelectCanvas.cs:17-51` (CRLF)
- Modify: `Assets/Scripts/Menu/LoadingScreenManager.cs:17-37` (CRLF)
- Modify: `Assets/Scripts/Menu/SceneManager.cs:165` (CRLF)
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF) — delete the temporary `PlayerInputs`
- Test: `Assets/Tests/Editor/LobbyScreensTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `PlayerInstantiate.Roster`, `PlayerRoster.Players`, `PlayerRoster.JoinLocal` / `Leave` (tests).
- Produces: `PlayerSelectCanvas.TogglePressButtonOnAllTexts(PlayerRoster roster)` and `LoadingScreenManager.InitalizeButtonGameobjects(PlayerRoster roster)` (were `PlayerInput[]`). `PlayerInstantiate.PlayerInputs` no longer exists.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/LobbyScreensTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/LobbyScreensTests.cs`):

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class LobbyScreensTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        GameObject[] NewObjects(string name, bool active)
        {
            GameObject[] created = new GameObject[Constants.MAX_PLAYERS];
            for (int i = 0; i < created.Length; i++)
            {
                created[i] = objects.NewGameObject(name + " " + i);
                created[i].transform.position = new Vector3(i, 0f, 0f);
                created[i].SetActive(active);
            }
            return created;
        }

        /// <summary>
        /// Players in slots 0 and 2 (the player in slot 1 left)
        /// </summary>
        PlayerRoster RosterWithAGap()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(objects.Add<PlayerInput>());
            PlayerInput leaver = objects.Add<PlayerInput>();
            roster.JoinLocal(leaver);
            roster.JoinLocal(objects.Add<PlayerInput>());
            roster.Leave(leaver);
            return roster;
        }

        [Test]
        public void PlayerSelect_HidesTheJoinPromptOfEveryTakenSlotOnly()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] joinPrompts = NewObjects("Join Prompt", true);
            Reflect.SetField(canvas, "pressButtonTexts", joinPrompts);

            canvas.TogglePressButtonOnAllTexts(RosterWithAGap());

            Assert.IsFalse(joinPrompts[0].activeSelf);
            Assert.IsTrue(joinPrompts[1].activeSelf);
            Assert.IsFalse(joinPrompts[2].activeSelf);
            Assert.IsTrue(joinPrompts[3].activeSelf);
        }

        [Test]
        public void LoadingScreen_ShowsOneConfirmButtonPerPlayer_PackedInSlotOrder()
        {
            LoadingScreenManager loading = objects.Add<LoadingScreenManager>();
            GameObject[] buttons = NewObjects("Button", false);
            GameObject[] positions = NewObjects("Button Position", true);
            GameObject confirmText = objects.NewGameObject("Confirm Text");
            confirmText.SetActive(false);
            Reflect.SetField(loading, "ButtonGameobjects", buttons);
            Reflect.SetField(loading, "buttonPositions", System.Array.ConvertAll(positions, p => p.transform));
            Reflect.SetField(loading, "textConfirm", confirmText);

            loading.InitalizeButtonGameobjects(RosterWithAGap());

            Assert.IsTrue(buttons[0].activeSelf);
            Assert.IsFalse(buttons[1].activeSelf);
            Assert.IsTrue(buttons[2].activeSelf);
            Assert.IsFalse(buttons[3].activeSelf);
            Assert.AreEqual(positions[0].transform.position, buttons[0].transform.position);
            Assert.AreEqual(positions[1].transform.position, buttons[2].transform.position);
            Assert.IsTrue(confirmText.activeSelf);
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh LobbyScreensTests`
Expected: `NO RESULTS`, with `error CS1503: Argument 1: cannot convert from 'PlayerRoster' to 'UnityEngine.InputSystem.PlayerInput[]'`.

- [ ] **Step 3: The lobby and loading screen read the roster**

`Assets/Scripts/Menu/PlayerSelectCanvas.cs` — `Start` becomes

```csharp
    public void Start()
    {
        playerInstantiate = PlayerInstantiate.Instance;

        TogglePressButtonOnAllTexts(playerInstantiate.Roster);
    }
```

and `TogglePressButtonOnAllTexts` becomes

```csharp
    // Hides the join prompt of every taken slot
    public void TogglePressButtonOnAllTexts(PlayerRoster roster)
    {
        foreach (PlayerSlot player in roster.Players)
            TogglePressButtonTexts(player.Index, false);
    }
```

then delete `using UnityEngine.InputSystem;` if nothing else in the file uses it.

`Assets/Scripts/Menu/LoadingScreenManager.cs` — `InitalizeButtonGameobjects` becomes

```csharp
    ///<summary>
    /// Shows a confirm button for every player, packed left to right in slot order
    ///</summary>
    public void InitalizeButtonGameobjects(PlayerRoster roster)
    {
        int buttons = 0;

        foreach (PlayerSlot player in roster.Players)
        {
            ButtonGameobjects[player.Index].gameObject.transform.position = buttonPositions[buttons].transform.position;

            // Enables buttons required for players
            ButtonGameobjects[player.Index].gameObject.SetActive(true);

            buttons++;
        }

        textConfirm.SetActive(true);
    }
```

then delete `using UnityEngine.InputSystem;` if nothing else in the file uses it.

`Assets/Scripts/Menu/SceneManager.cs:165` becomes

```csharp
                    LoadingScreenManager.Instance.InitalizeButtonGameobjects(PlayerInstantiate.Instance.Roster);
```

In `Assets/Scripts/Player/PlayerInstantiate.cs`, delete the temporary `PlayerInputs` property with its summary.

- [ ] **Step 4: Run them to see them pass**

Run: `bash tools/run-tests.sh LobbyScreensTests`
Expected: `tests: 2 total, 2 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: Check nothing reads PlayerInputs any more**

Run: `grep -rn "PlayerInputs" Assets/Scripts Assets/Tests`
Expected: no output.

- [ ] **Step 6: Run the whole suite, smoke test included**

Run: `bash tools/run-tests.sh`
Expected: `tests: 97 total, 97 passed, 0 failed, 0 skipped`.

- [ ] **Step 7: Checkpoint**

Files: `PlayerSelectCanvas.cs`, `LoadingScreenManager.cs`, `SceneManager.cs`, `PlayerInstantiate.cs`, `LobbyScreensTests.cs(.meta)`. CRLF files still `w/crlf`. No commit.

---

### Task 6: Docs

**Files:**
- Create: `docs/testing.md`
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (§R, Task 1.1 SpawnManager item, Partner steps' test count, Phase 2 progress and Task 2.1)

**Interfaces:**
- Consumes: everything above (test names and counts, `tools/run-tests.sh`).
- Produces: nothing code depends on.

- [ ] **Step 1: Write docs/testing.md**

```markdown
# Testing

## In the Unity editor

- Window → General → Test Runner → EditMode → **Run All**.
- It includes **LocalMatchSmokeTest**, an automated 4-player match that takes a minute or two:
  - It presses Play in the menu scene and joins 4 virtual controllers.
  - It plays through player select, the loading screen, the opening cutscene and a skipped tutorial.
  - It drives, pauses, unplugs player 4's controller and plugs it back in.
  - It fails on any error or exception on the way.
- Leave the editor alone while it plays. Controllers you touch count as input.
- Run a single test: select it in the Test Runner → **Run Selected**.

## From the command line

- `bash tools/run-tests.sh` runs every EditMode test in a mirror copy of the project, so it works while the editor is open.
- `bash tools/run-tests.sh LocalMatchSmokeTest` runs one test class. The filter is a class or test name, or a regex.
- Results go to `Logs/test-results.xml`, and Unity's log to `Logs/test-unity.log`.
- The mirror sits at `../_doa_test_mirror/CapstoneYear4`. It takes about 7 GB and needs a short path: Windows paths over 260 characters break the import.
- `DOA_MIRROR` and `DOA_UNITY` change the mirror folder and the Unity install.
- `DOA_NO_SYNC=1` tests the mirror without copying the project into it first.
- Unity runs with graphics, because the smoke test renders real scenes. Unity started with `-nographics` crashes in URP.
```

- [ ] **Step 2: Update the roadmap**

In `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`:

§R — replace the line `- [ ] EditMode tests green: Window → General → Test Runner → EditMode → Run All.` with

```markdown
- [ ] EditMode tests green: Window → General → Test Runner → EditMode → Run All. It includes an automated 4-player match (`LocalMatchSmokeTest`, 1–2 minutes: menus, loading, cutscene, driving, pause, controller unplug) — let it play. Command line: `bash tools/run-tests.sh` (see `docs/testing.md`).
```

Task 1.1 — replace the `SpawnManager` item with

```markdown
- [x] `SpawnManager` loops `i <= MAX_PLAYERS` inside an empty `catch` → `i < MAX_PLAYERS`, remove the empty catch (`SpawnManager.cs:57-78`, `:90-108`). *(Done in Phase 2A with the roster. The empty catch was hiding a 4-player bug: the golden-round scenes list 3 spawn points, so player 4 was never moved to the start of the golden round; they now use their normal "Spawn 4" point, which sits in line with the other three.)*
```

Partner steps checks — `Test Runner → EditMode → Run All: 75 passed.` becomes `Test Runner → EditMode → Run All: 97 passed (the smoke test plays a match for a minute or two).`

Phase 2 — under `Goal: …`, add

```markdown
Progress (2026-09-23): Phase 2A (`2026-09-23-phase2a-roster-and-smoke-test.md`) — automated 4-player match test and the player roster (Task 2.1). Next: Phase 2B (Tasks 2.3–2.5); Task 2.2 needs the editor.
```

Task 2.1 — replace its items with

```markdown
- [x] `PlayerSlot` (slot 0–3, company/colour/hat index, `isLocal`, `ownerClientId`, `PlayerInput` or `null`) + `PlayerRoster` as the single source of truth instead of `PlayerInstantiate.PlayerInputs`. *(`PlayerInstantiate.Roster`; `PlayerCount` is `Roster.Count`. `IsLocal` means "has a `PlayerInput`". Colour and hat stay in `CustomizationSelector` until Phase 3's `NetworkPlayer` needs them.)*
- [x] Migrate every user of the `PlayerInput[]` array. *(Loops use `Players` for what every player has (scooter, orders, spawns, cutouts, tutorial orders, lobby prompts, loading buttons) and `LocalPlayers` for what belongs to a split-screen view on this machine (camera, menus, compass UI, per-view indicators and boost pads). `QAManager` never used the array.)*
- [x] Keep the per-slot mapping: player layer `10+slot`, camera layer `17+slot`, icon layer `24+slot`, URP renderer `slot+1` (main) / `slot+5` (phase), phasing = `Physics.IgnoreLayerCollision(9, 10+slot)`. *(Unchanged in `AddPlayerReference`.)*
- [x] Tests: EditMode tests for slot assignment (fills lowest free slot, frees on leave, max 4); §R checklist. *(`PlayerRosterTests`; §R's play-through is automated by `LocalMatchSmokeTest`.)*
```

- [ ] **Step 3: Check the docs against the code**

Run: `grep -n "97 passed\|LocalMatchSmokeTest\|tools/run-tests.sh" docs/testing.md docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`
Expected: the lines added above. Then run `bash tools/run-tests.sh` once more and confirm the total really is 97.

- [ ] **Step 4: Checkpoint**

Files: `docs/testing.md`, `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`. No commit.

---

**Execution notes (2026-09-23):** all 6 tasks done, 97 EditMode tests green. One change from Task 1 Step 7 as written: `Alex Player Testing` saves `allowPlayerSpawn = false`, so the title screen only lets player 1 join. The smoke test joins player 1, picks Play, then joins players 2–4 in player select, which is the game's real flow. The same finding corrected the Phase 1B partner steps and `docs/dev-hotkeys.md` (add test players with F1 in player select, not on the title screen).

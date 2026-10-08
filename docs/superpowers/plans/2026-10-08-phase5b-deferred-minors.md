# Phase 5B: The Remaining Deferred Minors, and the Stuck Scene: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (the user's choice for these phases) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every deferred minor still open, from Phase 5A's review, Phase 3J's review and the title-screen change. Then fix the client stuck in the game scene when the host returns to the menu while that scene is still coming up.

**Architecture:** Small, independent fixes, each with a test that fails first. The stuck scene is last, and is debugged from a test that reproduces it (superpowers:systematic-debugging).

**Tech Stack:** Unity 2022.3.62f3, C#, Netcode for GameObjects 1.15.1, Input System 1.14, NUnit (Unity Test Framework).

**Spec:** none (a list of findings). The source of each item is `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md` ("Deferred minors still open"), plus the two minors from `docs/superpowers/plans/2026-10-07-local-and-online-menu.md`'s review. The user's decisions (2026-10-08):
- Include the stuck-scene bug, as the last task.
- Leave the per-machine seat cap: record it as a known limit.

## Global Constraints

- **Edit rules:**
  - No scene, prefab, `.asset`, `.inputactions` or ProjectSettings edits.
  - `i/crlf` files (`PlayerInstantiate.cs`, `MainMenu.cs`, `MenuInteractions.cs`, `SceneManager.cs`, `GameManager.cs`, `Order.cs`, `BallDriving.cs`, `PhaseIndicator.cs`): edit with the Edit tool or CRLF-preserving scripts, never `sed -i`.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
  - `SteamManager.cs` and `SteamStartup.cs` are untouched.
- **Git:** leave everything uncommitted (the user commits).
- **Tests:**
  - `bash tools/run-tests.sh [filter]`, one run at a time. A compile error prints `NO RESULTS`. `python tools/compile-check.py` gives a ~30 s compile check.
  - Network test ports: the next free one is **7816**.
  - Play Mode test rules (`docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`):
    - no lambda captures a test's local;
    - wait by real time;
    - only `yield return null`;
    - `EnterPlayMode` is yielded by the test itself, not a helper.

## Items closed without code (rulings, recorded in the handoff)

- **`PlayerCameraResizer`'s `[DefaultExecutionOrder(1000)]`:** 5A's review checked it and found nothing that depends on the old order. No change.
- **`OwnerNetworkTransform`, a forged teleport state:** Netcode applies a teleport inside `ApplyUpdatedState`, before any overridable hook sees the state (`LocalAuthoritativeNetworkState` is internal), and Netcode is a package, not embedded. Unity refuses a NaN position, so nothing moves; the cost is one error line per message, already bounded by the transport's rate limits. Record it in `docs/online-safety.md` as a known limit.
- **No cap on seats per machine** (the user: leave it): record it in `docs/online.md` Known limits.
- **A client's in-game seat asks aren't integration-tested:** two games can't run in one process. `EDITOR-TODO.md` section 2 covers it.

## Review Focus

- **Bumps:** a harder report arriving after a weak one within the pair's cooldown must go through on the host and on clients. A spam of equal reports must still be dropped.
- **Joins:** a fifth controller in offline player select with 4 players must not make a player. A first controller at the title screen still joins. The online "That match is full." hint still shows in player select.
- **Seat moves:** after any move, a pad's next ordinary join is an ordinary join (no leftover seat), and only one player per machine has the menus.
- **The match start:** dropping unready extra seats must never drop a machine's last seat, nor a ready player's.
- **The stuck scene:** the fix must not break a normal match load, a return from the results screen, or an ordinary leave to the menu (the existing `DisconnectsNetworkTests` and `OnlineSceneFlowTests` stay green).

---

### Task 1: Title screen leftovers (two minors from 2026-10-07)

**Files:** `Assets/Scripts/Menu/MainMenu.cs` (CRLF), `Assets/Tests/Editor/MainMenuTests.cs`, `Assets/Tests/Editor/OnlinePlayTests.cs`

**Interfaces:** `MainMenu`'s `selector`, `menuGhostImage` and `selectorGhostSprites` become `[SerializeField] internal` (same names: the scene keeps its references).

- [ ] **Step 1: Write the failing tests**
  - `MainMenuTests.ScrollMenu_AnEntryWithoutItsGhostSprite_StillScrolls`:
    - 5 selector objects (each a `TestObjects` GameObject);
    - 4 ghost sprites (nulls are fine);
    - a `selector` GameObject and an `Image` for `menuGhostImage`.
    - Call `ScrollMenu(true)` 4 times. Expect no exception, and the selector moves to the 5th object's y.
  - `MainMenuTests.ConfirmMenu_TheSecondOfFiveEntries_IsOnlinePlay`: with the same setup plus the failing-Steam `OnlinePlay` from `Choose_Online_…`, call `ScrollMenu(true)` then `ConfirmMenu()`. Expect the notice `OnlineLobby.NO_STEAM` and the state `Menu`.
  - `OnlinePlayTests.PressY_LocalPlay_AsksForNoLobby`: in player select with a `FakeLobbyService` (Steam available), `PressY(true)` gives `lobbies.Creates.Count == 0`.
- [ ] **Step 2:** `bash tools/run-tests.sh "MainMenuTests|OnlinePlayTests"`. Expected: `NO RESULTS` (fields not internal). Once they compile, the ghost-sprite test fails with IndexOutOfRange.
- [ ] **Step 3: Implement.** `ScrollMenu` sets the ghost sprite only when `selectorPos < selectorGhostSprites.Length`. `SwapToOptions` and `SwapToCredits` already reset to index 0, which always exists. `PressY_LocalPlay` should pass at once (coverage of existing behaviour, not a bug): rule it as such in the ledger.
- [ ] **Step 4:** the same run. Expected: all pass.

### Task 2: Bumps (Phase 5A minors)

**Files:** `Assets/Scripts/Online/BumpRules.cs`, `Assets/Scripts/Online/OnlineBumps.cs`, `Assets/Tests/Editor/BumpRulesTests.cs`

**Interfaces:**
- `BumpRules.TooSoon(int a, int b, float now, float strength)` replaces `TooSoon(a, b, now)`.
  - Within `PAIR_COOLDOWN` of the pair's last bump, it is too soon unless `strength` is greater than the strongest bump the pair had in that window.
  - A bump that goes through records its time and strength.
  - `reported` passes the speed; `judged` and `shown` pass the push.
- `static float BumpRules.Shown(float push, float maxPush)`: clamps to `[0, maxPush]`, and gives 0 for a value that isn't finite.

- [ ] **Step 1: Write the failing tests** (`BumpRulesTests`)
  - `TooSoon_AHarderBumpWithinTheCooldown_GoesThrough`: strengths 3 then 10 at t=0, 0.1 → false, false.
  - `TooSoon_AnEqualOrWeakerOneWithinTheCooldown_IsTooSoon`: 10 then 10, and 10 then 3 → the second is true.
  - `TooSoon_AfterTheCooldown_AnyBumpGoesThrough`: 10 at 0, then 1 at 0.6 → false.
  - Update the existing `TooSoon` tests to the 4-argument form, with equal strengths.
  - `Shown_NeverMoreThanAClash`: `Shown(1e9f, 4000) == 4000`, `Shown(-5, 4000) == 0`, `Shown(float.NaN, 4000) == 0`, `Shown(100, 4000) == 100`.
- [ ] **Step 2:** `bash tools/run-tests.sh "BumpRulesTests"`. Expected: `NO RESULTS`.
- [ ] **Step 3: Implement.** Replace the `Dictionary<int, float>` with one holding `(float time, float strength)`. `OnlineBumps.Show` pushes `BumpRules.Shown(bump.Push, toBall.ClashForce)`. Update the class summaries ("the hardest within the cooldown goes through").
- [ ] **Step 4:** `bash tools/run-tests.sh "BumpRulesTests|BumpsNetworkTests"`. Expected: all pass.

### Task 3: Small code fixes (Phase 5A minors)

**Files:**
- `Assets/Scripts/Player/OrderHandler.cs`
- `Assets/Scripts/Player/PlayerJoiner.cs`
- `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF)
- `Assets/Scripts/*/DisplayRules.cs`, `Assets/Scripts/UI/ControllerPrompts.cs`, `Assets/Scripts/UI/LobbyPrompt.cs`, `PresenceRules.cs`
- Tests: `OrderHandlerResetTests` (or the existing OrderHandler hook tests), `PlayerJoinerTests`

**Interfaces:**
- `static bool PlayerJoiner.Welcomes(bool online, bool allowSpawn, int playerCount, int localCount, bool inPlayerSelect)`:
  - offline: `(allowSpawn || playerCount == 0) && playerCount < Constants.MAX_PLAYERS`;
  - online: `localCount == 0 || inPlayerSelect`. A full roster in player select still reaches `AddPlayerReference`, which shows "That match is full.".
- `PlayerJoiner.Enable(Action<InputDevice> onTurnedAway, Func<bool> welcomes)`: `OnUnpairedDeviceUsed` returns before joining when `welcomes` is set and false. Seat moves call `JoinPlayer` themselves, so they're unaffected.
- `PlayerInstantiate` passes `() => PlayerJoiner.Welcomes(online, allowPlayerSpawn, PlayerCount, roster.LocalCount, gameManager != null && gameManager.MainState == GameState.PlayerSelect)` as a method-group helper, `bool WelcomesJoins()`.

- [ ] **Step 1: Write the failing tests**
  - `PlayerJoinerTests.Welcomes` as `[TestCase]`s:
    - offline, title (allowSpawn false, 0 players) → true;
    - offline, match (allowSpawn false, 2) → false;
    - offline, player select full (allowSpawn true, 4) → false;
    - offline, player select 2 → true;
    - online, 0 local mid-match → true;
    - online, 1 local mid-match → false;
    - online, 1 local in player select → true.
  - `OrderHandler`, `Destroyed_LeavesNoMatchEndListener`:
    - hook a handler to an `OrderManager` (as the existing hook tests do);
    - `Object.DestroyImmediate` the handler's object;
    - expect `Reflect.HandlerCount(orders, nameof(OrderManager.OnMainGameFinishes), handler) == 0`.
- [ ] **Step 2:** `bash tools/run-tests.sh "PlayerJoinerTests|OrderHandler"`. Expected: `NO RESULTS`, then the destroy test fails.
- [ ] **Step 3: Implement**
  - `OrderHandler.OnDestroy`: `if (OrderManager.Instance != null) UnhookMatchEnd(OrderManager.Instance);`. If OrderHandler already has an `OnDestroy`, add the line there.
  - The joiner change, as above.
  - **Doc comments:**
    - `DisplayRules`: the `VSyncCount` summary moves onto `VSyncCount`.
    - `ControllerPrompts` and `LobbyPrompt`: the "Builds the overlay canvas…" summary moves onto `Create`, and the `SORTING_ORDER` summary stays on the constant.
  - **`PresenceRules`:** remove `case GameState.StartingCutscene: return null;` and put its comment on `default`.
- [ ] **Step 4:** `bash tools/run-tests.sh "PlayerJoinerTests|OrderHandler|PresenceRulesTests|DisplayRulesTests|KeyboardPlayerTests|ControllerTests"`. Expected: all pass.

### Task 4: Seats (Phase 3J minors)

**Files:**
- `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Scripts/Online/SeatTable.cs`, `Assets/Scripts/Online/OnlineGame.cs`
- `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF)
- Tests: `SeatTableTests`, `CouchSeatsNetworkTests` (port 7813, already its own), `OnlineSeatTests`

**Interfaces:**
- `List<(ulong machine, int seat)> SeatTable.UnreadyExtras(Func<int, bool> ready)`: each machine's seats that aren't ready, minus one kept per machine. If none is ready, the machine keeps its lowest seat; otherwise it keeps its ready ones.
- `OnlineSession`:
  - `internal void OnSeatAsked(ulong machine)` ignores a machine holding no seat (`seats.CountOf(machine) == 0`: gone, or never let in);
  - `public void DropUnreadyExtras(Func<int, bool> ready)` (host only) runs `FreeSeatOf` on each of `UnreadyExtras`.
- `OnlineGame`, on the host, when the state leaves the menus for the match (where it already turns away unseated players, `OnStateApplied`), calls `session.DropUnreadyExtras(players.IsReady)`.
- `PlayerInstantiate`:
  - `isFirstPlayer` online becomes `roster.LocalCount == 0 && !MenuPlayerMoving()`, where `MenuPlayerMoving()` is true when any `rejoinSeats` value has `host == true`;
  - `MoveToSeat` removes the device's `rejoinSeats` entry after its `JoinPlayer` call, whatever happened.

- [ ] **Step 1: Write the failing tests**
  - `SeatTableTests.UnreadyExtras_KeepsEachMachinesLastSeatAndItsReadyOnes`:
    - machine 1 holds seats 0 and 2; machine 2 holds 1 and 3;
    - ready: {0, 1};
    - expect `[(1, 2), (2, 3)]`.
    - With none ready, machine 1 keeps 0.
  - `CouchSeatsNetworkTests.AMachineThatLeft_AsksForASeat_GetsNone`: host; `session.OnSeatAsked(99)`; expect `SeatsHeldBy(99)` empty and the seat count unchanged.
  - `CouchSeatsNetworkTests.TheMatchStarting_DropsAnUnreadyExtraSeat`:
    - a joined machine takes a second seat;
    - `DropUnreadyExtras` with only its first seat ready;
    - expect its second player and scooter despawned on both machines, and its first kept.
  - `OnlineSeatTests.APadJoiningWhileTheMenuPlayerMoves_IsNotASecondMenuPlayer`:
    - this machine's only player moves seats (`GiveSeat`);
    - in the same frame, `PlayerInputManager.instance.JoinPlayer(-1, -1, "Gamepad", secondPad)`;
    - after the move, exactly one local player has `hostPlayer`.
  - `OnlineSeatTests.AFailedSeatMove_LeavesNoSeatForThePadsNextJoin`:
    - make the move's rejoin fail by setting `PlayerInputManager`'s max player count (field `m_MaxPlayerCount`, via `Reflect`) to the current count before the frame it joins;
    - then restore it and join the same pad normally;
    - expect it to sit in an ordinary slot (an empty held seat or the lowest free one), not the old move's seat.
    - If the failure can't be forced this way, rule a narrower test in the ledger.
- [ ] **Step 2:** `bash tools/run-tests.sh "SeatTableTests|CouchSeatsNetworkTests|OnlineSeatTests"`. Expected: `NO RESULTS`, then the new tests fail.
- [ ] **Step 3: Implement** as in Interfaces.
- [ ] **Step 4:** the same run, plus `OnlineGameTests|OnlinePlayNetworkTests`. Expected: all pass.

### Task 5: The client stuck in the game scene

**Files:**
- `Assets/Scripts/*/SceneManager.cs` (CRLF; the game's own class, not Unity's)
- possibly `Assets/Scripts/Online/OnlineSceneFlow.cs`
- Test: `Assets/Tests/Editor/DisconnectsNetworkTests.cs`

**Known so far** (Phase 5A's ledger):
- The menu scene's `SceneManager`, a per-scene singleton, handles `menuAfterLoad` in its `sceneLoaded` callback and starts the menu load on itself.
- It is then disabled as its scene unloads, so the load dies in its `WaitForSeconds(8)`.
- The new scene's `SceneManager` subscribes to `sceneLoaded` only in `Start`, so it never hears its own scene.
- Two earlier fixes failed: a static flag handed over in `RaiseSceneUp`, then in `Start`.

- [ ] **Step 1: Write the failing test.** `DisconnectsNetworkTests.Joining_TheHostReturnsWhileThisMachinesSceneComesUp_ThisMachineReachesTheMenu`:
  - set up as `Joining_TheHostReturnsWhileThisMachinesSceneComesUp_NoMatchStateShowsHere`;
  - after the scene comes up, wait up to 30 s of real time;
  - assert the active scene is the menu scene and `GameManager.Instance.MainState` is a menu state (`LobbyRules.InTheMenus`).
- [ ] **Step 2:** run it alone. Expected: FAIL (stuck in the game scene).
- [ ] **Step 3: Debug with superpowers:systematic-debugging.**
  - Log each `SceneManager` instance's `Start`, `OnDisable`, `RaiseSceneUp` and the menu load coroutine's progress. Use temporary `Debug.Log` lines, removed afterwards.
  - Find which object owns the menu load when it dies, and why.
  - The likely fix: run the menu-after-load from a component that outlives the scene, or start it from the new scene's `SceneManager` once it is up (it can check a static "menu after load" flag in `Start`, after it subscribes).
  - Prove the cause before writing the fix.
- [ ] **Step 4:** run the new test, then `DisconnectsNetworkTests|OnlineSceneFlowTests|OnlineMatchNetworkTests|LocalMatchSmokeTest`. Expected: all pass.
- [ ] **Step 5: If after a time-boxed investigation (about two hours of test runs) the cause isn't proven:**
  - revert the code;
  - keep the failing test out (or `[Ignore]` it with the reason);
  - write the findings into the handoff for the user.

### Task 6: Docs and the full suite

- [ ] **The handoff:** remove the closed items and add the rulings above. The status line gives the test count.
- [ ] **`docs/online.md`:**
  - bumps (the hardest report in the cooldown), seats (unready extras dropped at match start), and joins (no join mid-match);
  - Known limits: no seat cap.
- [ ] **`docs/online-safety.md`:** the teleport limit.
- [ ] **`docs/testing.md`:** the new tests. The next free port stays 7816 unless a task used it.
- [ ] **`EDITOR-TODO.md` section 2:** add a line: in two editors, start a match while a client's second pad is just joining; nothing flashes up.
- [ ] **The full suite:** `bash tools/run-tests.sh` (in the background). Expected: all pass.

# Phase 5A — Deferred Minors Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** close every open item in the handoff's "Deferred minors still open" list: fix it, or close it with a reason.

**Architecture:** small, independent fixes grouped by area (Tasks 1–5 and 7), plus one new feature, two-sided bumps online (Task 6). Each fix gets a test that fails first, where a test can reach it. Item numbers (M1–M38) are in the index below, in the handoff's order.

**Tech Stack:** Unity 2022.3.62f3 · URP 14.0.12 · Input System 1.14 · Netcode for GameObjects 1.15.1 · Steamworks.NET 2025.164.1 · Unity Test Framework 1.1.33.

**Spec:**
- The user, 2026-10-07: "fix all deferred minors that are open". The list is the handoff's "Deferred minors still open" (`docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`).
- The user's answers, 2026-10-07:
  - **SteamManager:** allow the edit, a guard against a second `SteamAPI.Init`. This is a one-time exception to "SteamManager.cs stays untouched".
  - **Remote bumps:** build two-sided bumps (plan first; this plan is that review).
  - **Test access:** switch the tests from reflection to `internal` members.
  - **Joining:** the game takes over joining from `PlayerInputManager`.

## Item index (the handoff's list, in order)

| # | Item | Where |
|---|---|---|
| M1 | 4D: a first `ApplyFor(3-4)` resets the player count to 1 | Task 1 |
| M2 | 4D: `ApplyFor` after quitting re-applies with nothing to restore it | Task 1 |
| M3 | 4D: no warning if URP's renderer list field is missing | Task 1 |
| M4 | 4D: `CutoutHandler` logs for a ball without an `OrderHandler` | Task 1 |
| M5 | 4D: a camera toggled after the players' `LateUpdate` leaves a blank frame | Task 1 |
| M6 | 4D: the F6 overlay's 1% low allocates each `OnGUI` | Task 1 |
| M7 | 4D: check the ragdoll swap reads no culled bones | Ruling 1 (checked) |
| M8 | 4C: a kicked player can reconnect | Task 2 |
| M9 | 4C: the kick reason may not arrive | Task 2 |
| M10 | 4C: release builds warn about `directAddress`/`directPort` | Task 2 |
| M11 | 4C: NaN poses log an error per frame | Task 2 |
| M12 | 4C: a host hitch over 5 s drops a joining friend | Task 2 |
| M13 | 1C: display settings re-applied on every return to the menu | Task 4 |
| M14 | 1C: the keyboard join relies on event order | Task 5 |
| M15 | 4A: two launches in one second share a session log name | Task 4 |
| M16 | 4A: the opening cutscene shows "Delivering" | Task 4 |
| M17 | 4A: the `.vdf` uses em dashes | Task 4 |
| M18 | 3I: a slow client shows the match starting before following the host to the menu | Task 3 |
| M19 | 3I: a throwing `Ended` listener skips later ones | Task 3 |
| M20 | 3I: no joining-side test of the golden order's release | Task 3 |
| M21 | 3I: `ResetHandler`'s lambda removal is a no-op | Task 3 |
| M22 | 3I: `LobbyRules.VersionOf(null)` throws | Task 3 |
| M23 | 3H: another machine's death sound plays on the SFX group | Task 3 |
| M24 | 3H: another machine's phasing sound plays over its boost | Task 3 |
| M25 | 3D: a double "Join Game" enters the lobby twice | Task 3 |
| M26 | 3D: an invalid `SteamAPICall_t` isn't checked | Task 3 |
| M27 | 3D: Y would fire twice if the prefab also wires it | Task 3 |
| M28 | 3D: the editor's Online menu can host a second session | Task 3 |
| M29 | 3D: `LobbyPrompt` and `ControllerPrompts` share sorting order 1000 | Task 3 |
| M30 | 3D: after `UseDirect`, the replaced lobby stays subscribed | Task 3 |
| M31 | 3C: bumps with another machine's scooter are one-sided | Task 6 |
| M32 | 3C: `OnlineDriving` looks up `BallDriving` every frame | Ruling 1 (done in 4D) |
| M33 | 3C: the host's Main Menu during a slow client's activation | M18 |
| M34 | 1A/1B/2A: tests reach private members by reflection | Task 7 |
| M35 | 1A/1B/2A: `QAManager` catches only `IOException` | Gone: the QA recorder was removed (2026-10-07) |
| M36 | 1A/1B/2A: `SteamManager` has no guard against a second `SteamAPI.Init` | Task 4 |
| M37 | 1A/1B/2A: keyboard presses spawn and destroy a player prefab | Task 5 |
| M38 | 1A/1B/2A: unused usings, and test-message wording | Ruling 1 and 2, Task 7 |

## Global Constraints

- **Stay on Unity 2022.3 LTS** and Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**.
- **Never edit a scene, prefab, `.inputactions` asset or ProjectSettings file.**
- **`SteamStartup.cs` stays untouched.** `SteamManager.cs` gets only M36's guard.
- **The embedded Steam transport** (`Packages/com.community.netcode.transport.steamnetworkingsockets/`) may be patched for M8–M9. Mark each patch `// DoA`, and list it in its `CHANGES-DOA.md`.
- **Local play must behave exactly as before.** `LocalMatchSmokeTest` and every online test pass after every task.
- **Online safety stays** (`docs/online-safety.md`): every new ServerRpc starts with `Allowed(sender, kind)`, and the host checks `Owns`/`SeatOf` before acting on a seat. Values from another machine pass `NetChecks`.
- **Branch:** `steam-phase1a`. **No git commits:** the user commits.
- **Meta files:** run `bash tools/newmeta.sh <path>` for every new file under `Assets/`.
- **Line endings:** check with `git ls-files --eol`. Edit `i/crlf` files with the Edit tool only.
- **Tests:**
  - Use `bash tools/run-tests.sh [filter]`, one run at a time. Use `python tools/compile-check.py` while editing.
  - Play Mode rules: recorder classes; wait by real time; `EnterPlayMode` yielded by the test itself; `log.MachinesLeave()` before a machine leaves.
  - Network ports in use run to 7812 (Phase 3J's plan reserves 7813). This phase uses **7814** and **7815**; update `docs/testing.md`'s next free port to 7816.
- **Phase 3J** (`2026-10-06-phase3j-online-couch.md`) isn't started.
  - Where this phase touches `OnlineSession`, `OnlineGame` or `PlayerInstantiate`, keep the change small.
  - Where it uses `OnlineSession.SeatOf`, note in the ledger that 3J turns it into `Owns`.

## Rulings (decided while planning)

1. **Already done; close with a note, no code:**
   - M32 (`OnlineDriving`'s per-seat lookup, cached in Phase 4D);
   - M38's unused usings (removed in the 2026-10-06 cleanup);
   - M7 (pedestrian ragdolls): checked: `CivilianAgent.Die` reads only the root `renderBasket` transform, never a bone, so culled bones can't affect the swap.
2. **M38's "some test-message wording" has no details left.** The Phase 1A/1B/2A ledgers that held them were deleted in the cleanup. Task 7 rewords any assertion message it touches that doesn't say what's expected; nothing more.
3. **M12 (host hitch drops a joining friend):** `ClientConnectionBufferTimeout` goes from 5 s to **10 s**. Joins happen only in the menus, so a longer wait costs nothing there, and 10 s still drops an attacker's half-open connection quickly.
4. **M16 (cutscene presence):** the opening cutscene sets no presence (`null`), so "Getting ready" stays until the tutorial sets "Learning the ropes". Every match opens with the tutorial (Phase 3F).
5. **M17 (`.vdf` em dashes):** replace them with ASCII. Use "Delivering (%players% players)" and "Chasing the golden order (%players% players)", and update `docs/steam/in-game-features.md` if it quotes them. This removes the question instead of testing Steam's upload.
6. **M8 (a kicked player reconnecting):** the patched transport bans the kicked Steam ID for the rest of the session (`ConnectionGate.Ban`), and refuses it as it refuses strangers. Steam lobbies have no kick, so the player stays in the lobby but can't connect.
7. **M9 (the kick reason):** the transport closes a kicked connection with linger on, so Netcode's queued reason reaches the player first. It can only be checked on two PCs (`EDITOR-TODO.md`).
8. **M11 (NaN poses):** `OwnerNetworkTransform` stops applying states on other machines once a state isn't sane (`NetChecks.Finite`/`InWorld`). It resumes after `POISON_CLEAR` = 1 s of sane states, which flushes Netcode's interpolation buffer. Until then the proxy stays at its last good pose.
9. **M5 (a camera toggled late in the frame):** `PlayerCameraResizer` gets `[DefaultExecutionOrder(1000)]`, so its `LateUpdate` budget runs after other scripts' `LateUpdate`. A camera toggled in a coroutine that waits for the end of the frame can still show one blank frame; nothing in the game does that.
10. **M19 (`OnlineGame.ShowEnd` inside `Ended`):** `OnlineSession` calls each `Ended` listener in its own try/catch (`Debug.LogException`), so a throwing listener can't skip the Steam lobby's leave.
11. **M18 (the slow client shown the match starting):** when the host's return to the menu reaches a client (`OnlineMatch.ReturnRequested`), `OnlineGame` drops the host messages still waiting (`HostQueue.Clear`). Those are the abandoned match's. Messages after the return queue as usual.
12. **M27 (Y wired twice):** `PlayerUIHandler` wires Y in code only when the `PlayerInput`'s own events don't already have a persistent listener on that action.
13. **M29 (sorting order):** `LobbyPrompt` goes to 999, below `ControllerPrompts`' hints, which must stay readable.
14. **Joining (M14, M37):**
    - At `PlayerInstantiate.OnEnable`, the game sets `PlayerInputManager.instance.joinBehavior = JoinPlayersManually`, and joins devices itself from `InputUser.onUnpairedDeviceUsed`:
      - a gamepad joins on any button;
      - a keyboard joins on Space or Enter only, read from the event as `KeyboardControls` does today.
    - Anything else gets the unsupported-device hint that `AddPlayerReference` shows today. Then `AddPlayerReference` no longer turns devices away.
    - `ReplacementControllerListener` keeps its job: while it listens for a lost controller's replacement, the joiner doesn't join that pad.
15. **Tests through internal members (M34):**
    - Add `Assets/Scripts/AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]`.
    - Each private member a test reaches by name becomes `internal`, keeping `[SerializeField]`, which Unity needs to serialize a non-public field. Tests then use it directly.
    - `Reflect.HandlerCount` stays reflection (an event's backing field can't be reached otherwise), but its callers pass `nameof(...)`.
    - The reflection on URP's own private fields (`GraphicsQualityTests`) stays: that's another package.
    - `Reflect.GetField`/`SetField`/`Invoke`/`SetSingleton` go once unused.
16. **Two-sided bumps (M31), the design:**
    - A machine whose own ball (not boosting: boosting into someone is a steal or clash) hits another machine's scooter reports the bump: `BumpSync.Report(bumper, victim, speed)`.
      - `speed` is the closing speed along the contact normal, from the local ball's velocity, since the other is kinematic here.
      - At most one report per pair per `BumpRules.PAIR_COOLDOWN` = 0.5 s.
    - **The host checks:**
      - the sender owns `bumper`;
      - both players are there and not respawning;
      - their balls are within `StealRules`' 50 m on the host;
      - `speed` is finite;
      - then it clamps `speed` to `BumpRules.MAX_SPEED` = 40.
    - **The host sends `PlayerBump(bumper, victim, push)` to every machine.** The victim's own machine applies it as `BallDriving.BounceOff(bumper's ball position, push)`, where `push = BumpRules.Push(speed)` = `speed * PUSH_PER_SPEED` (0.5) capped at `ClashForce`.
    - The host applies its own player's at once. It uses `RpcKind.Bump` (10 burst, 5/s).
    - The bumper's own machine already bounced off the kinematic proxy, as today, so no change there.
    - **Cost if the feel is off:** two constants to tune by hand in a two-PC test (`EDITOR-TODO.md`).

## Review Focus

1. **Joining after the takeover (Task 5):**
   - a pad pressing Start or a stick click still joins;
   - Space joins the keyboard and other keys don't;
   - joins during a match or a lost-controller wait don't happen;
   - a second press doesn't make a second player.

   *(Task 5 tests.)*
2. **Bumps a modified client could abuse (Task 6):**
   - a huge or NaN speed, a bump for a seat it doesn't own, a far-away victim;
   - a flood: capped by `RpcKind.Bump`, and a pair cooldown on every machine.

   *(Task 6: `BumpRulesTests`, `BumpsNetworkTests`.)*
3. **The kick ban (Task 2):** it outlasts the kicked connection, but not the session: a new hosting session starts clean. *(Task 2: `ConnectionGateTests.Ban_RefusesThatSteamId_UntilCleared`.)*
4. **A NaN pose (Task 2):** the other machine logs no per-frame error, and the scooter shows again once good poses resume. *(Task 2: `NonsenseNetworkTests.Joining_NanPoses_HoldTheScooter_ThenItFollowsAgain`.)*
5. **Internal members (Task 7):**
   - no member a scene sets loses `[SerializeField]`;
   - no `public` member is narrowed;
   - `PlayerPrefabTests`/`ScooterLookTests` (which read serialized fields by name) still pass.

   *(Task 7: `compile-check`, `PlayerPrefabTests`, the full suite.)*

---

### Task 1: Graphics and frame rate (M1–M6)

**Files:**
- `Assets/Scripts/Management/GraphicsQuality.cs`
- `Assets/Scripts/Management/FrameStats.cs`
- `Assets/Scripts/World/CutoutHandler.cs`
- `Assets/Scripts/Player/PlayerCameraResizer.cs`
- Tests:
  - `GraphicsQualityTests`, `FrameStatsTests`;
  - create `Assets/Tests/Editor/LateCameraTests.cs` (menu scene, one pad, like `OnlineSeatTests`).

- [ ] **Step 1: Write the failing tests.**
  - **M1:** `GraphicsQualityTests.ApplyFor_TheFirstTime_KeepsThePlayerCount`. After `Restore()`, `ApplyForAsset(3, asset)` leaves `CurrentPlayers == 3`, and the shadow map is 2048 at High.
  - **M2:** `GraphicsQualityTests.Apply_AfterQuitting_ChangesNothing`:
    - apply Medium;
    - call the quitting handler (`GraphicsQuality.OnQuitting()`, internal; it restores and sets `quit`);
    - `ApplyForAsset(4, asset)`;
    - the asset reads as authored.
  - **M3:** `GraphicsQualityTests.SsaoFeatures_WithoutTheRendererList_WarnsOnce`. Use an internal seam `GraphicsQuality.RendererListMissing()`, which logs the warning once. Assert `LogAssert.Expect(LogType.Warning, …)` exactly once over two calls.
  - **M6:** `FrameStatsTests.OnePercentLow_AllocatesNothing`. With 500 frames added and one warm-up call, `GC.GetAllocatedBytesForCurrentThread()` doesn't change over 10 calls.
  - **M4:** `CutoutHandlerTests.ABallWithoutOrders_IsNotAScooter`:
    - a GameObject with a child `BallDriving` (disabled) and no `OrderHandler`;
    - `CutoutHandler.FindScooter` (made internal static) returns false, and logs nothing.
  - **M5:** `LateCameraTests.ACameraTurnedOnInLateUpdate_TurnsThePlayersViewOffThatFrame`:
    1. One player is in the menu scene.
    2. A test component with default execution order enables a new full-screen camera (depth 20, clears) in its `LateUpdate`.
    3. `yield return new WaitForEndOfFrame()`.
    4. The player's main camera is disabled.
- [ ] **Step 2: Run them, and see them fail.** Run: `bash tools/run-tests.sh "GraphicsQualityTests|FrameStatsTests|CutoutHandlerTests|LateCameraTests"`. Expected: compile errors or failures.
- [ ] **Step 3: Implement.**
  - **M1:** `Apply` saves `CurrentPlayers` before `Restore()` and puts it back after.
  - **M2:** `static bool quit`, set by the `Application.quitting` handler, which still restores. `Apply`/`ApplyForAsset` return at once while it's set. It's cleared by a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` reset.
  - **M3:** `SsaoFeatures` calls `RendererListMissing()` when the field is null: "Graphics quality: this URP version has no renderer list; SSAO is left as authored".
  - **M6:** a reused `float[] sorted` buffer, grown only when `Count` grows.
  - **M4:** `IsScooter = Ball != null && Player != null`.
  - **M5:** `[DefaultExecutionOrder(1000)]` on `PlayerCameraResizer`.
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "GraphicsQualityTests|FrameStatsTests|CutoutHandlerTests|LateCameraTests|CameraBudgetTests|LocalMatchSmokeTest"`. Expected: all pass.

### Task 2: Online safety (M8–M12)

**Files:**
- `Packages/com.community.netcode.transport.steamnetworkingsockets/Runtime/ConnectionGate.cs` and `SteamNetworkingSocketsTransport.cs`, plus its `CHANGES-DOA.md`.
- `Assets/Scripts/Online/OnlineSession.cs`
- `Assets/Scripts/Online/OnlinePlay.cs`
- `Assets/Scripts/Online/OwnerNetworkTransform.cs`
- Tests: `ConnectionGateTests`, `NonsenseNetworkTests`, `OnlineSessionTests`.

- [ ] **Step 1: Write the failing tests.**
  - **M8:** `ConnectionGateTests.Ban_RefusesThatSteamId_UntilCleared`:
    - `gate.Ban(7)`, then `Decide(7, …, accepts: _ => true, false)` is `Refuse`;
    - another ID is `Accept`;
    - after `gate.ClearBans()` it's `Accept`.
  - **M11:** `NonsenseNetworkTests.Joining_NanPoses_HoldTheScooter_ThenItFollowsAgain` (port 7812's file; its own port stays):
    1. A client's scooter proxy is fed a NaN position: the test sets its ball transform's state through `OwnerNetworkTransform` on the owner; the owner's own one error is expected with `LogAssert`.
    2. Over 1 s the host logs at most one "Input position is" error.
    3. Then the owner shares good poses for 1.5 s, and the host's proxy is within 1 m of the last one.
  - **M12:** `OnlineSessionTests.Config_WaitsTenSecondsForApproval`: `ClientConnectionBufferTimeout == 10` on a new session's config.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement.**
  - **M8:**
    - `ConnectionGate.Ban(ulong)` and `ClearBans()`; `Decide` refuses a banned ID first.
    - The transport takes a public `bool BanOnNextDisconnect`. `DisconnectRemoteClient` bans the closing connection's Steam ID when it's set, then clears it.
    - `OnlineSession.Kick` sets it before `DisconnectClient` (Steam sessions only).
    - Verify that Netcode calls the transport's `DisconnectRemoteClient` inside `DisconnectClient`. If it defers it, ledger a ruling and use a pending set of client IDs instead.
    - `StartHost` clears the bans.
  - **M9:** `DisconnectRemoteClient` closes with `bEnableLinger: true`.
  - **M10:** `directAddress`/`directPort` (`OnlinePlay.cs:28-29`) go inside the same `#if UNITY_EDITOR || DEVELOPMENT_BUILD` as their users. A release build then has no unused fields.
  - **M11:** `OwnerNetworkTransform`:
    - overrides `OnNetworkTransformStateUpdated` to record when a state isn't sane;
    - overrides `Update` to skip `base.Update()` until `POISON_CLEAR` (1 s) of sane states.

    Netcode's interpolation buffer can hold a bad state, so skipping `Update` is what keeps the error away.
  - **M12:** `ClientConnectionBufferTimeout = 10`.
  - **Docs:** `CHANGES-DOA.md` items 6–7, and `docs/online-safety.md`.
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "ConnectionGateTests|NonsenseNetworkTests|OnlineSessionTests|MessageLimitsNetworkTests|OnlineScootersNetworkTests"`. Expected: all pass.

### Task 3: Online flow, lobby and remote sounds (M18–M30)

**Files:**
- `Assets/Scripts/Online/OnlineGame.cs`, `OnlineSession.cs`, `LobbyRules.cs`, `OnlineLobby.cs`, `SteamLobbyService.cs`, `OnlinePlay.cs`
- `Assets/Scripts/Player/OrderHandler.cs`, `SoundPool.cs`
- `Assets/Scripts/Menu/PlayerUIHandler.cs`
- `Assets/Scripts/UI/LobbyPrompt.cs`
- `Assets/Scripts/Editor/OnlineTestMenu.cs`
- Tests:
  - `OnlineSessionTests`, `LobbyRulesTests`, `OnlineLobbyTests`, `SteamLobbyServiceTests`, `OnlineOrdersNetworkTests`, `OnlineCuesNetworkTests`, `DisconnectsNetworkTests`, `OnlinePlayTests`, `OnlineTestMenuTests`, `LobbyPromptTests`, `MenuButtonsTests`;
  - create `Assets/Tests/Editor/OrderHandlerResetTests.cs`.

- [ ] **Step 1: Write the failing tests.** One per item:
  - **M18:** `DisconnectsNetworkTests.Joining_TheHostReturnsWhileThisMachinesLoadWaits_NoMatchStateShowsHere`. Like `Joining_TheHostLeavesWhileThisMachinesLoadWaits_…`, but the host calls `RequestReturn()` instead of leaving. A recorder on `GameManager.StateApplied` hears no `StartingCutscene`/`Tutorial`, and the client ends in the menus.
  - **M19:** `OnlineSessionTests.Ended_AListenerThatThrows_DoesntStopTheNext`.
  - **M20:** `OnlineOrdersNetworkTests.Joining_AGoldenHolderLeaving_PutsItBackHereToo`. With 3 machines (two clients on 7814), the client holding the golden order leaves. The other client sees the golden order back at its start, at its starting value (`EraseGold` then `Spawn` replayed).
  - **M21:** `OrderHandlerResetTests.ResetHandler_RemovesWhatInitHandlerAdded`. Two `InitHandler`/`ResetHandler` cycles leave `Reflect.HandlerCount(orderManager, nameof(OrderManager.OnMainGameFinishes), handler) == 0`; use the real event name.
  - **M22:** `LobbyRulesTests.VersionOf_Null_IsEmpty`.
  - **M25:** `OnlineLobbyTests.Answer_TheSameLobbyTwice_JoinsOnce`.
  - **M26:** `SteamLobbyServiceTests.Create_AnInvalidCall_FailsAtOnce`. Through a seam: `SteamLobbyService.Started(SteamAPICall_t)` returns false for `k_uAPICallInvalid`, and `Create`/`Join` then raise their failure at once.
  - **M27:** `MenuButtonsTests.NorthButton_WithAPersistentListenerToo_FiresOnce`. Add a persistent listener in code with `UnityEventTools.AddPersistentListener` on a test copy, and count calls.
  - **M28:** `OnlineTestMenuTests.CanStart_WhileOnlinePlaysASession_IsFalse`.
  - **M29:** `LobbyPromptTests.SortsBelowTheHints`: `LobbyPrompt`'s canvas `sortingOrder` is less than `ControllerPrompts`'.
  - **M30:** `OnlinePlayTests.UseDirect_Twice_TheFirstLobbyHearsNothingMore`.
  - **M23:** `OnlineCuesNetworkTests.Joining_AnotherMachinesDeath_PlaysOnThePlayerGroup`. The remote death source's `outputAudioMixerGroup.name == "Player"`.
  - **M24:** `OnlineCuesNetworkTests.Joining_AnotherMachinesPhase_StopsItsBoostSound`. After a Boost cue then a Phase cue, no source on that scooter plays the boost clip.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement**, as the Rulings say (10–13). The rest:
  - **M21:** `InitHandler` stores its two handlers in fields; `ResetHandler` removes those.
  - **M22:** `VersionOf(null)` returns `""`.
  - **M25:** `OnlineLobby` ignores an answer or join for the lobby it's already joining or in.
  - **M26:** `SteamLobbyService` treats `k_uAPICallInvalid` as an immediate failure.
  - **M28:** `OnlineTestMenu.CanStart` is false while `GameAuthority.IsOnline`.
  - **M30:** `UseDirect` unsubscribes the old lobby's events.
  - **M23:** the remote death source switches to "Player", as `PlayDeathSound` does.
  - **M24:** the pool remembers the remote boost's source (`remoteBoostSource`), and a remote Phase resets it while it still plays the boost clip.
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "OnlineSessionTests|LobbyRulesTests|OnlineLobbyTests|SteamLobbyServiceTests|OnlineOrdersNetworkTests|OnlineCuesNetworkTests|DisconnectsNetworkTests|OnlinePlayTests|OnlinePlayNetworkTests|OnlineTestMenuTests|LobbyPromptTests|MenuButtonsTests|OrderHandlerResetTests"`. Expected: all pass.

### Task 4: Settings, Steam features and small fixes (M13, M15–M17, M36)

**Files:**
- `Assets/Scripts/Menu/DisplaySettings.cs`, `Assets/Scripts/Menu/DisplayRules.cs`
- `Assets/Scripts/Steam/SessionLog.cs`, `Assets/Scripts/Steam/PresenceRules.cs`
- `Assets/Scripts/Management/SteamManager.cs` (M36 only)
- `docs/steam/rich-presence-english.vdf`, `docs/steam/in-game-features.md`
- Tests: `DisplayRulesTests`, `SessionLogTests`, `PresenceRulesTests`.

- [ ] **Step 1: Write the failing tests.**
  - **M13:** `DisplayRulesTests.NeedsResize_OnlyWhenTheModeOrSizeDiffers`:
    - `DisplayRules.NeedsResize(FullScreenMode current, ScreenSize currentSize, FullScreenMode wanted, ScreenSize wantedSize)`;
    - the same mode and size is false; another mode or size is true.
  - **M15:** `SessionLogTests.FileName_TwoLaunchesInOneSecond_Differ`. Two starts 300 ms apart get different names, which still sort by time; `ToDelete` still keeps the newest 10 with the new format.
  - **M16:** `PresenceRulesTests.OpeningCutscene_KeepsTheLastPresence`: `For(StartingCutscene, n)` is null; `Begin` and `MainLoop` are unchanged.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement.**
  - **M13:** `DisplaySettings.Apply` calls `Screen.SetResolution` only when `NeedsResize`; VSync and the frame cap are set as before.
  - **M15:** `FileName` uses `"yyyyMMdd-HHmmss-fff"`.
  - **M16:** as in the Rulings.
  - **M17:** as in the Rulings.
  - **M36:** `SteamManager.Awake` returns before `SteamAPI.Init` when `SteamAPI.IsSteamRunning()` and a static `initialized` flag already say so. Read the file first, and keep the change to that guard.
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "DisplayRulesTests|SessionLogTests|PresenceRulesTests|SteamFeaturesTests|OptionsMenuTests"`. Expected: all pass. Then check by hand that `rich-presence-english.vdf` has no non-ASCII characters (`grep -nP '[^\x00-\x7F]'`).

### Task 5: The game joins players itself (M14, M37)

**Files:**
- create `Assets/Scripts/Player/PlayerJoiner.cs`, plus its `.meta`;
- `Assets/Scripts/Player/PlayerInstantiate.cs`, `KeyboardControls.cs`, `ReplacementControllerListener.cs`;
- Tests: create `Assets/Tests/Editor/PlayerJoinerTests.cs`; `KeyboardPlayerTests`, `PlayerSlotTests`, `OnlineSeatTests`, `LocalMatchSmokeTest`.

**Interfaces:**
- `public static class PlayerJoiner`:
  - `public static string SchemeFor(InputControl control, InputEventPtr eventPtr)`: `"Gamepad"` for a gamepad button press in the event, `KeyboardControls.SCHEME` for Space/Enter/numpad Enter on a keyboard, else null.
  - `public static bool Enabled`: on from `PlayerInstantiate.OnEnable` to `OnDisable`.
- When `SchemeFor` is null, the hint `AddPlayerReference` shows today (`ShowUnsupportedDeviceHint`) moves here, under the same condition: `allowPlayerSpawn || PlayerCount == 0`.
- `KeyboardControls.JoinKeyUsedThisFrame` and its event subscription go.

- [ ] **Step 1: Write the failing tests.**
  - `PlayerJoinerTests.SchemeFor_Pads_AnyButton_Keyboards_SpaceOrEnterOnly`, as a pure check with queued events on test devices.
  - `KeyboardPlayerTests.PressingOtherKeys_SpawnsNothing`. In the menu scene, 20 presses of A–Z make no `PlayerInput` (count `FindObjectsOfType<PlayerInput>()` every frame; it stays 0).
  - The existing keyboard and pad join tests stay as they are.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement**, as Ruling 14 says.
  - Joins call `PlayerInputManager.instance.JoinPlayer(-1, -1, scheme, device)`, only while `PlayerInputManager.instance.joiningEnabled`.
  - `MoveToOnlineSeat` keeps calling `JoinPlayer` itself.
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "PlayerJoinerTests|KeyboardPlayerTests|PlayerSlotTests|OnlineSeatTests|ControllerTests|LocalMatchSmokeTest"`. Expected: all pass. The smoke test's join, leave and rejoin and its controller unplug and replug cover the pads.

### Task 6: Two-sided bumps online (M31)

**Files:**
- create `Assets/Scripts/Online/BumpRules.cs`, `BumpSync.cs`, `PlayerBump.cs` and `OnlineBumps.cs`, plus their `.meta` files;
- `Assets/Scripts/Online/OnlineMatch.cs`, `RateGate.cs` (`RpcKind.Bump`, appended last), `OnlineGame.cs` (adds `OnlineBumps`);
- `Assets/Scripts/Player/BallCollision.cs` (or the ball's collision component: the one with `OnCollisionEnter`);
- Tests: create `Assets/Tests/Editor/BumpRulesTests.cs` and `Assets/Tests/Editor/BumpsNetworkTests.cs` (port **7815**, like `OnlineStealsNetworkTests`).

**Interfaces:**
- `public static class BumpRules`:
  - `PAIR_COOLDOWN = 0.5f`, `MAX_SPEED = 40f`, `PUSH_PER_SPEED = 0.5f`;
  - `float Push(float speed, float maxPush)`: `Mathf.Min(Mathf.Clamp(speed, 0, MAX_SPEED) * PUSH_PER_SPEED, maxPush)`, and 0 for a speed that isn't finite;
  - `string Refusal(…)`, mirroring `StealRules`' checks;
  - `bool TooSoon(int a, int b, float now)`, as an instance class with its own last-bump table.
- `public struct PlayerBump : INetworkSerializable { int Bumper; int Victim; float Push; }`
- `public static class BumpSync`, like `StealSync`: `Report(int bumper, int victim, float speed)`.
- `OnlineMatch`:
  - `AskBump(int bumper, int victim, float speed)` → `BumpServerRpc`, with `Allowed(sender, RpcKind.Bump)` first;
  - `SendBump(PlayerBump)` → `BumpClientRpc`, which `NetChecks` the values;
  - events `BumpAsked` and `BumpReceived`.
- `OnlineBumps` (added by `OnlineGame`):
  - the host judges each bump, and takes a request only for the sender's own seat;
  - every machine bounces its own victim with `BounceOff`.

  Bumps aren't queued with `HostQueue`: like cues, a bump is about the moment.

- [ ] **Step 1: Write the failing tests.**
  - `BumpRulesTests`:
    - `Push_ScalesWithSpeed_CappedAtTheClash`;
    - `Push_NanOrNegative_IsZero`;
    - `Refusal_FarApart_OrRespawning_OrMissing`;
    - `TooSoon_APairWithinHalfASecond_EitherWayRound`.
  - `BumpsNetworkTests`, a host and a client in an empty scene with test scooters, as in `OnlineStealsNetworkTests`:
    - `Joining_BumpingTheHostsScooter_PushesItOnTheHost`: the client reports (1, 0, 10); the host's ball gets a velocity change away from the client's within 1 s.
    - `Hosting_ABumpForASeatTheSenderDoesntOwn_IsIgnored`.
    - `Hosting_ABumpFlood_IsCapped`: 100 reports in a frame; at most 10 reach `BumpAsked` handling.
    - `Joining_ANanBump_IsIgnored`.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement**, as Ruling 16 says. The local report comes from the local ball's `OnCollisionEnter`:
  - the other collider's `OrderHandler` must be `RemoteAvatar.IsRemote`;
  - this ball isn't boosting;
  - `speed = Vector3.Dot(relativeVelocity, -contact.normal)`, kept only when positive.
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "BumpRulesTests|BumpsNetworkTests|RateGateTests|OnlineStealsNetworkTests|RemoteScooterRulesTests"`. Expected: all pass.

### Task 7: Tests use internal members (M34, M38)

**Files:**
- create `Assets/Scripts/AssemblyInfo.cs`, plus its `.meta`;
- the game files whose private members tests name (about 129 members), and the tests that use them;
- `Assets/Tests/Editor/TestSupport.cs`.

This task is mechanical and wide. It may go to Sonnet agents in parallel, by test file, with one agent per game file so no two agents edit the same game file. Only one test run at a time.

- [ ] **Step 1: Inventory.** Write the list of members reached by name to the ledger: `grep -rhoE 'Reflect\.(GetField|SetField|Invoke)\([^,]+, "[A-Za-z_0-9]+"' Assets/Tests`, plus `SetSingleton`. No failing test here: this is a refactor with the suite as its guard.
- [ ] **Step 2: Convert, game file by game file.**
  - Each member becomes `internal`, and `[SerializeField]` stays where it was. A member that's `protected` or `public` already stays as it is.
  - The tests use the member directly.
  - Run `python tools/compile-check.py` after each game file.
- [ ] **Step 3: Tidy up.**
  - Remove `Reflect.GetField`, `SetField`, `Invoke` and `SetSingleton` once nothing uses them.
  - `HandlerCount` callers pass `nameof(...)`.
  - Reword any assertion message touched here that doesn't say what's expected.
- [ ] **Step 4: Run the full suite.** Run: `bash tools/run-tests.sh`. Expected: all pass. `grep -rn 'Reflect\.\(GetField\|SetField\|Invoke\|SetSingleton\)' Assets/Tests` prints nothing.

### Task 8: Docs, handoff, full suite

- [ ] **Step 1: Docs.**
  - **The handoff:** "Deferred minors still open" loses every item closed here. Anything a review defers goes back in. Update the status.
  - **`docs/online.md`:**
    - Known limits: remove "bumps are one-sided" if listed;
    - add a Phase 5A note for bumps, the kick ban and NaN holding.
  - **`docs/online-safety.md`:** the ban, the linger, the NaN hold, and the 10 s approval wait.
  - **`docs/controls.md`:** joining, if it names `PlayerInputManager`.
  - **`docs/testing.md`:** the new test classes, and the next free port.
  - **`EDITOR-TODO.md`:**
    - import Phase 5A;
    - on two PCs: a kicked player sees the reason and can't reconnect;
    - tune bump feel (`BumpRules.PUSH_PER_SPEED`);
    - check exclusive fullscreen no longer flickers on returning to the menu.
  - **Memory:** a Phase 5A block.
- [ ] **Step 2: Run the full suite in the background.** Run: `bash tools/run-tests.sh`. Expected: all pass. Then the final review: a fresh Sonnet reviewer over the uncommitted diff, with this plan.

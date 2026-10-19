# Matchmaking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Players without friends online can find a match. In Dispatch, the host can open their lobby to strangers (**Fill**), and any lone player can search for an open lobby (**Find**). A lobby open to strangers starts by itself 60 s after the last player sat down.

**Architecture:**
- Everything goes through the existing Steam lobby layer:
  - `ILobbyService` gains public/friends-only switching, a lobby search, and this machine's Steam ID.
  - `OnlineLobby` gains Fill and a lobby **switch**.
  - A new plain-C# `Matchmaker` runs Find over both.
- Pure rules live in a new static `MatchRules`: tag parsing, picking a lobby, the countdown, the status text.
- The countdown is one new host-written network variable on `OnlineMatch`. Each machine readies its own player when the countdown ends, and the host forces the start 5 s later.
- Safety: every value from a found lobby or a host is parsed strictly and capped. Searches are rate-limited. A new `JoinChurn` refuses a Steam ID that keeps reconnecting.

**Tech Stack:** Unity 2022.3.62f3, C#, Netcode for GameObjects 1.15.1, Steamworks.NET, TextMesh Pro, NUnit (Unity Test Framework).

**Spec:** `docs/superpowers/specs/2026-10-18-matchmaking-design.md` (read it with this plan, especially "Calls made after the builder check").

## Global Constraints

- **Edit rules:**
  - No scene, prefab, `.asset`, `.inputactions` or ProjectSettings edits. Those go in `EDITOR-TODO.md`.
  - CRLF files (`PlayerInstantiate.cs`, `MainMenu.cs`, `MenuInteractions.cs`, `SceneManager.cs`, `GameManager.cs`): edit with the Edit tool only, never `sed -i`.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
  - `SteamManager.cs` and `SteamStartup.cs` are untouched. The transport package (`Packages/com.community.netcode.transport.steamnetworkingsockets`) is untouched.
  - Match the surrounding code: `///` summaries in the existing voice, `internal` for test seams.
- **Git:** branch `steam-phase1a`. Never check out `main`. Leave work uncommitted: the user commits. "Commit" steps below mean "stop and list the changed files".
- **Tests:**
  - Run with `bash tools/run-tests.sh [filter]`, one run at a time. A compile error prints `NO RESULTS`. Compile check (~30 s): `python tools/compile-check.py`.
  - Network test ports: the next free one is **7819**. This plan uses 7819 and 7820.
  - Play Mode rules: no lambda captures a test's local; wait by real time; only `yield return null`; `EnterPlayMode` is yielded by the test itself.
  - Fakes live in `Assets/Tests/Editor/OnlineFakes.cs` (`FakeLobbyService`, `FakeSessionControl`).
- **Constants (exact)**, all in `MatchRules`:
  - `MM_KEY = "mm"`, `MM_ON = "1"`, `OPEN_KEY = "open"` (free seats, "0".."4")
  - `MAX_RESULTS = 50`, `MAX_TAG = 64` (characters), `SEARCH_GAP = 5.0` s, `MAX_TRIES = 3`
  - `RESEARCH = 15.0` s (a lone Fill host searching again), `AUTO_START = 60.0` s, `FORCE_GRACE = 5.0` s
  - `JoinChurn`: `CHURN_LIMIT = 5` accepted connections per Steam ID within `CHURN_WINDOW = 60.0` s
- **Copy (exact):**
  - Searching: `LOOKING FOR A MATCH…`
  - None found (notice): `No open matches right now. Your lobby is open to anyone.`
  - Status line: `OPEN TO ANYONE`, `FRIENDS ONLY`, and while counting `OPEN TO ANYONE · STARTS IN {s}` (whole seconds, rounded up)
  - Prompts: `FIND MATCH` (left shoulder), `OPEN TO ANYONE` / `FRIENDS ONLY` (right shoulder, the label is what pressing does)
  - Find refused for a host with others in the lobby (notice): `Find a match before others join your lobby.`
- **Never public by default:** a lobby is created friends-only exactly as today. Only Fill (or Find's fallback) makes it public.

## Review Focus

- **Someone joins your lobby while Find is searching or switching.** The switch is abandoned, you stay host, and nobody is dragged out. Test in Task 5 (`Find_SomeoneJoinsDuringSearch_StaysHost`).
- **A found lobby with garbage tags** ("open" = "-1", "99", "4abc", 10 000 characters, a missing `mm`). It's skipped without throwing, and its text is never shown. Test in Task 1 (`Candidates_GarbageTags_Skipped`).
- **The found lobby's host leaves between entering and joining the session.** You end up hosting a fresh lobby that's open to anyone, still on Dispatch, and not on the title screen. Test in Task 5 (network, `Find_HostGoneAfterSwitch_RehostsOnDispatch`).
- **A hostile host's countdown** (NaN, infinity, negative, 1e300, far in the past). It's clamped to 0–60 s and never readies a player early by more than the clamp. Test in Task 6 (`AutoStartRemaining_Nonsense_Clamped`).
- **Fill after the lobby ends.** B to leave, or the session ending, sets the lobby back to friends-only. A new Online Play starts friends-only even if Fill was on last time. Test in Task 4 (`Fill_ResetOnLeave_NewLobbyFriendsOnly`).

---

### Task 1: MatchRules (pure rules)

**Files:**
- Create: `Assets/Scripts/Online/MatchRules.cs`
- Test: `Assets/Tests/Editor/MatchRulesTests.cs`

**Interfaces:**
- Produces:
  - The constants and copy above, as `public const`.
  - `public static int ParseOpenSeats(string tag)`: 0–4, or -1 for null, empty, longer than `MAX_TAG`, non-digit, or out of range.
  - `public static bool IsOpen(string mmTag)`: `mmTag == MM_ON`.
  - `public struct Candidate { public ulong Lobby; public ulong Owner; public int OpenSeats; }`
  - `public static List<Candidate> Candidates(IEnumerable<(ulong lobby, ulong owner, string game, string build, string mm, string open)> found, string myBuild, ulong ownLobby, int seatsNeeded)`. It keeps lobbies whose game is `LobbyRules.GAME`, whose build equals `myBuild`, that are open, have a valid seat count ≥ `seatsNeeded`, and aren't `ownLobby` or lobby 0. It looks at no more than `MAX_RESULTS` inputs. The result is ordered fullest first (fewest open seats), ties broken by lobby id ascending.
  - `public static bool MayYieldTo(ulong me, ulong otherOwner)`: true only when `me > otherOwner`. A lone Fill host joins another only if its own Steam ID is higher.
  - `public static double AutoStartAt(double lastSeatedAt)` = `lastSeatedAt + AUTO_START`.
  - `public static float AutoStartRemaining(double at, double now)`: 0 when `at` is 0 or not finite, else clamped to [0, `AUTO_START`].
  - `public static string StatusLine(bool filling, bool searching, float autoStartRemaining)`: searching gives `LOOKING FOR A MATCH…`; filling with remaining > 0 gives `OPEN TO ANYONE · STARTS IN {ceil}`; filling otherwise gives `OPEN TO ANYONE`; not filling gives `FRIENDS ONLY`.
  - `public static bool SearchAllowed(double lastSearchAt, double now)`: true when there was no search yet (`lastSearchAt` is `double.NegativeInfinity`) or `now - lastSearchAt >= SEARCH_GAP`.

- [ ] **Step 1: Write the failing tests** in `MatchRulesTests`:
  - `ParseOpenSeats_Valid`: "0" → 0, "4" → 4.
  - `ParseOpenSeats_Invalid`: null, "", "-1", "5", "99", "4abc", " 3", `new string('9', 10000)` → -1.
  - `Candidates_FullestFirst`: lobbies with open 3, 1, 2 (all valid, need 1) → order 1, 2, 3.
  - `Candidates_GarbageTags_Skipped`: wrong game, wrong build, mm "" or "0", open "-1"/"99"/"x", lobby 0, own lobby → all dropped. No exception.
  - `Candidates_NotEnoughSeats_Skipped`: open 1 with need 2 → dropped.
  - `Candidates_CapsInput`: 200 valid inputs → result count ≤ `MAX_RESULTS`.
  - `MayYieldTo_HigherOnly`: (5, 3) true, (3, 5) false, (4, 4) false.
  - `AutoStartRemaining_Nonsense_Clamped`: at = NaN, +∞, -∞, 0 → 0. at = now + 1e300 → 60. at = now - 10 → 0. at = now + 30 → 30.
  - `StatusLine_Copy`: the four cases above, with exact strings (29.2 s shows `STARTS IN 30`).
  - `SearchAllowed_Gap`: (-∞, 0) true; (10, 14.9) false; (10, 15) true.
- [ ] **Step 2: Run** `bash tools/run-tests.sh MatchRulesTests`. Expected: `NO RESULTS` (doesn't compile).
- [ ] **Step 3: Implement** `MatchRules` in `Assets/Scripts/Online/MatchRules.cs`, then `bash tools/newmeta.sh` for it and the test file. Parse with a digit-only check before `int.TryParse` (no culture, no whitespace).
- [ ] **Step 4: Run** `bash tools/run-tests.sh MatchRulesTests`. Expected: all pass.
- [ ] **Step 5: Commit** (list the files).

---

### Task 2: Lobby service: public lobbies, search, own ID

**Files:**
- Modify: `Assets/Scripts/Online/ILobbyService.cs`, `Assets/Scripts/Online/SteamLobbyService.cs`, `Assets/Tests/Editor/OnlineFakes.cs` (`FakeLobbyService`)
- Test: `Assets/Tests/Editor/SteamLobbyServiceTests.cs`

**Interfaces:**
- Produces (on `ILobbyService`):
  - `ulong Me { get; }`: this machine's Steam ID, or 0 without Steam.
  - `void SetPublic(ulong lobby, bool open)`: public when open, friends-only otherwise.
  - `void Search(string build, int seatsNeeded)`, then `event Action<ulong[]> Found` (at most `MatchRules.MAX_RESULTS` lobbies; empty when none or no Steam).
  - The fake records `PublicCalls` as a `List<(ulong lobby, bool open)>` and `Searches` as a `List<(string build, int seats)>`. Tests answer through `public void Answer(params ulong[] lobbies)`, which raises `Found`. The fake has a settable `Me` and per-lobby data and owner dictionaries, if they're not there already.
- Steam: `SetPublic` calls `SteamMatchmaking.SetLobbyType(id, open ? k_ELobbyTypePublic : k_ELobbyTypeFriendsOnly)`. `Search` adds these filters and then calls `RequestLobbyList`, with a `CallResult<LobbyMatchList_t>` kept like `creates`/`joins`:
  - string filters `game` = `doa`, `build` = build, `mm` = `1`, all `k_ELobbyComparisonEqual`
  - numerical filter `open` ≥ seatsNeeded
  - `AddRequestLobbyListFilterSlotsAvailable(seatsNeeded)`
  - `AddRequestLobbyListResultCountFilter(MAX_RESULTS)`
  - distance `k_ELobbyDistanceFilterDefault`

  The answer reads `min(m_nLobbiesMatching, MAX_RESULTS)` lobbies with `GetLobbyByIndex`. An IO failure answers empty.

- [ ] **Step 1: Write failing tests** in `SteamLobbyServiceTests` (no Steam in the editor):
  - `Search_WithoutSteam_AnswersEmptyAtOnce`: `Found` is raised once, with length 0.
  - `SetPublic_WithoutSteam_DoesNothing`: no throw.
  - `Me_WithoutSteam_IsZero`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh SteamLobbyServiceTests`. Expected: `NO RESULTS`.
- [ ] **Step 3: Implement** the interface members, the Steam side (guarded by `#if !DISABLESTEAMWORKS` and `Available`, like the existing calls) and the fake.
- [ ] **Step 4: Run** `bash tools/run-tests.sh SteamLobbyServiceTests` and `bash tools/run-tests.sh OnlineLobbyTests`. Expected: all pass, and the existing lobby tests still compile and pass with the extended fake.
- [ ] **Step 5: Commit** (list the files).

---

### Task 3: JoinChurn: refuse a Steam ID that keeps reconnecting

**Files:**
- Create: `Assets/Scripts/Online/JoinChurn.cs`
- Modify: `Assets/Scripts/Online/OnlineLobby.cs` (`LetsIn`; make a new `JoinChurn` whenever a hosted lobby is made), `docs/online-safety.md`
- Test: `Assets/Tests/Editor/JoinChurnTests.cs`, `Assets/Tests/Editor/OnlineLobbyTests.cs`

**Interfaces:**
- Produces:
  - `public class JoinChurn { public JoinChurn(Func<double> clock); public bool Allow(ulong steamId); }`. `Allow` records an accepted connection and returns false once a Steam ID has more than `CHURN_LIMIT` within `CHURN_WINDOW`. After that it returns false for that ID for the rest of the object's life (the session), keeping at most 64 IDs' histories so it can't grow without bound. When the cap is reached, the oldest non-refused ID is dropped.
  - The constants go on `JoinChurn`: `CHURN_LIMIT = 5`, `CHURN_WINDOW = 60.0`.
  - `OnlineLobby` constructor gains an optional `Func<double> clock` (default `() => UnityEngine.Time.realtimeSinceStartupAsDouble`; tests pass a fake). `LetsIn(steamId)` returns `IsHost && SteamPeerRules.Accepts(...) && churn.Allow(steamId)`, and calls `Allow` only when the first two hold. The transport calls `LetsIn` once per accepted connection (`ConnectionGate.Decide` stops asking after Accept), so each call is one connection.

- [ ] **Step 1: Write failing tests:**
  - `JoinChurnTests.FiveInAMinute_Allowed`, `SixthInAMinute_Refused`, `Refused_StaysRefused_AfterWindow`, `SpreadOut_Allowed` (6 connections 61 s apart), `ManyIds_Bounded` (1000 IDs, no growth past 64 tracked: assert via an `internal int Tracked` property).
  - `OnlineLobbyTests.LetsIn_RepeatedReconnects_Refused`: a hosted fake lobby, a member Steam ID, `LetsIn` 6 times within 60 fake seconds → the sixth is false.
- [ ] **Step 2: Run** `bash tools/run-tests.sh JoinChurn` → `NO RESULTS`.
- [ ] **Step 3: Implement**, add the metas, and add a row to the threat table in `docs/online-safety.md`: "A stranger connecting and dropping over and over | refused for the session after 5 connections in 60 s | `JoinChurn`, `OnlineLobby.LetsIn`".
- [ ] **Step 4: Run** `bash tools/run-tests.sh JoinChurn` and `bash tools/run-tests.sh OnlineLobbyTests`. Expected: pass.
- [ ] **Step 5: Commit** (list the files).

---

### Task 4: Fill in OnlineLobby

**Files:**
- Modify: `Assets/Scripts/Online/OnlineLobby.cs`, `Assets/Scripts/Online/OnlinePlay.cs` (passes the free-seat count each `Update` while hosting: 4 minus seated seats from the session's seat table; find it where `DispatchBoard` gets its tickets), `docs/online-safety.md`
- Test: `Assets/Tests/Editor/OnlineLobbyTests.cs`

**Interfaces:**
- Produces on `OnlineLobby`:
  - `public bool Filling { get; }`
  - `public void SetFill(bool on)`. Host only: on a client or with no lobby it does nothing. On: `SetPublic(lobby, true)`, `SetData(MM_KEY, MM_ON)`, and `SetData(OPEN_KEY, free)`. Off: `SetPublic(lobby, false)` and `SetData(MM_KEY, "")`.
  - `public void ShowOpenSeats(int free)`: host only. Clamps to 0–4, and calls `SetData(OPEN_KEY, …)` only when the value changed.
  - `public event Action<bool> FillChanged`
  - `LeaveLobby()` sets `Filling = false` (the lobby is gone, so no Steam call is needed). `OnCreated` always starts with `Filling = false`.
  - `Host(bool invite = true, bool fill = false)`: Find's fallback re-host uses `invite: false, fill: true` (Task 5). The default keeps today's behaviour.
- Rule: `SetPublic(…, true)` is called from `SetFill(true)` and nowhere else.

- [ ] **Step 1: Write failing tests** in `OnlineLobbyTests`:
  - `NewLobby_NeverPublic`: host, then no `PublicCalls` with open = true.
  - `Fill_On_PublicAndTagged`, `Fill_Off_FriendsOnlyAndUntagged`.
  - `Fill_OnClient_DoesNothing`.
  - `Fill_ResetOnLeave_NewLobbyFriendsOnly`: fill on, the session goes Offline, host again → `Filling` false and no new public call.
  - `ShowOpenSeats_OnlyOnChange`: 3, 3, 2 → two `SetData` calls for `open`.
  - `Host_FillTrue_NoInvite_PublicWhenMade`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh OnlineLobbyTests`. Expected: the new tests fail.
- [ ] **Step 3: Implement.** In `docs/online-safety.md`, change the in-scope line "no strangers joining" to "strangers join only through a lobby its host opened to anyone (Fill)", change "Lobbies are friends-only" to "Lobbies are friends-only unless the host turns Fill on", and add a row for the lobby-type rule.
- [ ] **Step 4: Run** `bash tools/run-tests.sh OnlineLobbyTests`. Expected: pass.
- [ ] **Step 5: Commit** (list the files).

---

### Task 5: Matchmaker (Find) and the lobby switch

**Files:**
- Create: `Assets/Scripts/Online/Matchmaker.cs`
- Modify:
  - `Assets/Scripts/Online/ISessionControl.cs`: add `void Leave()`.
  - `Assets/Scripts/Online/OnlinePlay.cs`: implement `Leave()` through `Session.Leave()`. Own a `Matchmaker`, call its `Tick()` in `Update`, and expose `public Matchmaker Match { get; }`.
  - `Assets/Scripts/Online/OnlineLobby.cs`
  - `OnlineFakes.cs` (`FakeSessionControl.Leave`)
- Test: `Assets/Tests/Editor/MatchmakerTests.cs`; network test in `Assets/Tests/Editor/OnlinePlayNetworkTests.cs` (port 7819)

**Interfaces:**
- Consumes: `MatchRules.*` (Task 1), `ILobbyService.Search/Found/Me/Owner/GetData` (Task 2), `OnlineLobby.SetFill/Filling/Host(invite, fill)` (Task 4).
- Produces:
  - `OnlineLobby.Switch(ulong lobby)`: allowed only while `IsHost` and the hosted session has no other machine (`Func<int> otherMachines`, passed to the `OnlineLobby` constructor as an optional parameter defaulting to `() => 0`, wired by `OnlinePlay` from the session's connected clients). It enters the lobby while keeping its own. Once `Entered` succeeds and `LobbyRules.Refusal` passes, it:
    1. stores the new lobby as `switchingTo` (so `OnRoleChanged(Offline)` from the old session leaves only the old lobby),
    2. leaves the old lobby,
    3. calls `session.Leave()`,
    4. sets `Current` to the new lobby and `IsHost` to false,
    5. calls `session.Join(owner)`.

    Any failure raises `public event Action<ulong, SwitchFailure> SwitchFailed`, where `public enum SwitchFailure { CouldNotEnter, Refused, NoSeat, JoinFailed }`. `NoSeat` is when the host's `Ended` reason equals the `JoinRules` "no free seat" text. Before step 2, a refused or unentered lobby is left and the old lobby is untouched.
  - `public class Matchmaker`:
    - `public Matchmaker(ILobbyService lobbies, OnlineLobby lobby, string build, Func<double> clock, Func<int> otherMachines)`
    - `public bool Searching { get; }`
    - `public bool Find()`: false (and does nothing) when `!SearchAllowed`, already searching, not hosting, or `otherMachines() > 0`. In that last case it also raises `Notice` with the "before others join" copy.
    - `public void Cancel()`
    - `public void Tick()`: re-searches every `RESEARCH` s while `Filling`, alone and not searching. On such a re-search it switches only to a candidate where `MayYieldTo(lobbies.Me, candidate.Owner)`.
    - `public event Action<string> Notice`
  - Find flow: `Search(build, 1)` → `Candidates(...)` → `Switch` to the first. On `SwitchFailed` it moves to the next: `NoSeat` doesn't count a try, anything else does, up to `MAX_TRIES`. If no candidate is left, `Notice(NONE)`. Then, if the player still hosts the old lobby, `SetFill(true)`; if the old lobby was left (failure after step 2), `lobby.Host(invite: false, fill: true)`.
  - If `otherMachines()` becomes > 0 at any point while `Searching` (a friend joined), it cancels. A `Found` or `Entered` that answers after a cancel is ignored or left.
- **The Dispatch stay:** after the switch, the player must still be on Dispatch (`GameState.PlayerSelect`) as a client of the new host, not on the title screen. Find out how `OnlineGame`/`PlayerInstantiate.GoOffline` reacts to the old session going Offline. If it leaves player select, add a guard `OnlineLobby.Switching` that `OnlineGame` checks so it stays put. Keep this change as small as possible, and say what you found in the task report.

- [ ] **Step 1: Write failing unit tests** in `MatchmakerTests` (fakes, fake clock):
  - `Find_PicksFullest_AndSwitches`
  - `Find_NoneFound_TurnsFillOn_AndTellsNone`
  - `Find_ThreeFailures_StopsAndFills` (four candidates, `CouldNotEnter` ×3 → the fourth is not tried)
  - `Find_NoSeat_DoesNotCountATry`
  - `Find_SomeoneJoinsDuringSearch_StaysHost`
  - `Find_WithOthersInLobby_Refused_WithNotice`
  - `Find_TooSoon_Ignored` (a second Find 2 s later)
  - `Tick_FillingAlone_ResearchesEvery15s`
  - `Tick_YieldsOnlyToLowerId`
  - `Switch_RefusedTags_KeepsOwnLobby`
  - `Switch_JoinFailsAfterLeaving_RehostsFilled`
  - `LateFoundAfterCancel_Ignored`
- [ ] **Step 2: Run** `bash tools/run-tests.sh MatchmakerTests` → `NO RESULTS`.
- [ ] **Step 3: Implement** `Matchmaker`, `Switch`, and `ISessionControl.Leave`, and add the metas.
- [ ] **Step 4: Run** `bash tools/run-tests.sh MatchmakerTests` and `bash tools/run-tests.sh OnlineLobbyTests`. Expected: pass.
- [ ] **Step 5: Write the network tests** in `OnlinePlayNetworkTests` (port 7819, menu scene, fake Steam, direct sessions, as the existing lobby tests do):
  - `Find_SwitchesToAnotherHost_StaysOnDispatch`: machine A hosts alone, and machine B hosts a Fill lobby the fake search returns. A's Find ends with A as B's client, `GameState.PlayerSelect`, A seated on B.
  - `Find_HostGoneAfterSwitch_RehostsOnDispatch`: B stops right after A leaves its own session. A ends up hosting with `Filling` true, still on `PlayerSelect`.
- [ ] **Step 6: Run** `bash tools/run-tests.sh OnlinePlayNetworkTests`. Expected: all pass, the old ones included.
- [ ] **Step 7: Commit** (list the files).

---

### Task 6: Auto-start

**Files:**
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`, `Assets/Scripts/Online/OnlineGame.cs`, `Assets/Scripts/Player/PlayerInstantiate.cs` (CRLF)
- Test: `Assets/Tests/Editor/MatchRulesTests.cs` (done in Task 1), `Assets/Tests/Editor/OnlinePlayersNetworkTests.cs` (port 7820), `Assets/Tests/Editor/NonsenseNetworkTests.cs`

**Interfaces:**
- Consumes: `MatchRules.AutoStartAt/AutoStartRemaining/FORCE_GRACE`, `OnlineLobby.Filling`.
- Produces:
  - `OnlineMatch`: `readonly NetworkVariable<double> autoStartAt = new NetworkVariable<double>(0)`, plus `public double AutoStartAt { get; }` and a host-only `internal void SetAutoStart(double at)`. This changes `OnlineSession.NetcodeSetup`, which is expected: the build tag changes and test pairs are rebuilt together (spec).
  - Host (`OnlineGame`, each frame in `PlayerSelect`): when `Filling` is on and at least two machines are seated, `at = AutoStartAt(lastSeatedAt)`, where `lastSeatedAt` is the server time a new seat was last taken. Otherwise `at = 0`. Publish only on change.
  - Every machine: when `AutoStartRemaining(at, serverTime)` reaches 0 with `at != 0`, ready this machine's own player through the same path as A (the Dispatch ready, which also closes the sliders). Do this once per `at` value.
  - Host: at `at + FORCE_GRACE`, if the start hasn't begun, `PlayerInstantiate.SetRemoteReady(seat, true)` for every seated remote seat, then the normal all-ready check runs.
  - Leaving `PlayerSelect` or the match starting sets `at = 0`.
- [ ] **Step 1: Write failing network tests:**
  - `OnlinePlayersNetworkTests.AutoStart_FillingWithTwo_StartsUnready` (port 7820): use an `internal static double AutoStartSecondsOverride` on `MatchRules` that tests set to 2 and reset in TearDown. Host with Fill on, one client, nobody ready → the load starts on both within 2 + `FORCE_GRACE` + 3 s.
  - `OnlinePlayersNetworkTests.AutoStart_FriendsOnly_NeverStarts`: the same with Fill off; after 8 s, still `PlayerSelect`.
  - `OnlinePlayersNetworkTests.AutoStart_HostAlone_NeverStarts`.
  - `NonsenseNetworkTests.AutoStart_NaNFromHost_Ignored`: the host writes NaN → the client's player stays unready.
- [ ] **Step 2: Run** `bash tools/run-tests.sh OnlinePlayersNetworkTests` and `bash tools/run-tests.sh NonsenseNetworkTests`. Expected: the new tests fail.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run** both again. Expected: all pass.
- [ ] **Step 5: Commit** (list the files).

---

### Task 7: Dispatch buttons, status line, docs

**Files:**
- Modify:
  - `Assets/Scripts/Menu/DispatchMenu.cs` (`DispatchButton` + `LeftShoulder`, `RightShoulder`; `DispatchAction` + `Find`, `Fill`)
  - `Assets/Scripts/Menu/PlayerUIHandler.cs` (wire `UI/Left Shoulder` and `UI/Right Shoulder` through `FindAction`, like `westFace`; null-safe when the actions don't exist yet)
  - `Assets/Scripts/Menu/MenuInteractions.cs` (CRLF)
  - `Assets/Scripts/UI/Dispatch/DispatchBoard.cs` (an optional `[SerializeField] TMP_Text matchmakingLine`, set from `MatchRules.StatusLine` each frame on change, null-safe)
  - `EDITOR-TODO.md`, `docs/controls.md`, `docs/online.md`, `docs/testing.md`
- Test: `Assets/Tests/Editor/DispatchMenuTests.cs`, `Assets/Tests/Editor/MenuButtonsTests.cs`

**Interfaces:**
- Rules (`DispatchMenu.Press`):
  - `LeftShoulder` returns `Find` only when the sliders are closed and the player isn't ready, else `None`.
  - `RightShoulder` returns `Fill` when the sliders are closed, else `None`.
  - B (`East`) while `Matchmaker.Searching` is `Cancel` (add `DispatchAction.CancelFind`, checked before `Unready`/`Leave`). `Press` gains a `bool searching = false` parameter.
- `MenuInteractions`: `Find` calls `OnlinePlay.Instance.Match.Find()`. `Fill` calls `Lobby.SetFill(!Lobby.Filling)`, and only on the host (a client gets no action and no sound). Both play the Dispatch sounds.

- [ ] **Step 1: Write failing tests:**
  - `DispatchMenuTests`: `LeftShoulder_FindsWhenIdle`, `LeftShoulder_NothingWhenReadyOrOpen`, `RightShoulder_FillWhenClosed`, `East_WhileSearching_Cancels`.
  - `MenuButtonsTests.Online_RightShoulder_OnClient_DoesNothing`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh DispatchMenuTests`. Expected: fail.
- [ ] **Step 3: Implement.** Docs:
  - `EDITOR-TODO.md`: add the UI actions `Left Shoulder` (`<Gamepad>/leftShoulder`, keyboard `F`) and `Right Shoulder` (`<Gamepad>/rightShoulder`, keyboard `R`) to `CapstoneYear4.inputactions`, add the `matchmakingLine` text to the Dispatch board and wire it, and add the two prompts.
  - `docs/controls.md`: two rows each in the controller and keyboard tables.
  - `docs/online.md`: a "Matchmaking" section with Fill, Find, auto-start and the constants.
  - `docs/testing.md`: the new tests, and the next free port 7821.

  Check first that no shoulder or `F`/`R` binding already exists in the UI map. If one does, pick free keys and say so.
- [ ] **Step 4: Run** `bash tools/run-tests.sh DispatchMenuTests` and `bash tools/run-tests.sh MenuButtonsTests`. Expected: pass.
- [ ] **Step 5: Commit** (list the files).

---

### Task 8: Hostile-host audit and full run

**Files:**
- Modify: whatever the audit finds, plus `docs/online-safety.md`
- Test: the whole EditMode suite

- [ ] **Step 1: Audit** every value a client takes from a host or a found lobby, assuming a stranger sent it: `OnlineMatch` client RPCs and network variables (`autoStartAt` included), `OnlineSession.AddPlayer/SetMatch/AddScooter`, `OnlinePlayer.displayName` (shown only through `PlayerNames.Shown`), lobby tags (read only through `MatchRules`/`LobbyRules`), and log lines (no raw lobby text). Write each finding with file:line in the task report, and fix the real ones with a test each.
- [ ] **Step 2: Add rows** to `docs/online-safety.md`: lobby search results untrusted (`MatchRules.Candidates`), search rate-limited (`SEARCH_GAP`, `MAX_RESULTS`), and the auto-start value clamped (`AutoStartRemaining`). Add the new test classes to its Tests list.
- [ ] **Step 3: Run** `bash tools/run-tests.sh` (everything). Expected: all pass. If `LocalMatchSmokeTest` or another existing test fails, stop and report the failure.
- [ ] **Step 4: Write the manual Steam check** (short bullets, two PCs with two Steam accounts) at the end of the matchmaking section in `docs/online.md`:
  - A: Online Play, then RB. The line shows `OPEN TO ANYONE`.
  - B: Online Play, then LB. B lands in A's Dispatch.
  - Wait 60 s without readying. The match starts.
  - A presses RB again. B (in a new lobby) can't find A.
- [ ] **Step 5: Commit** (list the files).

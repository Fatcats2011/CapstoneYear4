# Game Modes Framework + Mutators Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a game-mode choice and the new mutators (Speed, Low Gravity, Number of Waves, Round Timer) to MATCH RULES, with the rows shown, hidden or locked per mode, working offline and online.

**Architecture:** `HouseRules` grows a `Mode` and the mutator fields. A pure `ModeRules` table says which rule rows each mode shows, hides or locks. `RulesPanel` builds its rows from that table. The waves mutator lives in `OrderManager` (explicit count instead of the IndexOutOfRange trick). Speed and gravity are applied by a small `MatchPhysics` static on every machine. Online, the host shares the packed rules through one new `OnlineMatch` NetworkVariable so clients know the mode and apply the same physics.

**Tech Stack:** Unity 2022.3.62f3, C#, Netcode for GameObjects 1.15.1, NUnit EditMode tests run by `bash tools/run-tests.sh <filter>`.

**Spec:** `docs/game-modes-ideas.md` (modes, mutators, per-mode table) and `docs/superpowers/specs/2026-10-24-match-rules-design.md` (existing rules).

This is plan 1 of 5. Plans 2–5 add the modes themselves (Gold Rush, Kings Crown, Infection, Hot Potato + elimination + spectator). Until a mode's plan lands, it is not in `GameModes.Playable`, so the mode row only offers modes that work.

## Global Constraints

- Defaults must equal today's game: Mode Standard, Waves 3, Speed 100 %, Low Gravity off, Round Timer 3 min. `HouseRules.Normal` is that.
- A public lobby always plays `HouseRules.Normal` (`ForMatch(true)`), mode included.
- Speed choices: 50, 75, 100, 125, 150, 200 (%). Waves choices: 1–6. Round Timer choices: 2, 3, 5 (minutes). Low Gravity = gravity × 0.5 (`-13` → `-6.5`).
- The per-mode table in `docs/game-modes-ideas.md` ("Mutators per Mode") is the source of truth for shown / hidden / locked.
- When the mode isn't Standard or any mutator differs from default, the panel note reads (English): "Dead on Arrival wasn't designed to be played this way. Expect odd balance and some chaos."
- Never use git-bash `sed -i` on scripts (CRLF). New files may be LF.
- New user-facing text goes through `Loc` keys, added in `tools/strings.py` / `strings_en.py` / `strings_tr.py` the way existing `rules.*` keys are.
- Network test port: 7837 (record it in `docs/testing.md`).

## Review Focus

- Saved `settings.cfg` from before this change (no new keys) must load as today's rules, not zeros (Speed 0 % would freeze everyone). Test in Task 1.
- Changing mode in the panel with a row selected that the new mode hides: the selection must stay on a valid row. Test in Task 2.
- A client joining an online match must use the host's speed/gravity, not its own saved rules. Test in Task 5.
- Gravity must be restored after a Low Gravity match (back to menu, rematch, quit to title), or cans and civilians float in the next Standard match. Test in Task 4.
- Waves = 1 and Waves = 6 must still end in the golden round (or Results with Golden Order off), never throw. Test in Task 3.

---

### Task 1: Rules model — GameMode, mutator fields, per-mode table, saving

**Files:**
- Create: `Assets/Scripts/Management/GameMode.cs`
- Create: `Assets/Scripts/Management/ModeRules.cs`
- Modify: `Assets/Scripts/Management/HouseRules.cs`
- Modify: `Assets/Scripts/Menu/GameSettings.cs` (field, ToString, Parse, copy, clamp — beside `matchLength`/`steals`/`goldenOrder`/`bots`)
- Test: `Assets/Tests/Editor/HouseRulesTests.cs`, Create `Assets/Tests/Editor/ModeRulesTests.cs`, existing GameSettings tests file (find with grep `GameSettings.Parse`)

**Interfaces:**
- Produces:
  - `public enum GameMode { Standard, GoldRush, KingsCrown, Infection, HotPotato }`
  - `public static class GameModes { public static readonly GameMode[] Playable = { GameMode.Standard }; }` (later plans append)
  - `public enum Rule { Speed, LowGravity, Waves, WaveLength, RoundTimer, Steals, GoldenOrder, Bots }`
  - `public enum RuleShow { Shown, Hidden, LockedOn }`
  - `public static RuleShow ModeRules.For(GameMode mode, Rule rule)`
  - `HouseRules` new readonly fields: `GameMode Mode`, `int Waves`, `int SpeedPercent`, `bool LowGravity`, `int RoundMinutes`
  - Constructor: `HouseRules(MatchLength length, bool steals, bool goldenOrder, bool bots = true, GameMode mode = GameMode.Standard, int waves = 3, int speedPercent = 100, bool lowGravity = false, int roundMinutes = 3)`
  - Effective values (what the game reads; they apply the table, so a hidden rule reads as its default and a locked rule reads as on): `bool StealsOn`, `bool GoldenOn`, `int WavesPlayed`, `float SpeedScale` (= SpeedPercent / 100f), `float GravityScale` (0.5f or 1f)
  - `bool IsExperimental` (mode ≠ Standard or any of Waves/Speed/LowGravity differs from default)
  - `int Pack()` / `static HouseRules Unpack(int packed)` (round-trip every field; used by Task 5)
  - `static readonly int[] SpeedChoices = {50,75,100,125,150,200}`, `int[] WaveChoices = {1..6}`, `int[] RoundChoices = {2,3,5}`

- [ ] **Step 1: Write failing tests**
  - `ModeRulesTests`: one `[TestCase]` per cell of the spec table (8 rules × 5 modes; 2v2 Teams is not in this plan). E.g. `(GameMode.KingsCrown, Rule.Waves, RuleShow.Hidden)`, `(GameMode.GoldRush, Rule.Steals, RuleShow.LockedOn)`, `(GameMode.Infection, Rule.Steals, RuleShow.Hidden)`, `(GameMode.Standard, Rule.RoundTimer, RuleShow.Hidden)`.
  - `HouseRulesTests`:
    - `Normal_IsStandardWithDefaultMutators` — Mode Standard, Waves 3, SpeedPercent 100, LowGravity false, RoundMinutes 3, `IsExperimental` false.
    - `Equals_DiffersOnEachNewField` — five cases, one per new field.
    - `PackUnpack_RoundTripsEveryField` — a rules value with every field non-default.
    - `StealsOn_IsTrueWhenTheModeLocksIt` — GoldRush with Steals false → `StealsOn` true; Infection with Steals true → `StealsOn` false.
    - `WavesPlayed_IsThreeWhenTheModeHidesWaves`, `GravityScale_HalfWithLowGravity`.
    - `ForMatch_PublicLobby_IsNormalEvenInAnotherMode`.
    - `Summary_ListsSpeedAndLowGravityAndWaves` — expects the Loc keys' English text joined with " · ".
  - GameSettings tests: `Parse_OldFileWithoutNewKeys_GivesDefaultMutators`; `ToStringThenParse_KeepsNewFields`; `Parse_SpeedOutOfRange_ClampsToNearestChoice`.
- [ ] **Step 2:** `bash tools/run-tests.sh "ModeRulesTests|HouseRulesTests|GameSettings"` → FAIL (types missing).
- [ ] **Step 3:** Implement. `ModeRules.For` is a switch over the table. Update `Equals`/`GetHashCode`/`Summary`/`From`/`Into`. Existing callers of `Steals`/`GoldenOrder` that decide gameplay (`OrderHandler.StealFrom`, `BotDriver` `StealsOn`, `OrderManager`/`PostGameClarity`, `RulesPanel` note) switch to `StealsOn`/`GoldenOn`; grep `HouseRules.Playing.` to find them all.
- [ ] **Step 4:** Re-run the filter → PASS; then the full suite `bash tools/run-tests.sh` → all pass.
- [ ] **Step 5:** Commit "Modes: rules model and per-mode table".

### Task 2: MATCH RULES panel — mode row, per-mode rows, experimental note

**Files:**
- Modify: `Assets/Scripts/Menu/RulesPanel.cs`
- Modify: `tools/strings.py`, `tools/strings_en.py`, `tools/strings_tr.py` (+ regenerate the string tables the way their header says)
- Test: `Assets/Tests/Editor/RulesPanelTests.cs`

**Interfaces:**
- Consumes: Task 1's `ModeRules.For`, `GameModes.Playable`, `HouseRules` fields, choice arrays.
- Produces: rows in this order, each present only if `ModeRules.For(mode, rule) != Hidden`: Mode (only if `GameModes.Playable.Length > 1`), Wave Length, Waves, Round Timer, Steals, Golden Order, Bots, Speed, Low Gravity (as built: the existing rules first, then the mutators). Tests find rows with `RulesPanel.RowOf(Rule)` / `ModeRow`. A `LockedOn` row shows "ON 🔒" (keys `word.on` + `rules.locked`) and `Step` does nothing on it.
- Loc keys: `rules.mode`, `rules.mode.standard`, `rules.mode.goldrush`, `rules.mode.kingscrown`, `rules.mode.infection`, `rules.mode.hotpotato`, `rules.speed`, `rules.lowgravity`, `rules.waves`, `rules.wavelength` (relabels the old `rules.length` row), `rules.roundtimer`, `rules.locked`, `rules.note.experimental`. Value labels: speed "100%", waves "3", round "3 MIN", wave length "45s"/"60s"/"90s".

- [ ] **Step 1: Write failing tests** (`RulesPanelTests`)
  - `Open_Standard_ShowsSpeedLowGravityWavesWaveLengthStealsGoldenBots` (names in order; no Mode row while only Standard is playable; no Round Timer).
  - `Open_GoldRush_StealsRowIsLocked_AndStepDoesNotChangeIt` (set `GameModes.Playable` for the test via an internal setter, restore in TearDown).
  - `ChangingMode_RebuildsRows_AndKeepsTheSelectionOnAValidRow` — select the last row in Standard, switch to KingsCrown, `Row` < row count.
  - `Note_IsExperimental_WhenSpeedIsNot100`, `Note_IsEmpty_ForNormalRules`. Existing public-lobby / no-golden note tests keep passing; the experimental note joins after them on a new line.
  - `Picked_RoundTripsEveryRow` — step each row once, `HouseRules.Current` matches.
- [ ] **Step 2:** Run `bash tools/run-tests.sh RulesPanelTests` → FAIL.
- [ ] **Step 3:** Implement. Build the row list from the table; keep a `List<Rule>` beside the `OptionSetting`s so `Picked` reads by rule, not index. A hidden rule keeps its current value in `Picked` (taken from the rules the panel opened with). Changing Mode calls `BuildRows` again.
- [ ] **Step 4:** Run `RulesPanelTests` and `LocalizationTests` (or whatever grep `Loc.Get` tests are called) → PASS.
- [ ] **Step 5:** Commit "Modes: rules panel rows per mode".

### Task 3: Number of Waves

**Files:**
- Create: `Assets/Scripts/Management/WaveTable.cs`
- Modify: `Assets/Scripts/Management/OrderManager.cs` (`InitGame` ~:277-296, `InitWave` ~:302-330)
- Test: Create `Assets/Tests/Editor/WaveTableTests.cs`; add to `Assets/Tests/Editor/MatchRulesPlayTests.cs`

**Interfaces:**
- Produces: `public static int WaveTable.IndexFor(int wave, int waves, int authored)` — which authored quota row (`maxEasy[i]` etc.) wave `wave` (0-based) of `waves` uses, ramping easy→hard: `waves == 1` → `authored - 1`; otherwise `Mathf.RoundToInt(wave * (authored - 1) / (float)(waves - 1))`.
- `OrderManager`: `totalGameTime = WaveLength * HouseRules.Playing.WavesPlayed`; `InitWave` ends the main game when `wave >= WavesPlayed` (explicit check; delete the try/catch IndexOutOfRange path, keeping what its catch did: `finalOrderActive`, `OnMainGameFinishes`, `PostGameClarity(false)`, `ClockCue.TimeUp`). Quota reads go through `IndexFor`.

- [ ] **Step 1: Write failing tests**
  - `WaveTableTests`: `(0,3,3)→0, (1,3,3)→1, (2,3,3)→2`, `(0,1,3)→2`, `(0,6,3)→0, (5,6,3)→2, (2,6,3)→1`, `(0,2,3)→0, (1,2,3)→2`.
  - `MatchRulesPlayTests`: `OneWave_EndsInTheGoldenRound`, `SixWaves_TotalTimeIsSixWaveLengths` (check `totalGameTime` after `InitGame`), `SixWaves_NoGolden_EndsInResults` (follow `NoGoldenOrder_TheLastWaveEndsInTheResults`).
- [ ] **Step 2:** `bash tools/run-tests.sh "WaveTableTests|MatchRulesPlayTests"` → FAIL.
- [ ] **Step 3:** Implement as above.
- [ ] **Step 4:** Same filter → PASS; then `LocalMatchSmokeTest` → PASS.
- [ ] **Step 5:** Commit "Modes: number of waves".

### Task 4: Speed + Low Gravity

**Files:**
- Create: `Assets/Scripts/Management/MatchPhysics.cs`
- Modify: `Assets/Scripts/Player/BallDriving.cs` (`scaledVelocityMax` ~:478; `FixedUpdate` next to the golden multiplier ~:693)
- Modify: `Assets/Scripts/Management/OrderManager.cs` `InitGame` (call `MatchPhysics.Apply(HouseRules.Playing)` right after `Playing` is set)
- Test: Create `Assets/Tests/Editor/MatchPhysicsTests.cs`

**Interfaces:**
- Produces:
  - `public static class MatchPhysics { public const float BASE_GRAVITY = -13f; public static float SpeedScale { get; } ; public static void Apply(HouseRules rules); public static void Reset(); }`
  - `Apply` sets `SpeedScale = rules.SpeedScale` and `Physics.gravity = (0, BASE_GRAVITY * rules.GravityScale, 0)`.
  - `Reset` sets `SpeedScale = 1` and gravity back to `BASE_GRAVITY`. Called from `GameManager.OnNewMatch` (subscribe in a `[RuntimeInitializeOnLoadMethod]` hook inside `MatchPhysics`) and on `GameState.Menu`.
  - `BallDriving`: `totalForce *= MatchPhysics.SpeedScale` beside the golden multiplier, and `scaledVelocityMax = accelerationPower * MatchPhysics.SpeedScale * CSV_RATIO` (recompute at Begin, not only once). Bots drive through `BallDriving`, so they follow automatically.

- [ ] **Step 1: Write failing tests**
  - `Apply_LowGravity_HalvesGravity` (−6.5), `Apply_Speed150_ScaleIs1_5`, `Reset_RestoresGravityAndSpeed`, `NewMatch_ResetsGravity` (raise the OnNewMatch path: `GameManager.Instance.StartNewMatch()` or invoke the event the way existing tests do), `PublicLobbyRules_ApplyNormalPhysics`.
  - TearDown: `MatchPhysics.Reset()` and HouseRules reset.
- [ ] **Step 2:** `bash tools/run-tests.sh MatchPhysicsTests` → FAIL.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Same filter → PASS; `LocalMatchSmokeTest` and `BotDriverPlayTests` → PASS.
- [ ] **Step 5:** Commit "Modes: speed and low gravity mutators".

### Task 5: Online — share the rules with clients

**Files:**
- Modify: `Assets/Scripts/Online/OnlineMatch.cs` (new `NetworkVariable<int> rules`, server-written, beside `goldenValue`)
- Modify: `Assets/Scripts/Online/OnlineGame.cs` (host writes `HouseRules.Playing.Pack()` when the match enters Begin; client applies on Begin)
- Modify: `Assets/Scripts/Management/HouseRules.cs` comment (no longer "nothing sent")
- Test: Create `Assets/Tests/Editor/OnlineRulesNetworkTests.cs` (port 7837), `docs/testing.md`

**Interfaces:**
- Consumes: `HouseRules.Pack/Unpack`, `MatchPhysics.Apply/Reset`.
- Produces: on a client at Begin, `HouseRules.Playing = HouseRules.Unpack(match.rules.Value)` and `MatchPhysics.Apply(HouseRules.Playing)`. Plans 2–5 read the mode on clients from `HouseRules.Playing.Mode`.

- [ ] **Step 1: Write failing tests** (model on an existing small `*NetworkTests` file, e.g. `OnlineCuesNetworkTests`):
  - `Joining_ClientPlaysTheHostsRules` — host `Current` = Speed 150 + LowGravity; client `Current` = Normal; after Begin the client's `HouseRules.Playing` equals the host's and `Physics.gravity.y == -6.5f`, `MatchPhysics.SpeedScale == 1.5f`.
  - `Joining_PublicLobby_ClientPlaysNormal`.
  - `Leaving_ResetsPhysicsOnTheClient` (back to menu → gravity −13).
- [ ] **Step 2:** `bash tools/run-tests.sh OnlineRulesNetworkTests` → FAIL.
- [ ] **Step 3:** Implement. Write the variable before the Begin state is sent so it arrives first (NetworkVariables are delivered before RPCs in the same tick; set it in the same place `goldenValue` is set or just before `StateClientRpc(Begin)`).
- [ ] **Step 4:** Same filter → PASS; then the full suite → all pass.
- [ ] **Step 5:** Add port 7837 to `docs/testing.md`; commit "Modes: share match rules online".

### Task 6: Wrap-up

- [ ] Update `docs/game-modes-ideas.md`: mark the mutators built, and the Wave Length row's old name.
- [ ] Add a playtest entry to `EDITOR-TODO.md`: try Speed 50/200 and Low Gravity in splitscreen and online (two editors); check cans/civilians; check a drift at 200 % is still steerable.
- [ ] Full suite + `python tools/compile-check.py` → pass. Commit "Modes: docs and playtest notes".

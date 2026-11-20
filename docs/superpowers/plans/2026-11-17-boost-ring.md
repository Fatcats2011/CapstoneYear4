# Boost Ring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a ring on the ground under each scooter that shows the boost charge, explains itself with a button prompt when full, bursts when used, and backs this up with one first-time hint.

**Architecture:** All numbers and decisions live in a pure static `BoostRingRules` (tested in EditMode, like `HintRules`). A `BoostRing` MonoBehaviour on the player draws the ring through a small hand-written URP unlit shader and shows the prompt (world-space `ButtonGlyph`). `PhaseIndicator` feeds it the same ratio it gives the horns. The online match count is saved in `settings.cfg` the way `Hints` saves its mask.

**Tech Stack:** Unity 2022.3.62f3, URP, C#, NUnit EditMode tests (`tools/run-tests.sh`), Input System `PlayerInput`.

**Spec:** `docs/superpowers/specs/2026-11-17-boost-ring-design.md`

## Global Constraints
- Branch `steam-phase1a`. Never check out `main` in the real folder. Commit only when the user asks: the "Commit" steps below are stage-only until then.
- No change to boost rules or timings (3s recharge, 8s carrying a package — prefab value).
- Prompt: local (not `GameAuthority.IsOnline`) shows on every full ring. Online shows on every full ring for the first **3** online matches, then only after the ring has been full for `PROMPT_DELAY` seconds.
- Hint "ring full and unused" fires after **15s** full; it's one-time, saved in the hints mask, and not shown in the tutorial.
- Only the owning player's camera sees their prompt (their `PlayerCameraResizer.playerWorldLayer`, 10–13). Bots get a ring but no prompt.
- Online remote avatars get no ring.
- Added UI stays small. New `.meta` files are made with `tools/newmeta.sh`.
- Every new `strings.csv` row is filled in all 17 languages (columns as in `hint.boost_to_steal`, line 267).
- GDD and TDD `.docx` updated with the docx skill, plus a Revision History line and a minor version bump on each cover.

## Review Focus
1. **Boost used while the ring is mid-burst, or a reset mid-recharge** (`ResetBoost`, `FreezeBall`): the ring must snap to the true ratio, never stick at full or empty. Test: `Fill_FollowsRatio_AfterJumpFromFullToZeroAndBack`.
2. **`passInMax` of 0 before the first boost** (`boostTimerMaxTime` starts at 0, so the ratio is NaN or infinite): the ring must read as full, not vanish. Test: `Fill_TreatsZeroMaxAsFull`.
3. **A settings.cfg with no `onlineMatches=` line, or a garbage value:** the count is 0 and the prompt shows. Test: `Parse_MissingOrBadOnlineMatches_IsZero`.
4. **Leaving an online match early** (back to menu before Results): it must not count as a played match. Test: `OnlineMatch_CountsOnlyOnResults`.
5. **Package picked up while the ring is full:** it stays full (no slowed look on a full ring), and the slowed look appears only while charging. Test: `Slowed_ShowsOnlyWhileCharging`.

---

### Task 1: BoostRingRules

**Files:**
- Create: `Assets/Scripts/Player/BoostRingRules.cs`
- Test: `Assets/Tests/Editor/BoostRingRulesTests.cs` (namespace `DoA.Tests`)

**Interfaces:**
- Produces:
  - `public const int PROMPT_MATCHES = 3; public const float PROMPT_DELAY = 3f; public const float IDLE_HINT_SECONDS = 15f; public const float BURST_SECONDS = 0.4f;`
  - `static float Fill(float elapsed, float max)` returns 0..1, with max ≤ 0 or NaN treated as full (1).
  - `static bool Full(float fill)` returns `fill >= 1f`.
  - `static bool Slowed(bool rechargeSlowed, float fill)` returns `rechargeSlowed && fill < 1f`.
  - `static float Pulse(float timeFull)` returns 0..1, a gentle sine, and 0 when `timeFull <= 0`.
  - `static bool ShowPrompt(bool online, int onlineMatches, bool isBot, float timeFull)`: false for a bot or when not full (`timeFull <= 0`). Local: true. Online: true when `onlineMatches < PROMPT_MATCHES`, else `timeFull >= PROMPT_DELAY`.
  - `static bool OfferIdleHint(float timeFull)` returns `timeFull >= IDLE_HINT_SECONDS`.
  - `static float BurstScale(float age)` grows from 1 to about 1.6 over `BURST_SECONDS`. `static float BurstAlpha(float age)` goes from 1 to 0, and is 0 outside `[0, BURST_SECONDS)`.

- [ ] **Step 1: Write the failing tests**: `Fill_HalfIsHalf` (1.5/3 → 0.5), `Fill_ClampsAboveOne`, `Fill_TreatsZeroMaxAsFull` (0,0 → 1), `Fill_FollowsRatio_AfterJumpFromFullToZeroAndBack` (3/3, 0/3, 1/3 → 1, 0, 0.333), `Slowed_ShowsOnlyWhileCharging` ((true,0.5)→true, (true,1)→false, (false,0.5)→false), `Pulse_ZeroWhenNotFull`, `ShowPrompt_Local_AlwaysWhenFull` ((false, 99, false, 0.01)→true), `ShowPrompt_Online_FirstThreeMatches` ((true,2,false,0.01)→true; (true,3,false,0.01)→false; (true,3,false,3f)→true), `ShowPrompt_NeverForBots`, `ShowPrompt_NeverWhenNotFull`, `OfferIdleHint_At15s` (14.9→false, 15→true), `Burst_FadesOutBy_BURST_SECONDS`.
- [ ] **Step 2: Run** `tools/run-tests.sh BoostRingRulesTests`. Expected: compile failure / FAIL.
- [ ] **Step 3: Implement** `BoostRingRules` as a `public static class` with the constants and methods above. Give it a summary comment in the house style (see `HintRules`).
- [ ] **Step 4: Run** `tools/run-tests.sh BoostRingRulesTests`. Expected: all pass.
- [ ] **Step 5: Stage** both files and their `.meta`s.

### Task 2: Online match count, saved

**Files:**
- Modify: `Assets/Scripts/Menu/GameSettings.cs` (field after `hintsSeen` at line 80, save line ~101, `WithHints` ~108, copy ~159, parse ~275)
- Create: `Assets/Scripts/Player/BoostPrompts.cs`
- Test: `Assets/Tests/Editor/GameSettingsTests.cs`, `Assets/Tests/Editor/BoostPromptsTests.cs`

**Interfaces:**
- Produces:
  - `GameSettings.onlineMatches` (int, default 0, key `onlineMatches`), clamped to ≥ 0 on parse.
  - `static string GameSettings.WithLine(string text, string key, int value)`: the general form. `WithHints` becomes `WithLine(text, "hints", mask)`.
  - `static class BoostPrompts { static int OnlineMatches {get;set;} static event Action<int> Changed; internal static void Reset(); internal static void OnResults(bool online); internal static void Save(int count); }`. `OnResults(true)` increments and raises `Changed`; `OnResults(false)` does nothing.
  - Loading settings sets `BoostPrompts.OnlineMatches` wherever `Hints.Seen` is set from settings (find that line with `grep -rn "Hints.Seen =" Assets/Scripts`).
  - `HintWatcher` (or a sibling watcher made the same way) subscribes `GameManager.Instance.OnSwapResults` and calls `BoostPrompts.OnResults(GameAuthority.IsOnline)`, and subscribes `BoostPrompts.Changed += BoostPrompts.Save`. Save writes the file like `Hints.Save`.

- [ ] **Step 1: Write the failing tests**: `Parse_ReadsOnlineMatches`, `Parse_MissingOrBadOnlineMatches_IsZero` (no line → 0; `onlineMatches=-4` → 0; `onlineMatches=abc` → 0), `WithLine_ReplacesOnlyItsKey` (keeps `hints=` and the others), `Save_WritesOnlineMatches`, `OnlineMatch_CountsOnlyOnResults` (`OnResults(false)` leaves 0 with no event; `OnResults(true)` → 1 and one `Changed`).
- [ ] **Step 2: Run** `tools/run-tests.sh "GameSettingsTests|BoostPromptsTests"`. Expected: FAIL.
- [ ] **Step 3: Implement** the field, parse case, save line, copy, `WithLine` (`WithHints` delegates to it), `BoostPrompts`, and the Results subscription.
- [ ] **Step 4: Run** the same filter, then `tools/run-tests.sh HintRulesTests` (WithHints regression). Expected: all pass.
- [ ] **Step 5: Stage.**

### Task 3: The "ring full, unused" hint

**Files:**
- Modify: `Assets/Scripts/World/HintRules.cs` (enum line 4, `Key`)
- Modify: `Assets/Resources/Localization/strings.csv` (new row after line 270)
- Test: `Assets/Tests/Editor/HintRulesTests.cs`

**Interfaces:**
- Produces: `HintKind.BoostReady` appended last (bit 4, so saved masks stay valid). Key `hint.boost_ready`. English: `RING FULL: BOOST!` (all caps like the others, short enough for the callout). All 16 other languages are filled in, matching the tone and casing of the neighbouring rows.

- [ ] **Step 1: Update tests**: add `[TestCase(HintKind.BoostReady, "hint.boost_ready")]` to `EachHint_HasItsKey`, change "four lines" in the summary to five, and extend the every-language check to include the new key.
- [ ] **Step 2: Run** `tools/run-tests.sh HintRulesTests`. Expected: FAIL.
- [ ] **Step 3: Implement** the enum value, `Key` case, and CSV row.
- [ ] **Step 4: Run** `tools/run-tests.sh "HintRulesTests|HintCalloutTests|Localization"`. Expected: all pass.
- [ ] **Step 5: Stage.**

### Task 4: The ring itself

**Files:**
- Modify: `Assets/Scripts/Player/BallDriving.cs` (`SetBoostModifier` ~1600, `HardCodeBoostModifier` ~1611; properties near 250)
- Create: `Assets/Rendering/Boost Ring/BoostRing.shader`, `Assets/Rendering/Boost Ring/BoostRing.mat`
- Create: `Assets/Scripts/Player/BoostRing.cs`
- Modify: `Assets/Scripts/Player/PhaseIndicator.cs` (`SetHornColor`, line 96)
- Modify: `Assets/Scripts/Online/RemoteAvatar.cs` (next to the `PhaseIndicator` disable, line 47)
- Create: `Assets/Scripts/Editor/BoostRingSetup.cs` (menu item that adds the ring to `Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab`)
- Test: `Assets/Tests/Editor/BoostRingTests.cs`

**Interfaces:**
- Consumes: `BoostRingRules` (Task 1), `BoostPrompts.OnlineMatches` (Task 2), `HintKind.BoostReady` (Task 3), `Hints.Offer(kind, seat)`, `OrderSync.SeatOf(Component)`, `BallDriving.OnBoostStart`, `BallDriving.ScooterNormal`, `BallDriving.Grounded`, `PlayerCameraResizer.playerWorldLayer`, `ButtonGlyph`.
- Produces:
  - `BallDriving.RechargeSlowed` (bool): set in `SetBoostModifier(holding)` to `holding`, and in `HardCodeBoostModifier(mod)` to `mod > boostRechargeTimeSet`.
  - `BoostRing : MonoBehaviour` with `public void Show(float fill, bool slowed)` (called by `PhaseIndicator` every frame, not only on redraw, so `timeFull` keeps counting), `public void Burst()`, `public void Hide()`. Its serialized fields: `ring` (Renderer), `prompt` (GameObject, a world-space Canvas holding an `Image` + `ButtonGlyph` for the boost action), radius/lift/colours as tunables. Shader properties `_Fill`, `_Pulse`, `_Dashed` (0/1), `_ColorLow`, `_ColorFull`, `_BurstScale`, `_BurstAlpha`.
- Shader: URP unlit, transparent, additive-ish. Fill is drawn by angle (atan2 of the quad UV around its centre) against `_Fill`. Dashes come from `frac(angle * N)` when `_Dashed`. Colour comes from `_ColorLow`→`_ColorFull` by fill, brightened by `_Pulse`. The burst is a second outer band scaled by `_BurstScale` and faded by `_BurstAlpha`. Use ZTest LEqual with a small lift (no depth write) so the ground clips it rather than it z-fighting.
- Placement: the ring hangs from a child that copies `ScooterNormal`'s position plus `lift` along its up, and the ground-aligned rotation, every `LateUpdate`. It is not parented under the wheelie/lean pivot. It's hidden while not `Grounded` (airborne), faded rather than popped.
- Colours: `_ColorLow`/`_ColorFull` copy the ends of `PhaseIndicator.hornGlowGraident` and `readyColor`, so the ring matches the horns.
- Prompt: its layer is set to the owning `PlayerCameraResizer.playerWorldLayer` (found from the player root). It's disabled for bots (a `BotDriver` on the player) and visible only when `BoostRingRules.ShowPrompt(GameAuthority.IsOnline, BoostPrompts.OnlineMatches, isBot, timeFull)`.
- Hint: once `BoostRingRules.OfferIdleHint(timeFull)`, call `Hints.Offer(HintKind.BoostReady, OrderSync.SeatOf(this))` once per full spell.
- `Burst()` is subscribed to `OnBoostStart` and resets `timeFull` to 0.
- `RemoteAvatar` calls `Hide()` on every `BoostRing` under it.

- [ ] **Step 1: Write the failing tests** (EditMode, building a GameObject with `BoostRing` and a test material, like the existing component tests): `Show_SetsFillOnMaterial`, `Show_CountsTimeFull_AndResetsOnBurst`, `IdleHint_OfferedOnceAfter15s` (subscribe to `Hints.Offered`; feed `Show(1,false)` with a simulated 16s; expect exactly one offer), `Prompt_HiddenForBot`, `BallDriving_RechargeSlowed_FollowsPackage` (if `BallDriving` can be built in EditMode as other tests do. Otherwise test via `SetBoostModifier` on a prefab instance as in `BotDriverPlayTests`), `RemoteAvatar_HidesRing`. Make `timeFull` advance through an injectable `internal Func<float> DeltaTime` so tests don't need to run frames.
- [ ] **Step 2: Run** `tools/run-tests.sh BoostRingTests`. Expected: FAIL.
- [ ] **Step 3: Implement** `RechargeSlowed`, the shader + material, `BoostRing`, the `PhaseIndicator` call (`ring.Show(ratio clamped via BoostRingRules.Fill, control.RechargeSlowed)` before the `NeedsRedraw` early return), and the `RemoteAvatar` hide.
- [ ] **Step 4: Implement** `BoostRingSetup` (menu `DoA/Setup/Boost Ring`), which adds the ring object, prompt canvas (with `ButtonGlyph` sprites taken from the existing boost glyph in `ControlsPanel`/`ButtonGlyphs`) and the `PhaseIndicator` reference to `PlayerAvatar.prefab`. It must be idempotent. Add `BoostRingSetup_IsIdempotent` to `BoostRingTests`.
- [ ] **Step 5: Run** `tools/run-tests.sh` (full suite). Expected: 0 failures. Run `python tools/compile-check.py` if the suite isn't runnable.
- [ ] **Step 6: Add the user steps to `EDITOR-TODO.md`** as a new section `16o. Boost ring: setup and playtest (15 minutes)`, in short bullets: run the menu item, then check the ring in couch 1–4 players, with a bot, online (host and guest), in the tutorial, on a slope, phasing, and carrying a package. The fresh-player check: one match, no explanation, then ask "what's the ring?".
- [ ] **Step 7: Stage.**

### Task 5: Design documents

**Files:**
- Modify: `docs/design/DoA GDD.docx`: the HUD/feedback section (boost ring, its four states, prompt rules local vs online, the new hint and its 15s), and controls teaching. Revision History + minor version bump on the cover.
- Modify: `docs/design/DoA TDD.docx`: `BoostRingRules`, `BoostRing`, `BoostRing.shader`, `BoostPrompts`, `GameSettings.onlineMatches`/`WithLine`, `HintKind.BoostReady`, `BoostRingSetup`, `BallDriving.RechargeSlowed`, and the new tests. Revision History + minor version bump.

- [ ] **Step 1:** Use the docx skill to read both documents, find the boost/horn sections, and edit in place, keeping the styles and the TOC in step with any new heading. Values come from the code (Task 1 constants), not the spec.
- [ ] **Step 2:** Verify by re-extracting the text and checking the new sections and version lines are present.
- [ ] **Step 3: Stage.**

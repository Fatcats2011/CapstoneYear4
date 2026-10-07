# Phase 4D — Frame Rate Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** the game reaches 120 FPS, or as high as it can, with 1 to 4 players.
- **In code:** everything that can be done now.
- **In the editor:** a measured list of asset and settings changes for the user.
- **Art:** what's left after that.

**Architecture:**
- **Measure first:**
  - `FrameStats`, a plain class, keeps a rolling average and the 1% low.
  - A dev overlay (`PerfOverlay`, F6, editor and development builds) shows FPS, frame time, players and the number of cameras rendering.
  - Every build writes a `Perf:` line every 30 s to the opt-in session log (`-sessionlog`), so the user can measure a release build on any PC.
- **Fewer cameras:** each player has 7 cameras, and the prefab has 6 of them on.
  - `CameraBudget` turns off every camera whose picture isn't on screen in the current state. For example: the menu preview camera and the menu UI overlay while driving, and the minimap camera when no minimap image is shown.
  - The minimap, when shown, renders at 15 Hz, without shadows.
  - A test counts the enabled cameras in a 4-player match.
- **URP at runtime:**
  - `GraphicsQuality` grows from 3 settings to the costly ones URP lets code change: shadow distance, cascades, main shadow resolution, additional-light shadows (off: the scenes' lights are baked), SSAO (per level, and off at 3–4 players on Medium), and render scale (Low only).
  - Like today, the URP asset and renderer features are put back as authored when Play Mode ends.
- **CPU hot paths:**
  - fix the per-frame allocations and work the audit found: a coroutine started every frame while boosting, LINQ and string building in the compass, a new materials array per boost pad per player per frame, exceptions thrown in a trigger, `.name` and `.tag` reads, and HUD writes when nothing changed;
  - set pedestrians' animators to cull off-screen.

**Tech Stack:** Unity 2022.3.62f3 · URP 14.0.12 (`UniversalRenderPipelineAsset`, `ScriptableRendererFeature.SetActive`) · Unity Test Framework 1.1.33.

**Spec:**
- The user, 2026-10-06: "I want FPS to reach 120, Or as high as possible. do what you can as art could be the bottleneck here".
- The audits behind this plan (two read-only Sonnet agents, spot-checked):
  - **Renderers:**
    - The 4 "Player N – Main Renderer" assets are **Deferred**, with depth priming forced, SSAO at full resolution and high samples, outlines, a vignette blit and VFX passes.
    - The URP asset has MSAA 8× (ignored by Deferred), a 4096 shadow map with 4 cascades at 500 m, 4096 additional-light shadows, and HDR.
  - **Cameras:**
    - `Player - Cinemachine.prefab`, which the game spawns, has 6 enabled cameras: Player Camera (a render texture), Minimap, Menu UI (a render texture), Driving UI, Main, and Phase Post Process. Phase itself is off.
    - At 4 players that's up to 24 camera renders a frame.
  - **Lighting:** baked lightmaps; 1 mixed directional light; additional-light shadows unused.
  - **CPU:**
    - `BallDriving.cs:1402` starts a coroutine every frame while boosting.
    - `BoostPadUpdate.cs:43` reads `materials[1]` (a new array) per pad per player per frame.
    - `Compass.cs:92-98` runs `OrderByDescending().ToList()` and `SetSiblingIndex` per player per frame; its icons rebuild distance strings per frame.
    - `CutoutHandler.cs:120-148` throws and catches inside `OnTriggerStay`.
    - `OrderBeacon` reads `other.name`; `BallDriving` reads `collider.tag` per fixed step.
    - `DynamicNumberUI` formats the timer every frame.
    - `PhaseIndicator` writes its sliders every frame.
    - Pedestrian animators always animate.
  - **Player settings:** Mono scripting backend; Graphics Jobs unclear; VSync on at the Ultra quality level.

## Global Constraints

- **Stay on Unity 2022.3 LTS,** URP 14.
- **Never edit a scene, prefab, `.inputactions`, renderer or URP asset, or ProjectSettings file.**
  - Asset changes the editor must make go in `EDITOR-TODO.md`, each with the exact asset, field and value, and how to judge the result.
  - Runtime changes to the URP asset and renderer features are undone when Play Mode ends (`GraphicsQuality.Restore`).
- **The look must not change at the default level (High) with 1–2 players,** except for things a player can't see: cameras whose picture isn't shown, shadows beyond the new distance (Ruling 3), and additional-light shadows from lights that are baked.
- **Local split-screen and online must behave exactly as before.** `LocalMatchSmokeTest` and the online tests pass after every task.
- **Branch:** `steam-phase1a`, after Phase 4C. **No git commits:** the user commits.
- **Meta files, line endings and test rules** are as in earlier plans:
  - every new file under `Assets/` gets a `.meta`;
  - edit `i/crlf` files (`BallDriving.cs`, `PhaseIndicator.cs`, `Order.cs`, `SceneManager.cs`…) with the Edit tool only;
  - one test run at a time.
- **Subagents:** independent CPU fixes (Task 4) may go to Sonnet agents, one file group each, but tests run one at a time.
- **Dev keys:** read through `DevTools.GetKeyDown`; F6 is free. Add it to `docs/dev-hotkeys.md`.

## Rulings (decided while planning)

1. **What "120 FPS" means here:**
   - The frame rate with VSync off, or on a monitor of 120 Hz or more.
   - The game's default stays VSync on, with no cap, so on a 60 Hz monitor it shows 60 and never tears.
   - `docs/controls.md` already explains `vsync=0` and `framecap`.
   - The `Perf:` line reports the real frame rate whatever the cap.
2. **Measurement is the user's, on their PC.** Batch-mode tests can't measure the GPU. The overlay and the log line make each editor change measurable:
   - the steps are a 4-player match with F1 ×3, a minute driving, and reading the `Perf:` line;
   - `EDITOR-TODO.md` lists the changes in the order to try them, each with a before and after check.
3. **Shadows:**
   - Distance is capped at 150 m on High (from 500), 100 m on Medium and 60 m on Low.
   - The main shadow map is 4096 on High at 1–2 players, and 2048 at 3–4 players and on Medium and Low.
   - Cascades: 4 on High, 2 on Medium, 1 on Low.
   - Additional-light shadows are off on every level: the scenes' lights are baked, and the one mixed light is the directional.
   - Cost if wrong: shadows past 150 m fade sooner on High. The split-screen views are small, and the city blocks hide distant ground.
4. **SSAO:**
   - On at High.
   - On Medium it's on at 1–2 players and off at 3–4.
   - Off on Low.
   - Toggled with `ScriptableRendererFeature.SetActive` on every renderer that has one, and put back on quit.
5. **Render scale** is 1 on High and Medium, and 0.8 on Low. Nothing else changes resolution.
6. **Player count** comes from `PlayerInstantiate.Instance.PlayerCount`. `GraphicsQuality.ApplyFor(int players)` re-applies when it changes (join or leave), and at each match load.
7. **Cameras:**
   - A camera is turned off only when its picture isn't shown in the current state. The test checks the count in a 4-player match and the menus still work: `MainMenuTests`, `MenuButtonsTests`, the customization screen, and `LocalMatchSmokeTest` through results.
   - The minimap camera renders at 15 Hz (`Camera.Render()` on a timer, camera component disabled) and never draws shadows.
   - If code finds the minimap's image isn't visible in any state, the minimap camera stays off, and the ledger says so.
8. **Deferred → Forward on the Main renderers** is the biggest single change. It's an asset edit, so it goes first on the editor list, with a visual check:
   - the toon outlines;
   - the phase effect;
   - decals.

   Deferred doesn't do MSAA, so switching makes MSAA cost real. The list therefore pairs it with MSAA 4× (or 2×).
9. **Not in this phase:**
   - IL2CPP, Graphics Jobs, static flags, LODs and nested HUD canvases. These are editor work, listed with their expected gains.
   - Art: shader simplification, mesh LODs, the market model.
   - The physics step (handling feel).

## Review Focus

- **4 players in the golden round on Medium** → SSAO off, 2048 shadows, 2 cascades, with every view still correct. (Task 2 `ApplyFor_FourPlayersOnMedium_TurnsSSAOOff_AndHalvesShadowMaps`; the user's check.)
- **Leaving Play Mode in the editor** → the URP asset and every renderer feature are back as authored, so nothing is saved changed. (Task 2 `Restore_PutsEveryFeatureAndSettingBack`.)
- **A player joining mid-session** (player select) → their cameras follow the same budget as everyone else's. (Task 3 `AFourPlayerMatch_RendersOnlyCamerasThatAreShown`.)
- **The compass with many orders** → the same icons in the same order as before, without per-frame allocations. (Task 4 `Compass_OrdersIconsByDistance_AsBefore`.)
- **Boosting for 10 s** → one field-of-view hold, not hundreds of coroutines; the camera's FOV behaves as before. (Task 4 `Boosting_HoldsTheWideView_WithoutACoroutinePerFrame`.)

---

### Task 1: Measuring

**Files:**
- Create: `Assets/Scripts/Management/FrameStats.cs`, `Assets/Scripts/Management/PerfOverlay.cs`
- Modify: `Assets/Scripts/Management/HotKeys.cs` (F6), `docs/dev-hotkeys.md`
- Test: `Assets/Tests/Editor/FrameStatsTests.cs`

**Interfaces:**
- Produces:
  - `public class FrameStats`, with these members:
    - `public FrameStats(int capacity = 600)`
    - `public void Add(float seconds)`
    - `public float AverageFps { get; }`
    - `public float OnePercentLowFps { get; }`, the FPS of the slowest 1% of frames' average frame time.
    - `public int Count { get; }`
    - `public static string Line(float avg, float low, int players, int cameras)`, which gives `"Perf: {players} players, {avg:0} fps average, {low:0} fps 1% low, {cameras} cameras"` in `InvariantCulture`.
  - `public class PerfOverlay : MonoBehaviour`:
    - made at launch (AfterSceneLoad, `DontDestroyOnLoad`), like `SteamFeatures`;
    - feeds `Time.unscaledDeltaTime`;
    - every 30 s of real time, `Debug.Log`s `FrameStats.Line(...)`, which the session log keeps because it's a `Log`. Add the `"Perf:"` prefix to `SessionLog.Keeps`, with a test in `SessionLogTests`;
    - draws with `OnGUI` while toggled (F6, `DevTools` only), as a small box at the top left;
    - counts the cameras rendering with `Camera.allCamerasCount`.

- [ ] **Step 1: Write the failing tests:**
  - `FrameStatsTests.AverageAndOnePercentLow`: 99 frames of 1/120 s and 1 of 1/30 s give an average ≈ 115.4 and a 1% low ≈ 30.
  - `FrameStatsTests.KeepsOnlyTheLatestFrames`.
  - `FrameStatsTests.Line_InAnyCulture`, which is right under `de-DE`.
  - `SessionLogTests.Keeps_PerfLines`.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run them, and see them pass:** `bash tools/run-tests.sh "FrameStatsTests|SessionLogTests|DevToolsTests"`.

### Task 2: URP settings at runtime

**Files:**
- Modify: `Assets/Scripts/Management/GraphicsQuality.cs`, plus its caller in `OptionsMenu` (unchanged API) and a hook in `PlayerInstantiate` (or `SpawnManager`) for player-count changes.
- Test: `Assets/Tests/Editor/GraphicsQualityTests.cs` (extend it).

**Interfaces:**
- `GraphicsQuality.Preset` gains `int mainShadowResolution`, `bool additionalShadows`, `bool ssao` and `float renderScale`.
- `public static Preset PresetFor(int level, Preset authored, int players)`. The old 2-argument overload stays and calls it with 1 player.
- `public static void ApplyFor(int players)` re-applies the current level for that many players.
- `Read` and `Write` use reflection for the two fields URP doesn't expose, with the field names cached and checked once:
  - `m_MainLightShadowmapResolution`;
  - `m_AdditionalLightShadowsSupported`.

  If a field isn't found, it's skipped with one warning.
- **SSAO:**
  - every `ScriptableRendererData` the asset lists (`m_RendererDataList`, read once by reflection) is checked;
  - each feature whose type name is `ScreenSpaceAmbientOcclusion` gets `SetActive(preset.ssao)`;
  - the authored active state of each is remembered.
- `Restore` puts every field and feature back.

- [ ] **Step 1: Write the failing tests** (EditMode, on a `ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>()` copy, as the existing tests do):
  - `PresetFor_HighWithTwoPlayers_KeepsTheLookUpTo150m`.
  - `ApplyFor_FourPlayersOnMedium_TurnsSSAOOff_AndHalvesShadowMaps`, as pure `PresetFor` checks.
  - `PresetFor_Low_ScalesTo80Percent_AndTurnsOffSSAO`.
  - `PresetFor_NeverRaisesWhatTheAssetHas`: an asset authored with 1024 shadows stays at 1024.
  - `Restore_PutsEveryFeatureAndSettingBack`: on a test asset with a renderer data that has an SSAO feature, apply Low, then restore. The feature is active again, and the fields are as authored.
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Implement.** Call `ApplyFor(PlayerCount)` when a player joins or leaves (`PlayerInstantiate`), and when a match scene loads.
- [ ] **Step 4: Run the tests:** `bash tools/run-tests.sh "GraphicsQualityTests|LocalMatchSmokeTest"`, in the background.

### Task 3: Only the cameras that are shown

**Files:**
- Create: `Assets/Scripts/Player/CameraBudget.cs`
- Modify: `Assets/Scripts/UI/Minimap.cs`, plus wherever each camera's on/off state belongs: `PlayerCameraResizer.cs` and `PlayerInstantiate.cs` for the state switches.
- Test: `Assets/Tests/Editor/LocalMatchSmokeTest.cs` (one new test), and `Assets/Tests/Editor/CameraBudgetTests.cs`.

**Interfaces:**
- `public static class CameraBudget`:
  - `public static bool Shown(CameraRole role, GameState state, bool minimapVisible, bool phasing)`
  - `public enum CameraRole { Main, DrivingUI, MenuUI, PlayerPreview, Minimap, Phase, PhasePostProcess }`
- The rule table is the implementer's first finding. In the menu scene and a match, record which camera's output each state shows (target texture, then the `RawImage` or material that displays it), then write the table in `CameraBudget` and in the ledger.
  - Expected, to be confirmed: Main, DrivingUI and PhasePostProcess while driving; MenuUI and PlayerPreview in the menus and player select; Phase while phasing; Minimap only when its image is active.
  - Each player's cameras are switched where the state changes, by the scripts that own them today.
- **`Minimap.cs`:**
  - its camera component stays disabled;
  - a 15 Hz timer calls `minimapCamera.Render()` while the minimap image is active;
  - `GetUniversalAdditionalCameraData().renderShadows = false` in `Awake`.

- [ ] **Step 1: Write the failing tests:**
  - `CameraBudgetTests`: one test per state group (menus, driving, phasing, results).
  - `LocalMatchSmokeTest.AFourPlayerMatch_RendersOnlyCamerasThatAreShown`: during MainLoop with 4 players, count `Camera.allCameras`. It's at most 4 × 3 (Main, DrivingUI, PhasePostProcess), plus any cameras the match scene itself has; record the scene's own count first and add it. No camera has a target texture that nothing displays.
- [ ] **Step 2: Run them, and see them fail.** The count is about 4 × 6 today.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the tests:** `bash tools/run-tests.sh "CameraBudgetTests|LocalMatchSmokeTest|MainMenuTests|MenuButtonsTests|PlayerCameraResizerTests|OnlineMatchNetworkTests"`, in the background.

### Task 4: CPU hot paths

Each item keeps its behaviour, so the tests prove the same result with less work. These files are independent, so a Sonnet agent may take a group, one at a time for tests.

**Files and fixes:**

1. **`BallDriving.cs` (`i/crlf`) and `OrbitalCamera.cs`: the boost field of view.**
   - Replace the per-frame `StartCoroutine(SetFOVAfterTime(maxFOV, 0.3f))` with `OrbitalCamera.HoldFOV(float fov, float seconds)`. It sets `holdUntil = Time.time + seconds`, and `OrbitalCamera.Update` honours it. Keep `SetFOVAfterTime` only if something else calls it.
   - Cache `_SpeedLinesRemap` as an ID in `HashReference`, and set it only when the value changes.
   - Replace `collider.tag` reads in `GroundCheck` with `CompareTag`.
   - Write the drag and friction values only when they change.
2. **`BoostPadUpdate.cs`:**
   - in `Start`, make each renderer's second material once (`var mats = r.materials; mats[1] = new Material(reference); r.materials = mats;`) and keep it;
   - set `_RotateAxis` by ID, only when the angle changed by more than 0.5°.
3. **`Compass.cs` and `CompassIconUI.cs`:**
   - an in-place insertion sort by distance instead of LINQ;
   - `SetSiblingIndex` only when an icon's index changes;
   - the distance text only when the whole number changes, using `TMP_Text.SetText("{0}ft", distance)`.
4. **`CutoutHandler.cs`:** in `OnTriggerStay`, return early when it has stolen already, or when the collider isn't a player's ball. Check the ball by layer or a cached component, with no `try/catch` around per-step code.
5. **`OrderBeacon.cs`:** `IsPlayersBall` uses a cached component lookup or `CompareTag`, never `.name`.
6. **`DynamicNumberUI.cs`:** rebuild the timer text only when the whole second changes, and set `SetActive` only when the state changes.
7. **`PhaseIndicator.cs` (`i/crlf`):** write the sliders and horn colour only when the ratio changes.
8. **`OnlineDriving.cs`:** cache each seat's `BallDriving`, cleared when the roster changes.
9. **`CivilianAgent.cs`:**
   - in `Awake`, `animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms`;
   - one cached `WaitForSeconds` for `SlowUpdate`.

**Tests** (new or extended):
- `Assets/Tests/Editor/CompassTests.cs`: `Compass_OrdersIconsByDistance_AsBefore`. Feed distances [30, 5, 18]; the sibling order matches the old LINQ order. Compare against an `OrderByDescending` in the test. A second call with unchanged distances calls `SetSiblingIndex` zero times, checked with a counting test double or the icons' unchanged transforms.
- `Assets/Tests/Editor/OrbitalCameraTests.cs`: `Boosting_HoldsTheWideView_WithoutACoroutinePerFrame`. Play Mode, empty scene: `HoldFOV` is called every frame for 1 s, the camera reaches max FOV, and it returns after the hold. Check that no coroutine is running on the `OrbitalCamera`: its `SetFOVAfterTime` isn't used, and the hold time is set.
- `DynamicNumberUITests.TimerText_ChangesOnlyEachSecond`: the text object is unchanged when the seconds are.
- Existing: `LocalMatchSmokeTest` drives, boosts and delivers; the steal and cutout tests; and `OnlineDrivingNetworkTests`.

- [ ] **Step 1: Write the failing tests above.**
- [ ] **Step 2: Run them, and see them fail.**
- [ ] **Step 3: Make the 9 fixes,** one file group at a time.
- [ ] **Step 4: Run the tests:** `bash tools/run-tests.sh "CompassTests|OrbitalCameraTests|DynamicNumberUITests|LocalMatchSmokeTest|OnlineDrivingNetworkTests|OnlineStealsNetworkTests|OnlineTutorialNetworkTests"`, in the background.

### Task 5: The editor list, docs, full suite

- [ ] **Step 1: Write `docs/performance.md`:**
  - how to measure: the F6 overlay, `-sessionlog` and the `Perf:` line, and a 4-player match with F1 ×3;
  - what the code does now (Tasks 2–4);
  - the editor list, in order, with the asset, field, value, expected gain and what to check visually:
    1. **The `Player 1`–`4 - Main Renderer` assets:**
       - Rendering Path: Deferred → Forward;
       - Depth Priming: Disabled;
       - URP Asset MSAA 8× → 4×.

       Check: the outlines, the phase effect and decals.
    2. **The SSAO feature on each Main and Phase renderer:** Downsample on, Samples Low, Blur Medium. Remove it from the Phase renderers.
    3. **The URP asset:**
       - Shadow Distance 150;
       - Cascades 2;
       - Main Light shadow resolution 2048;
       - Additional Lights' Cast Shadows off;
       - HDR off if the bloom still looks right.
    4. **Main Volume:** keep one bloom (Bloom or BenDayBloom), and turn off the other.
    5. **Player settings:** Scripting Backend IL2CPP (it needs the Windows IL2CPP module and Visual Studio C++ tools); Graphics Jobs on (test both).
    6. **`Civilian` and `PlayerAvatar` prefabs:** Animator Culling Mode set to Cull Update Transforms. The code does this at runtime already for pedestrians.
    7. **The `Driving Canvas` prefab:**
       - the compass on its own nested Canvas;
       - Raycast Target off on non-interactive images and text.
    8. **Static scenery:** mark it static, then rebake occlusion per scene.
  - art: LODs for heavy props, the toon shader's options, and `Market01.fbx`.
- [ ] **Step 2: Update `EDITOR-TODO.md`.**
  - New sections:
    - import Phase 4D;
    - measure: a 4-player match, one minute, then the `Perf:` line, at High and at Medium, noting both;
    - the editor list, measuring after each change.
  - Point to `docs/performance.md`.
- [ ] **Step 3: Update `docs/testing.md`, `docs/dev-hotkeys.md` (F6) and the handoff.**
- [ ] **Step 4: Run the full suite in the background,** with `bash tools/run-tests.sh`. Expected: all pass, then check the `.meta` files.

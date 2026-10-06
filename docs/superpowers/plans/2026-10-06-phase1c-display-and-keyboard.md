# Phase 1C — Display Settings and Keyboard Play Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:**
- **Display settings:** resolution, fullscreen mode, VSync and a frame cap are saved in `settings.cfg` and applied at launch.
- **Keyboard play:** one player can play with the keyboard.
- **Art comes later:** the Options rows for the new settings, and keyboard prompt sprites.

**Architecture:**
- **Display:**
  - `GameSettings` gains five keys: `exclusive`, `width`, `height`, `vsync` and `framecap`.
  - `DisplayRules` is a plain class. It turns them into a `FullScreenMode`, a resolution that this screen has, a `vSyncCount` and a `targetFrameRate`.
  - `DisplaySettings.Apply` hands those to Unity, where `OptionsMenu.UpdateFullscreen` used to set `Screen.fullScreen` alone.
  - `OptionsMenu` keeps the loaded `GameSettings`, so saving the volumes keeps the display keys it has no rows for.
- **Keyboard:**
  - `KeyboardControls.Install` runs before the first scene loads. It changes `Assets/Resources/CapstoneYear4.inputactions` in memory, never on disk:
    - it removes the unused, binding-less `Keyboard&Mouse` scheme, which requires a mouse too;
    - it adds a `Keyboard` scheme that needs only a keyboard;
    - it adds keyboard bindings, in the `Keyboard` group, to every action.
  - Installing twice changes nothing. In the editor, `Uninstall` puts the asset back when Play Mode ends, as `GraphicsQuality.Restore` does for the URP asset.
  - `PlayerInstantiate.AddPlayerReference` accepts the `Keyboard` scheme, but only when Space or Enter is down. Any other key, such as an F-key dev hotkey or a letter, shows a hint that says how to join with the keyboard.
  - The online seat move re-joins with the player's own scheme and device, not with "Gamepad".
  - Rumble already skips a missing gamepad.

**Tech Stack:** Unity 2022.3.62f3 · Input System 1.14.0 (`InputActionSetupExtensions`: `AddControlScheme`, `RemoveControlScheme`, `AddBinding`, `AddCompositeBinding`) · Unity Test Framework 1.1.33.

**Spec:**
- The roadmap (`docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`):
  - Task 1.3: "Recommended: keyboard as one player (add a *Keyboard&Mouse* scheme to `Assets/Resources/CapstoneYear4.inputactions`, allow it in `AddPlayerReference`)."
  - Task 1.4: "Options menu: add resolution, window mode, VSync, frame cap and quality preset … Resolution / window mode / VSync / frame cap not started: builds run borderless at desktop resolution with VSync on."
- The handoff's section 3.
- The user, 2026-10-06, chose "Display + keyboard":
  - "Resolution, window mode, VSync and frame cap: saved to settings.cfg, applied at launch, with Steam Deck defaults. Their Options rows come later with art, so until then they change by editing settings.cfg."
  - "Also keyboard as one player, with its keys added from code (no asset edit). Prompts keep showing controller buttons until keyboard art exists."

## Global Constraints

- **Stay on Unity 2022.3 LTS,** with Input System 1.14.0.
- **Never edit a scene, prefab, `.inputactions` asset or ProjectSettings file.** The keyboard scheme is added in memory at runtime, so the asset on disk is never touched.
- **Local split-screen with controllers must behave exactly as before.** `LocalMatchSmokeTest` passes after every task that changes game code.
- **Default display behaviour stays as today:** borderless at the desktop resolution, with VSync on and no frame cap. That's for both a missing `settings.cfg` and one from before this phase.
- **Branch:** `steam-phase1a`, on top of the uncommitted Phase 4A. **No git commits:** the user commits.
- **Meta files:** run `bash tools/newmeta.sh <path>` for every new file under `Assets/`.
- **Line endings:** these files are `i/crlf`: `PlayerInstantiate.cs` (check it with `git ls-files --eol`), `MainMenu.cs` and `MenuInteractions.cs`. Edit them with the Edit tool only.
- **Tests:** `bash tools/run-tests.sh [filter]`. Run long runs in the background. Play Mode rules as in earlier plans:
  - recorder classes, never a lambda that captures a local;
  - wait by real time;
  - `EnterPlayMode` yielded by the test method itself.
- **Dev hotkeys:** read keys only through `DevTools.GetKeyDown` (`DevToolsTests`). The keyboard player's keys go through the Input System actions, not `DevTools`.

## Rulings (decided while planning)

1. **The new display keys:**

   | Key | Values | Default | Notes |
   |---|---|---|---|
   | `exclusive` | 0 or 1 | 0 | With the existing `fullscreen` row: 0 gives borderless fullscreen, 1 gives exclusive fullscreen. Windowed ignores it. |
   | `width` | 0, or 640–16384 | 0 | 0 means the desktop's size. Anything else is clamped. |
   | `height` | 0, or 360–16384 | 0 | 0 means the desktop's size. Anything else is clamped. |
   | `vsync` | 0 or 1 | 1 | |
   | `framecap` | 0, or 30–500 | 0 | 0 means no cap. 1–29 becomes 30, and above 500 becomes 500. |

   - There's no separate "window mode" key: the existing `fullscreen` row stays the switch between windowed and fullscreen, and `exclusive` picks which fullscreen.
   - Cost if wrong: one more key later.
2. **Resolution:**
   - **Fullscreen:** a saved size this screen doesn't list (`Screen.resolutions`) falls back to the desktop's size, so a game moved to another monitor never opens at a size the screen can't show.
   - **Windowed:** the saved size is used, shrunk to fit inside the desktop.
   - **Width or height 0:** the desktop's size, which in windowed mode means 1280×720, or the desktop's size if that's smaller.
3. **Frame cap and VSync:**
   - VSync on means `vSyncCount = 1`; off means 0.
   - `targetFrameRate` is the cap, or −1 for none. Unity ignores it while VSync is on, so both are set and Unity decides.
4. **Steam Deck defaults are the same as a PC's.** Its desktop is 1280×800, so "desktop size" covers it, and VSync stays on.
   - The roadmap's ≥ 30 fps target needs no cap.
   - Cost if wrong: a Deck player edits `framecap`.
5. **Where the settings apply:** at launch, through `OptionsMenu.LoadOptions` in the menu scene, as fullscreen applies today, and again whenever the fullscreen row changes.
6. **Keyboard keys** (scheme `Keyboard`, the same keys in every map they appear in):

   | Action (map) | Keys | Pad equivalent |
   |---|---|---|
   | Steer Value (Player) | A/D and ←/→ (1D axis) | left stick X |
   | Accelerate Value | W and ↑ | right trigger |
   | Brake Value | S and ↓ | left trigger |
   | Boost Button | Space | A |
   | Drift Button | Left Shift | X |
   | Camera Rotation X Value | Q / E (1D axis) | right stick X |
   | Camera Rotation Y Value | Z / C (1D axis) | right stick Y |
   | Camera Swap Button | Tab | right stick press |
   | Pause Button | Escape | Start |
   | ReadyUp Button (UI) | Space, Enter | A |
   | UnReadyUp Button | Backspace | B |
   | Left/Right/Up/Down D-Pad Button | A/D/W/S and arrows | D-pad, left stick |
   | UnPause Button | Escape | Start |
   | North Face Button | Tab | Y |
   | Confirm Button (Load) | Space, Enter | A |

   - Only one map is enabled at a time (`PlayerInstantiate` switches them), so a key may serve one action per map, as the pad's buttons do.
   - In the editor and development builds, some keys also work as dev hotkeys (S skips the tutorial, B refills boost, and so on). Release builds have no dev hotkeys. `docs/controls.md` says so, and suggests the arrows in the editor.
   - The mouse isn't used.
   - Cost if wrong: rebinding is a list in code.
7. **A keyboard joins only on Space or Enter.**
   - Unity joins on any button of an unused device, so without this rule F1 (the dev key that adds a test pad) would also join the keyboard as a player.
   - Any other key keeps today's behaviour: the player object is destroyed, and outside a match a hint shows. The hint now says `"Press Space to play with the keyboard, or connect a controller"` (`ControllerPrompts.KEYBOARD_JOIN_HINT`).
   - A device with no scheme still gets `CONNECT_CONTROLLER_HINT`.
   - If Unity hasn't updated the key's state by the time the join fires, the implementer checks the key's state another way. Either way, Space joins and F1 doesn't (Task 4's tests).
8. **A keyboard never replaces a lost controller.** The replacement listener stays gamepad-only: a keyboard can't be "lost", and handing a pad player's seat to the keyboard mid-match would surprise them. Cost if wrong: the player plugs the pad back in.
9. **Prompts keep showing controller buttons** until keyboard sprites exist (the user's choice). `docs/controls.md` lists the keys instead.
10. **The editor's in-memory asset:**
    - `Install` changes the imported asset object, so in the editor it is undone when Play Mode ends (`Application.quitting`).
    - `Install` is safe to run twice (it checks for the `Keyboard` scheme), so a skipped `Uninstall` can't double the bindings.
    - Nothing calls `EditorUtility.SetDirty`, so the change is never saved to disk.

## Review Focus

- **A `settings.cfg` from before this phase,** with no display keys → borderless, desktop size, VSync on, no cap: exactly today's behaviour. (Task 1 `SettingsFromBeforeDisplayKeys_KeepTheDisplayDefaults`.)
- **A saved fullscreen size the current monitor doesn't have** (the game moved to a smaller screen) → the desktop size, never an unsupported mode. (Task 2 `Fullscreen_AnUnlistedSize_FallsBackToTheDesktop`.)
- **Saving from the Options screen** → the display keys it has no rows for survive. (Task 2 `WithMenuPositions_KeepsTheDisplayKeys`.)
- **F1 in player select in the editor** → adds a test pad, but the keyboard doesn't join. (Task 4 `AKeyboard_DoesntJoinOnOtherKeys`.)
- **A keyboard player online, moved to the seat the host gave** → it's still the keyboard player in the new seat. (Task 4 `ThisMachinesKeyboardPlayer_MovesToTheSeatTheHostGave`.)

## Facts (read 2026-10-06)

- **`OptionsMenu`** (menu scene):
  - `LoadOptions` (from `Start`) reads `settings.cfg` from `persistentDataPath`, then applies the volumes, `UpdateFullscreen(position)` (`Screen.fullScreen` only) and `GraphicsQuality.Apply`.
  - `SaveOptions` builds a new `GameSettings` from its four row positions and writes it.
- **`GameSettings`** holds the fields `bgmPosition`, `sfxPosition`, `fullscreenPosition` and `qualityLevel`.
  - `ToText` writes `key=value` lines.
  - `Parse(text, defaults)` reads `int` values, clamps them and skips anything it can't read. Tests are in `GameSettingsTests`.
- **The input actions asset:**
  - Maps: Player (Steer Value, Camera Rotation X Value, Camera Rotation Y Value, Camera Swap Button, Boost Button, Accelerate Value, Brake Value, Drift Button, Pause Button), UI (ReadyUp Button, UnReadyUp Button, Left/Right/Up/Down D-Pad Button, UnPause Button, North Face Button) and Load (Confirm Button).
  - Every binding is a `<Gamepad>` path. The `Keyboard&Mouse` scheme exists with no bindings, and requires both a keyboard and a mouse.
- **The player prefab's `PlayerInput`** invokes Unity Events, has default scheme `Gamepad`, and never auto-switches schemes. The scene's `PlayerInputManager` joins when a button is pressed (`m_JoinBehavior: 0`).
- **`PlayerInstantiate.AddPlayerReference`:**
  - It destroys any player whose `currentControlScheme != "Gamepad"`. Outside a match it first calls `ShowUnsupportedDeviceHint`.
  - It stores `playerGamepads[i] = GetDevice<Gamepad>()` (null for a keyboard), and `Rumbler` checks for a null pad.
  - `MoveToOnlineSeat` re-joins with `JoinPlayer(-1, -1, "Gamepad", pad)`.
- **`ControllerPrompts.HintForDevice`:** a keyboard, a mouse or no device gives `CONNECT_CONTROLLER_HINT`; anything else gives `UNSUPPORTED_CONTROLLER_HINT`.
- **Dev hotkeys** (editor and development builds only): F1–F4, 1, 2, 4, 5, Y, P, S, L, B, T and R.

---

### Task 1: The display keys in settings.cfg

**Files:**
- Modify: `Assets/Scripts/Menu/GameSettings.cs`
- Test: `Assets/Tests/Editor/GameSettingsTests.cs`

**Interfaces:**
- Produces, on `GameSettings`:
  - new public fields, with the defaults from Ruling 1 set by field initializers: `int exclusiveFullscreen`, `int width`, `int height`, `int vsync`, `int frameCap`;
  - `public GameSettings WithMenuPositions(int bgm, int sfx, int fullscreen, int quality)`, which returns a copy with those four replaced and every display key kept.
- `Parse` reads the new keys with Ruling 1's clamps, and copies them from `defaults` when they're missing. `ToText` writes them after `quality`.

- [ ] **Step 1: Write the failing tests** (in `GameSettingsTests`):
  - `SavedDisplayKeys_LoadBack`: a settings object with `exclusive 1`, `1600×900`, `vsync 0` and `framecap 144` survives `ToText`, then `Parse`.
  - `SettingsFromBeforeDisplayKeys_KeepTheDisplayDefaults`: `"bgm=3\nsfx=9\nfullscreen=1\nquality=2\n"` gives exclusive 0, width 0, height 0, vsync 1 and framecap 0.
  - `DisplayKeys_AreClamped`:
    - `width=100` gives 640 and `height=100` gives 360;
    - `width=99999` gives 16384;
    - `vsync=5` gives 1 and `exclusive=-1` gives 0;
    - `framecap=10` gives 30, `framecap=9999` gives 500, and `framecap=-4` gives 0.
  - `ZeroSize_MeansTheDesktop_AndStaysZero`: `width=0` and `height=0` load as 0.
  - `WithMenuPositions_KeepsTheDisplayKeys`: a loaded `framecap 60`, `1600×900` stays after `WithMenuPositions(1, 2, 0, 1)`, and the four positions are the new ones.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`, CS1061/CS0117).

- [ ] **Step 3: Implement.** A 0 width or height stays 0; any other value is clamped into its range.

- [ ] **Step 4: Run them, and see them pass.**

  Run: `bash tools/run-tests.sh GameSettingsTests`. Expected: every test in the class passes (the old ones too).

### Task 2: Applying the display settings

**Files:**
- Create: `Assets/Scripts/Menu/DisplayRules.cs`, `Assets/Scripts/Menu/DisplaySettings.cs`
- Modify: `Assets/Scripts/Menu/OptionsMenu.cs`: `LoadOptions`, `SaveOptions` and `UpdateFullscreen`. Its line endings: check with `git ls-files --eol`.
- Test: `Assets/Tests/Editor/DisplayRulesTests.cs`

**Interfaces:**
- Consumes: Task 1's `GameSettings` fields and `WithMenuPositions`.
- Produces:
  - `public struct ScreenSize { public int Width; public int Height; public ScreenSize(int w, int h); }`, with value equality.
  - `public static class DisplayRules`, with these members:
    - `public static readonly ScreenSize WINDOWED_DEFAULT = new ScreenSize(1280, 720);`
    - `public static FullScreenMode ModeFor(int fullscreenPosition, int exclusive)`: 0 gives `Windowed`; 1 gives `FullScreenWindow`, or `ExclusiveFullScreen` when exclusive is 1.
    - `public static ScreenSize SizeFor(FullScreenMode mode, int width, int height, ScreenSize desktop, IReadOnlyList<ScreenSize> listed)` (Ruling 2).
    - `public static int VSyncCount(int vsync)`
    - `public static int TargetFrameRate(int frameCap)`, which gives −1 for 0.
  - `public static class DisplaySettings { public static void Apply(GameSettings settings); }`.
    - It reads `Screen.currentResolution`, which is the desktop, and `Screen.resolutions`, which it dedupes into `ScreenSize`s.
    - It then calls `Screen.SetResolution(w, h, mode)`, and sets `QualitySettings.vSyncCount` and `Application.targetFrameRate`.
- **`OptionsMenu` changes:**
  - A `GameSettings current` field is set by `LoadOptions`.
  - `SaveOptions` writes `current = current.WithMenuPositions(bgmPosition, sfxPosition, fullscreenPosition, qualityPosition)`.
  - `UpdateFullscreen(int)` sets `current.fullscreenPosition`, then calls `DisplaySettings.Apply(current)`. A null `current` falls back to a new default `GameSettings`.

- [ ] **Step 1: Write the failing tests** (`DisplayRulesTests`, EditMode):
  - `ModeFor_TheFullscreenRow_AndExclusive`: `(0, 1)` gives `Windowed`, `(1, 0)` gives `FullScreenWindow`, and `(1, 1)` gives `ExclusiveFullScreen`.
  - `Fullscreen_ZeroSize_IsTheDesktop`: with a 2560×1440 desktop, `(FullScreenWindow, 0, 0)` gives 2560×1440.
  - `Fullscreen_AListedSize_IsKept`: `(ExclusiveFullScreen, 1920, 1080)` gives 1920×1080 when it's listed.
  - `Fullscreen_AnUnlistedSize_FallsBackToTheDesktop`: 3840×2160 on a 1920×1080 desktop that lists only up to 1920×1080 gives 1920×1080.
  - `Windowed_ZeroSize_Is1280x720_OrTheDesktopIfSmaller`: 1280×720 on a 1920×1080 desktop, 1280×720 on a 1280×800 desktop (a Steam Deck), and 1024×600 on a 1024×600 desktop.
  - `Windowed_ABigSize_ShrinksIntoTheDesktop`: `(Windowed, 2560, 1440)` on a 1920×1080 desktop gives 1920×1080.
  - `VSyncAndFrameCap`: `VSyncCount(1)` is 1 and `VSyncCount(0)` is 0; `TargetFrameRate(0)` is −1 and `TargetFrameRate(144)` is 144.
  - `WithMenuPositions_KeepsTheDisplayKeys` lives in Task 1, not here.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement `DisplayRules`, `DisplaySettings` and the `OptionsMenu` changes.**

- [ ] **Step 4: Run the tests.**
  - Run `bash tools/run-tests.sh "DisplayRulesTests|GameSettingsTests|MainMenuTests|MenuButtonsTests"`: expect all to pass.
  - Run `LocalMatchSmokeTest` in the background: expect a pass.

### Task 3: Keyboard controls

**Files:**
- Create: `Assets/Scripts/Player/KeyboardControls.cs`
- Test: `Assets/Tests/Editor/KeyboardControlsTests.cs`

**Interfaces:**
- Produces: `public static class KeyboardControls`, with these members:
  - `public const string SCHEME = "Keyboard";`
  - `public const string OLD_SCHEME = "Keyboard&Mouse";`
  - `public const string ASSET = "CapstoneYear4";`, the Resources name.
  - `public static bool Install(InputActionAsset asset)`. It returns false when the asset already has `SCHEME`. Otherwise:
    - it removes `OLD_SCHEME` if it's there;
    - it adds `SCHEME` with `<Keyboard>` required;
    - it adds Ruling 6's bindings in group `SCHEME`;
    - it returns true.
  - `public static void Uninstall(InputActionAsset asset)`. It removes every binding in group `SCHEME`, composite parts included, removes `SCHEME`, and re-adds `OLD_SCHEME` with `<Keyboard>` and `<Mouse>` required, if it was removed.
  - A `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` that runs `Install(Resources.Load<InputActionAsset>(ASSET))`. In the editor, when it installed, it also subscribes `Uninstall` to `Application.quitting`, once.
- Bindings:
  - A 1D axis is `AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a", SCHEME).With("Positive", "<Keyboard>/d", SCHEME)`, one composite per key pair.
  - A button or trigger is `AddBinding("<Keyboard>/w", groups: SCHEME)`.
  - Find actions with `asset.FindAction("Map/Action Name", throwIfNotFound: true)`, so a renamed action fails loudly.

- [ ] **Step 1: Write the failing tests** (`KeyboardControlsTests`, EditMode). Each works on a copy made with `InputActionAsset.FromJson(Resources.Load<InputActionAsset>(ASSET).ToJson())`, never on the asset itself.
  - `Install_AddsAKeyboardOnlyScheme_AndDropsKeyboardAndMouse`: the copy has scheme `Keyboard` with exactly one required device, `<Keyboard>`, and no `Keyboard&Mouse`.
  - `Install_BindsEveryAction`: every action in every map has at least one binding in group `Keyboard`.
  - `Install_TheDrivingKeys`:
    - with a test `Keyboard` device, the copy's Player map enabled and its devices set to that keyboard, holding W gives `Accelerate Value` 1;
    - holding A gives `Steer Value` −1, and holding D gives +1.
    - Use `InputTestFixture`-free state events: `InputSystem.QueueStateEvent(kb, new KeyboardState(Key.W))`, then `InputSystem.Update()`.
  - `Install_Twice_ChangesNothing`: the second `Install` returns false, and the binding count is the same.
  - `Uninstall_PutsTheAssetBack`: after `Install` then `Uninstall`, the copy's binding count and scheme names equal a fresh copy's.
  - `TheGamepadBindings_AreUntouched`: every `<Gamepad>` binding in the fresh copy is still in the installed copy.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement `KeyboardControls`.**

- [ ] **Step 4: Run them, and see them pass:** `bash tools/run-tests.sh KeyboardControlsTests`.

### Task 4: The keyboard joins as a player

**Files:**
- Modify:
  - `Assets/Scripts/Player/PlayerInstantiate.cs`: `AddPlayerReference` and `MoveToOnlineSeat`;
  - `Assets/Scripts/UI/ControllerPrompts.cs`: `KEYBOARD_JOIN_HINT`, and `HintForDevice` for a keyboard.
- Test: `Assets/Tests/Editor/KeyboardPlayerTests.cs` (new); `Assets/Tests/Editor/ControllerPromptsTests.cs` (one test changes)

**Interfaces:**
- Consumes: Task 3's `KeyboardControls.SCHEME`.
- Produces:
  - `public const string KEYBOARD_JOIN_HINT = "Press Space to play with the keyboard, or connect a controller";`
  - `public static bool KeyboardJoinPressed(Keyboard keyboard)` on `PlayerInstantiate`: true while Space or Enter (`enterKey` or `numpadEnterKey`) is pressed.
- **`AddPlayerReference`:**
  - It accepts `currentControlScheme == "Gamepad"`, or `KeyboardControls.SCHEME` when `KeyboardJoinPressed(GetDevice<Keyboard>())`.
  - Otherwise it does what it does today: the hint outside a match, then destroy.
- **`HintForDevice`:** a `Keyboard` gives `KEYBOARD_JOIN_HINT`. A `Mouse`, or no device, still gives `CONNECT_CONTROLLER_HINT`.
- **`MoveToOnlineSeat`:** it keeps `player.currentControlScheme` and `player.devices[0]`, then re-joins with `JoinPlayer(-1, -1, scheme, device)` when the device is still `added`.

- [ ] **Step 1: Write the failing tests.**

  In `ControllerPromptsTests`, change `HintForDevice_KeyboardAsksForAController` to `HintForDevice_KeyboardSaysHowToJoin`, which expects `KEYBOARD_JOIN_HINT`. Add `HintForDevice_MouseAsksForAController`.

  `KeyboardPlayerTests`: these are Play Mode tests in the menu scene, set up as `OnlineSeatTests` does. A keyboard is `InputSystem.AddDevice<Keyboard>("DoA Test Keyboard")`, removed in teardown.
  - `AKeyboard_JoinsOnSpace_AsPlayerOne`:
    - at the title screen, press Space (a state event with Space down, then one with nothing);
    - within 10 s, `PlayerCount == 1`, and seat 0's `Input.currentControlScheme == "Keyboard"`;
    - its devices contain the keyboard;
    - `PlayerGamepads[0]` is null;
    - `log.Problems` is empty.
  - `AKeyboard_DoesntJoinOnOtherKeys`:
    - press F1, then wait 1 s: `PlayerCount == 0` (the dev hotkey also adds a test pad, which then joins as player 1, so allow `PlayerCount == 1` only when seat 0's scheme is `Gamepad`);
    - press X, then wait 1 s: no seat has the `Keyboard` scheme;
    - the hint shows `KEYBOARD_JOIN_HINT`.
  - `AKeyboardPlayer_ReadiesUp_WithSpace`:
    - join with Space, pick Play (Enter on the title screen's first item, as `MainMenuTests` does with A);
    - in player select, press Space;
    - the slot is ready (the same check `MenuButtonsTests` makes).
  - `ThisMachinesKeyboardPlayer_MovesToTheSeatTheHostGave`: copy `OnlineSeatTests`' first test with a keyboard. After `SetOnlineSeat(2)`, seat 2 is local, uses the `Keyboard` scheme and holds the keyboard; seat 0 is empty.

- [ ] **Step 2: Run them, and see them fail.**

  Run: `bash tools/run-tests.sh "KeyboardPlayerTests|ControllerPromptsTests"`. Expected: the hint tests fail on their text; the join tests fail with `PlayerCount` 0, or with the move missing the keyboard.

- [ ] **Step 3: Implement the `PlayerInstantiate` and `ControllerPrompts` changes.**
  - `PlayerInstantiate.cs` is `i/crlf`: use the Edit tool.
  - If Space isn't seen as pressed when the join fires, see Ruling 7: ledger what the key's state was, and check it another way, such as remembering the last key pressed through `Keyboard.onTextInput`, or `InputSystem.onEvent`.

- [ ] **Step 4: Run the tests.**
  - Run `bash tools/run-tests.sh "KeyboardPlayerTests|ControllerPromptsTests|OnlineSeatTests|MissingControllerTests|MainMenuTests"`: expect all to pass.
  - Run `LocalMatchSmokeTest` in the background: expect a pass.

### Task 5: Docs, editor chores, full suite

**Files:**
- Create: `docs/controls.md`
- Modify: `docs/dev-hotkeys.md`, `EDITOR-TODO.md`, `docs/testing.md`, the roadmap, the handoff

- [ ] **Step 1: Write `docs/controls.md`:**
  - the pad layout (from the asset);
  - the keyboard table (Ruling 6);
  - joining with Space or Enter;
  - prompts show pad buttons for now;
  - the dev-hotkey overlap in the editor.

  Also give it a section, "Display settings in settings.cfg": the five keys, their values, and where the file lives.
- [ ] **Step 2: Update `docs/dev-hotkeys.md`.** Replace the "Any key press, F-keys included, also counts as a keyboard trying to join…" paragraph: only Space and Enter join with the keyboard, and other keys show the keyboard hint on the title screen and in the lobby.
- [ ] **Step 3: Update `EDITOR-TODO.md`.**
  - A new section 1: let both editors import Phase 1C.
  - A new section 2:
    - play one match with the keyboard as P1 and a pad as P2;
    - in a build, set `framecap=60` and `vsync=0` in `settings.cfg`, then check the frame rate (the Steam overlay's FPS counter);
    - set `fullscreen=0` with `width=1600` and `height=900`, then check the window.
  - Section 9 gains: when there's art, the Options rows for resolution, window mode, VSync and frame cap, plus keyboard prompt sprites.
  - Renumber the rest.
- [ ] **Step 4: Update the rest.**
  - **Roadmap:** Task 1.3's keyboard line is ticked with a note. Task 1.4's Options line keeps "rows need art", with a note.
  - **Handoff:** section 3 is marked code-done, and the art items stay.
  - **`docs/testing.md`:** the new test classes.
- [ ] **Step 5: Run the full suite in the background.**

  Run: `bash tools/run-tests.sh`. Expected: 551 plus the new tests, all passing:
  - Task 1: +5.
  - Task 2: +7.
  - Task 3: +6.
  - Task 4: +4 in `KeyboardPlayerTests`, +1 in `ControllerPromptsTests`.

  That's **574**. Then check `git status` for files without a `.meta`.

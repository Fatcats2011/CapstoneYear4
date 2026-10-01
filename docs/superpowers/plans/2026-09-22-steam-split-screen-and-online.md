# Dead on Arrival — Steam Release Plan (Local Split-Screen → Online Multiplayer)

> **For agentic workers:** This is a phase roadmap. Before executing a phase, write its detailed task plan with superpowers:writing-plans (one plan per phase), then execute it with superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship Dead on Arrival on Steam as a 1–4 player local split-screen game, then add online play (Steam lobbies + relay) without breaking couch play.

**Architecture:** Phase 1 hardens the current single-machine design and ships it. Phase 2 refactors players into a slot roster so any slot can be local or remote. Phase 3 adds Netcode for GameObjects over Steam: the host owns the rules (game state, orders, score, steals); each client owns its own scooter's physics.

**Tech stack:** Unity 2022.3.62f3 LTS · URP 14.0.12 · Input System 1.14.0 · Cinemachine 2.1 (embedded in `Assets/Cinemachine`) · DOTween · Steamworks.NET · Netcode for GameObjects 1.x (Phase 3) · Unity Test Framework 1.1.33 (already installed).

**Spec:** No separate spec. Requirements = the 2026-09-22 request + the audit in §0.

## Global constraints

- Stay on Unity 2022.3 LTS until launch (no Unity 6 / Cinemachine 3 / URP 17 migration).
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`).
- Local split-screen must pass the §R checklist after every phase.
- Every contributor in the git history signs off on a commercial release before the store page goes public.
- Online: no mid-match joining; if the host leaves, the match ends.

## Review focus

1. Controller unplugged or battery dies mid-match → game pauses with "Reconnect P#", never soft-locks. (Task 1.3)
2. Keyboard-only or zero-controller start → clear "Connect a controller" prompt, no NullReferenceException. (Tasks 1.1, 1.3)
3. P3/P4 join after P1/P2 → every camera of every player (incl. icon + phase) uses the new viewport. (Task 1.1)
4. A client or the host drops mid-delivery → leaver's orders return to the pool; match continues (client) or ends cleanly (host). (Task 3.8)
5. Two players steal from each other at the same moment online → exactly one steal wins, everyone sees the same holder. (Task 3.6)

---

## §0 Audit summary (2026-09-22)

**How the game works today**

- Scene flow: `SplashScreen` → `Alex Player Testing` (menu + all persistent managers, `DontDestroyOnLoad`) → `Design Scene(Main)` (3 waves) → `FinalAreaScene` (golden order) → results → menu.
- `GameManager`: `GameState` state machine; broadcasts `OnSwap<State>` events that nearly every system subscribes to.
- `PlayerInstantiate`: joins players (PlayerInputManager, join on any gamepad button); slot 1–4 decides physics layer (10–13), camera layer (17–20), icon layer (24–27), URP renderer (main 1–4 / phase 5–8), render texture, company; builds split-screen viewports; swaps action maps (`UI` / `Player` / `Load`); pause.
- Player prefab `Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab`: `PlayerInput` (Unity Events) → `InputManager` (driving) / `PlayerUIHandler` → `MenuInteractions` (menus); `BallDriving` (rolling-sphere car: drift tiers, boost + wheelie, phasing through buildings, slipstream); `OrderHandler` (carry 2 orders, deliver, steal, score); `Respawn`; 6 cameras + Cinemachine rigs + menu/driving canvases.
- `OrderManager` spawns waves of Easy/Medium/Hard orders from scene-placed `Order` objects; `OrderBeacon` handles pickup/drop-off triggers; `ScoreManager` ranks players.
- Custom `SceneManager` (shadows Unity's) does async loads + "every player presses confirm"; `SoundManager`/`SoundPool` handle music, mixer snapshots and per-player SFX; `CutsceneManager` plays Timeline cutscenes; `QAManager` writes a playtest CSV.
- Rendering: URP with 12 renderers (per-player main + phase), custom features (speed lines, vignette, outlines), OToon toon shading.

**Upgrade 2022.3.7f1 → 2022.3.62f3** (Input System 1.6.3 → 1.14.0, URP/VFX/Shader Graph 14.0.8 → 14.0.12, Visual Scripting 1.8.0 → 1.9.4, Post Processing 3.2.2 → 3.4.0, TMP 3.0.6 → 3.0.7, Timeline 1.7.5 → 1.7.7)

- No upgrade-caused bugs found. Verified:
  - 0 compile errors (only pre-existing warnings); the API Updater had nothing to change.
  - Every Input System event on the live player prefab still resolves (matched by action ID).
  - `using UnityEditor;` in `BallDriving.cs`, `OrderBeacon.cs`, `PhaseIndicator.cs` still compiles against the 2022.3.62 **player** assemblies (tested with Unity's compiler), so builds aren't broken by it.
  - SplashScreen → menu → match play session: no exceptions in `Editor.log`.
  - No gameplay scene/prefab lost a script to a package upgrade (only leftovers from scripts the team deleted: `CustomPostScreenTint` in `Main Volume.asset` / `Distort Volume.asset`, OToon demo files, `FloatingLantern.prefab` variant parent — none are used at runtime).
- The 139 NullReferenceExceptions in the log came from pressing Play inside `Design Scene(Main)`. Its managers live in `Alex Player Testing`, so gameplay scenes can't run alone. Press Play from `SplashScreen`.
- Behaviour change to know: Input System 1.11+ detects a DualSense as a DualSense (older versions could mis-detect it as a DualShock 4).

**Pre-existing issues** (not upgrade-related) → fixed in Tasks 1.1–1.2.

## §R Local split-screen regression checklist (run after every phase)

- [ ] 1, 2, 3 and 4 players join; each gets the right colour, company and viewport.
- [ ] Ready-up countdown → load confirm → cutscene → tutorial → 3 waves → golden order → results → menu → a second match.
- [ ] Boost, drift tiers 1–3, phasing (only in that player's view), steal, clash, water respawn.
- [ ] Pause from any player (host vs sub pause menu), resume, return to menu.
- [ ] Unplug/replug a controller mid-match (after Task 1.3).
- [ ] EditMode tests green: Window → General → Test Runner → EditMode → Run All. It includes an automated 4-player match (`LocalMatchSmokeTest`, 1–2 minutes: menus, a player leaving and rejoining player select, loading, cutscene, driving, drifting, boosting, the match clock, pause, controller unplug and replug) — let it play. Command line: `bash tools/run-tests.sh` (see `docs/testing.md`).
- Short on controllers? F1 adds a virtual test player, F2 presses A on all of them, F3 unplugs / replugs the newest (editor and development builds; see `docs/dev-hotkeys.md`). The title screen only lets player 1 in, so add the others in player select.

---

## Phase 1 — Ship local split-screen on Steam ("as is")

Progress (2026-09-23): code done in Phase 1A (`2026-09-22-phase1a-release-hardening.md`) and Phase 1B (`2026-09-23-phase1b-controllers-and-quality.md`), 75 EditMode tests. Open items are editor work, art, the Steamworks dashboard (needs the App ID) and store/legal — see **Partner steps** at the end of Phase 1.

### Task 1.1: Stability fixes

- [ ] Delete the dead **Player Left** listener: `Alex Player Testing.unity` → *Instantiation Manager* → PlayerInputManager → Player Left Event (it calls `RemovePlayerReference`, which doesn't exist). Don't rewire it to `RemovePlayerRef`, which would then run twice. *(Partner step — scene edit.)*
- [x] `RemoveFromPlayerArray` returns `0` when the player isn't found → return `-1` and guard the caller (`PlayerInstantiate.cs:388-399`, caller `:335-338`).
- [x] `OrderHandler.OnDisable` subscribes instead of unsubscribing: `+= InitHandler` → `-= InitHandler` (`OrderHandler.cs:82`).
- [x] Lambdas unsubscribed with a new lambda never unsubscribe → store the delegate in a field: `BallDriving.cs:285-304`, `SoundManager.cs:105/115`, `OrderHandler.cs:311/319`. *(The `OrderHandler` game-end lambda was left: its `OrderManager` is destroyed every match, so it can't pile up.)*
- [x] `SpawnManager` loops `i <= MAX_PLAYERS` inside an empty `catch` → `i < MAX_PLAYERS`, remove the empty catch (`SpawnManager.cs:57-78`, `:90-108`). *(Done in Phase 2A with the roster. The empty catch was hiding a 4-player bug: the golden-round scenes list 3 spawn points, so player 4 was never moved to the start of the golden round; they now use their normal "Spawn 4" point, which sits in line with the other three.)*
- [x] `PlayerCameraResizer.Update` copies the viewport to follower cameras only once (`initalized` flag) → re-copy whenever `referenceCam.rect` changes (`PlayerCameraResizer.cs:106-123`); check P1's Icon Camera after P3/P4 join.
- [x] `Rumbler.Start` / `OnApplicationQuit` use `Gamepad.current` without a null check → keep only `InputSystem.ResetHaptics()` (`Rumbler.cs:14-24`).
- [ ] Exactly one active AudioListener: the bootstrap scene has 4 (*Black Camera Render*, *Color Camera Render*, *Menu Audio Listener*, *Sound Manager*) → removes ~7,000 "3 audio listeners" warnings per session. *(Partner step — scene edit.)*
- [x] Tests: EditMode test for `CalculateRects` (1–4 players → expected rects) and for `RemoveFromPlayerArray` (unknown player → `-1`); §R checklist.

### Task 1.2: Release-build hygiene

- [x] Debug hotkeys → wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD` *(done as `DevTools.GetKeyDown`; every key is listed in `docs/dev-hotkeys.md`)*:
  - `HotKeys.cs:12-24` (1/2 = wave −/+, 4 = load game)
  - `OrderManager.cs:123-133` (Y = delete orders, 5 = jump to wave 3), `:202-205` (P = freeze timer)
  - `TutorialManager.cs:38`, `TutorialHandler.cs:51` (S = skip tutorial)
  - `CutsceneManager.cs:40` (L = skip cutscene), `MainMenu.cs:39` (L = kick all players)
  - `BallDriving.cs:358` (B = reset boost), `OrderHandler.cs:87` (T = drop orders), `Respawn.cs:102` (R = respawn)
- [x] QA telemetry off in release: same define around `QAManager.SendData` (`QAManager.cs:86-104`) and the heatmap writes (`HeatmapCamera.cs:67,102`); delete `Assets/StreamingAssets/QAData.csv` and `Assets/StreamingAssets/HeatMaps/` (StreamingAssets ships inside the build). *(Playtest data moved to `docs/playtest-data/`; `HeatmapCamera` is switched off in code and left as is.)*
- [x] Settings save: replace `BinaryFormatter` (`OptionsMenu.cs:55-111`) with `JsonUtility` → `settings.json` via `File.WriteAllText`; wrap loading in `try/catch` so a corrupt file falls back to defaults. *(Done as plain `key=value` lines in **`settings.cfg`** (`GameSettings.cs`), so a damaged file only loses the lines it can't read.)*
- [ ] Player Settings: version `1.0` → `1.0.0`, icon, splash. Optional: IL2CPP backend (needs the *Windows Build Support (IL2CPP)* module + Visual Studio *Desktop development with C++*). *(Partner step — Project Settings.)*
- [x] Tests: EditMode round-trip test for settings JSON (save → load → same values; garbage file → defaults). A non-development build ignores all hotkeys and writes nothing next to the `.exe`.

### Task 1.3: Controllers

- [x] Device lost/regained: wire `PlayerInput` **Device Lost / Device Regained** events on the player prefab (empty today) → pause + "Reconnect P#" on that player's viewport; resume when regained. *(Wired in code (`PlayerInstantiate.AddPlayerReference`). Mid-race the match pauses and "P# controller disconnected" covers that player's view (`ControllerPrompts`). Any controller — the same one or another — takes the player back (`ReplacementControllerListener`); new players can't join meanwhile. The match stays paused until someone picks Resume.)*
- [x] Zero controllers / keyboard press on the title screen → show "Connect a controller" (today keyboard joins are silently destroyed in `PlayerInstantiate.cs:120`). *(Shown on the title screen and in the lobby, never mid-match; unrecognised controllers get "That controller isn't supported".)*
- [ ] Recommended: keyboard as one player (add a *Keyboard&Mouse* scheme to `Assets/Resources/CapstoneYear4.inputactions`, allow it in `AddPlayerReference`). *(Not started: menus and prompts show controller buttons only.)*
- [ ] Button prompts: device `is DualShockGamepad` (covers DualShock 4 + DualSense) → PlayStation glyphs, `SwitchProControllerHID` → Nintendo, else Xbox. *(Needs PlayStation / Nintendo versions of `Assets/Sprites/UI/MenUI_Buttons.png` first.)*
- [ ] Tests: unplug/replug each of 4 pads mid-match; Xbox, DualShock 4, DualSense and Switch Pro all join and rumble. *(Partner step; F3 simulates an unplug.)*

### Task 1.4: Settings & performance

- [ ] Options menu: add resolution, window mode, VSync, frame cap and quality preset (today: fullscreen toggle + 2 volumes). *(Quality is done in code and saved; its row needs menu art — partner step. Resolution / window mode / VSync / frame cap not started: builds run borderless at desktop resolution with VSync on.)*
- [x] Quality presets for `Assets/Rendering/URP Asset.asset` (today: MSAA 8x, HDR, 4096 shadow map, SSAO + decals + outlines on every player renderer), e.g. Low = no MSAA, no SSAO, 1024 shadows; High = MSAA 4x, 2048 shadows. *(Done as `GraphicsQuality`: High = the asset as authored; Medium = MSAA 4×, 200 m shadows, 2 cascades; Low = no MSAA, 100 m, 1 cascade. SSAO stays on (it lives in the 12 renderers). Steam Deck starts on Medium.)*
- [x] Phase-camera render texture is a fixed 1920×1080 per player (`PlayerCameraResizer.cs:87`) → size it to that player's viewport.
- [ ] Targets: 4 players at 1080p ≥ 60 fps on a GTX 1060-class GPU; Steam Deck ≥ 30 fps at 1280×800. Profile the 4-player golden-order scene first. *(Partner step: F1 ×3 in player select for 4 players, F4 to switch quality.)*
- [ ] Aspect ratios: `PlayerInstantiate.CalculateRects` assumes 16:9 → check 16:10 (Steam Deck) and 21:9. *(Viewports are fractions of the screen, so each view keeps the screen's shape, and the phase texture now follows it (tested at 1280×800 and 2560×1080). Still to check by eye: the HUD at 16:10 and 21:9 in the Game view.)*
- [ ] Tests: Profiler capture per preset, before/after.

### Task 1.5: Steamworks (no netcode needed)

- [ ] Steamworks partner account → $100 Steam Direct fee → App ID. New accounts wait 30 days before a first release; the Coming Soon page must be live ≥ 2 weeks before launch. *(Fee paid 2026-09-22; App ID pending.)*
- [x] Install Steamworks.NET (Package Manager → Add from git URL: `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net`). *(Pinned to `#2025.164.1`.)*
- [x] `SteamManager` in the bootstrap scene: `SteamAPI.RestartAppIfNecessary(appId)` (builds only) → `SteamAPI.Init()` → `SteamAPI.RunCallbacks()` every frame → `SteamAPI.Shutdown()` on quit. Use App ID 480 (Spacewar) + `steam_appid.txt` until yours exists. *(It creates itself before the first scene loads, so no scene holds it. When the App ID arrives: `SteamStartup.APP_ID`, `steam_appid.txt`, `docs/steam/steampipe/app_build.vdf`.)*
- [ ] **Remote Play Together**: enable it in Steamworks settings and tag *Shared/Split Screen*. Friends then join your couch game over the internet with zero netcode (Steam streams the host's screen; guests' pads appear as local gamepads). This is online play on day one.
- [ ] Steam Input: set the default config to Gamepad. Test that one PlayStation pad doesn't join as **two** players (physical HID + Steam's virtual Xbox pad); if it does, opt PlayStation controllers out of Steam Input.
- [ ] Steam Cloud (Auto-Cloud): root `WinAppDataLocalLow`, path `The Boo Crew/Dead on Arrival`, pattern `settings.cfg`.
- [ ] Optional: achievements (first delivery, win holding the golden order), Rich Presence ("Delivering — 3 players").
- [ ] Builds via SteamPipe: `steamcmd +login <user> +run_app_build <path>\app_build_<appid>.vdf +quit`; use a `beta` branch for testers; request Steam Deck compatibility review. *(Template and steps ready in `docs/steam/steampipe/`.)*
- [ ] Tests: launch from the Steam client, overlay (Shift+Tab) works, a Remote Play Together session with one remote friend works.

### Task 1.6: Store & legal

- [ ] Written agreement from every contributor (IP ownership, revenue split, credits); check the school's student-IP policy.
- [x] License audit → `LICENSES.md`: OToon (Asset Store EULA), DOTween, Cinemachine 2.1, Udar SceneField, Noisy Nodes, `Assets/Fonts`, all music/SFX. *(Inventory done; its ⚠️/❌ items still need answers — fonts, audio, brand names.)*
- [ ] Store page: header 920×430, small 462×174, main 1232×706, vertical 748×896, library 600×900 + hero 3840×1240; 5+ screenshots; trailer; tags (Local Multiplayer, Shared/Split Screen PvP, Party, Racing); price; content survey. *(Text drafted in `docs/steam/store-page-draft.md`.)*
- [ ] Submit store page + build for Valve review (a few business days each) → release.

### Partner steps (Unity editor)

Scene and Project Settings edits (the code can't safely make them while the editor is open):

- *Alex Player Testing* → **Instantiation Manager** → Player Input Manager → **Player Left Event**: remove the entry (it points at a method that no longer exists).
- *Alex Player Testing*: remove the **Audio Listener** component from **Black Camera Render** and **Color Camera Render** (keep Sound Manager's).
- Project Settings → Player: **Version** `1.0.0`, set the **Default Icon**.
- Options screen **Quality** row (optional — until then the saved or default quality applies):
  - *Alex Player Testing* → **Options Canvas**: duplicate the Fullscreen row (label + its 2 points) as "Quality" with **3** points: Low, Medium, High.
  - Add the new label to **OptionsMenu → Selector Objects** as the 4th entry.
  - Assign **Quality Selector** and the 3 points to **Quality Selector Positions** on **OptionsMenu**.

Checks (press Play from `SplashScreen`):

- Reconnect: join with your controller and pick Play → in player select, F1 twice (two test players join as P2 and P3; the title screen only lets player 1 in) → F2 and ready up yourself → confirm the loading screen (F2 + your A) → F3 mid-race (the newest test player's controller unplugs: the match pauses and "P3 controller disconnected" covers their view) → F1 (a new test controller takes that player over; nobody new joins) → pick Resume. Then try it with a real controller: unplug it mid-race, plug in a different one, press A.
- Keyboard: press a key on the title screen → "Connect a controller to play" appears for 3 seconds.
- Performance: 4 players (your controller + F1 ×3 in player select) → golden-order scene → Window → Analysis → Profiler → F4 through Low / Medium / High; note the CPU and GPU frame times for each.
- Aspect ratios: Game view at 1280×800 (Steam Deck) and 2560×1080 with 1, 2 and 4 players — check the HUD isn't cut off.
- Test Runner → EditMode → Run All: 381 tests (the smoke test plays a match for a minute or two). 380 pass until the online prefabs' network IDs are saved (`EDITOR-TODO.md` step 1), then all 381.

---

## Phase 2 — Networking-ready refactor (still local-only)

Goal: identical game, but a player's identity and authority no longer live in `PlayerInput`, so Phase 3 can plug in netcode.

Progress (2026-09-23): Phase 2A (`2026-09-23-phase2a-roster-and-smoke-test.md`) — automated 4-player match test and the player roster (Task 2.1). Phase 2B (`2026-09-23-phase2b-input-authority-scene-flow.md`) — Tasks 2.3–2.5. Phase 2C (`2026-09-23-phase2c-player-prefab-split.md`) — Task 2.2. Phase 2 is done.

### Task 2.1: Player roster

- [x] `PlayerSlot` (slot 0–3, company/colour/hat index, `isLocal`, `ownerClientId`, `PlayerInput` or `null`) + `PlayerRoster` as the single source of truth instead of `PlayerInstantiate.PlayerInputs`. *(`PlayerInstantiate.Roster`; `PlayerCount` is `Roster.Count`. `IsLocal` means "has a `PlayerInput`". Colour and hat stay in `CustomizationSelector` until Phase 3's `NetworkPlayer` needs them.)*
- [x] Migrate every user of the `PlayerInput[]` array. *(Loops use `Players` for what every player has (scooter, orders, spawns, cutouts, tutorial orders, lobby prompts, loading buttons) and `LocalPlayers` for what belongs to a split-screen view on this machine (camera, menus, compass UI, per-view indicators and boost pads). `QAManager` never used the array.)*
- [x] Keep the per-slot mapping: player layer `10+slot`, camera layer `17+slot`, icon layer `24+slot`, URP renderer `slot+1` (main) / `slot+5` (phase), phasing = `Physics.IgnoreLayerCollision(9, 10+slot)`. *(Unchanged in `AddPlayerReference`.)*
- [x] Tests: EditMode tests for slot assignment (fills lowest free slot, frees on leave, max 4); §R checklist. *(`PlayerRosterTests`; §R's play-through is automated by `LocalMatchSmokeTest`.)*

### Task 2.2: Split the player prefab

- [x] `Player - Cinemachine.prefab` → **PlayerAvatar** (sphere Rigidbody, scooter/ghost model, `BallDriving`, `OrderHandler`, `Respawn`, `SoundPool`, emote bubble, compass marker) + **PlayerView** (6 cameras, Cinemachine rigs, Menu/Driving canvases, `PlayerInput`, `PlayerUIHandler`, `MenuInteractions`, `OrbitalCamera`). *(Phase 2C, see `docs/player-prefab.md`. `PlayerAvatar.prefab` = `Ball Of Fun` + `Control` with the scooter's scripts, including `PhaseIndicator` and `DrivingIndicators` (what others see). The view stays `Player - Cinemachine.prefab`, which the menu scene spawns: it nests one avatar and adds its cameras, canvases, `OrbitalCamera`, `Compass`, `DynamicNumberUI`, `TutorialHandler` and `Rumbler` to the avatar's `Control` as prefab overrides. There's no emote bubble to move: `EmoteHandler` isn't on the live prefab, only in `NewDrive Test.unity`.)*
- [x] Local player = Avatar + View; remote player (Phase 3) = Avatar only.
- [x] Connect each avatar to its view's controller. *(Serialized, not at runtime: the nested avatar's `BallDriving.inp` and `OrbitalCamera.inputManager` are prefab overrides pointing at the view's `InputManager`. `DriveInput` stays the seam for other drivers. `InputManager` raises its button events with `?.Invoke`, so a controller nobody listens to no longer throws.)*

### Task 2.3: Input abstraction

- [x] `IDriveInput` (steer, accelerate, brake, drift, boost, camera X/Y + button events), implemented by `InputManager`; `BallDriving`, `EmoteHandler`, `OrbitalCamera` read the interface instead of `InputManager` fields. *(`Assets/Scripts/Player/IDriveInput.cs`. Each of the three has a `DriveInput` property: the prefab's `InputManager` unless something else is set. The button listeners move with it, so Task 2.2 can connect an avatar to its view at runtime.)*

### Task 2.4: Authority seam

- [x] `GameAuthority.IsAuthority`: always `true` offline, host-only online. *(`GameAuthority.Role` = `Offline` / `Host` / `Client`; `IsOnline` too. Phase 3 sets the role when a session starts and ends.)*
- [x] `GameManager`: `SetGameState` = authority request; new `ApplyGameState(GameState)` fires the existing `OnSwap*` events (listeners unchanged).
- [x] Gate authority-only logic: order spawning + wave timers (`OrderManager.Update/InitWave`), pickup/delivery (`OrderBeacon.OnTriggerStay`), steal/clash (`OrderHandler.OnTriggerEnter/AttemptSteal`), scoring (`OrderHandler.DeliverOrder`, golden bonus in `Order.EraseOrder`), spawns (`SpawnManager`), respawn point choice (`RespawnManager`), all `Random` calls (order shuffle, drop height). *(Gated: `OrderManager.Update/InitWave/InitGame/InitTutorial` (the shuffle runs in `InitGame`), `OrderBeacon.OnTriggerStay`, `OrderHandler.OnTriggerEnter/AttemptSteal`, the golden bonus (`OrderHandler.AwardGoldenBonus`). `DeliverOrder` needs no gate of its own (only the gated beacon calls it). Not gated, see Tasks 3.3, 3.5 and 3.6: spawns (fixed per slot, placed by each scooter's owner), the respawn point and the drop height (they wait for the host's answer).)*
- [x] `Time.timeScale` pause (`PlayerInstantiate.cs:699`) and end-of-round slow-mo (`OrderManager.cs:644`) → local mode only. *(Every write goes through `GameAuthority.SetTimeScale`, which does nothing online; a test fails if a script writes `Time.timeScale` directly.)*

### Task 2.5: Scene flow seam

- [x] Put the custom loader (`Menu/SceneManager.cs` async load + confirm) behind an `ISceneFlow` interface so Phase 3 can swap in NGO's scene manager. *(`ISceneFlow` + `SceneFlow.Current`: the local loader unless online play sets another. A test fails if a script uses `SceneManager.Instance` directly. `PlayerInstantiate` still hears the local loader's `OnConfirmToLoad` directly — online has no loading-screen confirm.)*
- [x] Tests: §R checklist passes unchanged.

---

## Phase 3 — Online multiplayer (Netcode for GameObjects + Steam)

Progress (2026-09-28):
- Phase 3A (`2026-09-24-phase3a-online-session.md`) — Task 3.1, plus the online session: host / join / leave, join rules, roles and end reasons (`OnlineSession`, `JoinRules`, `docs/online.md`).
- Phase 3B (`2026-09-24-phase3b-online-player-select.md`) — online player select:
  - Seats, and `OnlinePlayer` (seat, colour, hat, ready).
  - Other machines' scooters (`RemoteAvatar`, `ScooterLook`).
  - Clients follow the host's game states (`OnlineMatch`), wired up by `OnlineGame`.
- Phase 3C (`2026-09-27-phase3c-driving-together.md`) — driving together:
  - The match loads on every machine and starts together (`OnlineSceneFlow`, held loads). The host takes everyone back to the menu.
  - Each machine drives its own scooter, and the others follow it (`OnlineScooter`, `OnlineDriving`), with its boost, drift and sparks (`DriveFlags`).
  - The host's match clock on every machine (`MatchClock`). No tutorial online yet: players start in the city.
  - 2026-09-28 fixes: players start on the city start points online, not at the tutorial's start; a machine leaving on the loading screen no longer starts another ready-up countdown.
  - 2026-09-28 code review: a session ending in player select no longer starts a solo match; other machines hide a respawning scooter and are silent; the host's clock stops between matches; the online prefabs' saved network IDs (**Tools → Dead on Arrival → Online → Save Network Prefab IDs**).
- Phase 3D (`2026-09-28-phase3d-playing-over-steam.md`) — playing over Steam:
  - Y in player select hosts a friends-only Steam lobby and invites friends; friends join through Steam's invite or "Join Game" (`OnlinePlay`, `OnlineLobby`, `SteamLobbyService`).
  - B leaves; the host's B ends the match for everyone, who see why. After an online match everyone is back in player select, the lobby.
- Phase 3E (`2026-09-29-phase3e-orders-online.md`) — orders online:
  - Every machine shows the same orders. The host decides every scooter's pickups, deliveries and drops, and clients replay its changes (`OrderBook`, `OrderChange`, `OrderSync`, `OnlineOrders`).
  - Scores and the golden order's value are the host's, on every machine. A player who leaves mid-match puts their orders back in the pool.
- Phase 3F (`2026-09-29-phase3f-tutorial-online.md`) — the tutorial online:
  - Online matches start with the tutorial again, each player in their seat's lane. Each machine tells the host when its player has finished, and the host counts seats (`TutorialManager`, `TutorialSync`, `OnlineTutorial`). A player who leaves doesn't hold the others up.
  - Cutout steals: the machine that drives the scooter opens its barrier at once and asks; the host hands out the order (`CutoutHandler`).
- Phase 3G (`2026-09-29-phase3g-steals-and-respawns.md`) — steals, clashes and respawns online:
  - Boosting into another machine's scooter steals or clashes as in a local match. The machine that drives the boosting scooter asks, and the host decides for everyone (`StealRules`, `OnlineSteals`). Two machines asking at once make one steal.
  - The host picks every respawn point, so no two players rise on one (`OnlineRespawns`).
  - Two 2024 bugs fixed, in local play too: a player robbed of the golden order stayed slowed, and the first respawn point could be handed out while in use.
- Phase 3H (`2026-10-01-phase3h-one-shots-and-local-systems.md`) — one-shots, cutscenes and pause online:
  - Other players' scooters are heard here, quieter with distance: boosts, drift boosts, full horns (which flash), phasing, falls in the water, and the order sounds. Their respawn shows its wisp, gravestone and sparkle (`ScooterCue` through the host, `OnlineCues`).
  - The host's wave bells and whistle sound everywhere, and the whistle stops every scooter (`ClockCue`). A client's cutscene ends when the host's does; only the host skips.
  - Pausing online is this machine's own menu: the match goes on, the menu closes when players stop driving, and a hint says what Main Menu does (`PausePolicy`).
  - Pedestrians, cans and slipstream react to other players' scooters too.
- Next:
  - Disconnects (3.8).

### Task 3.1: Packages & transports

- [x] `com.unity.netcode.gameobjects`: latest **1.x** (2.x needs Unity 6). *(1.15.1, with Unity Transport 1.5.0.)*
- [x] Unity Transport for editor/LAN testing; a Steam transport for builds (the community *SteamNetworkingSockets* transport for Steamworks.NET, from `Unity-Technologies/multiplayer-community-contributions`; pin a commit that supports your NGO version). *(`OnlineSession.HostDirect` / `JoinDirect` use Unity Transport; `HostSteam` / `JoinSteam` use the Steam transport, pinned at `d862504b`, the last commit built for Netcode 1.x.)*
- [x] ParrelSync to run 2–4 editor instances; the Multiplayer Tools network simulator (Unity Transport) or *clumsy* for 150 ms / 1% loss tests. *(ParrelSync 1.5.3. Unity Transport 1.5's own simulator instead of Multiplayer Tools: **Tools → Dead on Arrival → Online → Bad Connection** adds 150 ms / 1%. See `docs/online.md`.)*

### Task 3.2: Steam lobby flow

- [x] Main menu → **Play Online** → *Host* (create a friends-only or public lobby, 4 slots, metadata `game=doa`, `build=<version>`) or *Join* (overlay invite via `GameLobbyJoinRequested_t`, or a lobby list filtered by `game` + `build`). *(Phase 3D: friends-only lobbies for 4, tagged `game=doa` and `build=<version>`. Friends join through Steam's invite or "Join Game" (`GameLobbyJoinRequested_t`, or `+connect_lobby` when Steam starts the game); no public lobbies or lobby list yet. "Play Online" is Y in player select, not a title-screen row, because the title's rows are hand-lettered art.)*
- [x] Lobby screen reuses Player Select; the host assigns slots/companies, and players pick colour/hat. *(Phase 3B: player select is the online lobby. Seats give companies; colour, hat and ready sync. Phase 3D: it's the Steam lobby too, with a line along the top saying what Y and B do (`LobbyPrompt`).)*
- [x] Host presses Start → `NetworkManager.StartHost()`; clients `StartClient()` to the host's SteamID → networked scene load. *(Phase 3D: Y hosts over Steam (`OnlineSession.HostSteam`); a joiner joins the lobby owner's Steam ID (`JoinSteam`). The match starts with Phase 3C's held loads when everyone's ready.)*

### Task 3.3: Network player

- [ ] `NetworkPlayer` (NetworkObject on PlayerAvatar): NetworkVariables for slot, company, colour, hat, score, ready, and a flags byte (boosting / phasing / drifting / drift tier). *(Phase 3B: `OnlinePlayer` has seat (company follows), colour, hat and ready. It's named `OnlinePlayer` because Unity still declares an obsolete `UnityEngine.NetworkPlayer`. It's its own network prefab, not on `PlayerAvatar`: see the Phase 3B plan's Ruling 1. Score and the flags byte come with driving.)* *(Phase 3C: the flags byte is on `OnlineScooter` (`DriveFlags`). Phase 3E: the score is on `OnlinePlayer`, written by the host.)*
- [x] Owner-authoritative movement: the owner runs `BallDriving` physics; owner-authoritative `NetworkTransform` (the `ClientNetworkTransform` pattern) with interpolation on sphere + control; non-owners make the sphere kinematic and skip `BallDriving` Update/FixedUpdate. *(Phase 3C: through two proxies on a per-player `OnlineScooter`, each an `OwnerNetworkTransform`: the ball, and the model's world pose. `OnlineDriving` copies them every frame. The proxies start parked below the map; long moves teleport.)*
- [x] Spawns: each owner puts its own scooter on its slot's spawn point (`SpawnManager.SpawnPoint`); the host doesn't place other machines' scooters. *(Phase 3C: `SpawnManager` places `LocalPlayers` only. Online, without the tutorial, they start on the city start points, `nonTutorialSpawnPositions` (`SpawnManager.PlacePlayersAtStart`).)*
- [x] Remote players' boost/phase/drift visuals and sounds come from the flags byte. *(Phase 3C: `DriveFlags` on `OnlineScooter`. `BallDriving.ShowRemote` drives the trail, skid marks, sparks and rider. Sounds come with Task 3.7.)* *(Phase 3H: one-shots, as `ScooterCue`s. Another machine's scooter has no engine or drift sounds.)*
- [ ] A remote avatar (`PlayerAvatar.prefab` alone) has no view:
  - Its view fields are empty: `BallDriving.inp` / `cameraResizer` / `orbitalCamera`, `OrderHandler.numberHandler`, `PhaseIndicator.hornSliderLeft` / `hornSliderRight`, `DrivingIndicators.iconCamera` / `thisPlayer`.
  - Its `Control` has no `Compass`, `TutorialHandler` or `Rumbler`, which `OrderHandler`, `Order.EraseOrder`, `OrderBeacon` and `BallDriving` fetch with `GetComponent`.
  - Guard those uses, or keep those scripts off on remote avatars.
  - The company look (scooter material, logo decals, indicator sprites) comes from the view (`PlayerCameraResizer.InitalizeCompanyScooter`): move it to the avatar so remote players get their colours.
  - A local online player can keep its nested avatar, e.g. handed to NGO through a prefab instance handler.
  - *(Phase 3B:*
    - *`RemoteAvatar` turns off `BallDriving`, `Respawn` and `PhaseIndicator`, and makes the ball kinematic. `ScooterLook` dresses it by path: company, colour, hat.*
    - *`OrderHandler` and `PlayerCameraResizer.UpdatePlayerObjectLayer` are guarded. `DrivingIndicators` finds `PlayerInstantiate` itself, and `SkideeSkidoo` unsubscribes.*
    - *The local player keeps its view; `OnlinePlayer` is separate.*
    - *Phase 3C: another machine's scooter doesn't respawn, collect orders, steal, clash or freeze on this machine (`RemoteAvatar.IsRemote`), so `Compass` lookups never reach it.)*

### Task 3.4: Game state, timers, scenes

- [x] `NetworkGameState`: `NetworkVariable<GameState>`; the host sets it, every peer calls `GameManager.ApplyGameState` in `OnValueChanged`. *(Phase 3B: `OnlineMatch` sends every state as an ordered `ClientRpc`, plus a variable holding the latest for players who join. States can switch twice in a frame, and a variable alone would skip the first. `GameManager.StateApplied` tells the host's `OnlineGame`. Clients show the host's options and credits as the title screen.)*
- [x] Wave/game timers → host-owned `NetworkVariable<double>` end times in server time; clients compute remaining time for the UI. *(Phase 3C: `OnlineMatch.ClockEnd`, plus started and golden round. Clients' `OrderManager.FollowHostClock` feeds their UI.)*
- [x] Loads via `NetworkManager.SceneManager.LoadScene`; start the cutscene on `OnLoadEventCompleted` (replaces the loading-screen confirm). *(Phase 3C, done differently:*
  - *Netcode's scene manager stays off. It breaks joining from an unsaved Play Mode scene, and it would sync the menu scene that holds every manager.*
  - *Instead the host coordinates the game's own loader: every machine loads the scene held and reports, then the host shows it everywhere at once (`OnlineSceneFlow`, `LoadRound`).*
  - *The opening cutscene starts on each machine when it's shown.)*

### Task 3.5: Orders

- [x] NetworkObject on every scene-placed `Order`; NetworkVariables for state (Inactive / AtPickup / Held / Dropped / Delivered), holder slot and value. *(Phase 3E, done differently: one stream of order changes (`OrderChange` on `OnlineMatch`), keyed by `OrderBook`. See the Phase 3E plan's Rulings 1–4.)*
- [x] Only the host runs `OrderBeacon.OnTriggerStay` and `OrderManager` spawning; clients drive meshes, beacons and compass markers from `OnValueChanged`. *(Phase 3E: the host's beacons decide every scooter's pickups and deliveries, another machine's too. Clients replay each change through the same game code (`OnlineOrders`, `OrderSync`).)*
- [x] Dropped orders: the host picks the drop height (`Order.Drop`'s `Random.Range`) and clients get the landing spot. *(Phase 3E: `Order.DropHeight` on the host. A client's respawn asks the host with its respawn point's spots (`OnlineMatch.AskDrop`).)*
- [x] The tutorial online (it teaches orders): `TutorialManager.IsSkipped` turns it back on. Players then start at the tutorial again (`SpawnManager.PlacePlayersAtStart`), and `IncrementAlumni` must count every machine's players, not just this machine's handlers. *(→ Phase 3F: it also needs the cardboard cutouts' steals.)* *(Phase 3F: `IsSkipped` and the city start points are gone. Each machine reports its player's finish, and the host counts seats. Cutout steals are asked for, like drops. See the Phase 3F plan's Rulings 1–4.)*

### Task 3.6: Steals, clashes, respawns

- [x] Steal: the attacker's client detects the overlap while boosting → `RequestStealServerRpc(victimSlot)`; the host checks distance ≤ trigger radius + lag tolerance, attacker boosting, victim not boosting, per-pair cooldown → first valid request wins → result replicates. *(Phase 3G: `StealSync` → `OnlineMatch.AskSteal`; the host's own player's bumps are judged at once. `StealRules`: the sender's own seat, neither player respawning, balls within 50 m on the host, one hit per pair per 2 s either way round. The asker's own boost is trusted: its flags can arrive after its request. See the Phase 3G plan's Rulings 1–4. The stolen order moves as `OrderChange.Steal`; the victim's drop is Phase 3E's.)*
- [x] Clash: host → `ClientRpc` to both owners → each calls `BounceOff` locally. *(Phase 3G: `PlayerHit` reaches every machine, and each bounces only its own player: a robbed player too (`OnlineSteals.Show`).)*
- [x] Respawn: the owner detects water → `ServerRpc`; the host drops that player's orders and picks a `RespawnPoint` → `ClientRpc` to the owner runs the respawn animation. *(Moves `RespawnManager.GetRespawnPoint` and `RespawnPoint.InUse` to the host.)* *(Phase 3E: the host drops the orders, on the spots of the respawn point the owner picked (`OnlineMatch.AskDrop`). Picking the point stays here.)* *(Phase 3G: a client asks (`AskRespawn`) and waits, hidden, up to 2 s, then picks its own. The host picks the point, holds it (`RespawnPoint.HoldFor`), drops the orders there and answers (`OnlineRespawns`). Its own player picks as before, among the points nobody holds.)*
- [x] Tests: EditMode tests for the steal arbitration rules (pure C# class); 2 clients stealing simultaneously at 150 ms → one steal, same holder everywhere. *(Phase 3G: `StealRulesTests`; `OnlineStealsNetworkTests.Hosting_…`: two other machines at 150 ms ask at once, and one steal wins, the same everywhere.)*

### Task 3.7: Local-only and one-shot systems

- [x] Not networked: pedestrians (`CivilianAgent`), kickables, DOTween animations, particles, cameras, compass UI, speed lines. *(Phase 3H: each machine keeps its own. They now feel other machines' scooters at their speed, as slipstream does: `BallDriving.ShowRemote` sets `CurrentVelocity`. See the Phase 3H plan's Ruling 7.)*
- [x] One-shots via `ClientRpc`: boost start, drift boost, emotes (`EmoteHandler` — not on the live player prefab yet, only in `NewDrive Test.unity`), horn/phase sounds. *(Phase 3G: other machines don't yet hear a thief's whoosh or see a respawn's gravestone.)* *(Phase 3H: the machine that plays a one-shot sends a `ScooterCue`; the host passes it on, for the sender's own seat, in a `ClientRpc`. Other machines play it on that scooter, quieter with distance (`OnlineCues`, `RemoteSound`). Order sounds, the whoosh among them, come from the order replays. A respawn's gravestone and sparkle come from a `Rise` cue. Emotes are left out: see the Phase 3H plan's Rulings 1–3 and 8.)*
- [x] Cutscenes: host sets the state; each client plays the Timeline locally; only the host can skip. *(Phase 3H: a client's cutscene also ends when the host's does: when the state it hands over to (the tutorial, or the golden round) arrives from the host. Other states don't end it: the opening plays on while the game switches to MainLoop under it. The developer skip (L) works only on the host: see the Phase 3H plan's Ruling 5.)*
- [x] Online pause = local overlay only (no `Time.timeScale`); the host's menu adds **End match**. *(`GameAuthority.SetTimeScale` already ignores pauses online. `ControllerDisconnectPolicy.ShouldPause` still reads `Time.timeScale == 0` to tell whether the game is paused — change that here.)* *(Phase 3C: the host's "Main Menu" takes everyone back; a client's leaves the session.)* *(Phase 3H: `ShouldPause` reads `PlayerInstantiate.IsPaused`. The pause menu closes when players stop driving. No "End match" row, because the rows are hand-lettered art: the host's "Main Menu" is it, and a hint says so. See the Phase 3H plan's Ruling 6.)*

### Task 3.8: Disconnects & versions

- [ ] Client leaves → the host drops/erases its orders, despawns its avatar, recomputes placements. *(Phase 3E: its orders go back to the pool (`OrderHandler.ReleaseOrders`). Its avatar goes and placements are redone already (Phase 3C). Still to do: a leaver holding the golden order ends the golden round as if delivered.)* *(Phase 3F: a player who leaves mid-tutorial doesn't hold up the others.)*
- [ ] Host leaves → clients return to the menu with "Host left the match". *(The client's session ends with `OnlineSession.HOST_LEFT`. Phase 3D: the message shows in the controller hint bar, and a client in player select stays there, offline. Returning to the menu from a match is still to do.)*
- [ ] Reject mismatched builds (lobby `build` metadata + NGO connection-approval payload). *(Approval half done: `JoinRules` compares `Application.version`. A build whose Netcode setup differs is dropped by Netcode before approval, with no reason, so the lobby filter matters. Phase 3D: a Steam lobby of another build is left before joining, with a message naming both versions (`LobbyRules.Refusal`).)*
- [ ] Tests: kill a client mid-delivery; kill the host mid-match.
- [ ] Long loads: in the editor, the first load of the game scene took 31 s. Two test sessions in one Unity process lost their connection during it: Unity Transport's disconnect timeout is 30 s. Time a build's load on a slow PC; if it comes close, raise the timeout while loading.

### Task 3.9 (optional, post-launch): Online + couch

- [ ] Several local players per client: each local `PlayerInput` requests its own `NetworkPlayer` owned by that client; that client split-screens only its local slots.

---

## Phase 4 — Online launch

- [ ] Steam Playtest for open testing.
- [ ] Achievements/stats unlocked by the host (authoritative).
- [ ] Opt-in crash/disconnect logs in `Application.persistentDataPath` (never `StreamingAssets`).
- [ ] Store page: add Online PvP tags, update screenshots/trailer.

## Rough effort (one developer, full-time)

- Phase 1: 3–5 weeks (Steam's 30-day wait runs in parallel).
- Phase 2: 2–4 weeks.
- Phase 3: 6–10 weeks.
- Phase 4: ~2 weeks.

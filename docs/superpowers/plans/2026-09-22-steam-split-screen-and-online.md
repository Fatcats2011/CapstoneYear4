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
- [ ] EditMode tests green: Window → General → Test Runner → EditMode → Run All.

---

## Phase 1 — Ship local split-screen on Steam ("as is")

### Task 1.1: Stability fixes

- [ ] Delete the dead **Player Left** listener: `Alex Player Testing.unity` → *Instantiation Manager* → PlayerInputManager → Player Left Event (it calls `RemovePlayerReference`, which doesn't exist). Don't rewire it to `RemovePlayerRef`, which would then run twice.
- [ ] `RemoveFromPlayerArray` returns `0` when the player isn't found → return `-1` and guard the caller (`PlayerInstantiate.cs:388-399`, caller `:335-338`).
- [ ] `OrderHandler.OnDisable` subscribes instead of unsubscribing: `+= InitHandler` → `-= InitHandler` (`OrderHandler.cs:82`).
- [ ] Lambdas unsubscribed with a new lambda never unsubscribe → store the delegate in a field: `BallDriving.cs:285-304`, `SoundManager.cs:105/115`, `OrderHandler.cs:311/319`.
- [ ] `SpawnManager` loops `i <= MAX_PLAYERS` inside an empty `catch` → `i < MAX_PLAYERS`, remove the empty catch (`SpawnManager.cs:57-78`, `:90-108`).
- [ ] `PlayerCameraResizer.Update` copies the viewport to follower cameras only once (`initalized` flag) → re-copy whenever `referenceCam.rect` changes (`PlayerCameraResizer.cs:106-123`); check P1's Icon Camera after P3/P4 join.
- [ ] `Rumbler.Start` / `OnApplicationQuit` use `Gamepad.current` without a null check → keep only `InputSystem.ResetHaptics()` (`Rumbler.cs:14-24`).
- [ ] Exactly one active AudioListener: the bootstrap scene has 4 (*Black Camera Render*, *Color Camera Render*, *Menu Audio Listener*, *Sound Manager*) → removes ~7,000 "3 audio listeners" warnings per session.
- [ ] Tests: EditMode test for `CalculateRects` (1–4 players → expected rects) and for `RemoveFromPlayerArray` (unknown player → `-1`); §R checklist.

### Task 1.2: Release-build hygiene

- [ ] Debug hotkeys → wrap in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`:
  - `HotKeys.cs:12-24` (1/2 = wave −/+, 4 = load game)
  - `OrderManager.cs:123-133` (Y = delete orders, 5 = jump to wave 3), `:202-205` (P = freeze timer)
  - `TutorialManager.cs:38`, `TutorialHandler.cs:51` (S = skip tutorial)
  - `CutsceneManager.cs:40` (L = skip cutscene), `MainMenu.cs:39` (L = kick all players)
  - `BallDriving.cs:358` (B = reset boost), `OrderHandler.cs:87` (T = drop orders), `Respawn.cs:102` (R = respawn)
- [ ] QA telemetry off in release: same define around `QAManager.SendData` (`QAManager.cs:86-104`) and the heatmap writes (`HeatmapCamera.cs:67,102`); delete `Assets/StreamingAssets/QAData.csv` and `Assets/StreamingAssets/HeatMaps/` (StreamingAssets ships inside the build).
- [ ] Settings save: replace `BinaryFormatter` (`OptionsMenu.cs:55-111`) with `JsonUtility` → `settings.json` via `File.WriteAllText`; wrap loading in `try/catch` so a corrupt file falls back to defaults.
- [ ] Player Settings: version `1.0` → `1.0.0`, icon, splash. Optional: IL2CPP backend (needs the *Windows Build Support (IL2CPP)* module + Visual Studio *Desktop development with C++*).
- [ ] Tests: EditMode round-trip test for settings JSON (save → load → same values; garbage file → defaults). A non-development build ignores all hotkeys and writes nothing next to the `.exe`.

### Task 1.3: Controllers

- [ ] Device lost/regained: wire `PlayerInput` **Device Lost / Device Regained** events on the player prefab (empty today) → pause + "Reconnect P#" on that player's viewport; resume when regained.
- [ ] Zero controllers / keyboard press on the title screen → show "Connect a controller" (today keyboard joins are silently destroyed in `PlayerInstantiate.cs:120`).
- [ ] Recommended: keyboard as one player (add a *Keyboard&Mouse* scheme to `Assets/Resources/CapstoneYear4.inputactions`, allow it in `AddPlayerReference`).
- [ ] Button prompts: device `is DualShockGamepad` (covers DualShock 4 + DualSense) → PlayStation glyphs, `SwitchProControllerHID` → Nintendo, else Xbox.
- [ ] Tests: unplug/replug each of 4 pads mid-match; Xbox, DualShock 4, DualSense and Switch Pro all join and rumble.

### Task 1.4: Settings & performance

- [ ] Options menu: add resolution, window mode, VSync, frame cap and quality preset (today: fullscreen toggle + 2 volumes).
- [ ] Quality presets for `Assets/Rendering/URP Asset.asset` (today: MSAA 8x, HDR, 4096 shadow map, SSAO + decals + outlines on every player renderer), e.g. Low = no MSAA, no SSAO, 1024 shadows; High = MSAA 4x, 2048 shadows.
- [ ] Phase-camera render texture is a fixed 1920×1080 per player (`PlayerCameraResizer.cs:87`) → size it to that player's viewport.
- [ ] Targets: 4 players at 1080p ≥ 60 fps on a GTX 1060-class GPU; Steam Deck ≥ 30 fps at 1280×800. Profile the 4-player golden-order scene first.
- [ ] Aspect ratios: `PlayerInstantiate.CalculateRects` assumes 16:9 → check 16:10 (Steam Deck) and 21:9.
- [ ] Tests: Profiler capture per preset, before/after.

### Task 1.5: Steamworks (no netcode needed)

- [ ] Steamworks partner account → $100 Steam Direct fee → App ID. New accounts wait 30 days before a first release; the Coming Soon page must be live ≥ 2 weeks before launch.
- [ ] Install Steamworks.NET (Package Manager → Add from git URL: `https://github.com/rlabrecque/Steamworks.NET.git?path=/com.rlabrecque.steamworks.net`).
- [ ] `SteamManager` in the bootstrap scene: `SteamAPI.RestartAppIfNecessary(appId)` (builds only) → `SteamAPI.Init()` → `SteamAPI.RunCallbacks()` every frame → `SteamAPI.Shutdown()` on quit. Use App ID 480 (Spacewar) + `steam_appid.txt` until yours exists.
- [ ] **Remote Play Together**: enable it in Steamworks settings and tag *Shared/Split Screen*. Friends then join your couch game over the internet with zero netcode (Steam streams the host's screen; guests' pads appear as local gamepads). This is online play on day one.
- [ ] Steam Input: set the default config to Gamepad. Test that one PlayStation pad doesn't join as **two** players (physical HID + Steam's virtual Xbox pad); if it does, opt PlayStation controllers out of Steam Input.
- [ ] Steam Cloud (Auto-Cloud): root `WinAppDataLocalLow`, path `The Boo Crew/Dead on Arrival`, pattern `settings.json`.
- [ ] Optional: achievements (first delivery, win holding the golden order), Rich Presence ("Delivering — 3 players").
- [ ] Builds via SteamPipe: `steamcmd +login <user> +run_app_build <path>\app_build_<appid>.vdf +quit`; use a `beta` branch for testers; request Steam Deck compatibility review.
- [ ] Tests: launch from the Steam client, overlay (Shift+Tab) works, a Remote Play Together session with one remote friend works.

### Task 1.6: Store & legal

- [ ] Written agreement from every contributor (IP ownership, revenue split, credits); check the school's student-IP policy.
- [ ] License audit → `LICENSES.md`: OToon (Asset Store EULA), DOTween, Cinemachine 2.1, Udar SceneField, Noisy Nodes, `Assets/Fonts`, all music/SFX.
- [ ] Store page: header 920×430, small 462×174, main 1232×706, vertical 748×896, library 600×900 + hero 3840×1240; 5+ screenshots; trailer; tags (Local Multiplayer, Shared/Split Screen PvP, Party, Racing); price; content survey.
- [ ] Submit store page + build for Valve review (a few business days each) → release.

---

## Phase 2 — Networking-ready refactor (still local-only)

Goal: identical game, but a player's identity and authority no longer live in `PlayerInput`, so Phase 3 can plug in netcode.

### Task 2.1: Player roster

- [ ] `PlayerSlot` (slot 0–3, company/colour/hat index, `isLocal`, `ownerClientId`, `PlayerInput` or `null`) + `PlayerRoster` as the single source of truth instead of `PlayerInstantiate.PlayerInputs`.
- [ ] Migrate every user of the `PlayerInput[]` array: `BeconIndicator`, `DrivingIndicators`, `BoostPadManager`, `CutoutManager`, `OrderManager`, `MainMenu`, `PlayerSelectCanvas`, `Menu/SceneManager`, `SpawnManager`, `CompassMarker`, `ScoreManager`, `QAManager`, `LoadingScreenManager`, `MenuInteractions`, `PlayerInstantiate`.
- [ ] Keep the per-slot mapping: player layer `10+slot`, camera layer `17+slot`, icon layer `24+slot`, URP renderer `slot+1` (main) / `slot+5` (phase), phasing = `Physics.IgnoreLayerCollision(9, 10+slot)`.
- [ ] Tests: EditMode tests for slot assignment (fills lowest free slot, frees on leave, max 4); §R checklist.

### Task 2.2: Split the player prefab

- [ ] `Player - Cinemachine.prefab` → **PlayerAvatar** (sphere Rigidbody, scooter/ghost model, `BallDriving`, `OrderHandler`, `Respawn`, `SoundPool`, emote bubble, compass marker) + **PlayerView** (6 cameras, Cinemachine rigs, Menu/Driving canvases, `PlayerInput`, `PlayerUIHandler`, `MenuInteractions`, `OrbitalCamera`).
- [ ] Local player = Avatar + View; remote player (Phase 3) = Avatar only.

### Task 2.3: Input abstraction

- [ ] `IDriveInput` (steer, accelerate, brake, drift, boost, camera X/Y + button events), implemented by `InputManager`; `BallDriving`, `EmoteHandler`, `OrbitalCamera` read the interface instead of `InputManager` fields.

### Task 2.4: Authority seam

- [ ] `GameAuthority.IsAuthority`: always `true` offline, host-only online.
- [ ] `GameManager`: `SetGameState` = authority request; new `ApplyGameState(GameState)` fires the existing `OnSwap*` events (listeners unchanged).
- [ ] Gate authority-only logic: order spawning + wave timers (`OrderManager.Update/InitWave`), pickup/delivery (`OrderBeacon.OnTriggerStay`), steal/clash (`OrderHandler.OnTriggerEnter/AttemptSteal`), scoring (`OrderHandler.DeliverOrder`, golden bonus in `Order.EraseOrder`), spawns (`SpawnManager`), respawn point choice (`RespawnManager`), all `Random` calls (order shuffle, drop height).
- [ ] `Time.timeScale` pause (`PlayerInstantiate.cs:699`) and end-of-round slow-mo (`OrderManager.cs:644`) → local mode only.

### Task 2.5: Scene flow seam

- [ ] Put the custom loader (`Menu/SceneManager.cs` async load + confirm) behind an `ISceneFlow` interface so Phase 3 can swap in NGO's scene manager.
- [ ] Tests: §R checklist passes unchanged.

---

## Phase 3 — Online multiplayer (Netcode for GameObjects + Steam)

### Task 3.1: Packages & transports

- [ ] `com.unity.netcode.gameobjects`: latest **1.x** (2.x needs Unity 6).
- [ ] Unity Transport for editor/LAN testing; a Steam transport for builds (the community *SteamNetworkingSockets* transport for Steamworks.NET, from `Unity-Technologies/multiplayer-community-contributions`; pin a commit that supports your NGO version).
- [ ] ParrelSync to run 2–4 editor instances; the Multiplayer Tools network simulator (Unity Transport) or *clumsy* for 150 ms / 1% loss tests.

### Task 3.2: Steam lobby flow

- [ ] Main menu → **Play Online** → *Host* (create a friends-only or public lobby, 4 slots, metadata `game=doa`, `build=<version>`) or *Join* (overlay invite via `GameLobbyJoinRequested_t`, or a lobby list filtered by `game` + `build`).
- [ ] Lobby screen reuses Player Select; the host assigns slots/companies, and players pick colour/hat.
- [ ] Host presses Start → `NetworkManager.StartHost()`; clients `StartClient()` to the host's SteamID → networked scene load.

### Task 3.3: Network player

- [ ] `NetworkPlayer` (NetworkObject on PlayerAvatar): NetworkVariables for slot, company, colour, hat, score, ready, and a flags byte (boosting / phasing / drifting / drift tier).
- [ ] Owner-authoritative movement: the owner runs `BallDriving` physics; owner-authoritative `NetworkTransform` (the `ClientNetworkTransform` pattern) with interpolation on sphere + control; non-owners make the sphere kinematic and skip `BallDriving` Update/FixedUpdate.
- [ ] Remote players' boost/phase/drift visuals and sounds come from the flags byte.

### Task 3.4: Game state, timers, scenes

- [ ] `NetworkGameState`: `NetworkVariable<GameState>`; the host sets it, every peer calls `GameManager.ApplyGameState` in `OnValueChanged`.
- [ ] Wave/game timers → host-owned `NetworkVariable<double>` end times in server time; clients compute remaining time for the UI.
- [ ] Loads via `NetworkManager.SceneManager.LoadScene`; start the cutscene on `OnLoadEventCompleted` (replaces the loading-screen confirm).

### Task 3.5: Orders

- [ ] NetworkObject on every scene-placed `Order`; NetworkVariables for state (Inactive / AtPickup / Held / Dropped / Delivered), holder slot and value.
- [ ] Only the host runs `OrderBeacon.OnTriggerStay` and `OrderManager` spawning; clients drive meshes, beacons and compass markers from `OnValueChanged`.

### Task 3.6: Steals, clashes, respawns

- [ ] Steal: the attacker's client detects the overlap while boosting → `RequestStealServerRpc(victimSlot)`; the host checks distance ≤ trigger radius + lag tolerance, attacker boosting, victim not boosting, per-pair cooldown → first valid request wins → result replicates.
- [ ] Clash: host → `ClientRpc` to both owners → each calls `BounceOff` locally.
- [ ] Respawn: the owner detects water → `ServerRpc`; the host drops that player's orders and picks a `RespawnPoint` → `ClientRpc` to the owner runs the respawn animation.
- [ ] Tests: EditMode tests for the steal arbitration rules (pure C# class); 2 clients stealing simultaneously at 150 ms → one steal, same holder everywhere.

### Task 3.7: Local-only and one-shot systems

- [ ] Not networked: pedestrians (`CivilianAgent`), kickables, DOTween animations, particles, cameras, compass UI, speed lines.
- [ ] One-shots via `ClientRpc`: boost start, drift boost, emotes (`EmoteHandler`), horn/phase sounds.
- [ ] Cutscenes: host sets the state; each client plays the Timeline locally; only the host can skip.
- [ ] Online pause = local overlay only (no `Time.timeScale`); the host's menu adds **End match**.

### Task 3.8: Disconnects & versions

- [ ] Client leaves → the host drops/erases its orders, despawns its avatar, recomputes placements.
- [ ] Host leaves → clients return to the menu with "Host left the match".
- [ ] Reject mismatched builds (lobby `build` metadata + NGO connection-approval payload).
- [ ] Tests: kill a client mid-delivery; kill the host mid-match.

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

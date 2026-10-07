# Dead on Arrival — Handoff: What's Left After Phase 1C

> **For the next session:** read this, then the roadmap (`docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Work one phase at a time:
> 1. Plan it with superpowers:writing-plans, in the format of the Phase 3G–3I plans.
> 2. Hand the plan to the user for review.
> 3. Run it inline (superpowers:executing-plans), then self-review.
> 4. Leave it uncommitted: the user commits.

## Where things stand (2026-10-06)

- **Branch `steam-phase1a`** is at `8ce31e85` "Phase 1C: display settings and keyboard play", with **Phase 4C (online safety) done on top, uncommitted** (`docs/superpowers/plans/2026-10-06-phase4c-online-safety.md`, `docs/online-safety.md`), and **Phase 4D (frame rate)** done on top of that, uncommitted (`docs/superpowers/plans/2026-10-06-phase4d-frame-rate.md`, `docs/performance.md`). The biggest remaining frame-rate gains are editor changes, listed in order in `docs/performance.md` (`EDITOR-TODO.md` section 2). All 629 tests pass.
- The user allows Sonnet agents "as you see fit" for these phases (memory: `code-review-agents`); still one test run at a time.
- **`main` holds the Steam API only** (the branch policy, below).
- **Done:**
  - Phase 1's code (1A, 1B, 1C), Phase 2 (2A–2C), Phase 3 (3A–3I) apart from the optional Task 3.9, and Phase 4A.
  - Each has a plan in `docs/superpowers/plans/`.
  - `docs/online.md` explains the online code, phase by phase.

## How to work in this repo

- **The user's rules:**
  - The user commits and pushes. Never commit, push or merge without being asked.
  - Branch policy, in the user's words: "for right now I just want the steam Api to go in main branch all other changes are to go into phase1a branch". Never check out `main` in the real project folder. Phase 4A's Steam calls went on `steam-phase1a` too (the user, 2026-10-06).
  - No subagents unless the user asks. Final reviews are self-reviews. Offer the 4-Sonnet review (memory: `code-review-agents`).
  - Editor chores go in `EDITOR-TODO.md` at the project root, as short, clean bullet points.
- **The editor is usually open,** with a ParrelSync clone at `../CapstoneYear4_clone_0` that shares `Assets/`:
  - Never edit a scene, prefab, `.inputactions` asset or ProjectSettings file. Everything is code; anything else is an editor chore.
  - `%LOCALAPPDATA%\Unity\Editor\Editor.log` is the clone's log; the main editor's can't be read.
- **Steam:**
  - `SteamManager.cs` and `SteamStartup.cs` stay untouched.
  - A Steamworks.NET bump goes to `main`, after asking.
  - Never copy DLLs from the Steamworks SDK 1.65 into `Assets/`: its `steam_api64.dll` lacks the Steam Deck check that `SteamManager.IsSteamDeck` calls.
  - The test App ID is 480 (Spacewar).
- **Line endings:** these files are `i/crlf`. Edit them with the Edit tool only, never `sed -i`:
  - `Order.cs`, `BallDriving.cs`, `PhaseIndicator.cs`, `PlayerInstantiate.cs`;
  - `MenuInteractions.cs`, `MainMenu.cs`, `GameManager.cs`, `SceneManager.cs`.
  - Check others with `git ls-files --eol <file>`.
- **Tests:**
  - `bash tools/run-tests.sh [filter]` syncs and runs the batch-mode mirror at `D:\.PersonalWork\DoA\_doa_test_mirror\CapstoneYear4`.
  - A compile error prints `NO RESULTS`.
  - Run long runs in the background. The full suite takes about 40 minutes. Check `ListAgents` first.
  - Every new file under `Assets/` gets a `.meta` (`bash tools/newmeta.sh <path>`).
- **Play Mode test rules:**
  - No lambda captures a test's local: after `EnterPlayMode`, even assigning one throws. Use recorder classes subscribed as method groups.
  - Wait by real time. Only `yield return null`.
  - Call `log.MachinesLeave()` before a machine leaves.
  - Network test ports in use: 7791–7812. The next free port is 7813.
  - A test that loads the golden round: `OnlineOrdersNetworkTests.Hosting_AMachineLeavesHoldingTheGoldenOrder_…` (a `LoadAnswerer` on the other machine's match, then time up).
  - Fake "mid-match" in the menu scene with `GameState.MainLoop`: `StartingCutscene`'s listeners need the match scene.
- **The game's own global `SceneManager` class** shadows Unity's: write `UnityEngine.SceneManagement.SceneManager` for Unity's.
- **Docs:** `docs/online.md`, `docs/testing.md`, `docs/dev-hotkeys.md`, `docs/player-prefab.md`, `docs/steam/`, `LICENSES.md`.

## What's left, in order

### 1. Art, then its small code tasks (`EDITOR-TODO.md` section 14)

- Phase 4A (`03818e1c`) and Phase 1C (`8ce31e85`) are committed. Phase 4A's dashboard half is in Phase 4B below.
- Still waiting on art:
  - Options rows for the display settings and Quality. The code reads and saves them already: each row's selector is wired like the existing rows (`OptionsMenu`).
  - Keyboard button prompts.
  - PlayStation and Nintendo button prompts (roadmap Task 1.3). They need those button sprites drawn first, then a small code task to pick the sprites by device.

### 2. Phase 4B: launch operations (mostly the user)

- **When the App ID arrives,** on `main` and after asking: `SteamStartup.APP_ID`, `steam_appid.txt`, and `docs/steam/steampipe/app_build.vdf` (`APP_ID`, `WINDOWS_DEPOT_ID`).
- **The Steamworks dashboard:**
  - Remote Play Together, and the Shared/Split Screen tags.
  - Steam Input: Gamepad as the default config. Then test that one PlayStation pad doesn't join as two players.
  - Steam Cloud (Auto-Cloud: `WinAppDataLocalLow`, `The Boo Crew/Dead on Arrival`, `settings.cfg`).
  - The achievements (`FIRST_DELIVERY`, `GOLDEN_WIN`: names, texts and icons in `docs/steam/in-game-features.md`), and the Rich Presence file (`docs/steam/rich-presence-english.vdf`). `EDITOR-TODO.md` section 13.
- **Builds:** SteamPipe (`docs/steam/steampipe/README.md`), a `beta` branch for testers, the Steam Deck review, and Steam Playtest for open testing.
- **The store page:**
  - The art sizes are in roadmap Task 1.6, plus screenshots and a trailer.
  - Tags: add Online PvP once online ships.
  - The text draft is `docs/steam/store-page-draft.md`.
- **Legal:**
  - A written agreement from every contributor (IP, revenue split, credits), and the school's student-IP policy.
  - `LICENSES.md` still has 13 ⚠️/❌ items. The urgent ones are the two fonts in use (Sobiscuit, jcandlestick), DOTween, Udar SceneField, OToon, which audio is original, and unused files to delete.
- **Valve's review** of the store page and the build (a few business days each).

### 3. Optional, after launch: Task 3.9, online plus couch

Several local players per machine online. Each local `PlayerInput` gets its own online player, and each machine split-screens only its own players.

## The user's checklist (no code)

- **`EDITOR-TODO.md` is the live list:**
  - the two-editor checks for Phases 3F–3I, and the slow-PC load time (Phase 3I);
  - the optional "Normal Positions" tidy-up;
  - the Phase 3D Steam checks on two PCs.
- **Roadmap Phase 1 partner steps:**
  - Remove the dead Player Left listener.
  - Player Settings: version `1.0.0`, icon, splash.
  - The Quality row's art.
  - Profile 4 players in the golden round (F1 ×3, F4).
  - Check the HUD at 16:10 and 21:9.
  - Test 4 controllers, including DualShock 4, DualSense and Switch Pro.
  - The "one AudioListener" item may be done already: the user turned off two camera listeners in the Phase 3A commit. Confirm, then tick it.
- **An open issue:**
  - On 2026-09-24, controllers stopped responding, game-wide, with Steam running: a DualSense, and later an Xbox One pad.
  - The suspect is Steam Input. `SteamManager` starts Steam on every launch, so Steam Input is in play even offline.
  - It's unconfirmed: retest with Steam's controller support switched off. It ties in with the Steam Input item above.

## Deferred minors still open

Collected from the plan ledgers. None blocks a release.
- **Phase 4D:**
  - A first-ever `GraphicsQuality.ApplyFor(3-4)` resets the player count to 1 (harmless in play).
  - An `ApplyFor` after `Application.quitting` in the editor re-applies with nothing to restore it (in memory only).
  - No warning if URP's renderer list field is ever missing (SSAO would stay on).
  - `CutoutHandler` would log every physics step for a ball with a `BallDriving` but no `OrderHandler` (none exists).
  - A camera toggled after the players' `LateUpdate` in the same frame would leave one blank frame.
  - The F6 overlay's 1%-low allocates each `OnGUI` (dev builds only).
  - Pedestrians' animators cull off-screen: check the ragdoll swap reads nothing from culled bones.
- **Phase 4C:**
  - A kicked player is still in the lobby and can reconnect and flood again (remember the kicked Steam ID for the session).
  - The kick reason may not reach the kicked player; they may see "connection lost".
  - Release builds warn that `OnlinePlay.directAddress`/`directPort` are never assigned.
  - A stream of NaN poses still makes Unity log an error per frame on the client (the log's 5 MB cap bounds it).
  - A host hitch over 5 s during a friend's approval would drop them (`ClientConnectionBufferTimeout`).
- **Phase 1C:**
  - The display settings are applied again on every return to the menu, so exclusive fullscreen may flicker once each time.
  - A keyboard tap's join relies on `KeyboardControls` hearing the press before the scene's `PlayerInputManager` (it subscribes before the first scene loads); that order isn't enforced.
- **Phase 4A:**
  - Two launches within one second share a session log name, so the second overwrites the first.
  - The opening cutscene shows "Delivering" even when the tutorial follows it.
  - The `.vdf` uses em dashes: check that Steam's upload takes them (UTF-8).
- **Phase 3I:**
  - The host picking Main Menu while a slow client is still activating the game scene: the client follows to the menu, but first shows the match starting for the ~8 s trip (the host's queued states release when its scene comes up).
  - `OnlineGame.ShowEnd` returns to the menu inside `OnlineSession.Ended`: anything that throws on the way would skip later listeners (the Steam lobby's leave). The known thrower is fixed.
  - No joining-side test of a golden-order release (the client replays the existing `EraseGold` and `Spawn` changes).
  - `OrderHandler.ResetHandler`'s `-= () => …` is a 2024 no-op (a fresh lambda never matches), so `InitHandler`'s lambdas pile up on each match's order manager.
  - `LobbyRules.VersionOf(null)` would throw; Steam returns "" for a missing key.
- **Phase 3H:**
  - Another machine's death sound plays on the SFX mixer group; its own machine plays it on "Player".
  - Another machine's phasing sound plays over its boost sound, instead of replacing it.
- **Phase 3D:**
  - A double "Join Game" can make Steam enter the lobby twice, and the second answer then leaves it.
  - `SteamLobbyService` doesn't check for an invalid `SteamAPICall_t`. If Steam refuses at once, Y stays blocked until the player leaves player select.
  - Y is wired in code (`PlayerUIHandler`). If the prefab ever gets the action too, Y fires twice.
  - The editor's Online menu can host a second session beside `OnlinePlay`'s (editor only).
  - `LobbyPrompt` and `ControllerPrompts` share sorting order 1000.
  - After `UseDirect`, the replaced lobby stays subscribed (tests and editor tools only).
- **Phase 3C:**
  - Another machine's ball is kinematic here, so bumps with it are one-sided.
  - `OnlineDriving` looks up each seat's `BallDriving` every frame. That could be cached.
  - The host picking Main Menu while a slow client is still activating the game scene: Phase 3I covers it (see Phase 3I above).
- **Phases 1A, 1B and 2A:**
  - Tests reach private members by reflection.
  - `QAManager` catches only `IOException`.
  - `SteamManager` has no guard against a second `SteamAPI.Init` if domain reload is ever turned off.
  - Keyboard presses spawn and destroy a player prefab.
  - A few unused usings, and some test-message wording.
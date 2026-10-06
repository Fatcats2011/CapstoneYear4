# Dead on Arrival — Handoff: What's Left After Phase 3I

> **For the next session:** read this, then the roadmap (`docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Work one phase at a time:
> 1. Plan it with superpowers:writing-plans, in the format of the Phase 3G–3I plans.
> 2. Hand the plan to the user for review.
> 3. Run it inline (superpowers:executing-plans), then self-review.
> 4. Leave it uncommitted: the user commits.

## Where things stand (2026-10-02)

- **Branch `steam-phase1a`** is at `651311e9` "Phase 3H: one-shots, cutscenes and pause online", with **Phase 3I done on top, uncommitted** (`docs/superpowers/plans/2026-10-01-phase3i-disconnects-and-versions.md`): all 514 tests pass. See "1. Commit Phase 3I" below.
- **`main` holds the Steam API only** (the branch policy, below).
- **Done:**
  - Phase 1's code (1A, 1B), Phase 2 (2A–2C) and Phase 3 (3A–3I), apart from the optional Task 3.9.
  - Each has a plan in `docs/superpowers/plans/`.
  - `docs/online.md` explains the online code, phase by phase.

## How to work in this repo

- **The user's rules:**
  - The user commits and pushes. Never commit, push or merge without being asked.
  - Branch policy, in the user's words: "for right now I just want the steam Api to go in main branch all other changes are to go into phase1a branch". Never check out `main` in the real project folder.
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
  - Network test ports in use: 7791–7809. The next free port is 7810.
  - A test that loads the golden round: `OnlineOrdersNetworkTests.Hosting_AMachineLeavesHoldingTheGoldenOrder_…` (a `LoadAnswerer` on the other machine's match, then time up).
  - Fake "mid-match" in the menu scene with `GameState.MainLoop`: `StartingCutscene`'s listeners need the match scene.
- **The game's own global `SceneManager` class** shadows Unity's: write `UnityEngine.SceneManagement.SceneManager` for Unity's.
- **Docs:** `docs/online.md`, `docs/testing.md`, `docs/dev-hotkeys.md`, `docs/player-prefab.md`, `docs/steam/`, `LICENSES.md`.

## What's left, in order

### 1. Commit Phase 3I (the user)

- Phase 3I (roadmap Task 3.8) is implemented and tested, uncommitted:
  - When the host goes, everyone goes back to the menu, from any point of a match, loads included.
  - A leaver's golden order goes back to its start, and the golden round goes on.
  - The lobby's build tag carries the Netcode setup.
  - Direct sessions wait 90 s for a silent machine, and every load logs its time and longest frame.
- Fixed on the way: a delivery's throw went with a scooter that left mid-throw; erasing the golden order "without delivering" delivered it; `Fader` logged an error in the golden round; a return to the menu from the menus (or mid-load) threw in `OrderHandler.ResetHandler`.
- The user: `EDITOR-TODO.md` sections 1–3 (two editors, then a slow PC's load time), then commit ("Phase 3I: disconnects and versions").
- Phase 3 is then done, apart from the optional Task 3.9.

### 2. Phase 4A: Steam features in the game (code)

From roadmap Task 1.5 (its optional achievements and Rich Presence) and Phase 4:
- **Achievements and stats:**
  - Examples from the roadmap: first delivery; win holding the golden order.
  - Steam unlocks an achievement for the user on that PC (`SteamUserStats.SetAchievement`, then `StoreStats`). So "unlocked by the host" means: the host decides, then tells each machine to unlock for its own player.
  - Offline, each local match unlocks for the PC's Steam user.
  - Put them behind a small seam with a fake for tests, as `ILobbyService` does.
  - Real ones need the game's own App ID: achievements are defined per app in the Steamworks dashboard, and app 480's are Spacewar's.
- **Rich Presence:** for example "Delivering — 3 players" (`SteamFriends.SetRichPresence`, plus a localization file uploaded in Steamworks).
- **Opt-in crash and disconnect logs** in `Application.persistentDataPath` (never `StreamingAssets`): session ends with their reasons, errors, and the Phase 3I load lines. "Opt-in" needs a switch: a menu row (art), or a launch option.
- **Ask first:** these call the Steam API. Does the branch policy put them on `main`, or on `steam-phase1a` with the gameplay hooks they need?

### 3. Phase 1C: settings and controls (code first, art after)

From roadmap Tasks 1.3 and 1.4:
- **Display options:** resolution, window mode, VSync and frame cap, saved in `settings.cfg` (`GameSettings`) and applied at launch. The Options screen's rows are hand-lettered art, so the code can land first and the rows follow when there's art. The Quality row is in the same state: its code is done, and its row needs art.
- **Keyboard as a player** (recommended, not required):
  - It needs a Keyboard&Mouse scheme in `Assets/Resources/CapstoneYear4.inputactions`. That's an asset edit, so it's an editor chore, or done while the editors are closed.
  - `AddPlayerReference` must allow the scheme, and the prompts need keyboard glyphs.
  - Today, keyboard presses make Unity spawn and destroy a whole player prefab (a Phase 1B minor).
- **Button glyphs per controller** (PlayStation and Nintendo): needs those button sprites drawn first.

### 4. Phase 4B: launch operations (mostly the user)

- **When the App ID arrives,** on `main` and after asking: `SteamStartup.APP_ID`, `steam_appid.txt`, and `docs/steam/steampipe/app_build.vdf` (`APP_ID`, `WINDOWS_DEPOT_ID`).
- **The Steamworks dashboard:**
  - Remote Play Together, and the Shared/Split Screen tags.
  - Steam Input: Gamepad as the default config. Then test that one PlayStation pad doesn't join as two players.
  - Steam Cloud (Auto-Cloud: `WinAppDataLocalLow`, `The Boo Crew/Dead on Arrival`, `settings.cfg`).
  - The achievement definitions, and the Rich Presence strings.
- **Builds:** SteamPipe (`docs/steam/steampipe/README.md`), a `beta` branch for testers, the Steam Deck review, and Steam Playtest for open testing.
- **The store page:**
  - The art sizes are in roadmap Task 1.6, plus screenshots and a trailer.
  - Tags: add Online PvP once online ships.
  - The text draft is `docs/steam/store-page-draft.md`.
- **Legal:**
  - A written agreement from every contributor (IP, revenue split, credits), and the school's student-IP policy.
  - `LICENSES.md` still has 13 ⚠️/❌ items. The urgent ones are the two fonts in use (Sobiscuit, jcandlestick), DOTween, Udar SceneField, OToon, which audio is original, and unused files to delete.
- **Valve's review** of the store page and the build (a few business days each).

### 5. Optional, after launch: Task 3.9, online plus couch

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
- The old ledger folders under `.superpowers/sdd/` (Phases 1A, 1B, 2A, 3C, 3D) hold nothing else. They're git-ignored scratch, and can be deleted.

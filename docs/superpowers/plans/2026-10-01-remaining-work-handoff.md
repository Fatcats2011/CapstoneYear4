# Dead on Arrival — Handoff: What's Left After Phase 1C

> **For the next session:** read this, then the roadmap (`docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Work one phase at a time:
> 1. Plan it with superpowers:writing-plans, in the format of the Phase 3G–3I plans.
> 2. Hand the plan to the user for review.
> 3. Run it inline (superpowers:executing-plans), then self-review.
> 4. Leave it uncommitted: the user commits.

## Where things stand (2026-10-08)

- **Branch `steam-phase1a`** is at `a1c8c76d` "Local Play and Online Play on the title screen" (Phases 3J and 5A, and the title screen), with **Phase 5B (the last deferred minors) done on top, uncommitted** (`docs/superpowers/plans/2026-10-08-phase5b-deferred-minors.md`, `docs/online.md` Phase 5B). Another session's achievements work (Steam feats files, `OrderHandler`, `BallDriving`, `Respawn`, `TutorialManager`) is uncommitted beside it. The biggest remaining frame-rate gains are editor changes, listed in order in `docs/performance.md` (`EDITOR-TODO.md` section 4). All 760 tests pass (with the achievements work in the tree).
- The user allows Sonnet agents "as you see fit" for these phases (memory: `code-review-agents`); still one test run at a time.
- **`main` holds the Steam API only** (the branch policy, below).
- **Done:**
  - Phase 1's code (1A, 1B, 1C), Phase 2 (2A–2C), Phase 3 (3A–3J; 3J is roadmap Task 3.9), Phases 4A, 4C and 4D, Phase 5A (the deferred minors), Local Play / Online Play on the title screen (its scene art is `EDITOR-TODO.md` section 1), and Phase 5B (the last deferred minors).
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

### 1. Art, then its small code tasks (`EDITOR-TODO.md` section 16)

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
  - The achievements (`FIRST_DELIVERY`, `GOLDEN_WIN`: names, texts and icons in `docs/steam/in-game-features.md`), and the Rich Presence file (`docs/steam/rich-presence-english.vdf`). `EDITOR-TODO.md` section 15.
- **Builds:** SteamPipe (`docs/steam/steampipe/README.md`), a `beta` branch for testers, the Steam Deck review, and Steam Playtest for open testing.
- **The store page:**
  - The art sizes are in roadmap Task 1.6, plus screenshots and a trailer.
  - Tags: add Online PvP once online ships.
  - The text draft is `docs/steam/store-page-draft.md`.
- **Legal:**
  - A written agreement from every contributor (IP, revenue split, credits), and the school's student-IP policy.
  - `LICENSES.md` still has 13 ⚠️/❌ items. The urgent ones are the two fonts in use (Sobiscuit, jcandlestick), DOTween, Udar SceneField, OToon, which audio is original, and unused files to delete.
- **Valve's review** of the store page and the build (a few business days each).

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

Phase 5B (2026-10-08, `docs/superpowers/plans/2026-10-08-phase5b-deferred-minors.md`) closed the list Phases 5A and 3J left. What's left:
- **Known limits, left by choice:**
  - No cap on seats per machine (the user, 2026-10-08): `docs/online.md` Known limits.
  - A forged teleport pose isn't held back (Netcode applies it before any hook): `docs/online-safety.md`.
  - `PlayerCameraResizer`'s `[DefaultExecutionOrder(1000)]` also moves its `Start` and `Update`; nothing depends on the old order.
- **The "stuck scene" from Phase 5A wasn't a bug:** a machine the host takes back to the menu while its match scene comes up gets there after the loading screen's 8 s delay. Phase 5A's checks gave up before the delay ended. A regression test now covers it.
- **Not testable here** (two-PC or two-editor checks in `EDITOR-TODO.md` section 2): the bump report from a real collision (`BumpReporter`), the kick reason's linger and the ban (Steam), the `SteamManager` guard, and a client's seat asks from inside a running game.

# Title Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A new title screen (logo top left on a purple-black backdrop, 12 cycling skits of dressed 3D scooter ghosts) with every old entry still working, plus an engine rev on the boot intro.

**Architecture:** Pure rule classes (wardrobe deck, skit playlist) with EditMode tests. A `TitleStage` in the menu scene owns a pool of 4 `PlayerAvatar` ghosts and a camera that renders to a transparent render texture shown on the title canvas between the backdrop and the logo. Skits are coroutines over the stage. An editor builder makes all scene changes.

**Tech Stack:** Unity 2022.3.62f3, URP, C#, DOTween, NUnit EditMode tests (`tools/run-tests.sh`, which runs in the mirror project), Python for generated art and audio.

**Spec:** `docs/superpowers/specs/2026-11-10-title-screen-design.md`

## Global Constraints

- Branch `steam-phase1a`. Never check out `main`. **Commit only when the user asks**; tasks end at "tests pass", not at a commit.
- No new wardrobe: only the 7 hat assets in `Assets/Scriptable Ojects/Player Hats`, the 6 in `Player Colors`, the 4 in `Company Information`.
- `MainMenu.cs`, `TitleEntries.cs`, `MouseMenus.cs` and the entry objects are not edited.
- Scene changes go through the editor builder, never hand-edited YAML. Menu scene: `Assets/Scenes/Alex Player Testing.unity`.
- New runtime code: `Assets/Scripts/Menu/Title/`. Editor code: `Assets/Scripts/Editor/`. Tests: `Assets/Tests/Editor/`. New `.meta` files via `tools/newmeta.sh` where Unity isn't making them.
- Members tests need are `internal` (the test assembly sees them).
- Tests run one at a time: there is one mirror. `tools/run-tests.sh <filter>`.
- GDD and TDD are updated in this work (CLAUDE.md), each with a Revision History line and a minor version bump.

## Review Focus

1. Leaving the title mid-skit (to player select, Options, Credits) and coming back: no stale package, no half-dressed or frozen ghost, no leftover tween. → Task 3 test `LeavingMidSkit_ResetsEveryActor`.
2. Screens that aren't 16:9 (ultrawide, 16:10, Steam Deck 1280×800): ghosts must still enter and leave past the real screen edges, and the render texture must not stretch. → Task 3 test `OffScreenX_IsPastTheCameraEdge_AtAnyAspect`.
3. Opening on player select instead of the title (online, after a match): the show must not start or render. → Task 3 test `StartsIdle_WhenTheSceneOpensOnPlayerSelect`.
4. A hat asset with `displayHat` false ("NoHat") in Hat Thief: there is nothing to steal. → Task 5 test `HatThief_VictimAlwaysWearsAHat`.
5. Mouse over the full-screen ghost image and backdrop: they must not swallow clicks or hover meant for the entries. → Task 4 test `NewTitleImages_AreNotRaycastTargets`.

---

### Task 1: Show rules (wardrobe deck and skit playlist)

**Files:**
- Create: `Assets/Scripts/Menu/Title/TitleWardrobe.cs`, `Assets/Scripts/Menu/Title/TitlePlaylist.cs`
- Test: `Assets/Tests/Editor/TitleWardrobeTests.cs`, `Assets/Tests/Editor/TitlePlaylistTests.cs`

**Interfaces — Produces:**
```csharp
public struct TitleOutfit { public int hat, colour, company; }
public class TitleWardrobe {
    public TitleWardrobe(int hats, int colours, int companies, System.Random random);
    /// count outfits for one skit: hats all differ, colours all differ, no (hat,colour) pair repeats
    /// until every pair has been dealt, none worn in the previous Deal
    public TitleOutfit[] Deal(int count);
}
public class TitlePlaylist {
    public TitlePlaylist(int skits, System.Random random);
    public int Next();   // every index once per round, reshuffled each round, never the same twice running
}
```

- [ ] **Step 1: Write failing tests.** `TitleWardrobeTests`: `Deal_HatsAndColoursDifferWithinASkit` (7,6,4; 200 deals of 4), `Deal_NoPairRepeatsBeforeAllFortyTwoAreWorn` (deals of 2: first 21 deals give 42 distinct pairs), `Deal_SharesNoPairWithThePreviousDeal` (500 deals of 4, seeds 0–9), `Deal_CompanyIsInRange`, `Deal_MoreThanHatsOrColours_Throws` (count 7 with 6 colours → `ArgumentOutOfRangeException`). `TitlePlaylistTests`: `Next_PlaysAllTwelveBeforeAnyRepeat`, `Next_NeverTheSameTwiceRunning` (1000 draws, seeds 0–9), `Next_OneSkit_AlwaysZero`.
- [ ] **Step 2:** `tools/run-tests.sh "TitleWardrobeTests|TitlePlaylistTests"` → fails to compile (types missing).
- [ ] **Step 3: Implement.** Wardrobe: a shuffled deck of all hat×colour pairs; a deal draws from the deck the first pairs that clash with nothing already picked this deal or worn last deal; when the deck can't complete a deal, the rest is reshuffled back in (excluding last deal's pairs).
- [ ] **Step 4:** Same command → all pass.

### Task 2: Backdrop art and engine clip (generated assets)

**Files:**
- Create: `tools/title-backdrop.py` → `Assets/Sprites/Title/TitleBackdrop.png` (1920×1080: `#07040D` at the edges to `#2A1147` glow centred behind the logo at 22 % across, 30 % down, plus vignette and 1 % noise against banding) and `Assets/Sprites/Title/TitleFog.png` (512×512 seamless soft cloud, white with alpha)
- Create: `tools/intro-engine.py` → `Assets/Audio/SFX/IntroEngine.wav` (from `Assets/Audio/TEST/78651__justinbw__moped-motor.wav`: the loudest 2.6 s window, 0.15 s fade in, 0.6 s fade out, peak-normalised to −3 dB, mono 44.1 kHz 16-bit)

- [ ] **Step 1:** Write both scripts (Pillow + numpy; Python `wave` for audio), in the style of `tools/achievement-icons.py`.
- [ ] **Step 2:** Run both; view the PNG; check the WAV is 2.6 s and not clipped (script prints duration and peak).

### Task 3: The stage (actors, pool, show loop)

**Files:**
- Create: `Assets/Scripts/Menu/Title/TitleActor.cs`, `TitleStage.cs`, `TitleSkit.cs`, `TitleBackdrop.cs`
- Test: `Assets/Tests/Editor/TitleStageTests.cs` (Play Mode in an empty scene with the stage built by `TitleStage.CreateForTest`, as `RemoteAvatarTests` does for avatars)

**Interfaces — Consumes:** Task 1 types; `ScooterLook.ShowCompany/ShowColour/ShowHat`; `RemoteAvatar`'s way of switching owner parts off; animator parameters `Speed` (float), `End Status` (int), `StealLeft` (trigger); `GameManager.OnSwapMenu` / `OnSwapAnything`.

**Produces:**
```csharp
public abstract class TitleSkit {
    public abstract string Name { get; }
    public abstract int Cast { get; }                       // 2–4
    public abstract IEnumerator Play(TitleStage stage, TitleActor[] cast);
}
public class TitleActor : MonoBehaviour {
    public Transform Model { get; }                         // lean, wheelie, wobble go here; the root is the path
    public void Wear(PlayerHatInformationSO hat, PlayerColorInformationSO colour, CompanyInformation company);
    public PlayerHatInformationSO Hat { get; }
    public void SetHat(PlayerHatInformationSO hat);        // Hat Thief only
    public void Carry(Transform package); public Transform Drop(); public Transform Package { get; }
    public void Grab(); public void Cheer(); public void Clap(); public void Slump(); public void Drive(float speed01);
    public void Park();                                     // off screen, package dropped, model pose reset, tweens killed
}
public class TitleStage : MonoBehaviour {
    public const int POOL = 4; public const float LANE_Z = 0f;
    public float OffScreenX { get; }                        // |x| just past the camera's edge at LANE_Z, plus a scooter length
    public float EdgeX { get; }                             // the visible edge at LANE_Z
    public Transform TakePackage(int size /*0 small,1 medium,2 large*/, bool golden = false);
    public void ReturnPackage(Transform package);
    public bool Running { get; }
    internal void Begin(); internal void Halt();            // Begin: fresh skit; Halt: kill it, park all, camera off
    internal TitleSkit Current { get; }
    internal static TitleStage CreateForTest(...);         // prefab + asset arrays, no scene
}
public class TitleBackdrop : MonoBehaviour { }             // scrolls the fog RawImage's uvRect slowly
```
Stage fields the builder wires: `avatarPrefab`, `packagePrefabs[3]`, `hats[]`, `colours[]`, `companies[]`, `stageCamera`, `goldenMaterial`. Gap between skits 1.5 s. Stage coordinates: x across the screen, y up, camera looking along +z at the lane.

- [ ] **Step 1: Failing tests:** `Pool_IsFourGhosts_WithOwnerPartsOff` (BallDriving, Respawn, PhaseIndicator, SoundPool disabled; ball kinematic), `Wear_DressesTheGhost` (hat mesh and colour material match), `Begin_PlaysASkit_WithACastDressedDifferently`, `LeavingMidSkit_ResetsEveryActor` (Halt mid-skit: all actors at |x| ≥ OffScreenX, no package parented to any, camera disabled, `DOTween.TotalActiveTweens()` for stage targets 0), `StartsIdle_WhenTheSceneOpensOnPlayerSelect`, `Runs_OnlyInGameStateMenu` (Options, Credits, PlayerSelect halt; Menu begins), `OffScreenX_IsPastTheCameraEdge_AtAnyAspect` (aspects 4:3, 16:10, 16:9, 21:9, 32:9), `Packages_ComeBackToThePool`.
- [ ] **Step 2:** Run `TitleStageTests` → fail.
- [ ] **Step 3: Implement.** Actors are built switched off like `RemoteAvatar.Create`; also disable every `Collider` and any `AudioSource`/`AudioListener` under the avatar. The stage listens to `OnSwapAnything` and compares `GameManager` state to `GameState.Menu`. The render texture is created at runtime at screen size (ARGB32, depth 24) and re-made when the screen size changes; the canvas `RawImage` gets it.
- [ ] **Step 4:** Run `TitleStageTests` → pass.

### Task 4: Editor builder and the scene

**Files:**
- Create: `Assets/Scripts/Editor/TitleScreenBuilder.cs` (menu `Tools/Dead on Arrival/Title Screen/Build`, also callable as `TitleScreenBuilder.BuildFromCommandLine`)
- Modify (by the builder): `Assets/Scenes/Alex Player Testing.unity`
- Delete, after a reference search: `Assets/Scripts/Menu/MenuGhostReveal.cs`, `Assets/Animations/Menu Animations/ScreenAnim/MenuGhosts.controller` and its clips, the old ghost render textures and reveal material if nothing else uses them
- Test: `Assets/Tests/Editor/TitleScreenSceneTests.cs` (opens the scene, no Play Mode, as `GameSceneSpawnTests` does)

**The builder, re-runnable:**
- On `Main Menu Canvas`: remove `Background Purple`, `Background Pink`, `Menu Ghost Reveal`; add as first children, in order, `Title Backdrop` (Image, `TitleBackdrop.png`, stretched), `Title Fog` (RawImage, `TitleFog.png`, alpha 0.10, `TitleBackdrop`), `Title Ghosts` (RawImage, stretched); all three `raycastTarget = false`. `Logo` and everything after it keep their order and transforms. Add `Title Fade` (black Image, last child, not a raycast target) that `TitleStage` fades from 1 to 0 over 0.5 s on first show.
- Remove root `Menu Ghost Animations`. Add root `Title Stage` at the old rig's position (-1119.75, 1.69, -9.18) with `TitleStage`, a child camera (perspective, FOV 30, clear to (0,0,0,0), no post-processing, culling mask = a `TitleStage` layer if a free user layer exists, else everything), a key light and a green/purple rim light restricted to the same layer where possible.
- Wire the asset arrays by loading every asset in the three wardrobe folders, sorted by name.
- Save the scene.

- [ ] **Step 1: Failing tests:** `MainMenu_StillHasItsFiveEntriesCanvasesAndSelector` (5 non-null `selectorObjects`, `selector`, `menuGhostImage`, three canvases, `p1ConnectedController`), `TitleCanvas_DrawOrder_BackdropFogGhostsThenLogoThenEntries`, `NewTitleImages_AreNotRaycastTargets`, `OldGhostRig_IsGone`, `Stage_HasSevenHatsSixColoursFourCompanies_AndItsPrefabs`, `Logo_IsWhereItWas` (anchored position (-623.1, 334.53) ± 0.1, size unchanged), `OptionsAndCredits_KeepTheirBackgrounds`.
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** Write the builder. Run it in the **mirror** (`Unity -batchmode -projectPath <mirror> -executeMethod TitleScreenBuilder.BuildFromCommandLine -quit`) so the open editor keeps its lock, then copy the changed scene (and any new assets with their metas) back into the real project. If the user's editor has the menu scene open it will offer to reload it.
- [ ] **Step 4:** Search scenes, prefabs and code for the old rig's script, controller, render textures and material GUIDs; delete only the unused.
- [ ] **Step 5:** Run `TitleScreenSceneTests`, then `MainMenuTests|TitleEntriesTests|MouseMenusTests|MenuButtonsTests` → pass.

### Task 5: The 12 skits

**Files:**
- Create: `Assets/Scripts/Menu/Title/Skits/TitleSkitTools.cs` (shared moves: `Ride(actor, fromX, toX, y, seconds, ease)`, `Lean`, `Wheelie`, `Donut`, `Wobble`, `Hop`, `Toss(package, from, to, height, seconds)`, `Puff(position)`, `Zzz(actor)`), and `TheSnatch.cs`, `PassTheParcel.cs`, `WrongWay.cs`, `ShowOff.cs`, `HatThief.cs`, `Tower.cs`, `TugOfWar.cs`, `ChainChase.cs`, `AirMail.cs`, `Sneak.cs`, `Formation.cs`, `GoldenParcel.cs`
- Modify: `TitleStage.cs` — `internal static TitleSkit[] AllSkits()` returns the 12 in the spec's order
- Test: `Assets/Tests/Editor/TitleSkitsTests.cs`

**Consumes:** Task 3's `TitleSkit`, `TitleActor`, `TitleStage`. Each skit follows its paragraph in the spec. Casts: Snatch 2, Pass the Parcel 3, Wrong Way 4, Show-off 2, Hat Thief 2, Tower 2, Tug of War 2, Chain Chase 4, Air Mail 2, Sneak 2, Formation 4, Golden Parcel 4. Each runs 5–9 s and ends with its whole cast past `OffScreenX`.

- [ ] **Step 1: Failing tests:** `ThereAreTwelve_WithDistinctNames`, `EveryCast_IsTwoToFour`, `EverySkit_PlaysThrough_AndLeavesTheStageClean` (Play Mode, `Time.timeScale = 8`, each skit by index: no error logged, finishes within 12 s of game time, all actors past `OffScreenX`, all packages back in the pool), `EverySkit_KeepsGhostsOnTheLaneSide` (no actor nearer the camera than the near clip + 1), `HatThief_VictimAlwaysWearsAHat` (100 seeded runs: the victim's dealt hat has `displayHat`; if the deal gave none the stage swaps in the first unused hat), `HatThief_PutsTheHatsRightAfterwards`, `StealingSkits_MoveThePackageBetweenActors` (Snatch, Pass the Parcel, Chain Chase, Golden Parcel: the carrier at the end differs from the carrier at the start).
- [ ] **Step 2:** Run → fail.
- [ ] **Step 3:** Write `TitleSkitTools`, then the skits. Tweens are linked to their target (`SetLink`) so `Park` and `Halt` kill them.
- [ ] **Step 4:** Run `TitleSkitsTests|TitleStageTests` → pass.

### Task 6: Engine sound on the boot intro

**Files:**
- Modify: `Assets/Scripts/Menu/SplashScreen.cs`
- Modify (by `TitleScreenBuilder`, menu `…/Title Screen/Wire Intro Sound`): `Assets/Scenes/SplashScreen.unity`
- Test: `Assets/Tests/Editor/SplashScreenTests.cs`

**Produces:** `SplashScreen` gains `[SerializeField] internal AudioClip engineClip; [SerializeField] internal float engineDelay;` and plays the clip once through an `AudioSource` it adds, `engineDelay` seconds after `Start`, at `IntroVolume()` = the saved overall × sound-effects volume read the way the menu scene's `GameSettings` reads `settings.cfg` (0–1; 0.6 when there is no file yet). `engineDelay` = the time of the wiper's first position key in `Animate.anim`, minus 0.15 s, floored at 0.

- [ ] **Step 1: Failing tests:** `Scene_HasTheEngineClipAndDelay`, `IntroVolume_FollowsTheSavedSettings` (mute → 0; no file → 0.6), `Engine_PlaysOnce_AfterTheDelay` (Play Mode, splash scene; restore `settings.cfg` afterwards as `ResolutionRowMenuTests` does).
- [ ] **Step 2:** Run → fail. **Step 3:** Implement and wire (builder in the mirror, scene copied back). **Step 4:** Run → pass.

### Task 7: See it, then the whole suite

- [ ] **Step 1:** Add `TitleScreenCapture` (EditMode test run only by name, like `HitchProbe`): plays the menu scene in the mirror, and for each skit index saves three frames (start, middle, end) to `Logs/title/<nn>-<name>-<k>.png` at 1920×1080, plus one frame each at 1280×800 and 3440×1440.
- [ ] **Step 2:** Run it; look at every frame. Fix what reads badly (ghost size, lighting against the backdrop, anything crossing the entries illegibly, timing), re-run. Repeat until every skit reads.
- [ ] **Step 3:** `tools/run-tests.sh` (everything) → no failures.
- [ ] **Step 4:** Give the user the manual checks as short bullets: each entry by pad, keyboard and mouse; back out of player select, Options, Credits; watch a full cycle; hear the intro.

### Task 8: Documents

- [ ] GDD (`docs/design/DoA GDD.docx`): the title screen section (backdrop, logo, the ghost show and its 12 skits, wardrobe rule) and the boot intro sound; replace or remove any title-screen figure that no longer matches; version bump and Revision History line.
- [ ] TDD (`docs/design/DoA TDD.docx`): `TitleStage`, `TitleActor`, `TitleSkit`, `TitleWardrobe`, `TitlePlaylist`, `TitleScreenBuilder`, the render-texture compositing, the generator scripts, the new tests; note `MenuGhostReveal` removed; version bump and Revision History line.
- [ ] `docs/testing.md`: the new test classes. `docs/unused-assets.md` if it lists the deleted or newly used files.
- [ ] Validate both `.docx` with the docx skill's validator.

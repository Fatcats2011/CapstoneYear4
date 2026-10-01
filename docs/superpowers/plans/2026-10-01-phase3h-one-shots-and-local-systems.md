# Phase 3H — One-Shots, Cutscenes and Pause Online Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Online, every machine hears and sees what other players' scooters do in an instant (a boost, a drift boost, a full horn, phasing, falling in the water, a steal's whoosh, rising from the grave). The host's match moments (a cutscene ending, the wave bells, time up) happen on every machine. Pausing is a local menu that never stops the match.

**Architecture:**
- **One-shots travel as cues.**
  - The calls that play a scooter's one-shots on its own machine also send a `ScooterCue` (`SoundPool`, `Respawn`, through `CueSync`).
  - A client reports its cue to the host (`OnlineMatch.ReportCue`). The host takes it only for the sender's own seat, shows it, and sends it to every client (`SendCue`). The host's own player's cues go straight to `SendCue`.
- **Each machine shows another player's cue on their scooter** (`OnlineCues.Show`):
  - its sound, through that scooter's sound pool, quieter the farther it is from this machine's player (`RemoteSound`);
  - a full horn's flash;
  - a rise's gravestone and sparkle at the point.
  - Order sounds need no cue. Every machine replays the host's order changes, so another player's scooter plays its pickups, deliveries and steal whoosh here too, once its pool can be heard.
- **A respawn** shows the wisp while the rider is hidden (the flags byte, as today). The gravestone and reborn sparkle go where the owner says it rises (`ScooterCue.Rise`).
- **The host's clock rings everywhere** (`ClockCue`): the wave bells, and time up, when a client also stops its own player.
- **Cutscenes:** a client's ends with the host's next state, and only the host's skip counts.
- **Pause** stays a local overlay online (`PausePolicy`). It closes when the host's state takes players out of driving, and it says what "Main Menu" does online.
- **Other machines' scooters go as fast here as there,** so this machine's pedestrians, cans and slipstream react to them.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 (ServerRpc, ClientRpc) · Unity Transport 1.5.0 · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap), Task 3.7:
- "Not networked: pedestrians (`CivilianAgent`), kickables, DOTween animations, particles, cameras, compass UI, speed lines."
- "One-shots via `ClientRpc`: boost start, drift boost, emotes (`EmoteHandler` — not on the live player prefab yet, only in `NewDrive Test.unity`), horn/phase sounds. *(Phase 3G: other machines don't yet hear a thief's whoosh or see a respawn's gravestone.)*"
- "Cutscenes: host sets the state; each client plays the Timeline locally; only the host can skip."
- "Online pause = local overlay only (no `Time.timeScale`); the host's menu adds **End match**. *(`GameAuthority.SetTimeScale` already ignores pauses online. `ControllerDisconnectPolicy.ShouldPause` still reads `Time.timeScale == 0` to tell whether the game is paused — change that here.)* *(Phase 3C: the host's "Main Menu" takes everyone back; a client's leaves the session.)*"
- And two of `docs/online.md`'s Known limits:
  - "Other machines' scooters are silent (no whoosh when they steal), and their respawn shows no wisp or gravestone: the scooter is gone until it rises from its grave (roadmap Task 3.7)."
  - "When the main game ends, only the host's scooter stops; the others keep driving until the golden round loads."

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**. A bump is a Steam API change: it goes to `main`, after asking.
- **Players:** online is one player per machine until Task 3.9.
- **Local split-screen must behave exactly as before.** `LocalMatchSmokeTest` passes after every task that changes game code.
- **Online rules:**
  - No mid-match joining (`JoinRules`).
  - The host leaving ends the match (`OnlineSession.HOST_LEFT`).
  - Netcode's scene management stays off.
- **Steam API files stay untouched:** `SteamManager.cs` and `SteamStartup.cs`.
- **The user may have Unity editors open on the real project** (their editor plus a ParrelSync clone sharing `Assets/`):
  - Never edit an existing scene, prefab or ProjectSettings file.
  - Everything in this plan is code.
- **No git commits:** your human partner commits.
- **Meta files:** every new file under `Assets/` gets a `.meta` (`bash tools/newmeta.sh <path>`). Before a task ends, check `git status` for new files without one.
- **Line endings:** `BallDriving.cs`, `PhaseIndicator.cs` and `PlayerInstantiate.cs` are `i/crlf`: edit them with the Edit tool only. Never `sed -i` on scripts.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]` (the mirror). Game-scene network tests take a minute or two each, so run long runs in the background.
  - Check `ListAgents` before a long run.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
- **Play Mode tests:**
  - No lambda captures a test method's local: after `EnterPlayMode` even assigning one throws. Use static helpers, and recorder classes subscribed as method groups.
  - Wait by real time, never by frame count. An EditMode test that enters Play Mode may only `yield return null` (`WaitForFixedUpdate` logs "EditMode test can only yield null").
  - Two-machine tests call `log.MachinesLeave()` before the first machine leaves.
  - The first cold load of the game scene can drop the other sessions (Unity Transport's 30 s timeout): run again warm, or after `LocalMatchSmokeTest`.
- **Tests can't see `internal` members.** Whatever tests call is `public`; they read private state through `Reflect`.
- **The game has its own global `SceneManager` class:** write `UnityEngine.SceneManagement.SceneManager` for Unity's.
- **Branch:** `steam-phase1a`. Phase 3G is committed (`9c2e2324`) and the tree is clean.

## Rulings (decided while planning)

1. **One-shots travel as cues from the machine that plays them, through the host** (`ScooterCue`; `OnlineMatch.ReportCue`, `SendCue`). That's the roadmap's "via `ClientRpc`": the host's `ClientRpc` carries every cue.
   - The host passes a cue on only for the sender's own seat. Each machine plays it on that player's scooter, unless that player is its own.
   - The flags byte can't carry them. A drift boost and a full horn are instants, and all 8 of its bits are taken.
   - Order sounds (pickup, delivery, a steal's whoosh) need no cue: every machine already replays the host's order changes (Phases 3E and 3G), on every scooter.
   - Rejected: boosts and phasing from the flags' edges. One path for every one-shot is simpler, and the cues leave from the very calls that play the sounds there.
   - Cost if wrong: one message per one-shot, about a dozen a minute per player.
2. **Other machines' scooters play one-shots only, quieter with distance** (`RemoteSound`): full volume within 10 m of this machine's player, silent from 60 m.
   - No engine, brake or drift-spark loops for them.
   - The game's sounds are all 2D and the listener doesn't move. A split-screen game mixes every player at full volume; online that would put every player's engine in your ears wherever they are.
   - Cost if wrong: numbers to tune; a boost 60 m away isn't heard.
3. **A respawn shows on other machines from what they already get, plus one cue:**
   - While the rider is hidden (the flags), the wisp shows. It sits on the ball, which follows its owner's wisp to the grave.
   - The owner sends `ScooterCue.Rise(point)` once it knows where it rises, whoever picked the point. Other machines put the gravestone up there, and play the reborn sparkle there during the lift, as on the owner's machine.
   - Rejected: the host's answer (`RespawnReceived`). It doesn't cover the host's own player, or a client that picked its own point.
4. **The host's clock rings on every machine** (`ClockCue`): a new wave's bells, and time up. At time up a client stops its own player and mutes, as the host does. That fixes "only the host's scooter stops".
   - Cues don't wait with the host's states (`HostQueue`): a one-shot is about the moment it happens.
5. **A client's cutscene ends with the host's next state** (`CutsceneManager`), and on its own timer as today.
   - *As built:* it ends when the state it hands over to arrives: the tutorial after the opening, the golden round after its cutscene. Other states don't end it, because the opening plays on while the game switches to MainLoop under it (`SpawnManager`).
   - The developer skip (L) works only where states are decided: offline, or on the host, whose skip then ends the cutscene everywhere.
   - Players have no skip, as today (L is a developer key).
6. **Online pause:**
   - **No "End match" row.** The pause menu's rows are hand-lettered art, like the title screen's (Phase 3D: no "Play Online" row). The host's "Main Menu" already ends the match for everyone (Phase 3C). Online, pausing shows a hint saying what "Main Menu" does (`PausePolicy.HintFor`).
   - **It closes when the host's state takes players out of driving:** cutscenes, results, loading, the menus. It stays through the tutorial, the waves and the golden round (`PausePolicy.KeepsPause`). Today the overlay stays up over the golden cutscene and the results.
   - **`ControllerDisconnectPolicy.ShouldPause` is told whether the pause menu is open** (`PlayerInstantiate.IsPaused`), not whether time stopped.
7. **Other machines' scooters go as fast here as they do there** (`BallDriving.CurrentVelocity` follows the shown speed).
   - So this machine's pedestrians and cans react to them, and slipstreaming behind them works. All three read `CurrentVelocity`, which stayed 0 for them.
   - Nothing is sent: each machine's pedestrians and cans are its own (the roadmap's "Not networked").
   - Another machine's scooter bumping something here doesn't drop its drift: its own machine does that.
8. **Emotes stay out:** `EmoteHandler` is only in "NewDrive Test.unity", not on the live player prefab.
9. **Not in this phase:**
   - Other machines' horns don't glow with their boost gauge (Phase 3B kept them plain). They flash when it's full.
   - Disconnects (Task 3.8).

## Review Focus

- **A client is paused when the host's state moves on** (the golden cutscene, the results, back to the lobby) → its pause menu closes and the new screen works. (Task 6 `Online_PausingLetsTheMatchGoOn_…`.)
- **Another machine's scooter is far away** → its one-shots are silent here; close by they're at full volume. (Task 1 `RemoteSoundTests`; Task 3 `Hosting_…`.)
- **A machine sends a one-shot for a seat that isn't its own** → nothing plays anywhere, and the host doesn't pass it on. (Task 3 `Hosting_…`.)
- **A rise names a point this machine doesn't have** (a bad index, or another scene) → nothing shows, and nothing breaks. (Task 4 `Joining_…`.)
- **The host's cutscene ends first** (it skipped) → a client's ends at once; a client's own skip does nothing. (Task 5 `Joining_…`.)

## Facts (probed 2026-10-01)

- **Sounds:**
  - Every sound is 2D: the pooled `Audio Source.prefab` has spatial blend 0. The enabled `AudioListener`s sit on menu-scene objects ("Menu Audio Listener", "Sound Manager") that don't follow players.
  - Each scooter's `SoundPool` (on "Control") builds 10 sources in `Awake`. Its `shouldPlay` turns on with Tutorial and FinalPackage (`InitEngineSource`, which also starts the engine hum), and off with Results and Menu.
  - Another machine's `SoundPool` is switched off before it's ever active (`RemoteAvatar`): its `shouldPlay` stays false, so it's silent. Its `Awake` still builds the pool.
  - `SoundManager.PlaySFX(key, source)` sets the source's clip and volume (`AudioObject.volume`), activates it and plays it. The pool's one-shots then free the source when it stops (`KillSource` → `ResetSource`: stopped, inactive, volume 1). **The clip stays on the source.**
  - Keys: "boost_used", "boost_charged", "mini", "phasing", "death", "pickup", "dropoff", "final_dropoff", "whoosh", "bells", "timeout". `SoundManager.GetSFX(key).clip`.
  - FMOD runs in the batch test runs on this PC: its threads show in `Logs/test-unity.log`.
- **A scooter's one-shots, on its own machine:**
  - `BallDriving.BoostFlag` → `PlayBoostActivate` ("boost_used").
  - A drift boost (`FixedUpdate`) → `PlayMiniBoost` ("mini").
  - `PhaseIndicator.SetHornColor`, when the boost gauge fills → `PlayBoostReady` ("boost_charged") and a flash on each horn (`CreateFlash`: a `flashParticles` copy under the indicator's transform, gone after 1.5 s). The horns' fields (`leftHorn`, `rightHorn`, `flashParticles`) are set in `PlayerAvatar.prefab`; its `hornSliderLeft/Right` are view parts (empty there).
  - Phasing through a building → `PlayPhaseSound` ("phasing", once per phase: `phasing` flag). Leaving it → `StopPhaseSound`, which can also run when nothing phased.
  - `Respawn.StartRespawnCoroutine` → `PlayDeathSound` ("death").
  - Pickups, deliveries and a steal's whoosh (`OrderHandler.AddOrder`, `DeliverOrder`, `TakeOrderFrom`; `CutoutHandler`) play inside order changes, which every machine replays (Phases 3E, 3G). On another machine's scooter they're called but silent.
  - `SoundPool`, `PhaseIndicator` and `OrderHandler` are all on "Control".
- **`DriveFlags`** uses all 8 bits.
- **Respawns** (`Respawn`, on "Ball Of Fun"):
  - The wisp is a `VisualEffect` ("DeathWisp", under Control/Groundcheck/Basket) with a trail; "RebornParticles" is its child.
  - The owner's sequence:
    - It picks the point, turns the scooter to face `PlayerFacingDirection` (the middle of the point's two order spots), and puts up `respawnGravestone` `tombstoneOffset` (2) behind the point.
    - Wisp rise 0.5 s, then to the casket 0.8 s (the ball follows it), then the lift, 0.7 s. The rider shows and the reborn particles play at the point for the lift.
  - A `RespawnGravestone` destroys itself after 5 s.
  - On other machines the hidden scooter's pose jumps with its owner's ball every tick (`OnlineScooter.Share`).
  - Only the rider and the ball's collider hide (`Respawn.ShowRemote`). No wisp, grave or sparkle shows.
- **The clock** (`OrderManager.InitWave`, run only on the authority):
  - Each wave after the first plays "bells" on `clockSource`.
  - Past the last wave, time is up: `OnMainGameFinishes` (each local player's `OrderHandler` freezes its ball, subscribed at StartingCutscene; orders erase, which waits for the host on a client), "timeout", the "paused" snapshot. Then `PostGameClarity` (`postGameLinger` 2.5 s, halved) loads the golden round.
  - A client's `InitWave` returns at once: no bells, no whistle, no freeze. FinalPackage unfreezes every local ball (`BallDriving.UnfreezeBallForGameState`).
  - The game scene has three waves (`wave` 0–2) of 60 s.
- **Cutscenes** (`CutsceneManager`, in each match scene from `Cutscene Manager.prefab`):
  - It plays on StartingCutscene and GoldenCutscene for `timeInSeconds`: 9.2 s for the opening in "Design Scene(Main)", 8.2 s in "FinalAreaScene".
  - Then `EndCutscene` turns its camera (`cutsceneCamera`) and canvas off, and asks for Tutorial or FinalPackage (a client's request is ignored).
  - L (`DevTools`) calls `EndCutscene` at once, on any machine.
  - It listens to no other state, so a client whose host skipped keeps showing its cutscene until its own timer ends.
- **Pause** (`PlayerInstantiate`):
  - `PlayerPause` switches to UI controls, calls `SetTimeScale(0)` (ignored online) and opens the pause menus: the pauser's with rows (`PauseMenu.OnPause(Host)` puts the selector on row 0).
  - `PlayerPlay` reverses it.
  - `OnPlayerControllerLost` asks `ControllerDisconnectPolicy.ShouldPause(menu is PauseMenu, Time.timeScale == 0f)`. Online that's never "paused", so a controller lost while paused pauses again, and the selector jumps back to Resume.
  - A state change while paused online leaves the overlay up: `SwapForCutscene` and `SwapForResults` don't close it.
  - The rows "Resume" and "Main Menu" are sprites in `Menu Canvas.prefab`.
  - `ControllerPrompts.Instance.ShowHint(text, seconds)`, `HintText`, `IsHintShown`.
- **Another machine's scooter and local-only things:**
  - `BallCollision` (on the ball) knocks a moving pedestrian down when `control.CurrentVelocity >= 10`. Otherwise it calls `DriftDrop` (which needs `Start`'s `Rumbler`) and makes impact sparks.
  - `CanKicker` kicks cans with a force × `CurrentVelocity`.
  - Slipstream reads the scooter ahead's `CurrentVelocity`.
  - Another machine's `BallDriving` is off, so its `CurrentVelocity` stays 0. Pedestrians, cans and slipstream ignore it.
- **Emotes:** `EmoteHandler` is in "NewDrive Test.unity" only.
- **Switched-off scripts:** another machine's `SoundPool` and `Respawn` are disabled components on active objects. Unity still runs their `Awake`, and they can start coroutines (only an inactive GameObject refuses one). `PlayRemote` frees its sources, and `ShowRise` times its sparkle, that way.
- **Test tools:**
  - `Reflect.GetField(OrderManager.Instance, "clockSource")`, `"wave"`; `OrderManager.InitWave()` is public.
  - `TestPlayers.Add()` / `ToggleNewest()` plug a virtual controller in and out.
  - The respawn and steal network tests' helpers (`CitySpot`, `FinishTutorialInTheCity`, `ShareAt`, `FarthestPointFrom`, …) are in `OnlineRespawnsNetworkTests` and `OnlineStealsNetworkTests`.

## File Structure

- Create in `Assets/Scripts/Online/`:
  - `ScooterCue.cs`: a player's one-shot as it travels (`CueKind`).
  - `ClockCue.cs`: the host's clock's one-shots.
  - `CueSync.cs`: one-shots on their way out of the game code.
  - `RemoteSound.cs`: how loud another machine's scooter is here, and each cue's sound.
  - `OnlineCues.cs`: the glue for one-shots.
- Create `Assets/Scripts/Player/PausePolicy.cs`: what the pause menu does online.
- Modify:
  - `Assets/Scripts/Online/OnlineMatch.cs`: cues and the clock.
  - `Assets/Scripts/Online/OnlineGame.cs`: adds `OnlineCues`.
  - `Assets/Scripts/Online/RemoteAvatar.cs`: the summary.
  - `Assets/Scripts/Player/SoundPool.cs`: cues out; another machine's one-shots in.
  - `Assets/Scripts/Player/PhaseIndicator.cs` (`i/crlf`): `FlashHorns`.
  - `Assets/Scripts/Player/Respawn.cs`: `GraveAt`, the rise cue, `ShowRise`, the wisp on other machines.
  - `Assets/Scripts/Management/OrderManager.cs`: the clock's cues out and in.
  - `Assets/Scripts/Menu/CutsceneManager.cs`: `Skip`; ends with the host's state.
  - `Assets/Scripts/Player/PlayerInstantiate.cs` (`i/crlf`): `IsPaused`; the pause closes with the state; the online hint.
  - `Assets/Scripts/Player/BallDriving.cs` (`i/crlf`): `ShowRemote` sets the speed.
  - `Assets/Scripts/Player/BallCollision.cs`: another machine's scooter only knocks pedestrians.
- Tests, all under `Assets/Tests/Editor/`:
  - Create:
    - `ScooterCueTests`, `RemoteSoundTests`, `PausePolicyTests`.
    - `CueMessagesNetworkTests` (port 7806, empty scene).
    - `OnlineCuesNetworkTests` (port 7807) and `OnlineMatchFlowNetworkTests` (port 7808): game scene.
    - `OnlinePauseTests` (menu scene, no network).
  - Modify: `RespawnPointsTests`, `RemoteDrivingTests`, `RemoteScooterRulesTests`.
- Docs: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`.

---

### Task 1: The one-shots' vocabulary

**Files:**
- Create: `Assets/Scripts/Online/ScooterCue.cs`, `Assets/Scripts/Online/ClockCue.cs`, `Assets/Scripts/Online/CueSync.cs`, `Assets/Scripts/Online/RemoteSound.cs`
- Test: `Assets/Tests/Editor/ScooterCueTests.cs`, `Assets/Tests/Editor/RemoteSoundTests.cs`

**Interfaces:**
- Produces:

```csharp
/// What a player's one-shot is (ScooterCue)
public enum CueKind : byte { Boost, DriftBoost, HornReady, Phase, PhaseEnd, Death, Rise }

/// A player's one-shot as it travels online, like PlayerHit
public struct ScooterCue : INetworkSerializable
{
    public CueKind Kind;
    public int Point; // a Rise's respawn point (RespawnManager.PointAt); -1 for every other kind
    public static ScooterCue Of(CueKind kind); // any kind but Rise: Point -1
    public static ScooterCue Rise(int point);
    // NetworkSerialize: Kind, then Point. ToString: "Boost", "Rise at 17"
}

/// The host's match clock's one-shots
public enum ClockCue : byte { WaveBells, TimeUp }

/// One-shots on their way out of the game code (like StealSync). Offline nothing listens
public static class CueSync
{
    public static event Action<int, ScooterCue> Played; // online: this machine's player (their seat) made a one-shot
    public static event Action<ClockCue> Rang;          // this machine's match clock rang (it runs only where states are decided)
    public static void Play(int seat, ScooterCue cue);
    public static void Ring(ClockCue cue);
    public static void Reset(); // forgets every listener (tests)
}

/// How another machine's scooter sounds here (Ruling 2)
public static class RemoteSound
{
    public const float NEAR = 10f; // m from this machine's player: full volume
    public const float FAR = 60f;  // m: silent from here on
    public static float Volume(float distance);   // 1 up to NEAR, then linear to 0 at FAR, 0 beyond
    public static string KeyFor(CueKind kind);    // the SoundManager key; null for PhaseEnd and Rise
    public static float VolumeAt(Vector3 where);  // Volume of the distance from there to this machine's online player's ball
                                                  // (PlayerInstantiate.OnlineSeat); 1 when there's none
}
```

- `KeyFor`: `Boost` "boost_used", `DriftBoost` "mini", `HornReady` "boost_charged", `Phase` "phasing", `Death` "death".

- [ ] **Step 1: Write the failing tests**

```csharp
// ScooterCueTests, as PlayerHitTests
static IEnumerable<ScooterCue> EveryKind()
{
    foreach (CueKind kind in new[] { CueKind.Boost, CueKind.DriftBoost, CueKind.HornReady, CueKind.Phase, CueKind.PhaseEnd, CueKind.Death })
        yield return ScooterCue.Of(kind);
    yield return ScooterCue.Rise(17);
}

[TestCaseSource(nameof(EveryKind))]
public void ACue_ArrivesAsItWasSent(ScooterCue sent) // FastBufferWriter → FastBufferReader round trip, as PlayerHitTests

[Test] public void OnlyARise_NamesAPoint()
{
    Assert.AreEqual(-1, ScooterCue.Of(CueKind.Boost).Point);
    Assert.AreEqual(CueKind.Rise, ScooterCue.Rise(17).Kind);
    Assert.AreEqual(17, ScooterCue.Rise(17).Point);
}

// RemoteSoundTests
[Test] public void AnotherMachinesScooter_IsHeardInFullNearby_FadingToSilenceFarAway()
{
    Assert.AreEqual(1f, RemoteSound.Volume(0f));
    Assert.AreEqual(1f, RemoteSound.Volume(RemoteSound.NEAR));
    Assert.AreEqual(0.5f, RemoteSound.Volume((RemoteSound.NEAR + RemoteSound.FAR) / 2f), 0.001f);
    Assert.AreEqual(0f, RemoteSound.Volume(RemoteSound.FAR));
    Assert.AreEqual(0f, RemoteSound.Volume(RemoteSound.FAR + 500f));
}

[Test] public void EachSoundCue_PlaysItsOwnersSound()
{
    Assert.AreEqual("boost_used", RemoteSound.KeyFor(CueKind.Boost));
    Assert.AreEqual("mini", RemoteSound.KeyFor(CueKind.DriftBoost));
    Assert.AreEqual("boost_charged", RemoteSound.KeyFor(CueKind.HornReady));
    Assert.AreEqual("phasing", RemoteSound.KeyFor(CueKind.Phase));
    Assert.AreEqual("death", RemoteSound.KeyFor(CueKind.Death));
    Assert.IsNull(RemoteSound.KeyFor(CueKind.PhaseEnd), "it stops the phasing sound");
    Assert.IsNull(RemoteSound.KeyFor(CueKind.Rise), "a gravestone, not a sound");
}

[Test] public void WithoutAPlayerOnThisMachine_EverythingIsHeardInFull()
{
    Assert.AreEqual(1f, RemoteSound.VolumeAt(new Vector3(500, 0, 0))); // no PlayerInstantiate in EditMode
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "ScooterCueTests|RemoteSoundTests"`
Expected: `NO RESULTS` with `error CS0246` (`ScooterCue`, `CueKind`, `RemoteSound`).

- [ ] **Step 3: Implement the four files** (and their metas).

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "ScooterCueTests|RemoteSoundTests"`
Expected: `tests: 11 total, 11 passed`.

- [ ] **Step 5: No commit.**

---

### Task 2: The match carries one-shots

**Files:**
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`
- Test: `Assets/Tests/Editor/CueMessagesNetworkTests.cs` (port 7806, empty scene, like `RespawnMessagesNetworkTests`)

**Interfaces:**
- Consumes: Task 1's `ScooterCue`, `ClockCue`.
- Produces, on `OnlineMatch`, following `AskSteal`/`SendHit` (the host skips its own `ClientRpc`s):

```csharp
/// Host: a client's player made a one-shot (the client's id, the player's seat, the cue)
public event Action<ulong, int, ScooterCue> CueReported;
/// Clients: a player's one-shot, from the host (their seat, the cue)
public event Action<int, ScooterCue> CueReceived;
/// Clients: the host's match clock rang
public event Action<ClockCue> ClockRang;

public void ReportCue(int seat, ScooterCue cue); // client → host (ServerRpc, RequireOwnership = false). Nothing on the host
public void SendCue(int seat, ScooterCue cue);   // host → every client (ClientRpc). Nothing on a client
public void RingClock(ClockCue cue);             // host → every client (ClientRpc). Nothing on a client
```

- The class summary gains: "- One-shots: players' (clients report theirs, and the host sends every one to every client) and the host's clock's (OnlineCues)."

- [ ] **Step 1: Write the failing tests** (recorders as in `RespawnMessagesNetworkTests`: `CueReportRecorder` (`Machine`, `Seats`, `Cues`), `CueRecorder` (`Seats`, `Cues`), `ClockRecorder` (`Cues`))

```csharp
[UnityTest] public IEnumerator AClientsCue_ReachesTheHost_WithWhoSentIt()
// host and client in one empty scene, as RespawnMessagesNetworkTests; reported = new CueReportRecorder(); host.Match.CueReported += reported.Heard
client.Match.ReportCue(1, ScooterCue.Rise(17));   // wait until reported.Cues.Count == 1
Assert.AreEqual(client.Network.LocalClientId, reported.Machine);
Assert.AreEqual(1, reported.Seats[0]);
Assert.AreEqual(ScooterCue.Rise(17), reported.Cues[0]);
host.Match.ReportCue(0, ScooterCue.Of(CueKind.Boost)); // wait 1 s
Assert.AreEqual(1, reported.Cues.Count, "the host sends its own player's cues: it never reports");
// leave as RespawnMessagesNetworkTests

[UnityTest] public IEnumerator TheHostsCues_ReachEveryClient_InOrder()
// heard = new CueRecorder() on client.Match.CueReceived; hostHeard on host.Match.CueReceived
host.Match.SendCue(0, ScooterCue.Of(CueKind.Boost));
host.Match.SendCue(1, ScooterCue.Of(CueKind.Death));
host.Match.SendCue(0, ScooterCue.Rise(3));        // wait until heard.Cues.Count == 3
CollectionAssert.AreEqual(new[] { ScooterCue.Of(CueKind.Boost), ScooterCue.Of(CueKind.Death), ScooterCue.Rise(3) }, heard.Cues);
CollectionAssert.AreEqual(new[] { 0, 1, 0 }, heard.Seats);
client.Match.SendCue(1, ScooterCue.Of(CueKind.Boost)); // wait 1 s
Assert.AreEqual(3, heard.Cues.Count, "a client never sends");
Assert.AreEqual(0, hostHeard.Cues.Count, "the host skips its own");

[UnityTest] public IEnumerator TheHostsClock_ReachesEveryClient_InOrder()
// rang = new ClockRecorder() on client.Match.ClockRang; hostRang on host.Match.ClockRang
host.Match.RingClock(ClockCue.WaveBells);
host.Match.RingClock(ClockCue.TimeUp);            // wait until rang.Cues.Count == 2
CollectionAssert.AreEqual(new[] { ClockCue.WaveBells, ClockCue.TimeUp }, rang.Cues);
client.Match.RingClock(ClockCue.WaveBells);       // wait 1 s
Assert.AreEqual(2, rang.Cues.Count, "a client's clock rings nowhere else");
Assert.AreEqual(0, hostRang.Cues.Count, "the host skips its own");
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh CueMessagesNetworkTests`
Expected: `NO RESULTS` with `error CS1061` (`OnlineMatch` has no `ReportCue`).

- [ ] **Step 3: Implement the events, methods and RPCs on `OnlineMatch`.**

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh CueMessagesNetworkTests`
Expected: `tests: 3 total, 3 passed`.

- [ ] **Step 5: No commit.**

---

### Task 3: Other machines hear a scooter's one-shots

**Files:**
- Create: `Assets/Scripts/Online/OnlineCues.cs`
- Modify: `Assets/Scripts/Player/SoundPool.cs`, `Assets/Scripts/Player/PhaseIndicator.cs` (`i/crlf`), `Assets/Scripts/Online/OnlineGame.cs`, `Assets/Scripts/Online/RemoteAvatar.cs`
- Test: `Assets/Tests/Editor/OnlineCuesNetworkTests.cs` (port 7807, game scene)

**Interfaces:**
- Consumes:
  - Task 1: `ScooterCue`, `CueKind`, `CueSync.Play` / `Played`, `RemoteSound.KeyFor`, `VolumeAt`.
  - Task 2: `OnlineMatch.ReportCue`, `CueReported`, `SendCue`, `CueReceived`.
- **`SoundPool` sends a cue** with `CueSync.Play(OrderSync.SeatOf(this), ScooterCue.Of(kind))`:
  - Only online, on this machine's scooter (`GameAuthority.IsOnline && !RemoteAvatar.IsRemote(this)`), and only when the sound plays here: after each method's own early returns.
  - `PlayBoostActivate` → `Boost`; `PlayMiniBoost` → `DriftBoost`; `PlayBoostReady` → `HornReady`; `PlayPhaseSound` → `Phase`; `StopPhaseSound` → `PhaseEnd`, only when it was phasing; `PlayDeathSound` → `Death`.
  - Order and UI sounds send nothing.
- **`SoundPool` on another machine's scooter** (`RemoteAvatar.IsRemote(this)`; the pool is off and its `shouldPlay` stays false):
  - `public void PlayRemote(CueKind kind)`:
    - It plays `RemoteSound.KeyFor(kind)` on a free source, at the sound's own volume × `RemoteSound.VolumeAt(transform.position)`. It frees the source when the sound ends, as the pool's one-shots do.
    - At volume 0 it takes no source.
    - `PhaseEnd` stops the phasing sound it's still playing. `Rise` plays nothing.
  - `PlayOrderPickup`, `PlayOrderDropoff` and `PlayOrderTheft` play the same way, without the `shouldPlay` check (keys: "pickup", the dropoff type, "whoosh").
  - No loops: the engine, brake, drift and spark methods stay as they are, gated by `shouldPlay`.
- **`PhaseIndicator`:** `public void FlashHorns()`: the flash on each horn that `SetHornColor` makes when the gauge fills. `SetHornColor` calls it.
- **`OnlineCues`** (`public class OnlineCues : MonoBehaviour`):
  - `public void Begin(OnlineSession session)`.
  - **This machine's player's cues** (`CueSync.Played`): the host sends them (`match.SendCue`); a client reports them (`match.ReportCue`).
  - **Host:** `match.CueReported(machine, seat, cue)` counts only when `session.SeatOf(machine) == seat`. Then `Show(seat, cue)` here, then `match.SendCue(seat, cue)` to every client.
  - **Clients:** `match.CueReceived` → `Show`, at once (Ruling 4: no `HostQueue`).
  - `public void Show(int seat, ScooterCue cue)`:
    - It acts only on another machine's player: `OrderSync.HandlerIn(seat)` exists and `RemoteAvatar.IsRemote` is true. This machine's own player played it already.
    - Every kind goes to that player's `SoundPool.PlayRemote(cue.Kind)` (on "Control", beside the handler).
    - `HornReady` also calls their `PhaseIndicator.FlashHorns()`.
    - `Rise` comes in Task 4.
  - **Subscriptions** follow `OnlineRespawns`: the match's events on `MatchSpawned`, and at `Begin` for a match already there; `CueSync.Played` at `Begin`. All are undone in `OnDestroy`.
- **`OnlineGame`** adds `OnlineCues` after `OnlineRespawns`. Its summary gains "- every player's one-shots, and the host's clock's (OnlineCues);".
- **`RemoteAvatar`'s summary:** its `SoundPool` stays off, and other players' one-shots play through it (`SoundPool.PlayRemote`, `OnlineCues`).

**Test helpers** (copied from `OnlineRespawnsNetworkTests`: `State`, `Slot`, `ScooterIn`, `Handler`, `PutBallAt`, `CitySpot`, `FinishTutorialInTheCity`, `ScooterInSeat`, `ShareAt`):
- `CueRecorder` (`Seats`, `Cues`) on `OnlineMatch.CueReceived`.
- `PoolOf(int seat)`: `Slot(seat).Player.GetComponentInChildren<SoundPool>(true)`.
- `Played(SoundPool pool, string key)`: some `AudioSource` under the pool has `SoundManager.Instance.GetSFX(key).clip`. A source keeps its clip after it ends.
- `Flashes(int seat)`: how many children of that player's `PhaseIndicator` are named `flashParticles.name + "(Clone)"` (`Reflect.GetField(indicator, "flashParticles")`).
- `IsAt(int seat, Vector3 spot)`: that player's ball is within 1 m of the spot here.
- `TearDown` also calls `CueSync.Reset()`.

- [ ] **Step 1: Write the failing test**

```csharp
[UnityTest] public IEnumerator Hosting_EveryPlayersOneShotsReachEveryMachine_AndPlayOnTheirScooterHere()
// This machine hosts with the game. Another machine (a session without the game) joins and sits in seat 1. To the first wave as in
// OnlineRespawnsNetworkTests.Hosting: FinishTutorialInTheCity(0); other.Match.ReportLearnt(1). spot = CitySpot().
// heard = new CueRecorder(other.Match); theirs = ScooterInSeat(other, 1)

// This machine's player's one-shots go to every client, in order. Its order sounds don't: every machine replays those
SoundPool mine = PoolOf(0);
mine.PlayBoostActivate(); mine.PlayMiniBoost(); mine.PlayBoostReady();
mine.PlayPhaseSound(); mine.PlayPhaseSound(); mine.StopPhaseSound(); mine.StopPhaseSound();
mine.PlayOrderPickup();                       // wait until heard.Cues.Count >= 5, then 1 s more
CollectionAssert.AreEqual(new[] { ScooterCue.Of(CueKind.Boost), ScooterCue.Of(CueKind.DriftBoost), ScooterCue.Of(CueKind.HornReady),
    ScooterCue.Of(CueKind.Phase), ScooterCue.Of(CueKind.PhaseEnd) }, heard.Cues, "one phase, one end; no pickup");
CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, heard.Seats);

// The other machine's player, 5 m from this machine's: its one-shots play on its scooter here, and every client hears them
ShareAt(theirs, spot + Vector3.right * 5f);   // wait until IsAt(1, …)
SoundPool remote = PoolOf(1);
other.Match.ReportCue(1, ScooterCue.Of(CueKind.Boost));      // wait until Played(remote, "boost_used")
// wait until heard has seat 1's Boost: the host sends it on to every client (its own machine skips it)
other.Match.ReportCue(1, ScooterCue.Of(CueKind.HornReady));  // wait until Played(remote, "boost_charged")
Assert.AreEqual(2, Flashes(1), "both horns flash");
remote.PlayOrderTheft();                      // a steal's whoosh, as the host's order changes replay it here
Assert.IsTrue(Played(remote, "whoosh"));

// 200 m away it's silent
ShareAt(theirs, spot + Vector3.right * 200f); // wait until IsAt(1, …)
other.Match.ReportCue(1, ScooterCue.Of(CueKind.DriftBoost)); // wait until heard has seat 1's DriftBoost, then 0.5 s
Assert.IsFalse(Played(remote, "mini"), "silent 200 m away");
remote.PlayOrderPickup();
Assert.IsFalse(Played(remote, "pickup"));

// A cue for a seat that isn't the sender's: nothing, and the host doesn't pass it on
int before = heard.Cues.Count;
other.Match.ReportCue(0, ScooterCue.Of(CueKind.Death));      // wait 1 s
Assert.AreEqual(before, heard.Cues.Count, "not the sender's seat");
// Leave: log.MachinesLeave(), the other machine leaves, then the host; Assert.IsEmpty(log.Problems)
```

- [ ] **Step 2: Run to verify it fails**

Run: `bash tools/run-tests.sh OnlineCuesNetworkTests`
Expected: 1 failure: the other machine hears no cue (the first `CollectionAssert`).

- [ ] **Step 3: Implement `OnlineCues`** (and the meta), **the `SoundPool` and `PhaseIndicator` changes, and the `OnlineGame` wiring.**

- [ ] **Step 4: Run to verify it passes** (in the background)

Run: `bash tools/run-tests.sh "OnlineCuesNetworkTests|RemoteAvatarTests|LocalMatchSmokeTest"`
Expected: `tests: 5 total, 5 passed`. `RemoteAvatarTests.RemoteScooter_MakesNoEngineSound_…` still passes: no loops for another machine's scooter.

- [ ] **Step 5: No commit.**

---

### Task 4: A respawn shows on every machine

**Files:**
- Modify: `Assets/Scripts/Player/Respawn.cs`, `Assets/Scripts/Online/OnlineCues.cs`
- Test: `Assets/Tests/Editor/RespawnPointsTests.cs`, `Assets/Tests/Editor/RemoteDrivingTests.cs`, `Assets/Tests/Editor/RemoteScooterRulesTests.cs`, `Assets/Tests/Editor/OnlineCuesNetworkTests.cs`

**Interfaces:**
- Consumes: Task 1's `ScooterCue.Rise`, `CueSync.Play`. Task 3's `OnlineCues.Show` and its test helpers. `RespawnManager.IndexOf`, `PointAt` (Phase 3G).
- Produces, on `Respawn`:

```csharp
/// Where a respawn's gravestone stands: offset behind the point, facing where the risen player will face
/// (the middle of the point's order spots)
public static Pose GraveAt(RespawnPoint point, float offset);
/// Another machine's scooter (this script is off there): its owner's player rises at that point
public void ShowRise(RespawnPoint point);
```

- **`GraveAt`:** rotation `Quaternion.LookRotation(point.PlayerFacingDirection - point.PlayerSpawn, Vector3.up)`; position `point.PlayerSpawn - rotation * Vector3.forward * offset`. `RespawnPlayer` uses it for its gravestone and for the scooter's turn (the same rotation).
- **The owner's rise cue:** in `RespawnPlayer`, once the point is known and the gravestone is up, online only: `CueSync.Play(OrderSync.SeatOf(this), ScooterCue.Rise(RespawnManager.Instance.IndexOf(rsp)))`. Nothing for an index of -1.
- **`ShowRise`:**
  - It puts up `respawnGravestone` at `GraveAt(point, tombstoneOffset)` at once.
  - After `wispRiseTime + wispToCasketTime` it plays a copy of `rebornParticles` at `point.PlayerSpawn` for `liftDuration`, then the copy goes.
  - If the point is gone by then (the scene changed), nothing plays.
  - A new rise replaces one whose sparkle is still waiting. The avatar's own particles are never moved.
- **`ShowRemote(bool hidden)`** also shows the wisp: while hidden it's on (enabled, `Reinit`, its trail at `wispTrailTime`), as when its owner's rises. Shown again, it's off, and so is its trail (time 0).
- **`OnlineCues.Show`:** a `Rise` looks up `RespawnManager.Instance?.PointAt(cue.Point)`. When there's one, that player's ball's `Respawn.ShowRise(point)`.

**Test helpers** for `Joining_…`: Task 3's, plus `RespawnOf` and `FarthestPointFrom` (from `OnlineRespawnsNetworkTests`) and `CueReportRecorder` (as Task 2).

- [ ] **Step 1: Write the failing tests**

```csharp
// RespawnPointsTests
[Test] public void AGrave_StandsBehindItsPoint_FacingWhereTheRisenPlayerWillFace()
{
    // the point at (50, 0, 0) faces the middle of its order spots, 10 m ahead along +z
    points[1].transform.Find("Order 1").localPosition = new Vector3(-1, 0, 10);
    points[1].transform.Find("Order 2").localPosition = new Vector3(1, 0, 10);
    points[1].InitPoint();
    Pose grave = Respawn.GraveAt(points[1], 2f);
    Assert.Less(Vector3.Distance(new Vector3(50, 0, -2), grave.position), 0.001f);
    Assert.Less(Quaternion.Angle(Quaternion.identity, grave.rotation), 0.01f);
}

// RemoteScooterRulesTests.AnotherMachinesScooter_HiddenForARespawn_ShowsNoRiderAndBumpsNobody: its bare Respawn gets a wisp
// (Awake doesn't run in EditMode): a child "DeathWisp" with a VisualEffect, and under it a TrailRenderer, set through
// Reflect as "deathWisp" and "wispTrail". After ShowRemote(true):
Assert.IsTrue(wisp.enabled, "its wisp shows while its owner's does");
Assert.Greater(trail.time, 0f);
// after ShowRemote(false):
Assert.IsFalse(wisp.enabled, "gone when the rider shows");
Assert.AreEqual(0f, trail.time);

// RemoteDrivingTests.AnotherMachinesScooter_ShowsItsOwnersBoostDriftSpeedAndRespawn, on the real scooter, where it's hidden:
VisualEffect wisp = (VisualEffect)Reflect.GetField(driving.Sphere.GetComponent<Respawn>(), "deathWisp");
Assert.IsTrue(wisp.enabled, "its wisp shows");
// and where it shows again:
Assert.IsFalse(wisp.enabled, "no wisp");

// OnlineCuesNetworkTests
[UnityTest] public IEnumerator Joining_ThisMachinesRespawnGoesOut_AndTheHostsShowsHere()
// This machine joins a host without the game and plays in seat 1, to the first wave, as OnlineRespawnsNetworkTests.Joining.
// spot = CitySpot(); PutBallAt(1, spot); hosts = ScooterInSeat(host, 0); ShareAt(hosts, spot + Vector3.right * 5f); wait until IsAt(0, …)
// reported = new CueReportRecorder(host.Match) (as Task 2)

// This machine's player falls in the water: the host hears it fall, then where it rises
Respawn respawn = RespawnOf(1); respawn.LastGroundedPos = spot; respawn.StartRespawnCoroutine(); // wait until reported.Cues.Count == 1
Assert.AreEqual(mine.Network.LocalClientId, reported.Machine);
Assert.AreEqual(1, reported.Seats[0]);
Assert.AreEqual(ScooterCue.Of(CueKind.Death), reported.Cues[0]);
int far = FarthestPointFrom(spot);
host.Match.SendRespawn(1, far);                    // wait until reported.Cues.Count == 2
Assert.AreEqual(ScooterCue.Rise(far), reported.Cues[1]);
// wait until !respawn.IsRespawning; then PutBallAt(1, spot): back beside the host's scooter

// The host's player falls in 5 m away: its sound here, then its gravestone and sparkle at its point
host.Match.SendCue(0, ScooterCue.Of(CueKind.Death)); // wait until Played(PoolOf(0), "death")
int theirs = RespawnManager.Instance.IndexOf(RespawnManager.Instance.GetRespawnPoint(spot)); // not `far`
RespawnPoint point = RespawnManager.Instance.PointAt(theirs);
Pose grave = Respawn.GraveAt(point, (int)Reflect.GetField(RespawnOf(0), "tombstoneOffset"));
host.Match.SendCue(0, ScooterCue.Rise(theirs));    // wait until a RespawnGravestone stands within 0.1 m of grave.position
// wait (WAIT) until a "RebornParticles(Clone)" is within 1 m of point.PlayerSpawn

// A rise at a point this scene doesn't have shows nothing, and breaks nothing
int graves = Object.FindObjectsOfType<RespawnGravestone>().Length;
host.Match.SendCue(0, ScooterCue.Rise(9999));      // wait 1 s
Assert.LessOrEqual(Object.FindObjectsOfType<RespawnGravestone>().Length, graves, "no new gravestone");
// Leave: log.MachinesLeave(), this machine leaves, then the host; Assert.IsEmpty(log.Problems)
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "RespawnPointsTests|RemoteScooterRulesTests|RemoteDrivingTests|OnlineCuesNetworkTests"`
Expected: `NO RESULTS` with `error CS0117` (`Respawn` has no `GraveAt`).

- [ ] **Step 3: Implement `GraveAt`, the rise cue, `ShowRise`, the wisp in `ShowRemote`, and `Rise` in `OnlineCues.Show`.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "RespawnPointsTests|RemoteScooterRulesTests|RemoteDrivingTests|OnlineCuesNetworkTests|OnlineRespawnsNetworkTests|LocalMatchSmokeTest"`
Expected: `tests: 23 total, 23 passed` (6 + 11 + 1 + 2 + 2 + 1).

- [ ] **Step 5: No commit.**

---

### Task 5: The host's cutscenes and clock on every machine

**Files:**
- Modify: `Assets/Scripts/Menu/CutsceneManager.cs`, `Assets/Scripts/Management/OrderManager.cs`, `Assets/Scripts/Online/OnlineCues.cs`
- Test: `Assets/Tests/Editor/OnlineMatchFlowNetworkTests.cs` (port 7808, game scene); `Assets/Tests/Editor/OnlineCuesNetworkTests.cs` (the `Hosting_…` test gains the clock)

**Interfaces:**
- Consumes: Task 1's `ClockCue`, `CueSync.Ring` / `Rang`. Task 2's `OnlineMatch.RingClock`, `ClockRang`. Task 3's `OnlineCues`.
- **`CutsceneManager`:**
  - `public void Skip()`: the developer skip. Only where states are decided (`GameAuthority.IsAuthority`): it stops the cutscene's coroutine and calls `EndCutscene`, as L does today. On a client it does nothing. L calls `Skip()`.
  - It listens to `GameManager.StateApplied` (in `OnEnable` / `OnDisable`). A state other than the playing cutscene's (StartingCutscene for the opening, GoldenCutscene for the golden one) stops the cutscene and closes its view (camera, canvas, camera positions), without asking for a state.
  - `EndCutscene` closes the view and forgets its coroutine before it asks for the next state. So that state doesn't close it twice.
- **`OrderManager`:**
  - In `InitWave`, beside "bells": `CueSync.Ring(ClockCue.WaveBells)`. Beside "timeout" at time up: `CueSync.Ring(ClockCue.TimeUp)`.
  - `public void FollowHostClockCue(ClockCue cue)`, for an online client, like `FollowHostClock`:
    - `WaveBells`: "bells" on `clockSource`.
    - `TimeUp`: `OnMainGameFinishes` (this machine's player stops; its orders wait for the host's erases), "timeout" on `clockSource`, and the "paused" snapshot.
- **`OnlineCues`:**
  - `CueSync.Rang` → on the host, `match.RingClock(cue)`.
  - A client's `match.ClockRang` → `public void ShowClock(ClockCue cue)` → `OrderManager.Instance?.FollowHostClockCue(cue)`, at once. A machine without an `OrderManager` (in the menu, or changing scenes) skips it.

- [ ] **Step 1: Write the failing tests**

```csharp
// OnlineMatchFlowNetworkTests (helpers as OnlineRespawnsNetworkTests; TearDown also calls CueSync.Reset())
// CutsceneOn(): CutsceneManager.Instance != null && ((Camera)Reflect.GetField(CutsceneManager.Instance, "cutsceneCamera")).enabled
[UnityTest] public IEnumerator Joining_TheHostsCutscenesAndClock_PlayHereAsOnTheHost()
// This machine joins a host without the game and plays in seat 1, as OnlineRespawnsNetworkTests.Joining, up to the host's show:
host.Match.RequestLoad(MatchScene.Game);           // wait for this machine's report
host.Match.RequestShow(MatchScene.Game);
host.Match.SendState(GameState.StartingCutscene);  // wait until State() == StartingCutscene && CutsceneOn()
float started = Time.realtimeSinceStartup;
CutsceneManager.Instance.Skip();                   // wait 1 s
Assert.IsTrue(CutsceneOn(), "only the host can skip");
Assert.AreEqual(GameState.StartingCutscene, State());
host.Match.SendState(GameState.Tutorial);          // the host skipped: wait until State() == Tutorial && !CutsceneOn()
Assert.IsFalse(CutsceneOn(), "it ends with the host's");
Assert.Less(Time.realtimeSinceStartup - started, 8f, "long before its own 9.2 s ran out");

AudioSource clock = (AudioSource)Reflect.GetField(OrderManager.Instance, "clockSource");
host.Match.RingClock(ClockCue.WaveBells);          // wait until clock.clip == SoundManager.Instance.GetSFX("bells").clip
host.Match.RingClock(ClockCue.TimeUp);             // wait until the ball's constraints freeze X
Rigidbody ball = ScooterIn(1).Sphere.GetComponent<Rigidbody>();
Assert.AreNotEqual(RigidbodyConstraints.None, ball.constraints & RigidbodyConstraints.FreezePositionX, "this machine's player stops");
Assert.AreSame(SoundManager.Instance.GetSFX("timeout").clip, clock.clip, "the whistle");
// Leave: log.MachinesLeave(), this machine leaves, then the host; Assert.IsEmpty(log.Problems)

// OnlineCuesNetworkTests.Hosting_…: before the machines leave, the host's clock rings out
// rang = new ClockRecorder(other.Match) (as Task 2)
Reflect.SetField(OrderManager.Instance, "wave", 1);
OrderManager.Instance.InitWave();                  // the second wave: wait until rang.Cues.Count == 1
Assert.AreEqual(ClockCue.WaveBells, rang.Cues[0]);
Reflect.SetField(OrderManager.Instance, "wave", 99);
OrderManager.Instance.InitWave();                  // past the last wave: time up
OrderManager.Instance.StopAllCoroutines();         // the golden round doesn't load: the test ends here
// wait until rang.Cues.Count == 2
Assert.AreEqual(ClockCue.TimeUp, rang.Cues[1]);
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "OnlineMatchFlowNetworkTests|OnlineCuesNetworkTests"`
Expected: `NO RESULTS` with `error CS1061` (`CutsceneManager` has no `Skip`).

- [ ] **Step 3: Implement the `CutsceneManager`, `OrderManager` and `OnlineCues` changes.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "OnlineMatchFlowNetworkTests|OnlineCuesNetworkTests|LocalMatchSmokeTest"`
Expected: `tests: 4 total, 4 passed`. The smoke test's opening cutscene still ends on its own timer.

- [ ] **Step 5: No commit.**

---

### Task 6: Online pause is a local menu

**Files:**
- Create: `Assets/Scripts/Player/PausePolicy.cs`
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (`i/crlf`)
- Test: `Assets/Tests/Editor/PausePolicyTests.cs`, `Assets/Tests/Editor/OnlinePauseTests.cs` (menu scene, no network)

**Interfaces:**
- Produces:

```csharp
/// What the pause menu does online, where pausing doesn't stop the match (Ruling 6)
public static class PausePolicy
{
    public const string HOST_HINT = "Online: the match goes on while you're paused. Main Menu ends it for everyone.";
    public const string CLIENT_HINT = "Online: the match goes on while you're paused. Main Menu leaves it.";
    /// Whether a pause menu stays open when the game switches to this state: only while players drive
    /// (Tutorial, Begin, MainLoop, FinalPackage)
    public static bool KeepsPause(GameState state);
    /// What pausing says: nothing offline (the game stops), else what Main Menu does for this machine
    public static string HintFor(NetworkRole role);
}

// PlayerInstantiate
///<summary>Whether this machine's players have the pause menu open (online the match goes on meanwhile)</summary>
public bool IsPaused { get; private set; }
```

- **`PlayerInstantiate`:**
  - `PlayerPause` sets `IsPaused`. When `PausePolicy.HintFor(GameAuthority.Role)` isn't null, it shows it (`ControllerPrompts.Instance.ShowHint(hint, OnlineGame.NOTICE_SECONDS)`).
  - `PlayerPlay` clears it, through the same closing as below.
  - `OnPlayerControllerLost`: `ControllerDisconnectPolicy.ShouldPause(menu is PauseMenu, IsPaused)`.
  - `gameManager.StateApplied` (in `OnEnable` / `OnDisable`): a state for which `!PausePolicy.KeepsPause(state)` closes an open pause: each local player's `pauseMenu.OnPlay()`, and `IsPaused` cleared. The controls are left to that state's own handlers, which run next.

- [ ] **Step 1: Write the failing tests**

```csharp
// PausePolicyTests
[Test] public void ThePauseMenu_StaysWhilePlayersDrive_AndClosesForAnythingElse()
{
    GameState[] driving = { GameState.Tutorial, GameState.Begin, GameState.MainLoop, GameState.FinalPackage };
    foreach (GameState state in (GameState[])System.Enum.GetValues(typeof(GameState)))
        Assert.AreEqual(System.Array.IndexOf(driving, state) >= 0, PausePolicy.KeepsPause(state), state.ToString());
}

[Test] public void PausingOnline_SaysWhatMainMenuDoes()
{
    Assert.IsNull(PausePolicy.HintFor(NetworkRole.Offline), "offline the game stops: nothing to say");
    Assert.AreEqual("Online: the match goes on while you're paused. Main Menu ends it for everyone.", PausePolicy.HintFor(NetworkRole.Host));
    Assert.AreEqual("Online: the match goes on while you're paused. Main Menu leaves it.", PausePolicy.HintFor(NetworkRole.Client));
}

// OnlinePauseTests: the menu scene in Play Mode, about 15 s each. GameAuthority.Role is set as a session would set it.
// Each test: the title screen, TestPlayers.Add(), wait for 1 player. menu = MenuOf(0):
// Slot(0).Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>().
// menu.SwapMenuType(MenuType.PauseMenu): it's driving, so Start opens its pause menu.
// Helpers: Tint(menu) and Selector(menu) read the PauseMenu's "tint" and "selector" (GameObjects), RowY(menu, i) its "selectorObjects"[i].
// TearDown: ExitPlayMode, playModeStartScene = null, TestPlayers.RemoveAll(), Role Offline, Time.timeScale = 1.

[UnityTest] public IEnumerator Online_PausingLetsTheMatchGoOn_UntilTheHostsStateMovesOn()
GameAuthority.Role = NetworkRole.Client;
PlayerInstantiate.Instance.PlayerPause(Slot(0).Input);
Assert.IsTrue(PlayerInstantiate.Instance.IsPaused);
Assert.AreEqual(1f, Time.timeScale, "online the match goes on");
Assert.IsTrue(Tint(menu).activeSelf, "the pause menu is up");
Assert.AreEqual(PausePolicy.CLIENT_HINT, ControllerPrompts.Instance.HintText);
Assert.IsTrue(ControllerPrompts.Instance.IsHintShown);
GameManager.Instance.ApplyGameState(GameState.PlayerSelect); // the host takes everyone back to the lobby
Assert.IsFalse(PlayerInstantiate.Instance.IsPaused);
Assert.IsFalse(Tint(menu).activeSelf, "it closes with the host's state");

[UnityTest] public IEnumerator Online_AControllerLostWhilePaused_LeavesThePauseMenuAsItWas()
GameAuthority.Role = NetworkRole.Host;
PlayerInstantiate.Instance.PlayerPause(Slot(0).Input);
menu.pauseMenu.ScrollMenu(true);                   // the selector on Main Menu
TestPlayers.ToggleNewest();                        // its controller is unplugged: wait 0.5 s
Assert.IsTrue(PlayerInstantiate.IsMissingController(Slot(0).Input.user));
Assert.AreEqual(RowY(menu, 1), Selector(menu).transform.position.y, 0.01f, "still on Main Menu: it didn't pause again");
Assert.IsTrue(PlayerInstantiate.Instance.IsPaused);
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "PausePolicyTests|OnlinePauseTests"`
Expected: `NO RESULTS` with `error CS0103` / `CS1061` (`PausePolicy`, `IsPaused`).

- [ ] **Step 3: Implement `PausePolicy`** (and the meta) **and the `PlayerInstantiate` changes.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "PausePolicyTests|OnlinePauseTests|ControllerDisconnectPolicyTests|MissingControllerTests|LocalMatchSmokeTest"`
Expected: `tests: 16 total, 16 passed` (2 + 2 + 9 + 2 + 1). The smoke test still pauses, and unplugs and replugs a controller, offline.

- [ ] **Step 5: No commit.**

---

### Task 7: Pedestrians, cans and slipstream feel other machines' scooters

**Files:**
- Modify: `Assets/Scripts/Player/BallDriving.cs` (`i/crlf`), `Assets/Scripts/Player/BallCollision.cs`
- Test: `Assets/Tests/Editor/RemoteDrivingTests.cs`, `Assets/Tests/Editor/RemoteScooterRulesTests.cs`

**Interfaces:**
- **`BallDriving.ShowRemote(flags, speed)`** also sets `currentVelocity = speed`. Its summary adds that this machine's pedestrians, cans and slipstream feel the scooter at that speed.
- **`BallCollision.OnTriggerEnter`:** on another machine's scooter (`RemoteAvatar.IsRemote(this)`) it still knocks a moving pedestrian down. Then it returns: no `DriftDrop` and no impact sparks here, because its own machine does those.

- [ ] **Step 1: Write the failing tests**

```csharp
// RemoteDrivingTests.AnotherMachinesScooter_ShowsItsOwnersBoostDriftSpeedAndRespawn, after the 15 m/s drift:
Assert.AreEqual(15f, driving.CurrentVelocity, 0.001f, "pedestrians, cans and slipstream feel its speed here");

// RemoteScooterRulesTests
[Test] public void AnotherMachinesScooter_BumpingSomethingHere_KeepsItsOwnersDrift()
{
    GameObject remote = Scooter(true);
    BallDriving driving = remote.GetComponentInChildren<BallDriving>();
    Respawn respawn = remote.GetComponentInChildren<Respawn>();
    Reflect.SetField(driving, "currentVelocity", 20f);
    Reflect.SetField(driving, "drifting", true);
    Reflect.SetField(driving, "respawn", respawn);
    BallCollision bump = respawn.gameObject.AddComponent<BallCollision>();
    Reflect.SetField(bump, "control", driving);
    BoxCollider post = objects.NewGameObject("Lamp post").AddComponent<BoxCollider>();
    post.isTrigger = true;

    Reflect.Invoke(bump, "OnTriggerEnter", post);

    Assert.IsTrue(driving.Drifting, "its own machine drops its drift");
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "RemoteDrivingTests|RemoteScooterRulesTests"`
Expected: 2 failures. `RemoteDrivingTests` sees 0 for the speed. `AnotherMachinesScooter_BumpingSomethingHere_…` throws a `NullReferenceException` in `BallDriving.DriftDrop` (the part its own machine runs).

- [ ] **Step 3: Implement the `ShowRemote` speed and the `BallCollision` return.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "RemoteDrivingTests|RemoteScooterRulesTests|LocalMatchSmokeTest"`
Expected: `tests: 14 total, 14 passed` (1 + 12 + 1).

- [ ] **Step 5: No commit.**

---

### Task 8: Docs and the full suite

**Files:**
- Modify: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`

- [ ] **Step 1: `docs/online.md`**
  - A "Phase 3H: one-shots, cutscenes and pause" section, in the doc's plain style:
    - Other players' scooters are heard here: a boost, a drift boost, a full horn (their horns flash too), phasing, falling in the water, pickups, deliveries and a steal's whoosh. They fade with distance: full within 10 m, silent from 60 m.
    - A player who falls in the water shows their wisp on every machine, and their gravestone and sparkle where they rise.
    - The host's cutscenes end everywhere when the host's do. Only the host's skip counts (the developer key L).
    - The wave bells and the whistle sound everywhere, and at the whistle every scooter stops.
    - Pausing online doesn't stop the match. The pause menu closes when a cutscene, the results or the lobby comes up. A hint says what "Main Menu" does: the host's ends the match for everyone, a client's leaves it.
    - Pedestrians, cans and slipstream react to other players' scooters too. Each machine has its own pedestrians and cans.
    - How it works: `ScooterCue`, `CueSync`, `ReportCue` / `SendCue`, `OnlineCues`, `SoundPool.PlayRemote`, `RemoteSound`; `ScooterCue.Rise`, `Respawn.ShowRise`; `ClockCue`, `RingClock`; `CutsceneManager.Skip`; `PausePolicy`, `PlayerInstantiate.IsPaused`; `BallDriving.ShowRemote`'s speed.
  - The ParrelSync walkthrough gains: hear the other editor's boosts, watch its respawn's gravestone, and pause in one editor while the other drives on.
  - Known limits:
    - Remove "Other machines' scooters are silent…" and "When the main game ends, only the host's scooter stops…".
    - Add: other machines' scooters play one-shots only: no engine, brake or drift-spark sounds. Their horns don't glow with their boost gauge; they flash when it's full.
    - Add: another player's one-shot plays here about a round trip after it played there (it goes through the host).
    - Add: the pause menu has no "End match" row (its rows are hand-lettered art). The host's "Main Menu" is it.

- [ ] **Step 2: `docs/testing.md`**
  - The EditMode list gains `ScooterCueTests`, `RemoteSoundTests` and `PausePolicyTests`.
  - Network tests:
    - `CueMessagesNetworkTests` (port 7806, empty scene): a client's one-shot reaching the host, and the host's one-shots and clock reaching clients.
    - `OnlineCuesNetworkTests` (port 7807): one-shots hosting and joining, a respawn's gravestone, the host's clock ringing out.
    - `OnlineMatchFlowNetworkTests` (port 7808): on a client, the host's cutscene ending and its clock.
    - These load the game scene: a minute or two each.
  - `OnlinePauseTests` (menu scene, no network): pausing online.

- [ ] **Step 3: The roadmap**
  - Progress: a Phase 3H line. "Next" becomes Task 3.8.
  - Task 3.7's four bullets are ticked, each with a short Phase 3H note:
    - Not networked: each machine's own, and they feel other machines' scooters (Ruling 7).
    - One-shots: `ScooterCue` through the host; order sounds from the replays; emotes out (Ruling 8).
    - Cutscenes: Ruling 5.
    - Pause: Ruling 6.
  - Task 3.3's "Sounds come with Task 3.7" note gains "(Phase 3H: one-shots)".

- [ ] **Step 4: `EDITOR-TODO.md`** (rewrite it for Phase 3H, and keep what's still open)
  - Import step: the new scripts (`ScooterCue`, `ClockCue`, `CueSync`, `RemoteSound`, `OnlineCues`, `PausePolicy`), the changed scripts and the new tests.
  - A two-editor check, then again with Bad Connection in the clone:
    - Drive the two scooters side by side and boost in the clone: editor 1 hears it. Drive apart: it fades.
    - Wait for a full horn in the clone: editor 1 sees its horns flash.
    - Steal from each other: the robbed editor hears the whoosh.
    - Drive the clone's scooter into the water: editor 1 sees its wisp, then its gravestone, then it rises from it.
    - Drive the clone's scooter through pedestrians and cans in editor 1's view: they react.
    - Start a match and press L in the clone during the opening cutscene: nothing. Press L in editor 1: both skip.
    - The bells at each new wave in both. At the whistle, both scooters stop.
    - Pause in the clone: editor 1 drives on, and the clone shows the hint. While the clone is paused, let the main game end: its pause menu closes for the golden cutscene.
  - Still open from before, if not done: the Phase 3G and 3F two-editor checks, the optional "Normal Positions" tidy-up, and the Phase 3D Steam checks.
  - Commit: suggested title "Phase 3H: one-shots, cutscenes and pause online".

- [ ] **Step 5: Run the full suite** (in the background)

Run: `bash tools/run-tests.sh`
Expected: 470 + 23 new = `tests: 493 total, 493 passed`. The new tests: Task 1 adds 11, Task 2 adds 3, Task 3 adds 1, Task 4 adds 2, Task 5 adds 1, Task 6 adds 4 and Task 7 adds 1.

- [ ] **Step 6: No commit.** Suggested title: "Phase 3H: one-shots, cutscenes and pause online".

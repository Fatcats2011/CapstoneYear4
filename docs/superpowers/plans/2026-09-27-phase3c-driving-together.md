# Phase 3C — Driving Together Online Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An online match starts on every machine together. Everyone drives through it seeing each other's scooters move, with their boosts, drifts and sparks, while the game's states and the match clock follow the host. The host takes everyone back to the menu. Local split-screen stays identical.

**Architecture:**
- **Scene loads stay local, and the host coordinates them** (Netcode's scene management stays off).
  - The host tells every machine to load the match scene. Each loads it behind its loading screen and says when it's ready.
  - Once every machine has it, the host tells them all to show it.
  - A state that reaches a client while its new scene is still coming up waits for that scene.
- **Each machine drives its own scooter.** A new network object per player, `OnlineScooter`, carries that scooter:
  - Its pose, through two proxies its owner moves (`OwnerNetworkTransform`): the ball, and the model's world pose.
  - A flags byte (`DriveFlags`: boost, drift, drift tier, ground, phase).
  - `OnlineDriving` copies this machine's scooter into its `OnlineScooter` every frame. It shows every other one on that player's `RemoteAvatar`, whose `BallDriving` displays the flags so the existing trails, skid marks and sparks react.
- **The match clock** travels as an end time in Netcode's server time.
- Online, the tutorial is skipped. Another machine's scooter doesn't respawn, pick up, deliver, steal or clash on this machine: orders and steals are shared in roadmap Tasks 3.5–3.6.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 (`NetworkTransform`) · Unity Transport 1.5.0 · Unity Test Framework 1.1.33 · Input System 1.14.0.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- **Task 3.3 bullet 2: owner-authoritative movement.**
  - The owner runs `BallDriving`'s physics.
  - An owner-authoritative `NetworkTransform` with interpolation.
  - Non-owners make the ball kinematic and skip `BallDriving`.
  - Ruling 2 puts the transforms on proxies: the avatar isn't a network object (Phase 3B Ruling 1).
- **Task 3.3 bullet 3: spawns.** Each owner puts its own scooter on its slot's spawn point; the host doesn't place other machines' scooters.
- **Task 3.3 bullet 4: remote effects.** Remote players' boost, phase and drift visuals come from the flags byte.
- **Task 3.3 bullet 5 (the rest from 3B):** trigger messages reach a remote scooter's disabled scripts (water, orders). They're guarded (Ruling 5).
- **Task 3.4 bullet 2: timers.** Wave/game timers become host-owned end times in server time; clients compute the time left.
- **Task 3.4 bullet 3: scene loads.**
  - Loads happen across the whole session; the match starts once every machine has the scene.
  - Clarification: Netcode's scene manager stays off (Ruling 1). The host coordinates the game's own loader instead.
- **Global:** no mid-match joining. The host leaving ends the session; Task 3.8 returns clients to the menu with a message.

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x** only.
- **Players:** 1–4, gamepad-first (`Constants.MAX_PLAYERS = 4`). Online, one player per machine until Task 3.9.
- **Local split-screen must behave exactly as before:**
  - `LocalMatchSmokeTest` passes after every task.
  - Nothing online runs unless a session is started.
- **Online rules:**
  - No mid-match joining (`JoinRules`, Phase 3A).
  - If the host leaves, the session ends. Returning clients to the menu with a message is Task 3.8.
- **Two Unity editors are open on the real project:** the user's editor, plus a ParrelSync clone that shares `Assets/`.
  - Never edit an existing scene, prefab or ProjectSettings file.
  - New prefabs are built in the mirror and copied in as new files.
  - `Assets/Resources/Online/OnlinePrefabs.asset` and `Assets/DefaultNetworkPrefabs.asset` are copied back from the mirror after the builder sets them. Neither is a scene, prefab or setting.
- **No git commits:** your human partner commits and pushes.
- **Meta files:** every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`. The prefab the mirror builds brings its own.
- **Line endings:**
  - Files marked `i/crlf` in `git ls-files --eol` must stay CRLF, so edit them with the Edit tool only. In this plan: `BallDriving.cs`, `SceneManager.cs` and `ResultsMenu.cs`.
  - Never use `sed -i` on scripts.
  - New files may be LF.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]`. They run in the mirror (`../_doa_test_mirror/CapstoneYear4`) and take 2–12 minutes, so run them in the background.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
  - Message the other local Claude sessions before long runs (they share the mirror).
- **Play Mode tests:**
  - A test resumes in a reloaded script domain after `yield return new EnterPlayMode()`.
  - Never write a lambda that captures a local of the test method: after the reload, **even assigning** such a local throws a bare `NullReferenceException`. Use static helper methods, and recorder classes whose methods are subscribed as method groups.
  - Wait by time, never by frame count (batch-mode frames take about 0.2 ms).
- **Branch:** `steam-phase1a` (memory `branch-policy.md`: only the Steam API goes to `main`).
- **The starting working tree:** it holds an uncommitted fix from 2026-09-24. `CustomizationSelector` gives the colour and hat sliders their icons in `Awake`, `OnlineGameNetworkTests` has a join test for it, and the roadmap's count says 259. Leave it; it ships with this phase.

## Rulings (decided while planning)

1. **Scene loads stay local; the host coordinates them.**
   - How it works:
     - The host asks every machine to load a match scene. Each loads it behind its loading screen, held (`IMatchLoader`), and reports when it's ready.
     - When every machine that was in the session has it, or has left, the host tells everyone to show it.
     - The loading screen's "every player presses A" isn't used online.
   - Why not Netcode's scene manager:
     - With it on, joining from an unsaved Play Mode scene breaks (Phase 3B finding).
     - It would also sync "Alex Player Testing", the menu scene that holds every manager.
   - Cost if wrong: late joiners wouldn't get the scenes synced for them, and there's no mid-match joining anyway.
2. **Pose sync goes through a new network object per player, `OnlineScooter`.**
   - It holds two proxies, each an `OwnerNetworkTransform`: a `NetworkTransform` whose owner, not the host, moves it.
     - `Ball`: the ball's position and the scooter's heading.
     - `Model`: the model's world pose, which already includes the slope, lean, drift angle, hop and wheelie.
   - The proxies are **parked** 10 km below the map until their owner shares a first pose. So another machine's scooter never flies in from the world's origin, and "has it posed yet" travels in the same stream as the pose.
   - A move longer than 10 m in one frame (a spawn point, a respawn, the first pose) is sent as a **teleport**, so others see a jump, not a slide across the map.
   - Why a new prefab:
     - The avatar isn't a network object (3B Ruling 1).
     - `OnlinePlayer.prefab` already exists, and two editors are open on the project.
   - Cost if wrong: one more small object per player.
3. **What a scooter is doing travels as a flags byte on `OnlineScooter`** (`DriveFlags`: boosting, drifting, drift direction, drift tier 0–3, grounded, phasing).
   - The remote avatar's `BallDriving.ShowRemote` sets its own state from the flags. So the existing `TrailHandler`, `SkideeSkidoo` and drift sparks react as for a scooter driven here, and the rider's run animation follows the speed.
   - Sounds and one-shot effects are roadmap Task 3.7.
   - Cost if wrong: remote scooters are silent until Task 3.7.
4. **Online, the tutorial is skipped** until orders are shared (Task 3.5).
   - The tutorial teaches picking up, delivering and stealing orders, which only the host has until then.
   - Its end (`TutorialManager`) also counts every player, and only local players have tutorial handlers, so the host would wait forever.
   - Online, every machine's players finish it at once, and the host starts the first wave a frame later.
   - Cost if wrong: new online players miss the tutorial; they get it in a local match.
5. **Until Tasks 3.5–3.6, another machine's scooter doesn't act on this machine.**
   - It doesn't fall in water, pick up or deliver orders, steal, clash, or freeze at the end of the main game here. Its own machine does all that, and the pose shows the result.
   - Orders stay the host's own: only the host's player collects them, and other machines don't see orders yet.
   - Cost if wrong: online matches have no order play for clients until Task 3.5, which is expected.
6. **The match clock travels as an end time in Netcode's server time** (roadmap 3.4), plus whether the waves have started and whether it's the golden round, all on `OnlineMatch`.
   - Clients show time left = end − server time.
   - The host republishes only when its clock moves more than 0.25 s from what it sent (a new wave, the P hotkey).
   - Cost if wrong: none.
7. **Going back to the menu:**
   - The host takes everyone back (from the results or its pause menu). On a client, the results screen waits for the host: online, a client's player counts as its machine's "host player" (Phase 3B), so its A would otherwise go back alone.
   - A client picking "main menu" in its pause menu leaves the session first, then goes back alone. It can't rejoin a running match.
   - Cost if wrong: a client who quits mid-match is out of the session.
8. **Pose sync runs everywhere**, player select included. A podium is where the owner's scooter stands anyway.
   - Cost if wrong: none seen; the 3B podium placement still happens first.

## Findings (from reading the code, 2026-09-27)

- **The scooter's parts** (`BallDriving`'s serialized fields in `PlayerAvatar.prefab`, relative to the avatar root):
  - `sphere` → `Ball Of Fun`, the physics ball. `Control` (`BallDriving`'s own object) sits 0.97 m below it (`BallDriving.Update`) and turns only about Y.
  - `scooterNormal` → `Control/Groundcheck`: tilts to the slope.
  - `scooterModel` → `Control/Groundcheck/Basket/SubBasket/ScooterBlockOut_01` (= `ScooterLook.MODEL`):
    - Its parent (`SubBasket`) leans, drifts and hops.
    - Its grandparent (`Basket`) wheelies.
    - Its world pose therefore carries all of it. `particleBasket` (the drift sparks) is its child `Particles`.
- **The effects read `BallDriving`'s public state:**
  - `TrailHandler` grows the boost trail on `OnBoostStart`, while `Boosting`.
  - `SkideeSkidoo` emits skid marks while `Grounded && Drifting`.
  - The drift sparks are `DriftSparkSet(tier)`. The particle references it uses are filled by `Start`, and `Start` never runs on a remote avatar's disabled `BallDriving`.
- **The match flow:**
  - The game scene's `SpawnManager.Start` asks for `StartingCutscene`. Its listener places every player and asks for `MainLoop` at once.
  - `CutsceneManager` plays for 9.2 s, then asks for `Tutorial`.
  - `TutorialManager` asks for `Begin` once as many handlers as `PlayerCount` have finished.
  - `OrderManager.InitGame` starts the waves and the clock on `Begin`.
  - The last wave's end loads `FinalAreaScene` through `SceneFlow`. There `SpawnManager.Start` asks for `GoldenCutscene`, then `FinalPackage`; delivering the golden order leads to `Results`.
- **`OrderManager` runs only on the authority** (Phase 2B). `DynamicNumberUI` reads its `GameTimer`, `GameStarted`, `FinalOrderActive` and `FinalOrderValue`.
- **The local loader (`SceneManager.LoadSceneAsync`)** loads with activation off. For a match it then waits 5 s and asks every player to press A; `ConfirmLoad()` activates. Going back to the menu confirms by itself.
- **A remote avatar's `OrderHandler` stays enabled** (its `Start` lists it with `ScoreManager`, for placings and results). So:
  - `OnEnable` hooks `AttemptSteal` onto its scooter's `OnBoostStart`.
  - `InitHandler` (on `StartingCutscene`) hooks `ball.FreezeBall` onto `OrderManager.OnMainGameFinishes`. `FreezeBall` on a `BallDriving` that never started throws, because `sphereBody` is null.
- **Netcode 1.15.1:**
  - A `NetworkTransform`'s authority commits its transform at each network tick. The other side interpolates in `Update`.
  - `Teleport(position, rotation, scale)` on the authority jumps without interpolation.
  - `OnIsServerAuthoritative()` returning `false` makes the owner the authority. The 3B spike showed it converging.
  - `[ServerRpc(RequireOwnership = false)]` with a `ServerRpcParams` gives `Receive.SenderClientId`.
  - `NetworkManager.ServerTime.Time` is the host's clock on every machine.
- **`SceneFlowTests.GameplayScripts_ReachTheSceneLoaderOnlyThroughSceneFlow`:** only `SceneManager.cs` and `ISceneFlow.cs` may name `SceneManager.Instance`. The online flow gets the loader through `SceneFlow.Loader` (Task 5).
- **Unity:** `SceneManager.sceneLoaded` fires after the new scene's `Awake`/`OnEnable` and before `Start`. That's the moment a client can hand the scene the host's waiting states.

## Review Focus

1. **A slow client:** its new scene is still coming up when the host's first states arrive (`StartingCutscene`, `MainLoop`). It should apply them once its scene is up, in order, so its opening spawn isn't skipped.
   - Test: Task 5, `OnlineMatchNetworkTests.Joining_…` (the spawn check).
2. **A machine leaves while the others load:** the host should stop waiting for it and start the match.
   - Tests: Task 5, `LoadRoundTests.AMachineThatLeaves_IsNoLongerWaitedFor` and `OnlineSceneFlowTests.Host_AMachineThatLeavesDuringTheLoad_IsNoLongerWaitedFor`.
3. **A scooter spawns or respawns far away:** other machines should see it jump, not slide across the map.
   - Test: Task 3, `OnlineScootersNetworkTests.ALongMove_Jumps_InsteadOfSlidingAcrossTheMap`.
4. **Another machine's scooter before its owner has shared a pose:** it should stay where it was (its podium), never at the world's origin.
   - Test: Task 4, `OnlineDrivingNetworkTests.AnotherMachinesScooter_StaysPut_UntilItsOwnerSharesAPose_ThenFollowsIt`.
5. **A client picks "main menu" in its own pause menu:** it should leave the session, not sit in the menu while the host still counts it in the match.
   - Test: Task 5, `OnlineSceneFlowTests.Client_ReturnToMenu_LeavesTheSessionFirst`.

## File map

| File | Task | Responsibility |
|---|---|---|
| `Assets/Scripts/Online/RemoteAvatar.cs` | 1 | `IsRemote(part)`: whether something belongs to another machine's scooter |
| `Assets/Scripts/Player/Respawn.cs`, `OrderHandler.cs`, `Assets/Scripts/World/OrderBeacon.cs` | 1 | Another machine's scooter doesn't respawn, steal, clash, freeze or collect orders here |
| `Assets/Tests/Editor/RemoteScooterRulesTests.cs` | 1 | EditMode |
| `Assets/Scripts/Online/DriveFlags.cs` | 2 | What a scooter is doing, in one byte (pure) |
| `Assets/Scripts/Player/BallDriving.cs` (CRLF) | 2 | `SCOOTER_BELOW_BALL`, `ScooterModel`, `Flags`, `ShowRemote` |
| `Assets/Tests/Editor/DriveFlagsTests.cs`, `RemoteDrivingTests.cs` | 2 | EditMode; Play Mode in the menu scene |
| `Assets/Scripts/Online/ScooterPose.cs` | 3 | A scooter's pose: read one, put one on another machine's scooter |
| `Assets/Scripts/Online/OwnerNetworkTransform.cs`, `OnlineScooter.cs` | 3 | The per-player network object that carries a scooter |
| `Assets/Prefabs/Online/OnlineScooter.prefab` (+ `.meta`) | 3 | Built in the mirror |
| `Assets/Scripts/Online/OnlinePrefabs.cs`, `Assets/Resources/Online/OnlinePrefabs.asset`, `Assets/DefaultNetworkPrefabs.asset` | 3 | The scooter prefab on the list (the assets come back from the mirror) |
| `Assets/Scripts/Online/OnlineSession.cs` | 3 | Registers, spawns and lists the scooters |
| `Assets/Tests/Editor/ScooterPoseTests.cs`, `OnlineScootersNetworkTests.cs`, `OnlinePrefabsTests.cs` | 3 | EditMode; Play Mode, empty scene, port 7794 |
| `Assets/Scripts/Online/OnlineDriving.cs`, `OnlineGame.cs` | 4 | Every frame: this machine's scooter out, the others in |
| `Assets/Tests/Editor/OnlineDrivingNetworkTests.cs` | 4 | Play Mode, menu scene, port 7795 |
| `Assets/Scripts/Menu/ISceneFlow.cs`, `SceneManager.cs` (CRLF) | 5 | `MatchScene`, `IMatchLoader` (held loads), `SceneFlow.Loader` |
| `Assets/Scripts/Online/LoadRound.cs` | 5 | Host: which machines still load a scene (pure) |
| `Assets/Scripts/Online/OnlineMatch.cs`, `OnlineSceneFlow.cs`, `OnlineGame.cs` | 5 | `IMatchLink`, the load and return messages, the online scene flow, states held while a scene comes up |
| `Assets/Tests/Editor/LoadRoundTests.cs`, `OnlineSceneFlowTests.cs`, `OnlineMatchNetworkTests.cs` | 5 | EditMode; Play Mode, menu and game scenes, port 7796 |
| `Assets/Scripts/Player/SpawnManager.cs`, `Assets/Scripts/Management/TutorialManager.cs`, `Assets/Scripts/Menu/ResultsMenu.cs` (CRLF) | 6 | Each machine places its own scooters; the tutorial is skipped online; the host ends the results |
| `Assets/Scripts/Online/MatchClock.cs` | 7 | The clock's arithmetic (pure) |
| `Assets/Scripts/Online/OnlineMatch.cs`, `OnlineGame.cs`, `Assets/Scripts/Management/OrderManager.cs` | 7 | The host's clock on every machine |
| `Assets/Tests/Editor/MatchClockTests.cs` | 7 | EditMode |
| `docs/online.md`, `docs/testing.md`, `EDITOR-TODO.md`, the roadmap | 8 | |

---

### Task 1: Another machine's scooter doesn't act here

**Files:**
- Modify: `Assets/Scripts/Online/RemoteAvatar.cs`
- Modify: `Assets/Scripts/Player/Respawn.cs` (`OnTriggerEnter`)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`OnTriggerEnter`, `AttemptSteal`, `InitHandler`)
- Modify: `Assets/Scripts/World/OrderBeacon.cs` (`OnTriggerStay`)
- Test: `Assets/Tests/Editor/RemoteScooterRulesTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `RemoteAvatar` (Phase 3B).
- Produces:
  - `public static bool RemoteAvatar.IsRemote(Component part)`: true when the part is under a `RemoteAvatar` (another machine's scooter), false for null.
  - `public static bool OrderBeacon.IsPlayersBall(Collider other)`: a scooter's ball (`Ball Of Fun`) that this machine drives.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/RemoteScooterRulesTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/RemoteScooterRulesTests.cs`):

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Online, another machine's scooter (a RemoteAvatar) doesn't act on this machine until orders and steals are shared
    /// (roadmap Tasks 3.5-3.6): it doesn't fall in water here, collect orders, steal, clash, or freeze when the main game
    /// ends. Its own machine does all that. EditMode: the scripts' messages are called directly on bare scooters
    /// </summary>
    public class RemoteScooterRulesTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            Reflect.SetSingleton<OrderManager>(null);
        }

        // A bare scooter: the avatar root (with a RemoteAvatar when it's another machine's), its ball and its Control
        GameObject Scooter(bool remote)
        {
            GameObject root = objects.NewGameObject(remote ? "P2" : "P1");
            if (remote)
                root.AddComponent<RemoteAvatar>();

            GameObject ball = new GameObject("Ball Of Fun");
            ball.transform.SetParent(root.transform);
            ball.AddComponent<SphereCollider>();
            ball.AddComponent<Respawn>();

            GameObject control = new GameObject("Control");
            control.transform.SetParent(root.transform);
            control.AddComponent<OrderHandler>();
            return root;
        }

        [Test]
        public void IsRemote_IsTrueForThePartsOfAnotherMachinesScooter()
        {
            GameObject remote = Scooter(true);
            GameObject local = Scooter(false);

            Assert.IsTrue(RemoteAvatar.IsRemote(remote.GetComponentInChildren<OrderHandler>()), "its order handler");
            Assert.IsTrue(RemoteAvatar.IsRemote(remote.GetComponentInChildren<SphereCollider>()), "its ball");
            Assert.IsFalse(RemoteAvatar.IsRemote(local.GetComponentInChildren<OrderHandler>()), "a scooter driven here");
            Assert.IsFalse(RemoteAvatar.IsRemote(null), "nothing");
        }

        [Test]
        public void OrderBeacons_OnlyServeTheScootersThisMachineDrives()
        {
            Assert.IsTrue(OrderBeacon.IsPlayersBall(Scooter(false).GetComponentInChildren<SphereCollider>()), "a scooter driven here");
            Assert.IsFalse(OrderBeacon.IsPlayersBall(Scooter(true).GetComponentInChildren<SphereCollider>()), "another machine's: orders are shared in roadmap Task 3.5");
            Assert.IsFalse(OrderBeacon.IsPlayersBall(objects.NewGameObject("Wall").AddComponent<BoxCollider>()), "not a scooter");
        }

        [Test]
        public void Water_DoesntRespawnAnotherMachinesScooter()
        {
            Respawn respawn = Scooter(true).GetComponentInChildren<Respawn>();
            GameObject water = objects.NewGameObject("Water");
            water.tag = "Water";
            Collider waterCollider = water.AddComponent<BoxCollider>();

            Reflect.Invoke(respawn, "OnTriggerEnter", waterCollider);

            Assert.IsFalse(respawn.IsRespawning, "its own machine respawns it");
        }

        [Test]
        public void AnotherMachinesScooter_AndALocalOne_DontTouchEachOtherHere()
        {
            GameObject remote = Scooter(true);
            GameObject local = Scooter(false);
            OrderHandler remoteHandler = remote.GetComponentInChildren<OrderHandler>();
            OrderHandler localHandler = local.GetComponentInChildren<OrderHandler>();

            Reflect.Invoke(remoteHandler, "OnTriggerEnter", local.GetComponentInChildren<SphereCollider>());
            Reflect.Invoke(localHandler, "OnTriggerEnter", remote.GetComponentInChildren<SphereCollider>());

            Assert.IsNull(localHandler.PlayerTouching, "another machine's scooter can't steal from or clash with it here");
            Assert.IsNull(remoteHandler.PlayerTouching, "nor be stolen from here");
        }

        [Test]
        public void AnotherMachinesBoost_DoesntStealHere()
        {
            OrderHandler remoteHandler = Scooter(true).GetComponentInChildren<OrderHandler>();
            remoteHandler.PlayerTouching = Scooter(false).GetComponentInChildren<OrderHandler>();

            Assert.DoesNotThrow(remoteHandler.AttemptSteal, "its own machine decides its steals: nothing is tried here");
        }

        [Test]
        public void TheEndOfTheMainGame_DoesntFreezeAnotherMachinesScooterHere()
        {
            OrderManager orders = objects.Add<OrderManager>();
            Reflect.SetSingleton(orders);
            OrderHandler remoteHandler = Scooter(true).GetComponentInChildren<OrderHandler>();

            Reflect.Invoke(remoteHandler, "InitHandler");

            Assert.IsNull(Reflect.GetField(orders, "OnMainGameFinishes"), "its own machine freezes it (here its BallDriving never started)");
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh RemoteScooterRulesTests`
Expected: `NO RESULTS` with `error CS0117: 'RemoteAvatar' does not contain a definition for 'IsRemote'`, and the same for `OrderBeacon.IsPlayersBall`.

- [ ] **Step 3: Add the two checks (no rules yet)**

In `Assets/Scripts/Online/RemoteAvatar.cs`, after `Create`:

```csharp
    /// <summary>
    /// Whether a part (a script, a collider) belongs to another machine's scooter. Online, such a scooter doesn't act on
    /// this machine until orders and steals are shared (roadmap Tasks 3.5-3.6): no water, orders, steals or clashes
    /// here. Its own machine does those, and its pose shows the result
    /// </summary>
    public static bool IsRemote(Component part)
    {
        return part != null && part.GetComponentInParent<RemoteAvatar>(true) != null;
    }
```

In `Assets/Scripts/World/OrderBeacon.cs`, replace the ball check in `OnTriggerStay`:

```csharp
        if (other.name == "Ball Of Fun")
        {
```

with:

```csharp
        if (IsPlayersBall(other))
        {
```

and add, after `OnTriggerStay`:

```csharp
    /// <summary>
    /// Whether a collider is the ball of a scooter this machine drives. Another machine's scooter (online) doesn't pick up
    /// or deliver here: orders stay the host's own until roadmap Task 3.5
    /// </summary>
    public static bool IsPlayersBall(Collider other)
    {
        return other.name == "Ball Of Fun" && !RemoteAvatar.IsRemote(other);
    }
```

- [ ] **Step 4: Run the tests: the checks pass, the rules fail**

Run: `bash tools/run-tests.sh RemoteScooterRulesTests`
Expected: 2 pass (`IsRemote_…`, `OrderBeacons_…`), and 4 fail for the right reasons:
- `Water_…`: a `NullReferenceException` in `Respawn.StartRespawnCoroutine`: it tried to respawn.
- `AnotherMachinesScooter_…`: a `NullReferenceException` in `OrderHandler.OnTriggerEnter`, after it marked the scooters as touching.
- `AnotherMachinesBoost_…`: `DoesNotThrow` fails, because `AttemptSteal` tried to steal (a `NullReferenceException` in `IsBoosting`).
- `TheEndOfTheMainGame_…`: expected null, but a handler was hooked.

- [ ] **Step 5: Add the rules**

`Assets/Scripts/Player/Respawn.cs`, at the top of `OnTriggerEnter`:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        // Another machine's scooter (online): its own machine respawns it (this script is off there, but triggers still arrive)
        if (RemoteAvatar.IsRemote(this))
            return;

        if (other.tag == "Water")
```

`Assets/Scripts/Player/OrderHandler.cs`:

1. In `AttemptSteal`, replace:

```csharp
    public void AttemptSteal()
    {
        // Online, only the host decides steals and clashes
        if (!GameAuthority.IsAuthority)
            return;
```

with:

```csharp
    public void AttemptSteal()
    {
        // Online, only the host decides steals and clashes, and not yet for another machine's scooter (roadmap Task 3.6)
        if (!GameAuthority.IsAuthority || RemoteAvatar.IsRemote(this))
            return;
```

2. In `OnTriggerEnter`, replace:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        // Online, only the host decides steals and clashes
        if (!GameAuthority.IsAuthority)
            return;
```

with:

```csharp
    private void OnTriggerEnter(Collider other)
    {
        // Online, only the host decides steals and clashes, and not yet for another machine's scooter (roadmap Task 3.6)
        if (!GameAuthority.IsAuthority || RemoteAvatar.IsRemote(this))
            return;
```

and, a few lines below, replace:

```csharp
            if(otherHandler == this)
            {
                return;
            }
```

with:

```csharp
            if(otherHandler == this || RemoteAvatar.IsRemote(otherHandler))
            {
                return;
            }
```

3. `InitHandler` becomes:

```csharp
    private void InitHandler()
    {
        // Another machine's scooter: its own machine freezes it when the main game ends (here it never started driving)
        if (RemoteAvatar.IsRemote(this))
            return;

        OrderManager.Instance.OnMainGameFinishes += () => ball.FreezeBall(true);
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh RemoteScooterRulesTests`
Expected: 6/6 pass.

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 265 total, 265 passed` (259 + 6). `LocalMatchSmokeTest` still passes: no local scooter is under a `RemoteAvatar`.

---

### Task 2: What a scooter is doing, and another machine's scooter showing it

**Files:**
- Create: `Assets/Scripts/Online/DriveFlags.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/BallDriving.cs` (**CRLF: Edit tool only**)
- Test: `Assets/Tests/Editor/DriveFlagsTests.cs`, `Assets/Tests/Editor/RemoteDrivingTests.cs` (+ `.meta` each)

**Interfaces:**
- Consumes: `RemoteAvatar.Create`, `RemoteAvatar.Driving` (Phase 3B); the Task 1 rules, so another machine's boost (`OnBoostStart`) steals nothing.
- Produces:
  - **`public readonly struct DriveFlags`:**
    - `DriveFlags(byte value)` and `DriveFlags(bool boosting, bool drifting, bool driftRight, int driftTier, bool grounded, bool phasing)`. The tier is clamped to 0–3.
    - `byte Value`, `bool Boosting`, `bool Drifting`, `bool DriftRight`, `int DriftTier`, `bool Grounded`, `bool Phasing`.
  - **`BallDriving`:**
    - `public const float SCOOTER_BELOW_BALL = 0.97f`: `Control` sits this far below the ball's centre.
    - `public Transform ScooterModel`.
    - `public DriveFlags Flags`: this scooter's state.
    - `public void ShowRemote(DriveFlags flags, float speed)`: another machine's scooter shows its owner's state. The same flags twice start one boost; `speed` is in m/s.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/DriveFlagsTests.cs`:

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What a scooter is doing, in one byte: each flag and the drift tier come back out as they went in, and a scooter
    /// driven here reports its own state. EditMode
    /// </summary>
    public class DriveFlagsTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void EachFlag_ComesBackOutOnItsOwn()
        {
            Assert.IsTrue(new DriveFlags(true, false, false, 0, false, false).Boosting, "boosting");
            Assert.IsTrue(new DriveFlags(false, true, false, 0, false, false).Drifting, "drifting");
            Assert.IsTrue(new DriveFlags(false, false, true, 0, false, false).DriftRight, "drifting right");
            Assert.IsTrue(new DriveFlags(false, false, false, 0, true, false).Grounded, "on the ground");
            Assert.IsTrue(new DriveFlags(false, false, false, 0, false, true).Phasing, "phasing");
            Assert.AreEqual(0, new DriveFlags(false, false, false, 0, false, false).Value, "nothing set");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheDriftTier_ComesBackOut_BesideTheOtherFlags(int tier)
        {
            DriveFlags flags = new DriveFlags(true, true, true, tier, true, true);

            Assert.AreEqual(tier, flags.DriftTier);
            Assert.IsTrue(flags.Boosting && flags.Drifting && flags.DriftRight && flags.Grounded && flags.Phasing);
        }

        [Test]
        public void TheByte_ComesBackTheSame_OnAnotherMachine()
        {
            DriveFlags sent = new DriveFlags(false, true, true, 2, true, false);
            DriveFlags received = new DriveFlags(sent.Value);

            Assert.AreEqual(sent.Value, received.Value);
            Assert.AreEqual(2, received.DriftTier);
            Assert.IsTrue(received.Drifting && received.DriftRight && received.Grounded);
            Assert.IsFalse(received.Boosting || received.Phasing);
        }

        [Test]
        public void ATierOutsideZeroToThree_IsClamped()
        {
            Assert.AreEqual(3, new DriveFlags(false, false, false, 7, false, false).DriftTier);
            Assert.AreEqual(0, new DriveFlags(false, false, false, -1, false, false).DriftTier);
        }

        [Test]
        public void AScooterDrivenHere_ReportsWhatItsDoing()
        {
            BallDriving driving = objects.Add<BallDriving>();
            Reflect.SetField(driving, "drifting", true);
            Reflect.SetField(driving, "driftDirection", 1);
            Reflect.SetField(driving, "driftTier", 2);
            Reflect.SetField(driving, "grounded", true);

            DriveFlags flags = driving.Flags;

            Assert.IsTrue(flags.Drifting && flags.DriftRight && flags.Grounded, "a tier-2 drift to the right, on the ground");
            Assert.AreEqual(2, flags.DriftTier);
            Assert.IsFalse(flags.Boosting || flags.Phasing);
        }
    }
}
```

`Assets/Tests/Editor/RemoteDrivingTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Another machine's scooter shows what its owner's is doing (BallDriving.ShowRemote): the boost trail starts once per
    /// boost, skid marks while it drifts on the ground, its drift tier's sparks, and the rider's speed. In the real menu
    /// scene (a scooter's scripts need its managers). Enters Play Mode (about 10 s)
    /// </summary>
    public class RemoteDrivingTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        // Counts the boosts a scooter starts (its boost trail grows on each)
        class BoostRecorder
        {
            public int Boosts;

            public BoostRecorder(BallDriving driving)
            {
                driving.OnBoostStart += OnBoost;
            }

            void OnBoost()
            {
                Boosts++;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static bool SparkOn(Transform sparks, int child)
        {
            return sparks.GetChild(child).gameObject.activeSelf;
        }

        [UnityTest]
        public IEnumerator AnotherMachinesScooter_ShowsItsOwnersBoostDriftAndSpeed()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            RemoteAvatar remote = RemoteAvatar.Create(OnlinePrefabs.Load().RemoteAvatarPrefab, null);
            yield return null; // its scripts start
            BallDriving driving = remote.Driving;
            BoostRecorder boosts = new BoostRecorder(driving);
            Transform sparks = driving.ScooterModel.Find("Particles"); // base, wide, flare 1, flare 2, flare 3, long
            TrailRenderer skid = (TrailRenderer)Reflect.GetField(remote.GetComponentInChildren<SkideeSkidoo>(), "frontTire");
            Animator rider = (Animator)Reflect.GetField(driving, "playerAnimator");

            // A boost: its trail starts once, however often the same flags arrive
            DriveFlags boosting = new DriveFlags(true, false, false, 0, true, false);
            driving.ShowRemote(boosting, 25f);
            driving.ShowRemote(boosting, 25f);
            Assert.IsTrue(driving.Boosting, "boosting");
            Assert.AreEqual(1, boosts.Boosts, "one boost, one trail");

            // A tier-2 drift to the right, on the ground: skid marks, and that tier's sparks
            driving.ShowRemote(new DriveFlags(false, true, true, 2, true, false), 15f);
            yield return null; // the skid marks follow in their Update
            Assert.IsFalse(driving.Boosting, "the boost ended");
            Assert.IsTrue(driving.Drifting, "drifting");
            Assert.IsTrue(skid.emitting, "skid marks");
            Assert.IsTrue(SparkOn(sparks, 0) && SparkOn(sparks, 1) && SparkOn(sparks, 2) && SparkOn(sparks, 3) && SparkOn(sparks, 5), "tier-2 sparks");
            Assert.IsFalse(SparkOn(sparks, 4), "no tier-3 flare");
            Assert.AreEqual(5f, rider.GetFloat(HashReference._speedFloat), 0.01f, "15 m/s: the rider's speed is 5 of 10");

            // Nothing going on: the sparks and skid marks stop
            driving.ShowRemote(new DriveFlags(0), 0f);
            yield return null;
            Assert.IsFalse(SparkOn(sparks, 0), "sparks off");
            Assert.IsFalse(skid.emitting, "skid marks stop");

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}
```

`bash tools/newmeta.sh` both new test files.

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "DriveFlagsTests|RemoteDrivingTests"`
Expected: `NO RESULTS` with `error CS0246: The type or namespace name 'DriveFlags' could not be found`.

- [ ] **Step 3: Add the flags and `BallDriving`'s new members, without behaviour yet**

`Assets/Scripts/Online/DriveFlags.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/DriveFlags.cs`):

```csharp
using UnityEngine;

/// <summary>
/// What a scooter is doing, packed in one byte: boosting, drifting (which way, and which spark tier), on the ground,
/// phasing through a building. Online, a scooter's own machine sends it with the pose (OnlineScooter), and other machines
/// show the boost trail, skid marks and drift sparks from it (BallDriving.ShowRemote)
/// </summary>
public readonly struct DriveFlags
{
    const int BOOSTING = 1 << 0;
    const int DRIFTING = 1 << 1;
    const int DRIFT_RIGHT = 1 << 2;
    const int TIER_SHIFT = 3;
    const int TIER_MASK = 3 << TIER_SHIFT; // tiers 0-3, in two bits
    const int GROUNDED = 1 << 5;
    const int PHASING = 1 << 6;

    /// <summary>The byte that travels</summary>
    public readonly byte Value;

    public DriveFlags(byte value)
    {
        Value = value;
    }

    public DriveFlags(bool boosting, bool drifting, bool driftRight, int driftTier, bool grounded, bool phasing)
    {
        int value = Mathf.Clamp(driftTier, 0, 3) << TIER_SHIFT;
        if (boosting)
            value |= BOOSTING;
        if (drifting)
            value |= DRIFTING;
        if (driftRight)
            value |= DRIFT_RIGHT;
        if (grounded)
            value |= GROUNDED;
        if (phasing)
            value |= PHASING;
        Value = (byte)value;
    }

    public bool Boosting { get { return (Value & BOOSTING) != 0; } }

    public bool Drifting { get { return (Value & DRIFTING) != 0; } }

    /// <summary>Which way it drifts: right when true, left when false (the sparks come off the other side)</summary>
    public bool DriftRight { get { return (Value & DRIFT_RIGHT) != 0; } }

    /// <summary>The drift's spark tier: 0 (no sparks yet) to 3</summary>
    public int DriftTier { get { return (Value & TIER_MASK) >> TIER_SHIFT; } }

    public bool Grounded { get { return (Value & GROUNDED) != 0; } }

    public bool Phasing { get { return (Value & PHASING) != 0; } }
}
```

`Assets/Scripts/Player/BallDriving.cs` (Edit tool):

1. After the line `    private const float WHEELIE_AMOUNT = 45f; //Degrees that the scooter rotates when doing a wheelie`, add:

```csharp
    public const float SCOOTER_BELOW_BALL = 0.97f; //How far below its ball's centre the scooter sits (it follows the ball there)
```

2. In `Update`, replace `transform.position = sphere.transform.position - new Vector3(0, 0.97f, 0); //makes the scooter follow the sphere` with:

```csharp
        transform.position = sphere.transform.position - new Vector3(0, SCOOTER_BELOW_BALL, 0); //makes the scooter follow the sphere
```

3. After `    [SerializeField] private Transform scooterModel;`, add:

```csharp
    public Transform ScooterModel { get { return scooterModel; } }
```

4. After the `SetGamepad` method, add (behaviour comes in Step 5):

```csharp

    /// <summary>
    /// What this scooter is doing (online, its machine sends this with its pose)
    /// </summary>
    public DriveFlags Flags
    {
        get { return new DriveFlags(0); }
    }

    /// <summary>
    /// Another machine's scooter (this script is off there, and drives nothing): shows what its owner's scooter is doing,
    /// so its boost trail, skid marks, drift sparks and rider behave as for a scooter driven here
    /// </summary>
    /// <param name="flags">What the owner's scooter is doing</param>
    /// <param name="speed">How fast it's going, in m/s</param>
    public void ShowRemote(DriveFlags flags, float speed)
    {
    }
```

- [ ] **Step 4: Run the tests: the flags pass, the scooter tests fail**

Run: `bash tools/run-tests.sh "DriveFlagsTests|RemoteDrivingTests"`
Expected:
- The flag tests pass: 7.
- `AScooterDrivenHere_ReportsWhatItsDoing` fails with "a tier-2 drift to the right, on the ground".
- `AnotherMachinesScooter_ShowsItsOwnersBoostDriftAndSpeed` fails with "boosting".

- [ ] **Step 5: Give them their behaviour**

In `BallDriving.cs`, the `Flags` getter becomes:

```csharp
        get { return new DriveFlags(boosting, drifting, driftDirection > 0, driftTier, grounded, phasing); }
```

`ShowRemote`'s body becomes:

```csharp
    {
        FindSparks();

        bool boostStarts = flags.Boosting && !boosting;
        boosting = flags.Boosting;
        drifting = flags.Drifting;
        grounded = flags.Grounded;
        phasing = flags.Phasing;

        int direction = flags.DriftRight ? 1 : -1;
        if (flags.DriftTier != driftTier || direction != driftDirection)
        {
            driftTier = flags.DriftTier;
            driftDirection = direction;
            DriftSparkSet(driftTier);
        }

        // The boost trail (TrailHandler) grows on each boost
        if (boostStarts)
            OnBoostStart?.Invoke();

        playerAnimator.SetFloat(HashReference._speedFloat, RangeMutations.Map_Linear(speed, 0, 30, 0, 10));
    }

    /// <summary>
    /// The drift sparks, in the particle basket. Start finds them; another machine's scooter never starts
    /// </summary>
    private void FindSparks()
    {
        if (baseSpark != null)
            return;

        baseSpark = particleBasket.GetChild(0).GetComponent<ParticleManipulator>();
        wideSpark = particleBasket.GetChild(1).GetComponent<ParticleManipulator>();
        flare1Spark = particleBasket.GetChild(2).GetComponent<ParticleManipulator>();
        flare2Spark = particleBasket.GetChild(3).GetComponent<ParticleManipulator>();
        flare3Spark = particleBasket.GetChild(4).GetComponent<ParticleManipulator>();
        longSpark = particleBasket.GetChild(5).GetComponent<ParticleManipulator>();
    }
```

In `Start`, replace the six `…Spark = particleBasket.GetChild(…)…` lines with:

```csharp
        FindSparks();
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "DriveFlagsTests|RemoteDrivingTests"`
Expected: 9/9 pass.

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 274 total, 274 passed`. `LocalMatchSmokeTest` still drifts and boosts: local scooters still find their sparks in `Start`.

---

### Task 3: Every machine's scooter on the network (`OnlineScooter`)

**Files:**
- Create: `Assets/Scripts/Online/ScooterPose.cs`, `OwnerNetworkTransform.cs`, `OnlineScooter.cs` (+ `.meta` each)
- Create (built in the mirror, then copied in): `Assets/Prefabs/Online/OnlineScooter.prefab` (+ `.meta`)
- Modify: `Assets/Scripts/Online/OnlinePrefabs.cs`, `Assets/Scripts/Online/OnlineSession.cs`
- Modify (copied from the mirror): `Assets/Resources/Online/OnlinePrefabs.asset`, `Assets/DefaultNetworkPrefabs.asset`
- Test: `Assets/Tests/Editor/ScooterPoseTests.cs`, `Assets/Tests/Editor/OnlineScootersNetworkTests.cs` (+ `.meta` each), `Assets/Tests/Editor/OnlinePrefabsTests.cs`

**Interfaces:**
- Consumes: `BallDriving.Sphere`, `ScooterModel`, `SCOOTER_BELOW_BALL` and `DriveFlags` (Task 2); `OnlineSession.SeatOf` (Phase 3B).
- Produces:
  - **`public struct ScooterPose`:**
    - Fields: `Vector3 Ball`, `float Heading` (degrees about Y), `Vector3 ModelPosition`, `Quaternion ModelRotation`.
    - `static ScooterPose Read(BallDriving driving)`.
    - `static void Apply(BallDriving driving, ScooterPose pose)`: moves the ball, puts `Control` `SCOOTER_BELOW_BALL` under it facing `Heading`, and gives the model its world pose.
  - **`public class OwnerNetworkTransform : NetworkTransform`**, whose owner is its authority.
  - **`public class OnlineScooter : NetworkBehaviour`:**
    - Constants: `PARKED_Y = -10000f`, `TELEPORT_DISTANCE = 10f`.
    - `int Seat` (the host sets it as it spawns).
    - `DriveFlags Flags`.
    - `bool HasPose`: its owner has shared a pose; until then it's parked.
    - `ScooterPose Pose`: its latest; smoothed on other machines.
    - `void Share(ScooterPose pose, DriveFlags flags)`: owner only; a no-op elsewhere. A first pose, or a move longer than `TELEPORT_DISTANCE`, teleports.
    - `internal bool Shown; internal Vector3 ShownBall; internal float ShownSpeed`: `OnlineDriving`'s memory, for the rider's speed.
  - **`OnlinePrefabs.ScooterPrefab`**.
  - **`OnlineSession`:**
    - `IReadOnlyList<OnlineScooter> Scooters`.
    - The host spawns a scooter per machine (owned by that machine), right after its `OnlinePlayer`.
    - `internal AddScooter` / `RemoveScooter`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/ScooterPoseTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A scooter's pose (what another machine shows of it): read from one scooter, it puts another in the same spot, down
    /// to the model's lean. EditMode, on instances of PlayerAvatar.prefab (their scripts don't run)
    /// </summary>
    public class ScooterPoseTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        readonly List<GameObject> scooters = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject scooter in scooters)
                Object.DestroyImmediate(scooter);
            scooters.Clear();
        }

        BallDriving NewScooter()
        {
            GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR));
            scooters.Add(avatar);
            return avatar.GetComponentInChildren<BallDriving>(true);
        }

        [Test]
        public void Read_TakesTheBall_TheHeading_AndTheModelsWorldPose()
        {
            BallDriving driving = NewScooter();
            driving.Sphere.transform.position = new Vector3(10, 2, -4);
            driving.transform.rotation = Quaternion.Euler(0, 75, 0);
            driving.ScooterModel.SetPositionAndRotation(new Vector3(10, 1.1f, -4), Quaternion.Euler(-20, 80, 5));

            ScooterPose pose = ScooterPose.Read(driving);

            Assert.AreEqual(new Vector3(10, 2, -4), pose.Ball);
            Assert.AreEqual(75f, pose.Heading, 0.01f);
            Assert.Less(Vector3.Distance(new Vector3(10, 1.1f, -4), pose.ModelPosition), 0.001f);
            Assert.Less(Quaternion.Angle(Quaternion.Euler(-20, 80, 5), pose.ModelRotation), 0.01f);
        }

        [Test]
        public void Apply_MovesTheBall_PutsTheScooterBelowIt_AndPosesTheModel()
        {
            BallDriving driving = NewScooter();
            ScooterPose pose = new ScooterPose
            {
                Ball = new Vector3(3, 5, 7),
                Heading = 200f,
                ModelPosition = new Vector3(3, 4.2f, 7),
                ModelRotation = Quaternion.Euler(10, 190, -3),
            };

            ScooterPose.Apply(driving, pose);

            Assert.AreEqual(pose.Ball, driving.Sphere.transform.position, "the ball");
            Assert.Less(Vector3.Distance(pose.Ball - new Vector3(0, BallDriving.SCOOTER_BELOW_BALL, 0), driving.transform.position), 0.001f, "the scooter follows its ball");
            Assert.Less(Quaternion.Angle(Quaternion.Euler(0, 200, 0), driving.transform.rotation), 0.01f, "its heading");
            Assert.Less(Vector3.Distance(pose.ModelPosition, driving.ScooterModel.position), 0.001f, "the model");
            Assert.Less(Quaternion.Angle(pose.ModelRotation, driving.ScooterModel.rotation), 0.01f, "the model's lean");
        }

        [Test]
        public void APoseReadFromOneScooter_PutsAnotherInTheSameSpot()
        {
            BallDriving owner = NewScooter();
            owner.Sphere.transform.position = new Vector3(-8, 0.5f, 12);
            owner.transform.SetPositionAndRotation(new Vector3(-8, 0.5f - BallDriving.SCOOTER_BELOW_BALL, 12), Quaternion.Euler(0, 33, 0));
            owner.ScooterModel.localRotation = Quaternion.Euler(-45, 90, 0); // mid-wheelie, leaning
            BallDriving copy = NewScooter();

            ScooterPose.Apply(copy, ScooterPose.Read(owner));

            Assert.Less(Vector3.Distance(owner.transform.position, copy.transform.position), 0.001f, "the scooter");
            Assert.Less(Vector3.Distance(owner.ScooterModel.position, copy.ScooterModel.position), 0.001f, "the model");
            Assert.Less(Quaternion.Angle(owner.ScooterModel.rotation, copy.ScooterModel.rotation), 0.01f, "the model's lean");
        }
    }
}
```

`Assets/Tests/Editor/OnlineScootersNetworkTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Every machine's scooter on every machine (OnlineScooter), with hosts and clients on this computer (127.0.0.1) in one
    /// empty Play Mode scene. The host spawns one per machine in its seat, parked until its owner shares a pose. A pose
    /// and the flags reach the other machines, only from the owner. A long move jumps. A scooter goes with its machine.
    /// A few seconds each. Each test enters Play Mode itself (never from a helper: the domain reloads there). No lambda
    /// here captures a local: after EnterPlayMode even assigning a captured local throws
    /// </summary>
    public class OnlineScootersNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7794;
        const float WAIT = 10f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
        }

        static OnlineScooter ScooterInSeat(OnlineSession session, int seat)
        {
            foreach (OnlineScooter scooter in session.Scooters)
            {
                if (scooter.Seat == seat)
                    return scooter;
            }
            return null;
        }

        static bool BothSeeBothScooters(OnlineSession host, OnlineSession client)
        {
            return ScooterInSeat(host, 0) != null && ScooterInSeat(host, 1) != null
                && ScooterInSeat(client, 0) != null && ScooterInSeat(client, 1) != null;
        }

        // A pose around a ball position: the model below it, leaning into a turn
        static ScooterPose PoseAt(Vector3 ball, float heading)
        {
            return new ScooterPose
            {
                Ball = ball,
                Heading = heading,
                ModelPosition = ball - new Vector3(0, 0.9f, 0),
                ModelRotation = Quaternion.Euler(-10, heading, 5),
            };
        }

        [UnityTest]
        public IEnumerator EveryMachine_HasAScooterInItsSeat_ParkedUntilItsOwnerSharesAPose()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(2, host.Scooters.Count, "the host has both scooters");
            Assert.AreEqual(2, client.Scooters.Count, "so does the client");
            Assert.IsTrue(ScooterInSeat(host, 0).IsOwner, "the host's own scooter sits in seat 0");
            Assert.IsFalse(ScooterInSeat(host, 1).IsOwner, "on the host, seat 1 is another machine's");
            Assert.IsTrue(ScooterInSeat(client, 1).IsOwner, "the client's own sits in seat 1");
            Assert.IsFalse(ScooterInSeat(host, 1).HasPose, "parked until its owner shares a pose");

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AScootersPoseAndFlags_ReachTheOtherMachine_OnlyFromItsOwner()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineScooter mine = ScooterInSeat(client, 1);
            OnlineScooter onHost = ScooterInSeat(host, 1);
            ScooterPose pose = PoseAt(new Vector3(12, 3, -40), 135f);
            DriveFlags drifting = new DriveFlags(false, true, true, 3, true, false);

            // Its owner shares it every frame, as OnlineDriving does
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onHost.HasPose && Vector3.Distance(pose.Ball, onHost.Pose.Ball) < 0.05f && onHost.Flags.Value == drifting.Value)
                && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(pose, drifting);
                yield return null;
            }

            Assert.Less(Vector3.Distance(pose.Ball, onHost.Pose.Ball), 0.05f, "the ball");
            Assert.AreEqual(135f, onHost.Pose.Heading, 0.5f, "the heading");
            Assert.Less(Vector3.Distance(pose.ModelPosition, onHost.Pose.ModelPosition), 0.05f, "the model");
            Assert.Less(Quaternion.Angle(pose.ModelRotation, onHost.Pose.ModelRotation), 1f, "the model's lean");
            Assert.AreEqual(drifting.Value, onHost.Flags.Value, "what it's doing");

            // Another machine can't move it
            onHost.Share(PoseAt(Vector3.zero, 0f), new DriveFlags(0));
            float until = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < until)
                yield return null;
            Assert.Less(Vector3.Distance(pose.Ball, onHost.Pose.Ball), 0.05f, "only its owner moves it");
            Assert.AreEqual(drifting.Value, onHost.Flags.Value, "only its owner sets what it's doing");

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ALongMove_Jumps_InsteadOfSlidingAcrossTheMap()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineScooter mine = ScooterInSeat(client, 1);
            OnlineScooter onHost = ScooterInSeat(host, 1);
            Vector3 here = new Vector3(0, 2, 0);
            Vector3 there = new Vector3(200, 2, 0);

            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onHost.HasPose && Vector3.Distance(here, onHost.Pose.Ball) < 0.05f) && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(PoseAt(here, 0f), new DriveFlags(0));
                yield return null;
            }
            Assert.Less(Vector3.Distance(here, onHost.Pose.Ball), 0.05f, "at the first spot");

            // A respawn or a spawn point: 200 m in one frame. On the host it never shows in between
            float worst = 0f;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Vector3.Distance(there, onHost.Pose.Ball) > 0.05f && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(PoseAt(there, 0f), new DriveFlags(0));
                yield return null;
                Vector3 shown = onHost.Pose.Ball;
                worst = Mathf.Max(worst, Mathf.Min(Vector3.Distance(shown, here), Vector3.Distance(shown, there)));
            }

            Assert.Less(Vector3.Distance(there, onHost.Pose.Ball), 0.05f, "it got there");
            Assert.Less(worst, 1f, "a jump: never shown in between");

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AScooter_GoesWithItsMachine()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.Scooters.Count > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, host.Scooters.Count, "the client's scooter went with it");
            Assert.AreEqual(0, host.Scooters[0].Seat, "the host's stays");
            host.Leave();
            yield return null;
        }
    }
}
```

In `Assets/Tests/Editor/OnlinePrefabsTests.cs`:

1. In `NetworkPrefabs_AreNetworkObjectsThatCarryData`, after `AssertNetworkPrefab(prefabs.MatchPrefab, typeof(OnlineMatch));`, add:

```csharp
            AssertNetworkPrefab(prefabs.ScooterPrefab, typeof(OnlineScooter));
```

2. Add this test after it:

```csharp
        [Test]
        public void TheScooterPrefab_CarriesItsPoseInTwoOwnerMovedProxies_ParkedUntilItsFirstPose()
        {
            GameObject scooter = OnlinePrefabs.Load().ScooterPrefab;
            OwnerNetworkTransform ball = scooter.transform.Find("Ball").GetComponent<OwnerNetworkTransform>();
            OwnerNetworkTransform model = scooter.transform.Find("Model").GetComponent<OwnerNetworkTransform>();

            Assert.IsTrue(ball.SyncPositionX && ball.SyncPositionY && ball.SyncPositionZ, "the ball's position");
            Assert.IsTrue(ball.SyncRotAngleY && !ball.SyncRotAngleX && !ball.SyncRotAngleZ, "the scooter's heading only");
            Assert.IsTrue(model.SyncPositionX && model.SyncPositionY && model.SyncPositionZ
                && model.SyncRotAngleX && model.SyncRotAngleY && model.SyncRotAngleZ, "the model's whole pose");
            Assert.IsTrue(model.UseQuaternionSynchronization, "lean, drift, slope and wheelie together");
            foreach (OwnerNetworkTransform proxy in new[] { ball, model })
            {
                Assert.IsFalse(proxy.SyncScaleX || proxy.SyncScaleY || proxy.SyncScaleZ, proxy.name + ": no scale");
                Assert.IsTrue(proxy.Interpolate, proxy.name + ": smoothed");
                Assert.AreEqual(OnlineScooter.PARKED_Y, proxy.transform.localPosition.y, proxy.name + " is parked");
            }
        }
```

`bash tools/newmeta.sh` both new test files.

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "ScooterPoseTests|OnlineScootersNetworkTests|OnlinePrefabsTests"`
Expected: `NO RESULTS` with `error CS0246: The type or namespace name 'ScooterPose' could not be found` (and `OnlineScooter`, `OwnerNetworkTransform`).

- [ ] **Step 3: Write the pose, the proxies' transform and the scooter object**

`Assets/Scripts/Online/ScooterPose.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Where a scooter is, as other machines show it: its ball, its heading, and its model's world pose. The model's pose
/// already has the slope, the lean, the drift angle, the hop and the wheelie in it. See docs/online.md
/// </summary>
public struct ScooterPose
{
    /// <summary>The ball's position: the scooter follows it</summary>
    public Vector3 Ball;

    /// <summary>The scooter's heading, in degrees about Y</summary>
    public float Heading;

    /// <summary>The model's world position</summary>
    public Vector3 ModelPosition;

    /// <summary>The model's world rotation</summary>
    public Quaternion ModelRotation;

    /// <summary>
    /// A scooter's pose now
    /// </summary>
    public static ScooterPose Read(BallDriving driving)
    {
        Transform model = driving.ScooterModel;
        return new ScooterPose
        {
            Ball = driving.Sphere.transform.position,
            Heading = driving.transform.eulerAngles.y,
            ModelPosition = model.position,
            ModelRotation = model.rotation,
        };
    }

    /// <summary>
    /// Puts another machine's scooter (a RemoteAvatar, whose ball is kinematic and controls are off) in a pose
    /// </summary>
    public static void Apply(BallDriving driving, ScooterPose pose)
    {
        driving.Sphere.transform.position = pose.Ball;
        driving.transform.SetPositionAndRotation(pose.Ball - new Vector3(0, BallDriving.SCOOTER_BELOW_BALL, 0), Quaternion.Euler(0, pose.Heading, 0));
        driving.ScooterModel.SetPositionAndRotation(pose.ModelPosition, pose.ModelRotation);
    }
}
```

`Assets/Scripts/Online/OwnerNetworkTransform.cs`:

```csharp
using Unity.Netcode.Components;

/// <summary>
/// A NetworkTransform its owner moves (Netcode's own is moved by the host). Online, each machine moves its own scooter's
/// proxies (OnlineScooter), and the other machines follow them, smoothed
/// </summary>
public class OwnerNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
```

`Assets/Scripts/Online/OnlineScooter.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One player's scooter in an online match, on every machine: where it is, and what it's doing (DriveFlags).
/// - Its own machine copies the scooter into it every frame (OnlineDriving). The other machines show it on that
///   player's RemoteAvatar.
/// - Two proxies carry the pose, each moved by its owner through an OwnerNetworkTransform:
///   - Ball: the ball's position and the scooter's heading.
///   - Model: the model's world pose.
/// - The proxies wait parked far below the map until the owner's first pose, so a scooter never flies in from the
///   world's origin.
/// - The host spawns one per machine, beside its OnlinePlayer. See docs/online.md
/// </summary>
public class OnlineScooter : NetworkBehaviour
{
    /// <summary>Where the proxies wait until their owner shares a pose: far below any map</summary>
    public const float PARKED_Y = -10000f;

    /// <summary>A move longer than this in one frame (a spawn point, a respawn) jumps on other machines instead of sliding</summary>
    public const float TELEPORT_DISTANCE = 10f;

    [SerializeField] Transform ball;
    [SerializeField] Transform model;

    readonly NetworkVariable<int> seat = new NetworkVariable<int>(-1);
    readonly NetworkVariable<byte> flags = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    OnlineSession session;
    OwnerNetworkTransform ballSync;
    OwnerNetworkTransform modelSync;

    // Other machines: what OnlineDriving last showed of it (for the rider's speed)
    internal bool Shown;
    internal Vector3 ShownBall;
    internal float ShownSpeed;

    /// <summary>The seat of the player whose scooter this is: their slot on every machine (-1 until the host gives one)</summary>
    public int Seat { get { return seat.Value; } }

    /// <summary>What the scooter is doing</summary>
    public DriveFlags Flags { get { return new DriveFlags(flags.Value); } }

    /// <summary>Whether its owner has shared a pose yet (until then it's parked)</summary>
    public bool HasPose { get { return ball.position.y > PARKED_Y / 2; } }

    /// <summary>Its latest pose (on other machines, smoothed by Netcode)</summary>
    public ScooterPose Pose
    {
        get
        {
            return new ScooterPose
            {
                Ball = ball.position,
                Heading = ball.eulerAngles.y,
                ModelPosition = model.position,
                ModelRotation = model.rotation,
            };
        }
    }

    public override void OnNetworkSpawn()
    {
        session = NetworkManager.GetComponent<OnlineSession>();
        ballSync = ball.GetComponent<OwnerNetworkTransform>();
        modelSync = model.GetComponent<OwnerNetworkTransform>();

        // The host seats it as it spawns it: a value set here goes out with the spawn itself
        if (IsServer && session != null)
            seat.Value = session.SeatOf(OwnerClientId);

        DontDestroyOnLoad(gameObject);

        if (session != null)
            session.AddScooter(this);
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
            session.RemoveScooter(this);
    }

    /// <summary>
    /// This machine's scooter: shares its pose and what it's doing with every machine. The first pose, and a long move (a
    /// spawn point, a respawn), jump. Does nothing for another machine's scooter
    /// </summary>
    public void Share(ScooterPose pose, DriveFlags driveFlags)
    {
        if (!IsOwner || !IsSpawned)
            return;

        Quaternion heading = Quaternion.Euler(0, pose.Heading, 0);
        if (!HasPose || Vector3.Distance(ball.position, pose.Ball) > TELEPORT_DISTANCE)
        {
            ballSync.Teleport(pose.Ball, heading, Vector3.one);
            modelSync.Teleport(pose.ModelPosition, pose.ModelRotation, Vector3.one);
        }
        else
        {
            ball.SetPositionAndRotation(pose.Ball, heading);
            model.SetPositionAndRotation(pose.ModelPosition, pose.ModelRotation);
        }

        if (flags.Value != driveFlags.Value)
            flags.Value = driveFlags.Value;
    }
}
```

`Assets/Scripts/Online/OnlinePrefabs.cs`:

1. After `[SerializeField] GameObject localPlayerPrefab;`, add `[SerializeField] GameObject scooterPrefab;`.
2. After the `LocalPlayerPrefab` property, add:

```csharp

    /// <summary>An OnlineScooter: the host spawns one per machine, beside its OnlinePlayer</summary>
    public GameObject ScooterPrefab { get { return scooterPrefab; } }
```

3. In the class summary, change "its two network objects" to "its three network objects".

`Assets/Scripts/Online/OnlineSession.cs` lists the scooters (spawning them comes in Step 7):

1. After `readonly List<OnlinePlayer> players = new List<OnlinePlayer>();`, add:

```csharp
    readonly List<OnlineScooter> scooters = new List<OnlineScooter>();
```

2. After the `Players` property, add:

```csharp

    /// <summary>Every machine's scooter in this session, as this machine has them (the host spawns them)</summary>
    public IReadOnlyList<OnlineScooter> Scooters { get { return scooters; } }
```

3. After `ClearMatch`, add:

```csharp

    // OnlineScooter reports here as it arrives and goes
    internal void AddScooter(OnlineScooter scooter)
    {
        scooters.Add(scooter);
    }

    internal void RemoveScooter(OnlineScooter scooter)
    {
        scooters.Remove(scooter);
    }
```

`bash tools/newmeta.sh` each of the three new scripts.

- [ ] **Step 4: Run the pose tests to see them pass**

Run: `bash tools/run-tests.sh ScooterPoseTests`
Expected: 3/3 pass. (This run also copies the new scripts into the mirror and compiles them there, for Step 5.)

- [ ] **Step 5: Build the scooter prefab in the mirror**

1. Write the one-time builder into the **mirror only**, as `../_doa_test_mirror/CapstoneYear4/Assets/Scripts/Editor/OnlineScooterBuilder.cs`:

```csharp
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

// ONE-TIME BUILDER (Phase 3C, Task 3): run in the test mirror with -executeMethod. Never add it to the project
public static class OnlineScooterBuilder
{
    public static void Build()
    {
        GameObject root = new GameObject("OnlineScooter");
        NetworkObject networkObject = root.AddComponent<NetworkObject>();
        networkObject.SynchronizeTransform = false; // the root stays put: its two proxies move
        networkObject.AutoObjectParentSync = false;
        OnlineScooter scooter = root.AddComponent<OnlineScooter>();

        OwnerNetworkTransform ball = Proxy(root, "Ball");
        ball.SyncRotAngleX = false; // the scooter's heading only
        ball.SyncRotAngleZ = false;
        OwnerNetworkTransform model = Proxy(root, "Model");
        model.UseQuaternionSynchronization = true; // lean, drift, slope and wheelie together

        SerializedObject fields = new SerializedObject(scooter);
        fields.FindProperty("ball").objectReferenceValue = ball.transform;
        fields.FindProperty("model").objectReferenceValue = model.transform;
        fields.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Online/OnlineScooter.prefab");
        Object.DestroyImmediate(root);

        OnlinePrefabs prefabs = OnlinePrefabs.Load();
        SerializedObject list = new SerializedObject(prefabs);
        list.FindProperty("scooterPrefab").objectReferenceValue = prefab;
        list.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(prefabs);
        AssetDatabase.SaveAssets();

        Debug.Log("ONLINE SCOOTER BUILT: " + prefab.GetComponent<NetworkObject>().PrefabIdHash);
    }

    // A pose proxy, parked until its owner's first pose
    static OwnerNetworkTransform Proxy(GameObject root, string name)
    {
        GameObject proxy = new GameObject(name);
        proxy.transform.SetParent(root.transform, false);
        proxy.transform.localPosition = new Vector3(0, OnlineScooter.PARKED_Y, 0);
        OwnerNetworkTransform sync = proxy.AddComponent<OwnerNetworkTransform>();
        sync.SyncScaleX = false;
        sync.SyncScaleY = false;
        sync.SyncScaleZ = false;
        return sync;
    }
}
```

2. Run it (about a minute; no Unity may be running on the mirror):

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -quit -projectPath "$(cygpath -w ../_doa_test_mirror/CapstoneYear4)" -executeMethod OnlineScooterBuilder.Build -logFile "$(cygpath -w Logs/online-scooter.log)"; grep "ONLINE SCOOTER BUILT\|error" Logs/online-scooter.log
```

Expected: `ONLINE SCOOTER BUILT: <non-zero>` and no errors.

3. Copy the results into the project:

```bash
M=../_doa_test_mirror/CapstoneYear4/Assets
cp "$M/Prefabs/Online/OnlineScooter.prefab" "$M/Prefabs/Online/OnlineScooter.prefab.meta" Assets/Prefabs/Online/
cp "$M/Resources/Online/OnlinePrefabs.asset" Assets/Resources/Online/OnlinePrefabs.asset
cp "$M/DefaultNetworkPrefabs.asset" Assets/DefaultNetworkPrefabs.asset
rm "$M/Scripts/Editor/OnlineScooterBuilder.cs" "$M/Scripts/Editor/OnlineScooterBuilder.cs.meta"
git status --short Assets/Prefabs Assets/Resources Assets/DefaultNetworkPrefabs.asset
git diff Assets/Resources/Online/OnlinePrefabs.asset
```

Expected:
- New: `Assets/Prefabs/Online/OnlineScooter.prefab` and its `.meta`.
- Modified: `OnlinePrefabs.asset`, with one line added (`scooterPrefab: {fileID: …, guid: <the prefab's guid>, type: 3}`), and `DefaultNetworkPrefabs.asset`, which now lists the scooter too.
- The builder is never in the project.

- [ ] **Step 6: Run the tests: the prefab passes, the session doesn't spawn scooters yet**

Run: `bash tools/run-tests.sh "OnlinePrefabsTests|OnlineScootersNetworkTests"`
Expected: `OnlinePrefabsTests` 4/4 pass. The 4 `OnlineScootersNetworkTests` fail: the session spawns no scooters yet, so they time out waiting for them ("the host has both scooters") or find none (a `NullReferenceException` on a missing scooter).

- [ ] **Step 7: The session registers and spawns the scooters**

`Assets/Scripts/Online/OnlineSession.cs`:

1. In `Create`, after `network.AddNetworkPrefab(session.prefabs.MatchPrefab);`, add:

```csharp
        network.AddNetworkPrefab(session.prefabs.ScooterPrefab);
```

2. In `StartHost`, after `Spawn(prefabs.PlayerPrefab, NetworkManager.ServerClientId, true); // seat 0`, add:

```csharp
        Spawn(prefabs.ScooterPrefab, NetworkManager.ServerClientId, false);
```

3. In `OnConnected`, after `Spawn(prefabs.PlayerPrefab, clientId, true);`, add:

```csharp
                Spawn(prefabs.ScooterPrefab, clientId, false);
```

4. In the class summary, "spawns the match and every machine's player" becomes "spawns the match, and every machine's player and scooter".

- [ ] **Step 8: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "OnlinePrefabsTests|OnlineScootersNetworkTests|OnlinePlayersNetworkTests|OnlineSessionNetworkTests"`
Expected: all pass. The Phase 3A and 3B session tests still pass; their hosts now spawn scooters too.

- [ ] **Step 9: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 282 total, 282 passed` (274 + 3 pose + 4 network + 1 prefab).

---

### Task 4: Driving together (`OnlineDriving`)

**Files:**
- Create: `Assets/Scripts/Online/OnlineDriving.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Online/OnlineGame.cs` (`Begin`, class summary)
- Test: `Assets/Tests/Editor/OnlineDrivingNetworkTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes:
  - `OnlineSession.Scooters` and `OnlineScooter.Seat` / `IsOwner` / `HasPose` / `Pose` / `Flags` / `Share` / `Shown*` (Task 3).
  - `ScooterPose.Read` / `Apply` (Task 3).
  - `BallDriving.Flags` / `ShowRemote` (Task 2).
  - `PlayerInstantiate.Roster` (Phase 2A/3B).
- Produces: **`OnlineDriving : MonoBehaviour`**, with `[DefaultExecutionOrder(1000)]`.
  - `void Begin(OnlineSession session)`.
  - In `LateUpdate`: for every scooter in the session, the owner shares its seat's local scooter; another machine's is shown on its seat's remote avatar, once it has a pose.
  - `OnlineGame` adds it to the session's object.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/OnlineDrivingNetworkTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/OnlineDrivingNetworkTests.cs`):

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Driving together, in the real menu scene on this computer (127.0.0.1).
    /// - This machine hosts with the game.
    /// - Another machine is a session without the game: its scooter moves only when the test shares a pose for it, as
    ///   its own OnlineDriving would.
    /// - Another machine's scooter stays put until its owner shares a pose, then goes where its owner has it, showing
    ///   what it does. This machine's scooter reaches the other machine.
    /// Enters Play Mode (about 20 s each). No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineDrivingNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7795;
        const string VERSION = "test";
        const float WAIT = 15f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
        }

        static OnlineScooter ScooterInSeat(OnlineSession session, int seat)
        {
            foreach (OnlineScooter scooter in session.Scooters)
            {
                if (scooter.Seat == seat)
                    return scooter;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator AnotherMachinesScooter_StaysPut_UntilItsOwnerSharesAPose_ThenFollowsIt()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && ScooterInSeat(other, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            BallDriving shown = ScooterIn(1);
            Vector3 podium = shown.Sphere.transform.position;

            // Their machine hasn't shared a pose yet: their scooter stays on its podium, never at the world's origin
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
                yield return null;
            Assert.Less(Vector3.Distance(podium, shown.Sphere.transform.position), 0.01f, "on its podium");

            // Their machine shares a pose every frame, as its OnlineDriving would: a tier-1 drift, off the podium
            OnlineScooter theirs = ScooterInSeat(other, 1);
            ScooterPose pose = new ScooterPose
            {
                Ball = podium + new Vector3(4, 0, 2),
                Heading = 90f,
                ModelPosition = podium + new Vector3(4, -0.9f, 2),
                ModelRotation = Quaternion.Euler(-15, 95, 0),
            };
            DriveFlags drifting = new DriveFlags(false, true, false, 1, true, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Vector3.Distance(pose.Ball, shown.Sphere.transform.position) < 0.05f && shown.Drifting)
                && Time.realtimeSinceStartup < deadline)
            {
                theirs.Share(pose, drifting);
                yield return null;
            }

            Assert.Less(Vector3.Distance(pose.Ball, shown.Sphere.transform.position), 0.05f, "the ball");
            Assert.Less(Vector3.Distance(pose.Ball - new Vector3(0, BallDriving.SCOOTER_BELOW_BALL, 0), shown.transform.position), 0.05f, "the scooter");
            Assert.AreEqual(90f, shown.transform.eulerAngles.y, 1f, "its heading");
            Assert.Less(Vector3.Distance(pose.ModelPosition, shown.ScooterModel.position), 0.05f, "the model");
            Assert.IsTrue(shown.Drifting, "what it's doing");

            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator ThisMachinesScooter_ReachesTheOtherMachine()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (ScooterInSeat(other, 0) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            BallDriving mine = ScooterIn(0);
            OnlineScooter onOther = ScooterInSeat(other, 0);

            // This machine shares its scooter every frame, and the other machine has it where it is
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onOther.HasPose && Vector3.Distance(mine.Sphere.transform.position, onOther.Pose.Ball) < 0.05f)
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(onOther.HasPose, "this machine shares its scooter");
            Assert.Less(Vector3.Distance(mine.Sphere.transform.position, onOther.Pose.Ball), 0.05f, "where it is");
            Assert.AreEqual(mine.transform.eulerAngles.y, onOther.Pose.Heading, 1f, "its heading");
            Assert.Less(Vector3.Distance(mine.ScooterModel.position, onOther.Pose.ModelPosition), 0.05f, "its model");

            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh OnlineDrivingNetworkTests`
Expected: both fail. Nothing copies a scooter yet:
- The first gets past "on its podium", then fails at "the ball" (still on its podium).
- The second fails at "this machine shares its scooter".

- [ ] **Step 3: Write `OnlineDriving` and add it with the game**

`Assets/Scripts/Online/OnlineDriving.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/OnlineDriving.cs`):

```csharp
using UnityEngine;

/// <summary>
/// Online, every frame:
/// - This machine's scooter goes into its OnlineScooter.
/// - Every other machine's OnlineScooter moves that player's RemoteAvatar: its pose, plus its boost trail, skid marks,
///   drift sparks and rider (BallDriving.ShowRemote). It waits where it is until its owner has shared a pose.
/// It runs after everything else. So it sends this machine's scooter as the frame left it, and shows the others after
/// Netcode moved their proxies. OnlineGame adds it. See docs/online.md
/// </summary>
[DefaultExecutionOrder(1000)]
public class OnlineDriving : MonoBehaviour
{
    /// <summary>How much of the gap the rider's shown speed closes each frame</summary>
    const float SPEED_SMOOTHING = 0.2f;

    /// <summary>The fastest speed the rider shows (a jump isn't speed)</summary>
    const float TOP_SPEED = 60f;

    OnlineSession session;

    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
    }

    void LateUpdate()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (session == null || players == null)
            return;

        foreach (OnlineScooter scooter in session.Scooters)
        {
            int seat = scooter.Seat;
            if (seat < 0 || seat >= Constants.MAX_PLAYERS)
                continue;

            PlayerSlot slot = players.Roster[seat];
            if (slot == null)
                continue; // nobody in that seat here yet (this machine's player may be moving into it)

            BallDriving driving = slot.Player.GetComponentInChildren<BallDriving>(true);
            if (scooter.IsOwner)
            {
                if (slot.IsLocal)
                    scooter.Share(ScooterPose.Read(driving), driving.Flags);
            }
            else if (!slot.IsLocal && scooter.HasPose)
                Show(scooter, driving);
        }
    }

    // Another machine's scooter, where its owner has it, doing what it does
    static void Show(OnlineScooter scooter, BallDriving driving)
    {
        ScooterPose pose = scooter.Pose;
        float speed = 0f;
        if (scooter.Shown && Time.deltaTime > 0f)
            speed = Mathf.Min(Vector3.Distance(scooter.ShownBall, pose.Ball) / Time.deltaTime, TOP_SPEED);
        scooter.ShownSpeed = Mathf.Lerp(scooter.ShownSpeed, speed, SPEED_SMOOTHING);
        scooter.ShownBall = pose.Ball;
        scooter.Shown = true;

        ScooterPose.Apply(driving, pose);
        driving.ShowRemote(scooter.Flags, scooter.ShownSpeed);
    }
}
```

In `Assets/Scripts/Online/OnlineGame.cs`:

1. In `Begin`, after `choices = prefabs.LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);`, add:

```csharp

        // Every frame, this machine's scooter goes out and the other machines' come in
        gameObject.AddComponent<OnlineDriving>().Begin(session);
```

2. In the class summary's list, after `/// - other machines' players as scooters in their seats;`, add:

```csharp
/// - every scooter's pose and what it's doing (OnlineDriving);
```

- [ ] **Step 4: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "OnlineDrivingNetworkTests|OnlineGameNetworkTests|OnlineGameTests|RemoteAvatarTests"`
Expected: all pass. The Phase 3B game tests still pass: until a pose arrives, a remote scooter stays where 3B put it.

- [ ] **Step 5: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 284 total, 284 passed`.

---

### Task 5: Loading the match together, and going back together

**Files:**
- Modify: `Assets/Scripts/Menu/ISceneFlow.cs` (`MatchScene`, `IMatchLoader`, `SceneFlow.Loader`)
- Modify: `Assets/Scripts/Menu/SceneManager.cs` (**CRLF: Edit tool only**)
- Create: `Assets/Scripts/Online/LoadRound.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`, `Assets/Scripts/Online/OnlineSceneFlow.cs`, `Assets/Scripts/Online/OnlineGame.cs`
- Test: `Assets/Tests/Editor/LoadRoundTests.cs`, `Assets/Tests/Editor/OnlineMatchNetworkTests.cs` (+ `.meta` each), `Assets/Tests/Editor/OnlineSceneFlowTests.cs` (rewritten)

**Interfaces:**
- Consumes: `ISceneFlow` and `SceneFlow` (Phase 2B); `OnlineMatch`, `OnlineSession` and `OnlineGame` (Phase 3B).
- Produces:
  - **`public enum MatchScene { Game, FinalOrder }`.**
  - **`public interface IMatchLoader`**, implemented by `SceneManager`:
    - `void LoadHeld(MatchScene scene)`, `event Action HeldSceneReady`, `void ShowHeld()`.
    - `event Action SceneUp`: after any scene load, once its objects are awake.
  - **`SceneFlow.Loader`** (`IMatchLoader`, null before the menu scene).
  - **`public class LoadRound`:** `LoadRound(MatchScene, IEnumerable<ulong> machines)`, `MatchScene Scene`, `void Loaded(ulong)`, `void Left(ulong)`, `bool Ready`.
  - **`public interface IMatchLink`**, implemented by `OnlineMatch`:
    - `bool IsHost`.
    - Events: `LoadRequested(MatchScene)`, `ShowRequested(MatchScene)`, `ReturnRequested()`, `MachineLoaded(ulong, MatchScene)`.
    - Methods: `RequestLoad`, `RequestShow`, `RequestReturn` (host only), `ReportLoaded` (client only).
  - **`OnlineSceneFlow`:**
    - `OnlineSceneFlow(ISceneFlow localFlow, IMatchLoader matchLoader)`.
    - `void Link(IMatchLink, Func<IEnumerable<ulong>> sessionMachines, ulong selfId, Action leaveSession)` and `void Unlink()`.
    - `void MachineLeft(ulong)`.
    - `bool Changing`, `event Action SceneChanged`.
  - **`OnlineGame`:**
    - Links the flow to the session's match.
    - On a client, it holds the host's states while `Changing` and applies them on `SceneChanged`, in order.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/LoadRoundTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Host, online: one scene load across the session's machines. It's ready once every machine that was in the session
    /// when it began has the scene loaded, or has left
    /// </summary>
    public class LoadRoundTests
    {
        [Test]
        public void Ready_OnlyOnceEveryMachineHasTheSceneLoaded()
        {
            LoadRound round = new LoadRound(MatchScene.Game, new ulong[] { 0, 1, 2 });

            round.Loaded(0);
            round.Loaded(2);
            Assert.IsFalse(round.Ready, "machine 1 is still loading");

            round.Loaded(1);
            Assert.IsTrue(round.Ready);
            Assert.AreEqual(MatchScene.Game, round.Scene);
        }

        [Test]
        public void AMachineThatLeaves_IsNoLongerWaitedFor()
        {
            LoadRound round = new LoadRound(MatchScene.FinalOrder, new ulong[] { 0, 1 });

            round.Loaded(0);
            round.Left(1);

            Assert.IsTrue(round.Ready);
        }

        [Test]
        public void AMachineOutsideTheRound_OrHeardTwice_ChangesNothing()
        {
            LoadRound round = new LoadRound(MatchScene.Game, new ulong[] { 0, 1 });

            round.Loaded(0);
            round.Loaded(0);
            round.Loaded(7);

            Assert.IsFalse(round.Ready);
        }

        [Test]
        public void TheMachines_AreThoseInTheSessionWhenItBegan()
        {
            List<ulong> machines = new List<ulong> { 0 };
            LoadRound round = new LoadRound(MatchScene.Game, machines);
            machines.Add(1); // a machine connecting later isn't waited for (and there's no mid-match joining)

            round.Loaded(0);

            Assert.IsTrue(round.Ready);
        }
    }
}
```

`Assets/Tests/Editor/OnlineSceneFlowTests.cs` (replaces the Phase 3B file):

```csharp
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    // The local loader's held loads, as the online flow sees them
    class FakeMatchLoader : IMatchLoader
    {
        public readonly List<MatchScene> HeldLoads = new List<MatchScene>();
        public int Shows;

        public event Action HeldSceneReady;
        public event Action SceneUp;

        public void LoadHeld(MatchScene scene) { HeldLoads.Add(scene); }
        public void ShowHeld() { Shows++; }

        public void RaiseHeldSceneReady() { HeldSceneReady?.Invoke(); }
        public void RaiseSceneUp() { SceneUp?.Invoke(); }
    }

    // A session's match, as the online flow sees it: what this machine sent, and messages to raise
    class FakeMatchLink : IMatchLink
    {
        public bool IsHost { get; set; }
        public readonly List<MatchScene> Loads = new List<MatchScene>();
        public readonly List<MatchScene> Shows = new List<MatchScene>();
        public readonly List<MatchScene> Reported = new List<MatchScene>();
        public int Returns;

        public event Action<MatchScene> LoadRequested;
        public event Action<MatchScene> ShowRequested;
        public event Action ReturnRequested;
        public event Action<ulong, MatchScene> MachineLoaded;

        public void RequestLoad(MatchScene scene) { Loads.Add(scene); }
        public void RequestShow(MatchScene scene) { Shows.Add(scene); }
        public void RequestReturn() { Returns++; }
        public void ReportLoaded(MatchScene scene) { Reported.Add(scene); }

        public void RaiseLoadRequested(MatchScene scene) { LoadRequested?.Invoke(scene); }
        public void RaiseShowRequested(MatchScene scene) { ShowRequested?.Invoke(scene); }
        public void RaiseReturnRequested() { ReturnRequested?.Invoke(); }
        public void RaiseMachineLoaded(ulong machine, MatchScene scene) { MachineLoaded?.Invoke(machine, scene); }
    }

    /// <summary>
    /// The scene flow while online, with a fake loader and a fake session:
    /// - The host starts every load on every machine, and shows the scene once every machine has it loaded or has left.
    /// - A client loads what the host asks, says when it's ready, shows it when told, and is "changing" until the new
    ///   scene is up.
    /// - Only the host starts loads. The host takes everyone back to the menu; a client going back alone leaves the
    ///   session first.
    /// - The loading screen never waits for A.
    /// </summary>
    public class OnlineSceneFlowTests
    {
        const ulong HOST = 0;
        const ulong CLIENT = 1;

        FakeSceneFlow local;
        FakeMatchLoader loader;
        FakeMatchLink link;
        OnlineSceneFlow flow;
        int leaves;
        int sceneChanges;

        [SetUp]
        public void SetUp()
        {
            local = new FakeSceneFlow();
            loader = new FakeMatchLoader();
            link = new FakeMatchLink();
            flow = new OnlineSceneFlow(local, loader);
            leaves = 0;
            sceneChanges = 0;
            flow.SceneChanged += CountSceneChange;
        }

        void CountSceneChange() { sceneChanges++; }

        void Leave() { leaves++; }

        static IEnumerable<ulong> BothMachines() { return new[] { HOST, CLIENT }; }

        void LinkAsHost()
        {
            link.IsHost = true;
            flow.Link(link, BothMachines, HOST, Leave);
        }

        void LinkAsClient()
        {
            link.IsHost = false;
            flow.Link(link, BothMachines, CLIENT, Leave);
        }

        [Test]
        public void Host_LoadingTheGame_AsksEveryMachine_AndHoldsItHereToo()
        {
            LinkAsHost();

            flow.LoadGameScene();

            CollectionAssert.AreEqual(new[] { MatchScene.Game }, link.Loads, "every machine is asked");
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, loader.HeldLoads, "it loads here, held behind the loading screen");
            Assert.AreEqual(0, loader.Shows, "nothing shown yet");
            CollectionAssert.IsEmpty(link.Shows);
        }

        [Test]
        public void Host_ShowsTheScene_OnlyOnceEveryMachineHasItLoaded()
        {
            LinkAsHost();
            flow.LoadGameScene();

            loader.RaiseHeldSceneReady();
            Assert.AreEqual(0, loader.Shows, "the other machine is still loading");

            link.RaiseMachineLoaded(CLIENT, MatchScene.Game);
            Assert.AreEqual(1, loader.Shows, "shown here");
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, link.Shows, "and on every machine");

            loader.RaiseSceneUp();
            Assert.AreEqual(0, sceneChanges, "the host holds no states for its scene");
        }

        [Test]
        public void Host_AReportForAnotherScene_DoesntCount()
        {
            LinkAsHost();
            flow.LoadGameScene();
            loader.RaiseHeldSceneReady();

            link.RaiseMachineLoaded(CLIENT, MatchScene.FinalOrder);

            Assert.AreEqual(0, loader.Shows);
        }

        [Test]
        public void Host_AMachineThatLeavesDuringTheLoad_IsNoLongerWaitedFor()
        {
            LinkAsHost();
            flow.LoadGameScene();
            loader.RaiseHeldSceneReady();

            flow.MachineLeft(CLIENT);

            Assert.AreEqual(1, loader.Shows, "the match starts without it");
        }

        [Test]
        public void Host_TheGoldenRoundsScene_LoadsTheSameWay()
        {
            LinkAsHost();

            flow.LoadFinalOrderScene();
            link.RaiseMachineLoaded(CLIENT, MatchScene.FinalOrder);
            loader.RaiseHeldSceneReady();

            CollectionAssert.AreEqual(new[] { MatchScene.FinalOrder }, link.Loads);
            CollectionAssert.AreEqual(new[] { MatchScene.FinalOrder }, loader.HeldLoads);
            Assert.AreEqual(1, loader.Shows);
        }

        [Test]
        public void Host_ReturnToMenu_TakesEveryoneBack()
        {
            LinkAsHost();

            flow.ReturnToMenu();

            Assert.AreEqual(1, link.Returns, "every machine");
            Assert.AreEqual(1, local.MenuReturns, "and this one");
            Assert.AreEqual(0, leaves, "the host stays in its session");
        }

        [Test]
        public void Client_NeverStartsALoadItself()
        {
            LinkAsClient();

            flow.LoadGameScene();
            flow.LoadFinalOrderScene();

            CollectionAssert.IsEmpty(link.Loads, "the host starts every load");
            CollectionAssert.IsEmpty(loader.HeldLoads);
        }

        [Test]
        public void Client_LoadsWhatTheHostAsks_AndSaysWhenItsReady()
        {
            LinkAsClient();

            link.RaiseLoadRequested(MatchScene.Game);
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, loader.HeldLoads, "held behind the loading screen");
            CollectionAssert.IsEmpty(link.Reported, "not loaded yet");

            loader.RaiseHeldSceneReady();
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, link.Reported, "the host hears it's ready");
            Assert.AreEqual(0, loader.Shows, "until the host says");
        }

        [Test]
        public void Client_ShowsWhenTheHostSays_AndIsChangingUntilTheSceneIsUp()
        {
            LinkAsClient();
            link.RaiseLoadRequested(MatchScene.Game);
            loader.RaiseHeldSceneReady();

            link.RaiseShowRequested(MatchScene.Game);
            Assert.AreEqual(1, loader.Shows, "shown");
            Assert.IsTrue(flow.Changing, "the scene is coming up");
            Assert.AreEqual(0, sceneChanges);

            loader.RaiseSceneUp();
            Assert.IsFalse(flow.Changing);
            Assert.AreEqual(1, sceneChanges, "the held states can go");
        }

        [Test]
        public void Client_TheHostTakingEveryoneBack_BringsItBackToo()
        {
            LinkAsClient();

            link.RaiseReturnRequested();
            Assert.AreEqual(1, local.MenuReturns, "back to the menu");
            Assert.IsTrue(flow.Changing, "the menu is coming up");
            Assert.AreEqual(0, leaves, "still in the session");

            loader.RaiseSceneUp();
            Assert.IsFalse(flow.Changing);
            Assert.AreEqual(1, sceneChanges);
        }

        [Test]
        public void Client_ReturnToMenu_LeavesTheSessionFirst()
        {
            LinkAsClient();

            flow.ReturnToMenu();

            Assert.AreEqual(1, leaves, "it can't come back into the host's match");
            Assert.AreEqual(1, local.MenuReturns);
            Assert.AreEqual(0, link.Returns, "only the host takes everyone");
        }

        [Test]
        public void Unlinked_ItFollowsNoSessionAnyMore()
        {
            LinkAsClient();
            flow.Unlink();

            link.RaiseLoadRequested(MatchScene.Game);
            loader.RaiseHeldSceneReady();
            flow.LoadGameScene();

            CollectionAssert.IsEmpty(loader.HeldLoads);
            CollectionAssert.IsEmpty(link.Reported);
        }

        [Test]
        public void ReturnToMenu_ListenersHearTheLocalLoader()
        {
            int heard = 0;
            flow.OnReturnToMenu += () => heard++;

            flow.ReturnToMenu();

            Assert.AreEqual(1, local.MenuReturns, "not in a session: straight back");
            Assert.AreEqual(1, heard, "listeners of the online flow hear the local loader");
        }

        [Test]
        public void TheLoadingScreen_NeverWaitsForA()
        {
            FakeSceneFlow waiting = new FakeSceneFlow { WaitingForConfirm = true };
            OnlineSceneFlow online = new OnlineSceneFlow(waiting, loader);

            Assert.IsFalse(online.WaitingForConfirm);
            online.ConfirmLoad();
            Assert.AreEqual(0, waiting.Confirms);
        }
    }
}
```

`Assets/Tests/Editor/OnlineMatchNetworkTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// An online match on this computer (127.0.0.1), from the menu into the game scene and back.
    /// - Hosting: this machine hosts with the game. Another machine is a session without the game, which reports its
    ///   load when the test says.
    /// - Joining: this machine joins with the game. The host is a session without the game, whose requests and states
    ///   the test sends.
    /// Loads the game scene: about a minute each. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineMatchNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string MENU = "Alex Player Testing";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7796;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        // What another machine hears from the host's scene flow
        class LoadRecorder
        {
            public readonly List<MatchScene> Loads = new List<MatchScene>();
            public readonly List<MatchScene> Shows = new List<MatchScene>();
            public int Returns;

            public LoadRecorder(OnlineMatch match)
            {
                match.LoadRequested += OnLoad;
                match.ShowRequested += OnShow;
                match.ReturnRequested += OnReturn;
            }

            void OnLoad(MatchScene scene) { Loads.Add(scene); }
            void OnShow(MatchScene scene) { Shows.Add(scene); }
            void OnReturn() { Returns++; }
        }

        // Which machines told the host they have the game loaded
        class ReportRecorder
        {
            public readonly List<ulong> Machines = new List<ulong>();

            public ReportRecorder(OnlineMatch match)
            {
                match.MachineLoaded += OnLoaded;
            }

            void OnLoaded(ulong machine, MatchScene scene)
            {
                if (scene == MatchScene.Game)
                    Machines.Add(machine);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static string ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
        }

        // The game scene is loaded here, held behind the loading screen until it's shown
        static bool HeldHere()
        {
            SceneManager loader = SceneFlow.Loader as SceneManager;
            return loader != null && Reflect.GetField(loader, "sceneLoad") != null;
        }

        static GameObject[] GameSpawns()
        {
            return (GameObject[])Reflect.GetField(SpawnManager.Instance, "gameSpawnPositions");
        }

        [UnityTest]
        public IEnumerator Hosting_TheMatchStarts_OnceEveryMachineHasItLoaded_AndTheHostTakesEveryoneBack()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            LoadRecorder theirs = new LoadRecorder(other.Match);
            Vector3 theirSpot = ScooterIn(1).Sphere.transform.position; // where the other machine's scooter stands in the menu

            // Everyone's ready: the host asks every machine to load the game, and loads it behind its own loading screen
            SceneFlow.Current.LoadGameScene();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (theirs.Loads.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, theirs.Loads, "the other machine is asked to load the game");

            // The host has it loaded; the other machine hasn't said so: the host waits
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!HeldHere() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(HeldHere(), "the game is loaded here, behind the loading screen");
            float until = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < until)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "still waiting for the other machine");
            CollectionAssert.IsEmpty(theirs.Shows, "nobody's shown the game yet");

            // The other machine has it too: the match starts everywhere
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GAME, ActiveScene());
            Assert.AreEqual(GameState.MainLoop, State(), "the opening cutscene started, and the players are placed");
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, theirs.Shows, "the other machine shows it too");

            // [Task 6 inserts here: the first wave starts without the tutorial, and each machine places its own scooter]

            // [Task 7 inserts here: the match clock reaches the other machine]

            // Back to the menu: the host takes everyone
            SceneFlow.Current.ReturnToMenu();
            deadline = Time.realtimeSinceStartup + 60;
            while (ActiveScene() != MENU && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "back in the menu");
            Assert.AreEqual(1, theirs.Returns, "the other machine is taken back too");

            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostsMatchLoadsHere_ItsStatesWaitForTheScene_AndTheHostBringsUsBack()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION); // another machine hosting: a session without the game
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(mine);
            mine.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player moved to seat 2");
            ReportRecorder reports = new ReportRecorder(host.Match);
            Vector3 hostSpot = ScooterIn(0).Sphere.transform.position; // where the host's scooter stands in the menu

            // The host asks for the game: it loads here behind the loading screen, and the host hears when it's ready
            host.Match.RequestLoad(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            CollectionAssert.AreEqual(new[] { mine.Network.LocalClientId }, reports.Machines, "this machine says it has the game loaded");
            Assert.AreEqual(MENU, ActiveScene(), "held until the host shows it");

            // The host shows it and starts the match at once: those states wait here until the scene is up
            host.Match.RequestShow(MatchScene.Game);
            host.Match.SendState(GameState.StartingCutscene);
            host.Match.SendState(GameState.MainLoop);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GAME, ActiveScene());
            Assert.AreEqual(GameState.MainLoop, State(), "the host's states arrived in order");
            Assert.Less(Vector3.Distance(GameSpawns()[1].transform.position, ScooterIn(1).Sphere.transform.position), 1.5f,
                "the opening cutscene put this machine's scooter on seat 2's spawn point: the state waited for the scene");

            // [Task 6 inserts here: the tutorial is skipped here too, and another machine's scooter isn't placed here]

            // [Task 7 inserts here: the host's match clock shows here]

            // The host takes everyone back to the menu
            host.Match.RequestReturn();
            deadline = Time.realtimeSinceStartup + 60;
            while (ActiveScene() != MENU && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "back in the menu");
            Assert.IsTrue(mine.IsRunning, "still in the session");

            mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}
```

`bash tools/newmeta.sh` the two new test files (`LoadRoundTests.cs`, `OnlineMatchNetworkTests.cs`).

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "LoadRoundTests|OnlineSceneFlowTests|OnlineMatchNetworkTests"`
Expected: `NO RESULTS` with `error CS0246` for `LoadRound`, `MatchScene`, `IMatchLoader` and `IMatchLink`.

- [ ] **Step 3: Write the held loads, the load round, the match's messages and the online flow**

1. `Assets/Scripts/Menu/ISceneFlow.cs`: after the `ISceneFlow` interface, add:

```csharp

/// <summary>The scenes an online match loads on every machine</summary>
public enum MatchScene { Game, FinalOrder }

/// <summary>
/// The local loader's online side (SceneManager): it loads a match scene behind the loading screen and holds it there
/// until the host has heard that every machine has it (OnlineSceneFlow)
/// </summary>
public interface IMatchLoader
{
    /// <summary>Loads a match scene behind the loading screen, and holds it until ShowHeld</summary>
    void LoadHeld(MatchScene scene);

    /// <summary>Raised once the held scene is loaded, ready to show</summary>
    event Action HeldSceneReady;

    /// <summary>Shows the held scene</summary>
    void ShowHeld();

    /// <summary>
    /// Raised after any scene loads, once its objects are awake (before their Start): a held scene shown, or the menu
    /// after going back
    /// </summary>
    event Action SceneUp;
}
```

and in `SceneFlow`, after the `Current` property, add:

```csharp

    /// <summary>
    /// The local loader's held loads, for online play (null until the menu scene's loader exists)
    /// </summary>
    public static IMatchLoader Loader
    {
        get { return SceneManager.Instance; }
    }
```

2. `Assets/Scripts/Menu/SceneManager.cs` (**CRLF, Edit tool**):
   - `public class SceneManager : SingletonMonobehaviour<SceneManager>, ISceneFlow` → `…, ISceneFlow, IMatchLoader`.
   - In `Start`, after `OnConfirmToLoad += SwapToSceneAfterConfirm;`, add `        UnityEngine.SceneManagement.SceneManager.sceneLoaded += RaiseSceneUp;`.
   - In `OnDisable`, after `OnConfirmToLoad -= SwapToSceneAfterConfirm;`, add `        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= RaiseSceneUp;`.
   - After `bool ISceneFlow.WaitingForConfirm { get { return enableConfirm; } }`, add:

```csharp

    // Online (IMatchLoader): a match scene loads behind the loading screen and waits for the host to show it
    public event Action HeldSceneReady;
    public event Action SceneUp;

    void IMatchLoader.LoadHeld(MatchScene scene)
    {
        if (scene == MatchScene.Game)
            StartGameLoad(true);
        else
            StartFinalOrderLoad(true);
    }

    void IMatchLoader.ShowHeld()
    {
        ConfirmLoad();
    }

    void RaiseSceneUp(Scene scene, LoadSceneMode mode)
    {
        SceneUp?.Invoke();
    }
```

   - Replace:

```csharp
    public void LoadGameScene()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != GameScene.BuildIndex && PlayerInstantiate.Instance.PlayerCount >= 1)
```

with:

```csharp
    public void LoadGameScene()
    {
        StartGameLoad(false);
    }

    // Loads the game behind the loading screen: every player presses A to show it, or online the host shows it
    private void StartGameLoad(bool holdForHost)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != GameScene.BuildIndex && PlayerInstantiate.Instance.PlayerCount >= 1)
```

   and in that method, `LoadSceneAsync(GameScene.BuildIndex, loadingScreenDelay, true, false)` → `LoadSceneAsync(GameScene.BuildIndex, loadingScreenDelay, true, false, holdForHost)`.
   - Replace:

```csharp
    public void LoadFinalOrderScene()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != FinalOrderScene.BuildIndex)
```

with:

```csharp
    public void LoadFinalOrderScene()
    {
        StartFinalOrderLoad(false);
    }

    // Loads the golden round's scene the same way as the game
    private void StartFinalOrderLoad(bool holdForHost)
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != FinalOrderScene.BuildIndex)
```

   and in that method, `LoadSceneAsync(FinalOrderScene.BuildIndex, loadingScreenDelay, true, false)` → `…, true, false, holdForHost)`.
   - `private IEnumerator LoadSceneAsync(int sceneToLoad, float delayTime, bool waitForConfirm, bool spawnMenu)` → `…, bool spawnMenu, bool holdForHost = false)`.
   - In it, replace:

```csharp
                sceneLoad = asyncLoad;
                spawnMenuBool = spawnMenu;

                // Wait for player confirm
```

with:

```csharp
                sceneLoad = asyncLoad;
                spawnMenuBool = spawnMenu;

                // Online: the host shows the scene once every machine has it loaded
                if (holdForHost)
                {
                    HeldSceneReady?.Invoke();
                    break;
                }

                // Wait for player confirm
```

3. `Assets/Scripts/Online/LoadRound.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/LoadRound.cs`):

```csharp
using System.Collections.Generic;

/// <summary>
/// Host, online: one scene load across the session's machines. The host shows the scene once every machine that was in
/// the session when the load began has it loaded, or has left (OnlineSceneFlow)
/// </summary>
public class LoadRound
{
    readonly HashSet<ulong> waitingFor;

    /// <summary>The scene being loaded</summary>
    public MatchScene Scene { get; private set; }

    /// <param name="machines">The machines in the session now (Netcode client ids, the host's included)</param>
    public LoadRound(MatchScene scene, IEnumerable<ulong> machines)
    {
        Scene = scene;
        waitingFor = new HashSet<ulong>(machines);
    }

    /// <summary>A machine has the scene loaded (one outside the round, or heard twice, changes nothing)</summary>
    public void Loaded(ulong machine)
    {
        waitingFor.Remove(machine);
    }

    /// <summary>A machine left the session: the round stops waiting for it</summary>
    public void Left(ulong machine)
    {
        waitingFor.Remove(machine);
    }

    /// <summary>Whether every machine has the scene loaded, or has left</summary>
    public bool Ready { get { return waitingFor.Count == 0; } }
}
```

4. `Assets/Scripts/Online/OnlineMatch.cs` becomes:

```csharp
using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The host's match, on every machine:
/// - The host's game state. The host sends each state it switches to, in order, and keeps the latest for players who
///   join later. States go one by one because the game sometimes switches twice in a frame (the opening cutscene hands
///   over to the main loop at once), and a shared value would only send the second.
/// - The scene flow's messages (IMatchLink). The host asks every client to load a match scene, show it, or go back to
///   the menu, and each client says when it has a scene loaded (OnlineSceneFlow).
/// </summary>
public class OnlineMatch : NetworkBehaviour, IMatchLink
{
    readonly NetworkVariable<GameState> state = new NetworkVariable<GameState>(GameState.Default);

    OnlineSession session;

    /// <summary>The host's latest state (Default until it sends one)</summary>
    public GameState State { get { return state.Value; } }

    /// <summary>Clients: each state the host switches to, in order</summary>
    public event Action<GameState> StateReceived;

    /// <summary>Clients: the host wants a match scene loaded, held behind the loading screen</summary>
    public event Action<MatchScene> LoadRequested;

    /// <summary>Clients: every machine has the scene loaded: show it</summary>
    public event Action<MatchScene> ShowRequested;

    /// <summary>Clients: the host is taking everyone back to the menu</summary>
    public event Action ReturnRequested;

    /// <summary>Host: a client has a scene loaded (its Netcode client id)</summary>
    public event Action<ulong, MatchScene> MachineLoaded;

    public override void OnNetworkSpawn()
    {
        session = NetworkManager.GetComponent<OnlineSession>();
        DontDestroyOnLoad(gameObject);

        if (session != null)
            session.SetMatch(this);
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
            session.ClearMatch(this);
    }

    /// <summary>
    /// Host: tells every client the host switched to a state. Does nothing on a client
    /// </summary>
    public void SendState(GameState newState)
    {
        if (!IsServer)
            return;

        state.Value = newState;
        StateClientRpc(newState);
    }

    /// <summary>Host: asks every client to load a match scene. Does nothing on a client</summary>
    public void RequestLoad(MatchScene scene)
    {
        if (IsServer)
            LoadClientRpc(scene);
    }

    /// <summary>Host: asks every client to show the scene it loaded. Does nothing on a client</summary>
    public void RequestShow(MatchScene scene)
    {
        if (IsServer)
            ShowClientRpc(scene);
    }

    /// <summary>Host: takes every client back to the menu. Does nothing on a client</summary>
    public void RequestReturn()
    {
        if (IsServer)
            ReturnClientRpc();
    }

    /// <summary>Client: tells the host it has a scene loaded. Does nothing on the host, which counts itself</summary>
    public void ReportLoaded(MatchScene scene)
    {
        if (!IsServer)
            LoadedServerRpc(scene);
    }

    // The host is a client too: it skips its own messages below, as it acted on them already

    [ClientRpc]
    void StateClientRpc(GameState newState)
    {
        if (IsServer)
            return;

        StateReceived?.Invoke(newState);
    }

    [ClientRpc]
    void LoadClientRpc(MatchScene scene)
    {
        if (!IsServer)
            LoadRequested?.Invoke(scene);
    }

    [ClientRpc]
    void ShowClientRpc(MatchScene scene)
    {
        if (!IsServer)
            ShowRequested?.Invoke(scene);
    }

    [ClientRpc]
    void ReturnClientRpc()
    {
        if (!IsServer)
            ReturnRequested?.Invoke();
    }

    [ServerRpc(RequireOwnership = false)]
    void LoadedServerRpc(MatchScene scene, ServerRpcParams rpc = default)
    {
        MachineLoaded?.Invoke(rpc.Receive.SenderClientId, scene);
    }
}
```

   (`IMatchLink.IsHost` is Netcode's own `NetworkBehaviour.IsHost`.)

5. `Assets/Scripts/Online/OnlineSceneFlow.cs` becomes:

```csharp
using System;
using System.Collections.Generic;

/// <summary>
/// An online session's side of the scene flow (OnlineMatch): the host's requests to every client, and clients' reports
/// to the host
/// </summary>
public interface IMatchLink
{
    /// <summary>Whether this machine hosts</summary>
    bool IsHost { get; }

    /// <summary>Clients: the host wants a match scene loaded, held behind the loading screen</summary>
    event Action<MatchScene> LoadRequested;

    /// <summary>Clients: every machine has the scene loaded: show it</summary>
    event Action<MatchScene> ShowRequested;

    /// <summary>Clients: the host is taking everyone back to the menu</summary>
    event Action ReturnRequested;

    /// <summary>Host: a client has a scene loaded (its Netcode client id)</summary>
    event Action<ulong, MatchScene> MachineLoaded;

    /// <summary>Host: asks every client to load a match scene</summary>
    void RequestLoad(MatchScene scene);

    /// <summary>Host: asks every client to show the scene it loaded</summary>
    void RequestShow(MatchScene scene);

    /// <summary>Host: takes every client back to the menu</summary>
    void RequestReturn();

    /// <summary>Client: tells the host it has a scene loaded</summary>
    void ReportLoaded(MatchScene scene);
}

/// <summary>
/// The scene flow while online (OnlineGame sets it as SceneFlow.Current). See docs/online.md.
/// - The host starts every load. Each machine loads the scene behind its loading screen (the local loader's held loads,
///   IMatchLoader), and the host shows it on every machine once all of them have it, or have left. There's no "press A".
/// - The host takes everyone back to the menu. A client going back on its own leaves the session first: it can't come
///   back into the host's match.
/// - While a client's new scene comes up (Changing), OnlineGame holds the host's states until SceneChanged.
/// - Netcode's scene management stays off.
/// </summary>
public class OnlineSceneFlow : ISceneFlow
{
    readonly ISceneFlow local;
    readonly IMatchLoader loader;
    IMatchLink link;
    Func<IEnumerable<ulong>> machines; // host: the machines in the session now, the host included
    ulong self;                       // this machine's Netcode client id
    Action leave;                     // client: leaves the session
    LoadRound round;                  // host: the load in progress
    MatchScene loading;               // client: the scene the host asked for

    /// <param name="localFlow">The local loader: it takes this machine back to the menu</param>
    /// <param name="matchLoader">The local loader's held loads (null loads nothing, as in tests without the menu scene)</param>
    public OnlineSceneFlow(ISceneFlow localFlow, IMatchLoader matchLoader)
    {
        local = localFlow;
        loader = matchLoader;
    }

    /// <summary>Client: the host showed a scene or took everyone back, and that scene isn't up here yet</summary>
    public bool Changing { get; private set; }

    /// <summary>Client: the scene the host showed (or the menu) is up here</summary>
    public event Action SceneChanged;

    /// <summary>
    /// Follows a session's match: the host's requests on a client, clients' reports on the host
    /// </summary>
    /// <param name="sessionMachines">Host: the machines in the session now, the host included</param>
    /// <param name="selfId">This machine's Netcode client id</param>
    /// <param name="leaveSession">Client: leaves the session</param>
    public void Link(IMatchLink matchLink, Func<IEnumerable<ulong>> sessionMachines, ulong selfId, Action leaveSession)
    {
        Unlink();
        link = matchLink;
        machines = sessionMachines;
        self = selfId;
        leave = leaveSession;

        link.LoadRequested += LoadForHost;
        link.ShowRequested += ShowForHost;
        link.ReturnRequested += ReturnForHost;
        link.MachineLoaded += MachineHasScene;
        if (loader != null)
        {
            loader.HeldSceneReady += HeldSceneReady;
            loader.SceneUp += SceneUp;
        }
    }

    /// <summary>Stops following the match (the session ended)</summary>
    public void Unlink()
    {
        if (link == null)
            return;

        link.LoadRequested -= LoadForHost;
        link.ShowRequested -= ShowForHost;
        link.ReturnRequested -= ReturnForHost;
        link.MachineLoaded -= MachineHasScene;
        if (loader != null)
        {
            loader.HeldSceneReady -= HeldSceneReady;
            loader.SceneUp -= SceneUp;
        }
        link = null;
        round = null;
        Changing = false;
    }

    bool IsHost { get { return link != null && link.IsHost; } }

    public void LoadGameScene()
    {
        Begin(MatchScene.Game);
    }

    public void LoadFinalOrderScene()
    {
        Begin(MatchScene.FinalOrder);
    }

    public void ReturnToMenu()
    {
        if (IsHost)
            link.RequestReturn();
        else if (link != null && leave != null)
            leave();

        if (local != null)
            local.ReturnToMenu();
    }

    public event Action OnReturnToMenu
    {
        add
        {
            if (local != null)
                local.OnReturnToMenu += value;
        }
        remove
        {
            if (local != null)
                local.OnReturnToMenu -= value;
        }
    }

    /// <summary>Online there's no "press A" on the loading screen</summary>
    public bool WaitingForConfirm { get { return false; } }

    public void ConfirmLoad()
    {
    }

    /// <summary>Host: a machine left the session: a load in progress stops waiting for it</summary>
    public void MachineLeft(ulong machine)
    {
        if (round == null)
            return;

        round.Left(machine);
        ShowIfEveryoneHasIt();
    }

    // Host: a match scene loads on every machine. A client's own countdown or clock starts nothing: the host decides
    void Begin(MatchScene scene)
    {
        if (!IsHost || loader == null)
            return;

        round = new LoadRound(scene, machines());
        link.RequestLoad(scene);
        loader.LoadHeld(scene);
    }

    // This machine has the scene loaded: the host counts itself, a client tells the host
    void HeldSceneReady()
    {
        if (IsHost)
        {
            if (round == null)
                return;
            round.Loaded(self);
            ShowIfEveryoneHasIt();
        }
        else if (link != null)
            link.ReportLoaded(loading);
    }

    // Host: a client has the scene loaded
    void MachineHasScene(ulong machine, MatchScene scene)
    {
        if (round == null || scene != round.Scene)
            return;

        round.Loaded(machine);
        ShowIfEveryoneHasIt();
    }

    void ShowIfEveryoneHasIt()
    {
        if (round == null || !round.Ready)
            return;

        MatchScene scene = round.Scene;
        round = null;
        link.RequestShow(scene);
        loader.ShowHeld();
    }

    // Client: the host asked for a scene
    void LoadForHost(MatchScene scene)
    {
        if (loader == null)
            return;

        loading = scene;
        loader.LoadHeld(scene);
    }

    // Client: every machine has it: show it
    void ShowForHost(MatchScene scene)
    {
        if (loader == null)
            return;

        Changing = true;
        loader.ShowHeld();
    }

    // Client: the host is taking everyone back to the menu
    void ReturnForHost()
    {
        Changing = true;
        if (local != null)
            local.ReturnToMenu();
    }

    void SceneUp()
    {
        if (!Changing)
            return;

        Changing = false;
        SceneChanged?.Invoke();
    }
}
```

6. `Assets/Scripts/Online/OnlineGame.cs`:
   - In `Begin`, `sceneFlow = new OnlineSceneFlow(SceneFlow.Current);` → `sceneFlow = new OnlineSceneFlow(SceneFlow.Current, SceneFlow.Loader);`.
   - In `OnDestroy`, before `if (GameManager.Instance != null)`, add:

```csharp
        if (sceneFlow != null)
            sceneFlow.Unlink();
```

   - In `OnRoleChanged`, after `SceneFlow.Current = null;`, add `        sceneFlow.Unlink();`.
   - At the top of `OnPlayerDespawned`, add:

```csharp
        // A machine that leaves during a load isn't waited for
        sceneFlow.MachineLeft(player.OwnerClientId);

```

   - At the top of `OnMatchSpawned`, add:

```csharp
        // The scene flow follows the host's loads (client) or the clients' reports (host)
        sceneFlow.Link(match, SessionMachines, session.Network.LocalClientId, LeaveSession);

```

   - After `OnMatchSpawned`, add:

```csharp

    // Host: the machines in the session now, the host included
    IEnumerable<ulong> SessionMachines()
    {
        return session.Network.ConnectedClientsIds;
    }

    // Client: going back to the menu alone leaves the session
    void LeaveSession()
    {
        session.Leave();
    }
```

   - In the class summary, `/// - this machine's role (GameAuthority) and the scene flow;` → `/// - this machine's role (GameAuthority) and the scene flow (loads with the whole session: OnlineSceneFlow);`.

- [ ] **Step 4: Run the tests: the flow passes; a slow client still misses the host's first states**

Run: `bash tools/run-tests.sh "LoadRoundTests|OnlineSceneFlowTests|OnlineMatchNetworkTests"`
Expected:
- `LoadRoundTests` 4/4 and `OnlineSceneFlowTests` 14/14 pass.
- `Hosting_…` passes.
- `Joining_…` fails at "the opening cutscene put this machine's scooter on seat 2's spawn point". `StartingCutscene` arrived before the game scene was up, so the scene's `SpawnManager` never heard it.

- [ ] **Step 5: A client holds the host's states while its new scene comes up**

In `Assets/Scripts/Online/OnlineGame.cs`:

1. After the `remotes` field, add:

```csharp
    readonly Queue<GameState> held = new Queue<GameState>(); // client: the host's states while this machine's new scene comes up
```

2. In `Begin`, after the `sceneFlow = …` line, add `        sceneFlow.SceneChanged += ApplyHeldStates;`.
3. In `OnRoleChanged`, after `sceneFlow.Unlink();`, add `        held.Clear();`.
4. `FollowHost` becomes:

```csharp
    // Client: the host switched state. While this machine's new scene is still coming up, the state waits for it: its
    // objects (the spawns, the cutscene) must hear it
    void FollowHost(GameState hostState)
    {
        if (sceneFlow.Changing)
        {
            held.Enqueue(hostState);
            return;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.ApplyGameState(ForClient(hostState));
    }

    // Client: the new scene is up: the states that waited go, in order
    void ApplyHeldStates()
    {
        while (held.Count > 0)
            FollowHost(held.Dequeue());
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "LoadRoundTests|OnlineSceneFlowTests|OnlineMatchNetworkTests|OnlineGameTests|OnlineGameNetworkTests|SceneFlowTests"`
Expected: all pass. `SceneFlowTests.GameplayScripts_ReachTheSceneLoaderOnlyThroughSceneFlow` still passes: only `ISceneFlow.cs` names `SceneManager.Instance`.

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 301 total, 301 passed` (284 + 4 + 11 more flow tests + 2). `LocalMatchSmokeTest` still loads the match with every player pressing A.

---

### Task 6: The match on every machine (spawns, no tutorial online, the host ends it)

**Files:**
- Modify: `Assets/Scripts/Player/SpawnManager.cs`
- Modify: `Assets/Scripts/Management/TutorialManager.cs`
- Modify: `Assets/Scripts/Menu/ResultsMenu.cs` (**CRLF: Edit tool only**)
- Test: `Assets/Tests/Editor/OnlineMatchNetworkTests.cs`, `Assets/Tests/Editor/SceneFlowTests.cs`

**Interfaces:**
- Consumes: `OnlineMatchNetworkTests` (Task 5), `PlayerRoster.LocalPlayers` / `Players` (Phase 2A), `GameAuthority.IsOnline` / `IsAuthority` (Phase 2B).
- Produces:
  - `SpawnManager` places only this machine's players, at the opening and in the golden round. Every scooter still gets its compass marker.
  - Online, `TutorialManager` finishes the tutorial for this machine's players a frame after `Tutorial` starts. The host then asks for `Begin`.
  - Online, a client's A on the results screen does nothing: the host takes everyone back (Ruling 7). Without this, a client's player, which counts as its machine's "host player" online (Phase 3B), would go back alone and leave the session.

- [ ] **Step 1: Add the checks to the match tests**

In `Assets/Tests/Editor/OnlineMatchNetworkTests.cs`:

1. In `Hosting_…`, replace the line `// [Task 6 inserts here: the first wave starts without the tutorial, and each machine places its own scooter]` with:

```csharp
            // Each machine places its own scooter: this one on seat 1's spawn point. The other machine's is where its owner
            // puts it; it hasn't shared a pose, so it's still where it stood in the menu
            Assert.Less(Vector3.Distance(GameSpawns()[0].transform.position, ScooterIn(0).Sphere.transform.position), 1.5f, "this machine's scooter on its spawn point");
            Assert.Less(Vector3.Distance(theirSpot, ScooterIn(1).Sphere.transform.position), 0.01f, "the other machine's scooter isn't placed here");

            // The first wave starts after the opening cutscene, without the tutorial: online it's skipped until orders are shared
            deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");
            Assert.IsTrue(Slot(0).Player.GetComponentInChildren<TutorialHandler>().HasLearnt, "this machine's player is done with the tutorial");
```

2. In `Joining_…`, replace the line `// [Task 6 inserts here: the tutorial is skipped here too, and another machine's scooter isn't placed here]` with:

```csharp
            // Another machine's scooter isn't placed here: its owner places it (it hasn't shared a pose: it's where it stood)
            Assert.Less(Vector3.Distance(hostSpot, ScooterIn(0).Sphere.transform.position), 0.01f, "the host's scooter isn't placed here");

            // The host skips the tutorial online: this machine's player finishes it at once, then the host starts the first wave
            host.Match.SendState(GameState.Tutorial);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Slot(1).Player.GetComponentInChildren<TutorialHandler>().HasLearnt && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1).Player.GetComponentInChildren<TutorialHandler>().HasLearnt, "this machine's player is done with the tutorial");
            host.Match.SendState(GameState.Begin);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave, as the host says");
```

3. In `Assets/Tests/Editor/SceneFlowTests.cs`, add `GameAuthority.Role = NetworkRole.Offline;` at the top of `TearDown`, and this test after `ResultsScreen_ReturnButton_GoesBackToTheMenuThroughTheSceneFlow`:

```csharp
        [Test]
        public void ResultsScreen_OnlineOnAClient_WaitsForTheHostToTakeEveryoneBack()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>()); // no players to reset
            ResultsMenu results = objects.Add<ResultsMenu>();
            Reflect.SetField(results, "displayText", new TMPro.TMP_Text[0]);
            Reflect.SetField(results, "canQuit", true);

            GameAuthority.Role = NetworkRole.Client;
            results.ConfirmMenu();
            Assert.AreEqual(0, flow.MenuReturns, "a client waits: the host takes everyone back");

            GameAuthority.Role = NetworkRole.Host;
            results.ConfirmMenu();
            Assert.AreEqual(1, flow.MenuReturns, "the host goes, and takes everyone");
        }
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "OnlineMatchNetworkTests|SceneFlowTests"`
Expected:
- Both match tests fail at "… isn't placed here": `SpawnManager` placed every player, the other machine's included. The tutorial checks would fail next: the host waits in `Tutorial` for a handler only the other machine has.
- `ResultsScreen_OnlineOnAClient_…` fails with "a client waits: the host takes everyone back" (expected 0, was 1).

- [ ] **Step 3: Each machine places its own players**

In `Assets/Scripts/Player/SpawnManager.cs`, `SpawnPlayersStartOfGame` becomes:

```csharp
    private void SpawnPlayersStartOfGame()
    {
        // Each machine places its own players: online, another machine's scooter is where its owner puts it
        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            PlacePlayer(player, SpawnPoint(player.Index, gameSpawnPositions, null));

        // Every scooter shows on this machine's compasses
        foreach (PlayerSlot player in playerInstantiate.Roster.Players)
            player.Player.GetComponentInChildren<CompassMarker>().InitalizeCompassUIOnAllPlayers();

        // After players have been placed, begin main loop
        gameManager.SetGameState(GameState.MainLoop);
    }
```

and in `SpawnPlayersFinalPackage`, the comment and loop become:

```csharp
        // The golden round lists 3 spawn points; a 4th player starts on their normal spawn point, in line with the others.
        // Each machine places its own players (online, another machine's scooter is where its owner puts it)
        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            PlacePlayer(player, SpawnPoint(player.Index, goldenPackageSpawnPositions, gameSpawnPositions));
```

- [ ] **Step 4: Online, the tutorial is skipped**

In `Assets/Scripts/Management/TutorialManager.cs`:

1. In `OnEnable`, after `GameManager.Instance.OnSwapBegin += handlers.Clear;`, add `        GameManager.Instance.OnSwapTutorial += SkipWhenOnline;`.
2. In `OnDisable`, after `GameManager.Instance.OnSwapBegin -= handlers.Clear;`, add `        GameManager.Instance.OnSwapTutorial -= SkipWhenOnline;`.
3. After `SkipTutorial`, add:

```csharp

    /// <summary>
    /// Online, the tutorial is skipped until orders are shared between machines (roadmap Task 3.5). It teaches picking
    /// up, delivering and stealing orders, which only the host has until then. And it waits for every player's handler,
    /// which only exists on that player's machine.
    /// Every machine's players finish it at once, and the host starts the first wave a frame later. Switching state
    /// inside the tutorial's own switch would run its other listeners (the tutorial orders) after the first wave's
    /// </summary>
    private void SkipWhenOnline()
    {
        if (GameAuthority.IsOnline)
            StartCoroutine(SkipOnline());
    }

    private IEnumerator SkipOnline()
    {
        yield return null;

        OnTutorialComplete?.Invoke();
        if (GameAuthority.IsAuthority && GameManager.Instance.MainState == GameState.Tutorial)
            GameManager.Instance.SetGameState(GameState.Begin);
    }
```

- [ ] **Step 5: Online, the host ends the results screen**

In `Assets/Scripts/Menu/ResultsMenu.cs` (Edit tool), `ConfirmMenu` starts:

```csharp
    public void ConfirmMenu()
    {
        // Online, the host takes everyone back from the results; a client waits for it
        if (!canQuit || !GameAuthority.IsAuthority)
        {
            return;
        }
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "OnlineMatchNetworkTests|SceneFlowTests|LocalMatchSmokeTest"`
Expected: all pass. The smoke test still spawns all 4 local players and plays its tutorial (offline, nothing is skipped).

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 302 total, 302 passed`.

---

### Task 7: The match clock on every machine

**Files:**
- Create: `Assets/Scripts/Online/MatchClock.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`, `Assets/Scripts/Online/OnlineGame.cs`, `Assets/Scripts/Management/OrderManager.cs`
- Test: `Assets/Tests/Editor/MatchClockTests.cs` (+ `.meta`), `Assets/Tests/Editor/OnlineMatchNetworkTests.cs`

**Interfaces:**
- Consumes: `OrderManager.GameTimer` / `GameStarted` / `FinalOrderActive`; `NetworkManager.ServerTime`; `OnlineMatchNetworkTests` (Tasks 5–6).
- Produces:
  - **`public static class MatchClock`:**
    - `REPUBLISH_TOLERANCE = 0.25f`.
    - `double EndTime(double now, float timeLeft)`, `float Remaining(double end, double now)` (never below 0), `bool NeedsRepublish(double publishedEnd, double now, float timeLeft)`.
  - **`OnlineMatch`:** `double ClockEnd`, `bool ClockStarted`, `bool FinalOrder`, and `void ShareClock(double end, bool started, bool finalOrder)` (host only).
  - **`OrderManager.FollowHostClock(float timeLeft, bool started, bool finalOrder)`:** online clients only.
  - **`OnlineGame`, every frame:**
    - The host shares its clock while its waves run, and whenever started or golden-round changes.
    - A client shows the host's.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/MatchClockTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/MatchClockTests.cs`):

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The match clock online: an end time in the server time every machine shares. Clients work out the time left; the
    /// host only sends the end again when its clock moved
    /// </summary>
    public class MatchClockTests
    {
        [Test]
        public void TheEnd_IsNowPlusTheTimeLeft()
        {
            Assert.AreEqual(130.5, MatchClock.EndTime(100.5, 30f), 1e-6);
        }

        [Test]
        public void TheTimeLeft_CountsDownToTheEnd_AndStopsAtZero()
        {
            Assert.AreEqual(20f, MatchClock.Remaining(130, 110), 1e-4f);
            Assert.AreEqual(0f, MatchClock.Remaining(130, 140), "past the end");
        }

        [Test]
        public void AClient_SeesTheHostsTimeLeft()
        {
            double end = MatchClock.EndTime(1000, 90f); // the host, with 90 s to go

            Assert.AreEqual(60f, MatchClock.Remaining(end, 1030), 1e-4f, "30 s later on any machine");
        }

        [Test]
        public void TheHost_SendsTheEndAgain_OnlyWhenItsClockMoved()
        {
            Assert.IsFalse(MatchClock.NeedsRepublish(130, 110, 20.1f), "0.1 s of drift");
            Assert.IsTrue(MatchClock.NeedsRepublish(130, 110, 45f), "a new wave's clock");
        }
    }
}
```

In `Assets/Tests/Editor/OnlineMatchNetworkTests.cs`:

1. In `Hosting_…`, replace the line `// [Task 7 inserts here: the match clock reaches the other machine]` with:

```csharp
            // The match clock reaches the other machine as an end time in server time: its time left matches the host's
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!other.Match.ClockStarted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(other.Match.ClockStarted, "the waves started");
            Assert.AreEqual(OrderManager.Instance.GameTimer, MatchClock.Remaining(other.Match.ClockEnd, other.Network.ServerTime.Time), 1f, "the other machine's time left");
```

2. In `Joining_…`, replace the line `// [Task 7 inserts here: the host's match clock shows here]` with:

```csharp
            // The host's match clock shows here: its end, in the server time both machines share
            host.Match.ShareClock(host.Network.ServerTime.Time + 100, true, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!OrderManager.Instance.GameStarted && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(OrderManager.Instance.GameStarted, "the waves started, as on the host");
            Assert.AreEqual(100f, OrderManager.Instance.GameTimer, 1.5f, "the host's time left");
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "MatchClockTests|OnlineMatchNetworkTests"`
Expected: `NO RESULTS` with `error CS0103: The name 'MatchClock' does not exist`, plus `OnlineMatch` having no `ClockStarted` / `ShareClock`.

- [ ] **Step 3: The clock's arithmetic, and the match carrying it**

`Assets/Scripts/Online/MatchClock.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/MatchClock.cs`):

```csharp
using System;
using UnityEngine;

/// <summary>
/// Online, the match clock travels as the moment it runs out, in Netcode's server time (the host's clock, on every
/// machine). Clients work out the time left. The host only sends the end again when its clock moved from it (a new
/// wave, the clock stopped). See docs/online.md
/// </summary>
public static class MatchClock
{
    /// <summary>How far the host's clock may move from the end it sent before it sends it again, in seconds</summary>
    public const float REPUBLISH_TOLERANCE = 0.25f;

    /// <summary>When a clock with this much time left runs out</summary>
    public static double EndTime(double now, float timeLeft)
    {
        return now + timeLeft;
    }

    /// <summary>The time left until the end, never below zero</summary>
    public static float Remaining(double end, double now)
    {
        return Mathf.Max(0f, (float)(end - now));
    }

    /// <summary>Host: whether its clock moved from the end it sent</summary>
    public static bool NeedsRepublish(double publishedEnd, double now, float timeLeft)
    {
        return Math.Abs(EndTime(now, timeLeft) - publishedEnd) > REPUBLISH_TOLERANCE;
    }
}
```

In `Assets/Scripts/Online/OnlineMatch.cs`:

1. After the `state` field, add:

```csharp
    readonly NetworkVariable<double> clockEnd = new NetworkVariable<double>(0);
    readonly NetworkVariable<bool> clockStarted = new NetworkVariable<bool>(false);
    readonly NetworkVariable<bool> finalOrder = new NetworkVariable<bool>(false);
```

2. After the `State` property, add:

```csharp

    /// <summary>When the host's match clock runs out, in Netcode's server time (MatchClock)</summary>
    public double ClockEnd { get { return clockEnd.Value; } }

    /// <summary>Whether the host's waves have started</summary>
    public bool ClockStarted { get { return clockStarted.Value; } }

    /// <summary>Whether the host is in the golden round</summary>
    public bool FinalOrder { get { return finalOrder.Value; } }
```

3. After `SendState`, add:

```csharp

    /// <summary>
    /// Host: shares its match clock (only what changed goes out). Does nothing on a client
    /// </summary>
    public void ShareClock(double end, bool started, bool final)
    {
        if (!IsServer)
            return;

        if (clockEnd.Value != end)
            clockEnd.Value = end;
        if (clockStarted.Value != started)
            clockStarted.Value = started;
        if (finalOrder.Value != final)
            finalOrder.Value = final;
    }
```

4. In the class summary, after the scene flow's bullet, add `/// - The host's match clock (MatchClock).`

- [ ] **Step 4: Run the tests: the arithmetic passes, nothing shares the clock yet**

Run: `bash tools/run-tests.sh "MatchClockTests|OnlineMatchNetworkTests"`
Expected: `MatchClockTests` 4/4 pass. `Hosting_…` fails at "the waves started" (the host shares nothing); `Joining_…` fails at "the waves started, as on the host" (this machine doesn't follow it).

- [ ] **Step 5: The host shares its clock; clients show it**

`Assets/Scripts/Management/OrderManager.cs`, before the `/// Ensures next game will run smoothly.` summary of `ResetForNextGame`, add:

```csharp
    /// <summary>
    /// Online client: shows the host's match clock (this machine's waves don't run: the host's do)
    /// </summary>
    public void FollowHostClock(float timeLeft, bool started, bool finalOrder)
    {
        gameTimer = timeLeft;
        gameStarted = started;
        finalOrderActive = finalOrder;
    }

```

`Assets/Scripts/Online/OnlineGame.cs`:

1. In `Update`, after `ShowRemoteChoices(players);`, add `        ShareOrFollowClock();`.
2. After `ShowRemoteChoices`, add:

```csharp

    // The match clock: the host shares it as an end time in server time while its waves run; a client shows it
    void ShareOrFollowClock()
    {
        OnlineMatch match = session.Match;
        OrderManager orders = OrderManager.Instance;
        if (match == null || orders == null || !session.IsRunning)
            return;

        double now = session.Network.ServerTime.Time;
        if (match.IsServer)
        {
            bool moved = orders.GameStarted && MatchClock.NeedsRepublish(match.ClockEnd, now, orders.GameTimer);
            if (moved || match.ClockStarted != orders.GameStarted || match.FinalOrder != orders.FinalOrderActive)
                match.ShareClock(MatchClock.EndTime(now, orders.GameTimer), orders.GameStarted, orders.FinalOrderActive);
        }
        else
            orders.FollowHostClock(MatchClock.Remaining(match.ClockEnd, now), match.ClockStarted, match.FinalOrder);
    }
```

3. In the class summary, after `/// - the host's game states.`, add `/// - the host's match clock (MatchClock).` (and end the line before it with `;`).

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "MatchClockTests|OnlineMatchNetworkTests"`
Expected: all pass.

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 306 total, 306 passed`.

---

### Task 8: Docs, the two-editor check and the roadmap

**Files:**
- Modify: `docs/online.md`, `docs/testing.md`, `EDITOR-TODO.md`
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap)

**Interfaces:**
- Consumes: everything above.
- Produces: docs only; no code.

- [ ] **Step 1: `docs/online.md`**

1. In "Phase 3B: online player select", replace the bullet that starts `- **The match doesn't start online yet** (Phase 3C).` with:

```markdown
- **When everyone's ready, the match starts on every machine** (Phase 3C, below).
```

2. After the Phase 3B section (before `## Two editors on one computer (ParrelSync)`), add:

```markdown
### Phase 3C: driving together

- **Starting the match.** When everyone's ready, the host starts the load on every machine:
  - Each machine loads the game behind its loading screen and tells the host when it's done. Online there's no "press A".
  - Once every machine has it (or has left), the host shows it everywhere, and the match starts together.
  - The golden round's scene loads the same way.
- **Each machine drives its own scooter.** Every other machine shows it where its owner has it, with its boost trail, skid marks, drift sparks and rider.
  - A scooter that spawns or respawns far away appears there at once. It doesn't slide across the map.
  - Until its machine sends a first position, it stays where it was (its podium).
- **The host runs the match:** its states (cutscenes, waves, results) and its match clock reach every machine.
  - Online there's no tutorial yet. It teaches orders, which aren't shared between machines yet.
- **Back to the menu:**
  - The host takes everyone back, from the results or its pause menu. On a client, the results screen waits for the host.
  - A client picking "main menu" on its own leaves the session.
- How it works:
  - **Scene loads:**
    - `OnlineSceneFlow` and `LoadRound`: the host starts every load, counts who's ready and shows the scene.
    - `SceneManager`'s held loads (`IMatchLoader`) do the loading.
    - A state that reaches a client while its new scene is still coming up waits for it (`OnlineGame`).
    - Netcode's own scene management stays off.
  - **`OnlineScooter`:** one per machine, beside its `OnlinePlayer`.
    - Two proxies carry its pose, each moved by its owner through an `OwnerNetworkTransform`: the ball, and the model's world pose (slope, lean, drift, hop and wheelie).
    - `DriveFlags` say what it's doing.
    - The proxies wait parked 10 km below the map until the first pose.
  - **`OnlineDriving`** (added by `OnlineGame`) runs every frame: it sends this machine's scooter out, and shows the others on their `RemoteAvatar` (`ScooterPose`, `BallDriving.ShowRemote`).
  - **The match clock** is the host's end time, in Netcode's server time (`MatchClock`, on `OnlineMatch`).
  - **Another machine's scooter doesn't act on this machine** (`RemoteAvatar.IsRemote`): no water, orders, steals, clashes or end-of-game freeze here. Its own machine does those.
```

3. In "Two editors on one computer", replace the bullet `- Ready up in both. The countdown runs, then ends with the "isn't in yet" message.` with:

```markdown
- Ready up in both. After the countdown both show the loading screen, and the game appears in both at once: the host waits for the other editor.
  - The opening cutscene plays in each, then driving starts (no tutorial online).
  - Drive in one editor and watch the other: the scooter moves there, boosting and drifting.
- Pause in editor 1 and pick **Main Menu**: both go back to the menu, still in the session. In editor 2 (a client), **Main Menu** leaves the session.
```

4. In "Known limits", replace `- The match can't start online yet (Phase 3C).` with:

```markdown
- **Orders, steals and clashes are the host's alone** until roadmap Tasks 3.5–3.6:
  - Only the host's player collects orders, and other machines don't see them.
  - The results count only the host's deliveries.
  - The golden round ends when the host delivers the golden order.
- There's no tutorial online (it teaches orders).
- **Other machines' scooters are silent**, and a respawn shows as a jump without the ghost animation (roadmap Tasks 3.6–3.7).
- When the main game ends, only the host's scooter stops; the others keep driving until the golden round loads.
- If the host leaves mid-match, clients stay where they are, offline (roadmap Task 3.8).
```

- [ ] **Step 2: `docs/testing.md`**

After the line that starts `  - \`RemoteAvatarTests\`, \`OnlineSeatTests\` and \`OnlineGameNetworkTests\` (port 7793)`, add:

```markdown
  - `OnlineScootersNetworkTests` (port 7794, empty scene): every machine's scooter, its pose and flags, and long moves that jump.
  - `OnlineDrivingNetworkTests` (port 7795, menu scene): another machine's scooter following its owner, and this machine's reaching the others.
  - `OnlineMatchNetworkTests` (port 7796): a whole online start, hosting and joining. They load the game scene, so about a minute each.
```

- [ ] **Step 3: The roadmap**

In `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`:

1. Phase 3's progress: `Progress (2026-09-24):` → `Progress (2026-09-27):`. Then replace the `- Next:` block with:

```markdown
- Phase 3C (`2026-09-27-phase3c-driving-together.md`) — driving together:
  - The match loads on every machine and starts together (`OnlineSceneFlow`, held loads). The host takes everyone back to the menu.
  - Each machine drives its own scooter, and the others follow it (`OnlineScooter`, `OnlineDriving`), with its boost, drift and sparks (`DriveFlags`).
  - The host's match clock on every machine (`MatchClock`). No tutorial online yet.
- Next:
  - Phase 3D: the Steam lobby and a Play Online menu (Task 3.2).
  - Then orders (Task 3.5), steals, clashes and respawns (3.6), one-shots and sounds (3.7), and disconnects (3.8).
```

2. Task 3.3:
   - The bullet that starts `- [ ] Owner-authoritative movement:` becomes `- [x]`, and ends with:

```markdown
 *(Phase 3C: through two proxies on a per-player `OnlineScooter`, each an `OwnerNetworkTransform`: the ball, and the model's world pose. `OnlineDriving` copies them every frame. The proxies start parked below the map; long moves teleport.)*
```

   - `- [ ] Spawns:` becomes `- [x] Spawns:`, and ends with ` *(Phase 3C: `SpawnManager` places `LocalPlayers` only.)*`.
   - `- [ ] Remote players' boost/phase/drift visuals and sounds come from the flags byte.` becomes:

```markdown
- [x] Remote players' boost/phase/drift visuals and sounds come from the flags byte. *(Phase 3C: `DriveFlags` on `OnlineScooter`. `BallDriving.ShowRemote` drives the trail, skid marks, sparks and rider. Sounds come with Task 3.7.)*
```

   - In the remote-avatar bullet, replace its `*Still to do for the match:*` sub-list (the two lines about `Order.EraseOrder` / `OrderBeacon` and trigger messages) with:

```markdown
    - *Phase 3C: another machine's scooter doesn't respawn, collect orders, steal, clash or freeze on this machine (`RemoteAvatar.IsRemote`), so `Compass` lookups never reach it.)*
```

3. Task 3.4:
   - The timers bullet (`- [ ] Wave/game timers → …`) becomes `- [x]`, with its note replaced by ` *(Phase 3C: `OnlineMatch.ClockEnd`, plus started and golden round. Clients' `OrderManager.FollowHostClock` feeds their UI.)*`.
   - The loads bullet becomes:

```markdown
- [x] Loads via `NetworkManager.SceneManager.LoadScene`; start the cutscene on `OnLoadEventCompleted` (replaces the loading-screen confirm). *(Phase 3C, done differently:*
  - *Netcode's scene manager stays off. It breaks joining from an unsaved Play Mode scene, and it would sync the menu scene that holds every manager.*
  - *Instead the host coordinates the game's own loader: every machine loads the scene held and reports, then the host shows it everywhere at once (`OnlineSceneFlow`, `LoadRound`).*
  - *The opening cutscene starts on each machine when it's shown.)*
```

4. Task 3.7's pause bullet: add at its end ` *(Phase 3C: the host's "Main Menu" takes everyone back; a client's leaves the session.)*`.
5. The §R and Phase 1 checks' Run All count: `259 passed` → `306 passed`.

- [ ] **Step 4: `EDITOR-TODO.md`**

Replace its contents with:

```markdown
# Editor to-do (master list)

Things only you can do in the Unity editor, top to bottom. Updated 2026-09-27 (Phase 3C).

## 1. Let both editors import Phase 3C (2 minutes)

- Click into the **main editor**. It imports the new scripts and prefab, and recompiles:
  - `Assets/Scripts/Online/`: `OnlineScooter`, `OnlineDriving`, `DriveFlags`, `ScooterPose`, `OwnerNetworkTransform`, `LoadRound`, `MatchClock`.
  - `Assets/Prefabs/Online/OnlineScooter.prefab`.
- Then click into the **ParrelSync clone**. It shares `Assets/`, so it imports the same files.
- If Unity says `OnlinePrefabs.asset` or `DefaultNetworkPrefabs.asset` changed on disk, reload them.
- The Console should end with no red errors.

## 2. Play an online match in two editors (10 minutes)

- If your controller doesn't respond even offline, sort that out first. It's the Steam controller-support issue, looked at in its own session: turn off Steam's controller support (Steam → Settings → Controller) and try again.
- Press **Play** in both editors. In each, wait for the title screen and press **A**.
- Editor 1: **Tools → Dead on Arrival → Online → Host**. Editor 2: **Tools → Dead on Arrival → Online → Join This Computer**.
- **Ready up** in both. Click into an editor before pressing its buttons.
- What you should see:
  - After the countdown, both show the loading screen. The game appears in both at about the same time: editor 1 waits for editor 2.
  - The opening cutscene plays in each, then driving starts. There's no tutorial online.
  - Drive in one editor and watch the other: your scooter moves there, with its boost trail, skid marks and drift sparks.
  - The match clock shows the same time in both.
  - Orders are editor 1's alone for now. Editor 2 doesn't see them (roadmap Task 3.5).
- **Pause** in editor 1 and pick **Main Menu**: both go back to the menu, still in the session.
- Anything odd? Note it, plus both editors' Console lines starting `Online:`.

## 3. Review and commit (your call)

- Phase 3C is **not committed**. Everything is on `steam-phase1a` as working-tree changes, together with the 2026-09-24 slider fix.
- Plan: `docs/superpowers/plans/2026-09-27-phase3c-driving-together.md`.
- How it works: `docs/online.md`.
```

- [ ] **Step 5: Whole suite, and the player build's scripts**

Run: `bash tools/run-tests.sh`
Expected: `tests: 306 total, 306 passed`.

Then check that the Windows player's scripts compile (the new code is runtime code, and `OwnerNetworkTransform` pulls in `Unity.Netcode.Components`). Put a one-off into the **mirror only**, as `../_doa_test_mirror/CapstoneYear4/Assets/Scripts/Editor/PlayerCompileCheck.cs`:

```csharp
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

// ONE-OFF (Phase 3C, Task 8): compiles the Windows player's scripts in the test mirror. Never add it to the project
public static class PlayerCompileCheck
{
    public static void Run()
    {
        ScriptCompilationSettings settings = new ScriptCompilationSettings { target = BuildTarget.StandaloneWindows64, group = BuildTargetGroup.Standalone };
        ScriptCompilationResult result = PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/PlayerCompileCheck");
        Debug.Log("PLAYER COMPILE: " + result.assemblies.Count + " assemblies");
    }
}
```

Then run it and delete it:

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -quit -projectPath "$(cygpath -w ../_doa_test_mirror/CapstoneYear4)" -executeMethod PlayerCompileCheck.Run -logFile "$(cygpath -w Logs/player-compile.log)"; grep "PLAYER COMPILE\|error CS" Logs/player-compile.log
rm ../_doa_test_mirror/CapstoneYear4/Assets/Scripts/Editor/PlayerCompileCheck.cs*
```

Expected: `PLAYER COMPILE: <about 33> assemblies` and no `error CS` lines. (Run the whole suite first: it syncs the mirror from the project.)

---

## Done when

- `tests: 306 total, 306 passed` in the mirror, and the Windows player's scripts compile.
- `EDITOR-TODO.md` lists the two-editor match check for your human partner.
- Nothing is committed: your human partner reviews and commits.

# Phase 3F — The Tutorial Online Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Online matches start with the tutorial again, as local ones do. Each player drives their own lane: they pick up their tutorial order, steal from their cardboard cutout and deliver. The first wave begins once every machine's player has finished.

**Architecture:**
- **The tutorial runs online** (`TutorialManager.IsSkipped` goes). Players start at the start of their seat's lane. The host hands out the tutorial orders as in a local match, and Phase 3E shares them.
- **Each machine says when its own player has finished.** Its own triggers decide, as today. A client's `TutorialManager` tells the host (`TutorialSync` → `OnlineMatch.ReportLearnt`). The host counts seats: the first wave begins once every seat in the roster has finished, and a player who leaves no longer holds the others up.
- **Cutout steals:** the machine that drives the scooter sees its own boost into its cutout, opens its barrier at once and asks the host (`OnlineMatch.AskCutout`). The host checks the seat and hands out the cutout's order. Every machine replays that (Phase 3E), and opens any cutout whose order is taken.
- **`OnlineTutorial`** (added by `OnlineGame`, like `OnlineOrders`) is the glue.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 (ServerRpc) · DOTween · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- **Task 3.5 bullet 4:** the tutorial online. `TutorialManager.IsSkipped` turns it back on. Players start at the tutorial again (`SpawnManager.PlacePlayersAtStart`). `IncrementAlumni` counts every machine's players. The bullet's Phase 3E note adds "the cardboard cutouts' steals".
- **Task 3.8 bullet 1, in part:** a player who leaves mid-tutorial doesn't hold up the others (Ruling 3).

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1** (a bump is a Steam API change: it goes to `main`, after asking).
- **Players:** online is one player per machine until Task 3.9.
- **Local split-screen must behave exactly as before:** `LocalMatchSmokeTest` passes after every task.
- **Online rules:**
  - No mid-match joining (`JoinRules`).
  - The host leaving ends the match (`OnlineSession.HOST_LEFT`).
  - Netcode's scene management stays off.
- **Steam API files stay untouched:** `SteamManager.cs` and `SteamStartup.cs`.
- **The user may have Unity editors open on the real project** (their editor plus a ParrelSync clone sharing `Assets/`):
  - Never edit an existing scene, prefab or ProjectSettings file.
  - Everything in this plan is code.
- **No git commits:** your human partner commits.
- **Meta files:** every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`. List `git status` for new files without one before a task ends.
- **Line endings:** `Order.cs` is `i/crlf`: Edit tool only. Never `sed -i` on scripts.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]` (the mirror). Game-scene network tests take a minute or two each, so run long runs in the background.
  - Check `ListAgents` before a long run.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
- **Play Mode tests:**
  - No lambda captures a test method's local: after `EnterPlayMode` even assigning one throws. Use static helpers, and recorder classes subscribed as method groups.
  - Wait by time, never by frame count.
  - Two-machine tests call `log.MachinesLeave()` before the first machine leaves.
- **Tests can't see `internal` members.** What tests call is `public`; private state is read through `Reflect`.
- **The game has its own global `SceneManager` class:** write `UnityEngine.SceneManagement.SceneManager` for Unity's.
- **Branch:** `steam-phase1a`. Phase 3E should be committed before this starts, so each phase stays one commit.

## Rulings (decided while planning)

1. **The tutorial runs online, and the city start points go.**
   - `TutorialManager.IsSkipped`, the online skip (`SkipWhenOnline`/`SkipOnline`) and `SpawnManager.nonTutorialSpawnPositions` are removed. Online players start in their lane, as local ones do.
   - `GameSceneSpawnTests` now checks the tutorial start points.
   - The four city `Spawn 1-4` objects stay in the scene, unused (optional cleanup, EDITOR-TODO).
   - Cost if wrong: a later "skip the tutorial" option needs start points again.
2. **Each machine decides when its own player has finished, and the host counts seats.**
   - A client's player finishes through their own triggers, as today, and their machine reports the seat. The host checks the report came from that seat's machine.
   - Rejected: the host watching `IntoGameTrigger` for other machines' scooters. It would miss the S dev skip, and the owner's view is the one that counts.
   - Cost if wrong: a modified client can end only its own player's tutorial early.
3. **A player who leaves mid-tutorial:** the host checks again whether everyone left has finished (pulled from Task 3.8). So does a client whose host leaves: it goes on offline.
4. **Cutout steals are asked for, like drops (Phase 3E).**
   - A cutout's trigger is 1.5 m deep. On the host, another machine's scooter shows smoothed and a little late, and a boosting scooter crosses the trigger in about two frames, so the host can miss the steal.
   - So the owner's machine detects its own boost, opens its barrier at once (no bump into it while waiting) and asks. The host checks the lane is the asker's, then hands out the order as a local steal would.
   - Every machine opens a cutout when its order is taken.
   - Cost if wrong: none. On a bad connection, the order reaches the scooter a round trip after the barrier opens.
5. **Every match starts the count over.** The finished seats clear on the opening cutscene as well as at the first wave.
   - `TutorialManager` persists across matches (it's in the menu scene).
   - This also fixes an offline edge: a match abandoned mid-tutorial could start the next one's first wave early.
6. **`Order.InitOrder` clears the cutout's hold (`stealActive`).** On a client, the replayed steal otherwise leaves the order showing at its pickup after delivery. Offline nothing changes: `CutoutHandler` already cleared it first.
7. **Steals between players stay in Task 3.6.** The tutorial teaches the steal on a cutout, which this phase shares.

## Review Focus

- **A player who leaves mid-tutorial** → the others aren't held up; the first wave begins. (Task 3 `Hosting_APlayerWhoLeavesMidTutorial_…`; Task 1 `APlayerWhoLeaves_…`.)
- **A report or a cutout request for someone else's seat** (a buggy or hostile client) → ignored. (Task 3 and Task 4 `Hosting_TheTutorialRunsOnline_…`.)
- **A client boosting into its cutout** → its barrier opens at once, before the host answers, and the order arrives from the host. (Task 4 `Joining_…`.)
- **A match abandoned mid-tutorial, then a new one** → nobody's finish from the old match counts. (Task 1 `ANewMatch_StartsTheTutorialOver`.)
- **The host leaving mid-tutorial** → a client whose player had finished isn't stuck: it goes on offline into the first wave. (Task 1 `TheHostLeaves_…`.)

## Facts (probed 2026-09-29)

- **Seat `i`'s lane:**
  - Start point: `SpawnManager.gameSpawnPositions[i]` (x ≈ 16.6 / 28.6 / 41.1 / 53.2, z −417.1).
  - Tutorial order: `OrderManager.tutorialOrders[i]` = "Tutorial Order (i+1)".
  - Cutout: `CutoutManager.cutouts[i]` = "GhostCutout (i+1)" at z −250.2. Its trigger `BoxCollider` is about 12.5 × 6.9 × 1.5 m.
  - Barrier: "Barrier (i+1)", a solid `BoxCollider` across the lane at z −247.3.
  - Steal order: "Steal Tut Order (i+1)".
- **Triggers** (`TutorialTrigger`, across all lanes): Wall1 (z −479) → Pickup, Wall2 (z −331.5) → Steal, Wall3 (z −201) → Dropoff, `IntoGameTrigger` (z −60.4, the city's edge) → Final.
- **`CutoutManager`** (in `Tutorial.prefab`) calls `InitCutout` on each roster seat's cutout at the opening cutscene, on every machine: the cutout holds its order (`CardboardHold`) and the barrier's collider comes on. A boost into the cutout (`OnTriggerStay` + `BallDriving.Boosting`) steals.
- **`TutorialManager`** is in the menu scene and persists across matches.
- **Only a player's own machine has their `TutorialHandler`** (`Player - Cinemachine.prefab`). Another machine's scooter has none, so the triggers do nothing for it.
- **`BallDriving.AutoBoost()`** starts a boost from code (tests). A remote scooter's `Boosting` follows its owner's (`ShowRemote`).
- **The tutorial area turns off** 1 s after the first wave begins (`TutorialAreaDisabler`): its ground goes. There are no respawn points in it.
- **The player-select tutorial toggle is commented out** ("outdated"), so every match has the tutorial. `ShouldTutorialize` goes false only when the waves end, so the golden round's `SpawnManager` opens with the golden cutscene.

## File Structure

- Create `Assets/Scripts/Online/TutorialSync.cs`: a client's tutorial messages leaving the game code.
- Create `Assets/Scripts/Online/OnlineTutorial.cs`: the session's tutorial on this machine.
- Modify `Assets/Scripts/Management/TutorialManager.cs`: the finished seats; no online skip.
- Modify `Assets/Scripts/Player/SpawnManager.cs`: players always start at the tutorial.
- Modify `Assets/Scripts/World/CutoutHandler.cs`: its seat, `InSeat`, `StealFor`, opening; the online rules.
- Modify `Assets/Scripts/Management/CutoutManager.cs`: gives each cutout its seat.
- Modify `Assets/Scripts/World/Order.cs` (`i/crlf`): `InitOrder` clears `stealActive`.
- Modify `Assets/Scripts/Online/OnlineMatch.cs`: tutorial reports and cutout requests.
- Modify `Assets/Scripts/Online/OnlineGame.cs`: adds `OnlineTutorial`; checks the tutorial again when a player leaves.
- Tests, all under `Assets/Tests/Editor/`:
  - Create `TutorialManagerTests`, `TutorialMessagesNetworkTests` (port 7800) and `OnlineTutorialNetworkTests` (port 7801).
  - Modify `SpawnManagerTests`, `GameSceneSpawnTests`, `OnlineMatchNetworkTests` and `OnlineOrdersNetworkTests`.
- Docs: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`.

---

### Task 1: The tutorial counts seats

**Files:**
- Create: `Assets/Scripts/Online/TutorialSync.cs`
- Modify: `Assets/Scripts/Management/TutorialManager.cs`
- Test: `Assets/Tests/Editor/TutorialManagerTests.cs`

**Interfaces:**
- Consumes: `OrderSync.SeatOf(Component)` (Phase 3E), `GameAuthority`, `PlayerInstantiate.Instance.Roster`.
- Produces:

```csharp
public static class TutorialSync
{
    public static event Action<int> Finished;    // online client: its player in this seat finished the tutorial
    public static event Action<int> CutoutAsked; // online client: its player boosted into the cutout of this seat's lane
    public static void Finish(int seat);
    public static void AskCutout(int seat);
    public static void Reset();                  // tests
}
// TutorialManager
public void IncrementAlumni(TutorialHandler inHandler); // (exists) this machine's player finished: their seat (OrderSync.SeatOf) counts.
                                                        // An online client records it, then tells the host (TutorialSync.Finish) and ends nothing itself
public void SeatLearnt(int seat);   // the player in a seat finished. Only the authority ends the tutorial
public void RecheckAlumni();        // the roster changed (a player left, this machine went offline)
```

- The finished seats are a `HashSet<int> finished`. A `StartOver()` clears it and `handlers`; it listens to `OnSwapStartingCutscene` and `OnSwapBegin` (Begin replaces today's `handlers.Clear`). Unsubscribe both in `OnDisable`.
- `RecheckAlumni` works only on the authority, and only when every seat in `PlayerInstantiate.Instance.Roster.Players` is in `finished` and there's at least one seat. It ends the tutorial as today: `if (shouldTutorialize) GameManager.Instance.SetGameState(GameState.Begin); OnTutorialComplete?.Invoke();`.
- `SeatLearnt` adds the seat, then `RecheckAlumni()`. On an authority, `IncrementAlumni` also goes through `SeatLearnt`.
- `handlers` stays: the S hotkey uses it.
- The online skip stays until Task 3.

- [ ] **Step 1: Write the failing tests**
  - `TutorialManagerTests`:
    - `SetUp`: a `GameManager`, `PlayerInstantiate` and `TutorialManager` from `TestObjects`, each set with `Reflect.SetSingleton`. Then `Reflect.Invoke(tutorial, "OnEnable")`, so it hears the game's states.
    - `SetUp` counts endings: `tutorial.OnTutorialComplete += CountEnd` (a method adding to an `int ended`).
    - `TearDown`: `Reflect.Invoke(tutorial, "OnDisable")`, the three singletons back to `null`, `GameAuthority.Role = Offline`, `TutorialSync.Reset()`, `objects.DestroyAll()`.
  - Helpers:
    - `TutorialHandler Player(int seat)`: a root "P{seat+1}" with a child "Control" holding a `TutorialHandler`, joined with `players.Roster.JoinRemoteAt(root, (ulong)seat, seat)`. `SeatOf` doesn't care whose machine it is.
    - `void Remote(int seat)`: a root without a handler, joined the same way.

```csharp
[Test] public void EveryPlayerFinished_TheFirstWaveBegins()
{   // a local match
    TutorialHandler first = Player(0), second = Player(1);
    tutorial.IncrementAlumni(first);
    Assert.AreNotEqual(GameState.Begin, game.MainState, "one still in the tutorial");
    tutorial.IncrementAlumni(second);
    Assert.AreEqual(GameState.Begin, game.MainState);
    Assert.AreEqual(1, ended);
}

[Test] public void OnTheHost_AnotherMachinesPlayer_CountsWhenTheirMachineSaysSo()
{
    GameAuthority.Role = NetworkRole.Host;
    TutorialHandler mine = Player(0); Remote(1);
    tutorial.IncrementAlumni(mine);
    Assert.AreNotEqual(GameState.Begin, game.MainState, "waits for the other machine's player");
    tutorial.SeatLearnt(1);
    Assert.AreEqual(GameState.Begin, game.MainState);
}

[Test] public void OnAClient_ItsPlayerFinishing_GoesToTheHost_AndEndsNothingHere()
{
    GameAuthority.Role = NetworkRole.Client;
    Remote(0); TutorialHandler mine = Player(1);
    List<int> told = new List<int>(); TutorialSync.Finished += told.Add;
    tutorial.IncrementAlumni(mine);
    tutorial.SeatLearnt(0);
    CollectionAssert.AreEqual(new[] { 1 }, told);
    Assert.AreNotEqual(GameState.Begin, game.MainState, "the host starts the first wave");
    Assert.AreEqual(0, ended);
}

[Test] public void APlayerWhoLeaves_DoesntHoldUpTheOthers()
{
    GameAuthority.Role = NetworkRole.Host;
    TutorialHandler mine = Player(0); Remote(1);
    tutorial.IncrementAlumni(mine);
    players.Roster.LeaveSlot(1);
    tutorial.RecheckAlumni();
    Assert.AreEqual(GameState.Begin, game.MainState);
}

[Test] public void TheHostLeaves_AClientWhosePlayerFinished_GoesOnOffline()
{
    GameAuthority.Role = NetworkRole.Client;
    Remote(0); TutorialHandler mine = Player(1);
    tutorial.IncrementAlumni(mine);
    GameAuthority.Role = NetworkRole.Offline;
    players.Roster.LeaveSlot(0);
    tutorial.RecheckAlumni();
    Assert.AreEqual(GameState.Begin, game.MainState);
}

[Test] public void ANewMatch_StartsTheTutorialOver()
{   // a match left mid-tutorial: who finished it doesn't count in the next one
    TutorialHandler first = Player(0), second = Player(1);
    tutorial.IncrementAlumni(second);
    game.SetGameState(GameState.StartingCutscene);
    tutorial.IncrementAlumni(first);
    Assert.AreNotEqual(GameState.Begin, game.MainState, "the second player hasn't finished this match's tutorial");
}

[Test] public void InTheGoldenRound_FinishingBeginsNoWave()
{
    tutorial.ShouldTutorialize = false; // the waves are over
    tutorial.IncrementAlumni(Player(0));
    Assert.AreNotEqual(GameState.Begin, game.MainState);
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh TutorialManagerTests`
Expected: `NO RESULTS` with `error CS0103`/`CS1061` for `TutorialSync`, `SeatLearnt` and `RecheckAlumni`.

- [ ] **Step 3: Implement `TutorialSync` and the finished seats** (and the meta).

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "TutorialManagerTests|AuthorityGateTests|LocalMatchSmokeTest"`
Expected: `tests: 30 total, 30 passed` (7 + 22 + the smoke test, whose four local players finish through `TeachHandler(Final)`).

- [ ] **Step 5: No commit.**

---

### Task 2: The match carries tutorial reports and cutout requests

**Files:**
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`
- Test: `Assets/Tests/Editor/TutorialMessagesNetworkTests.cs` (port 7800; an empty Play Mode scene, like `OrderMessagesNetworkTests`)

**Interfaces:**
- Produces:

```csharp
// OnlineMatch
public event Action<ulong, int> MachineLearnt; // host: a client's player finished the tutorial (the client's id, the seat)
public event Action<ulong, int> CutoutAsked;   // host: a client's player boosted into a cutout (the client's id, the cutout's seat)
public void ReportLearnt(int seat);            // client -> LearntServerRpc (RequireOwnership = false); nothing on the host, which counts its own
public void AskCutout(int seat);               // client -> CutoutServerRpc (RequireOwnership = false); nothing on the host, whose player steals at once
```

The `OnlineMatch` summary gains "- Clients' tutorial reports and cutout requests (OnlineTutorial)."

- [ ] **Step 1: Write the failing tests**
  - A `SeatRecorder` with `Machine`, `Seat` and `Count`, and a method `Heard(ulong machine, int seat)`, subscribed as a method group.
  - Setup and leaving as in `OrderMessagesNetworkTests`: host `HostDirect("127.0.0.1", 7800)`, client `JoinDirect`, wait until both see 2 players; at the end `log.MachinesLeave()`, the client leaves, then the host, and `Assert.IsEmpty(log.Problems)`.

```csharp
[UnityTest] public IEnumerator AClientsTutorialReport_ReachesTheHost_WithWhoSentIt()
// learnt = new SeatRecorder(); host.Match.MachineLearnt += learnt.Heard; client.Match.ReportLearnt(1); wait (10 s) until learnt.Count == 1
Assert.AreEqual(client.Network.LocalClientId, learnt.Machine);
Assert.AreEqual(1, learnt.Seat);
// host.Match.ReportLearnt(0); wait 1 s
Assert.AreEqual(1, learnt.Count, "the host counts its own player at once: it never reports");

[UnityTest] public IEnumerator AClientsCutoutRequest_ReachesTheHost_WithWhoSentIt()
// the same with host.Match.CutoutAsked and AskCutout: seat 1 from the client arrives with its id; the host's own AskCutout(0) sends nothing
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh TutorialMessagesNetworkTests`
Expected: `NO RESULTS` with `error CS1061` (`MachineLearnt`, `ReportLearnt`, `CutoutAsked`, `AskCutout`).

- [ ] **Step 3: Implement the members above.**

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "TutorialMessagesNetworkTests|OrderMessagesNetworkTests"`
Expected: `tests: 5 total, 5 passed`.

- [ ] **Step 5: No commit.**

---

### Task 3: The tutorial runs online

**Files:**
- Create: `Assets/Scripts/Online/OnlineTutorial.cs`
- Modify: `Assets/Scripts/Management/TutorialManager.cs`, `Assets/Scripts/Player/SpawnManager.cs`, `Assets/Scripts/Online/OnlineGame.cs`
- Test:
  - `Assets/Tests/Editor/OnlineTutorialNetworkTests.cs` (new; port 7801; the menu scene, like `OnlineMatchNetworkTests`).
  - Modify `SpawnManagerTests`, `GameSceneSpawnTests`, `OnlineMatchNetworkTests` and `OnlineOrdersNetworkTests`.

**Interfaces:**
- Consumes:
  - Task 1: `TutorialManager.SeatLearnt`, `RecheckAlumni`, `TutorialSync.Finished`.
  - Task 2: `OnlineMatch.ReportLearnt`, `MachineLearnt`.
  - Existing: `OnlineSession.SeatOf(ulong)`, `Match`, `MatchSpawned`.
- Produces:
  - `public class OnlineTutorial : MonoBehaviour` with `public void Begin(OnlineSession session)`. `OnlineGame.Begin` adds it next to `OnlineOrders`.
  - On a client: `TutorialSync.Finished(seat)` → `session.Match.ReportLearnt(seat)`, while a match exists and isn't the server.
  - On the host: `match.MachineLearnt(machine, seat)` → `TutorialManager.Instance.SeatLearnt(seat)`, when `session.SeatOf(machine) == seat`.
  - Subscriptions follow `OnlineOrders`: the match's event on `MatchSpawned`, and at `Begin` for a match already there. All are undone in `OnDestroy`.
  - `OnlineGame` calls `TutorialManager.Instance.RecheckAlumni()` (when there's one) in two places: after `RemoveRemotePlayer` in `OnPlayerDespawned`, and after removing every remote player in `OnRoleChanged(Offline)`.
  - Removed: `TutorialManager.IsSkipped`, `SkipWhenOnline`, `SkipOnline` and their `OnSwapTutorial` subscription.
  - Removed: `SpawnManager.nonTutorialSpawnPositions`. `PlacePlayersAtStart` always uses `gameSpawnPositions`. Update both summaries.

**Test helpers**, used in `OnlineTutorialNetworkTests` and copied into `OnlineMatchNetworkTests` and `OnlineOrdersNetworkTests`:
- `TutorialStarts()`: `(GameObject[])Reflect.GetField(SpawnManager.Instance, "gameSpawnPositions")`. It replaces `CitySpawns()` wherever that was used.
- `PutBallAt(seat, spot)`: from `OnlineOrdersNetworkTests`.
- `CitySpot()`: the position of the `RespawnPoint` farthest from every scene order's pickup and dropoff (`Object.FindObjectsOfType<Order>(true)`, skipping unset points). It's in the city, over ground, away from every beacon.
- `FinishTutorialInTheCity(seat)`: `PutBallAt(seat, CitySpot())`, then that seat's `TutorialHandler.TeachHandler(TutorialType.Final)`.
  - Why: that's where a finished player waits. A player left in a lane would fall when the tutorial area turns off at the first wave, and respawn: an unasked-for drop.

- [ ] **Step 1: Write the failing tests**

`SpawnManagerTests`:
- `StartingSpawns` sets only `gameSpawnPositions`, with no `out city`.
- The online test becomes `PlacePlayersAtStart_OnlineToo_PlayersStartAtTheTutorial(NetworkRole role)` (Host, Client), asserting `tutorial[0]` and `tutorial[1]`.

`GameSceneSpawnTests`:
- The test becomes `StartPoints_OnePerSeat_EachInTheOpenOverGround`, over `gameSpawnPositions`.
- It keeps the count, not-null, in-the-open, ground-within-`DROP` and not-over-water checks. The tutorial-area check and its `TutorialAreaDisabler` lookup go.
- The summary says these are the start of each seat's tutorial lane.
- It already passes: it checks scene data that Task 3 starts using online.

`OnlineTutorialNetworkTests`:
- Recorders:
  - `ChangeRecorder` on `other.Match` (from `OnlineOrdersNetworkTests`).
  - `SeatRecorder` on `host.Match.MachineLearnt` (from Task 2).
  - `ReportRecorder` (from `OnlineMatchNetworkTests`).
- Helpers from the other tests: `State`, `ActiveScene`, `Slot`, `ScooterIn`, `Handler(seat)`, plus the ones above.
- `TutorialHandlerOf(seat)`: `Slot(seat).Player.GetComponentInChildren<TutorialHandler>()`.

```csharp
[UnityTest] public IEnumerator Hosting_TheTutorialRunsOnline_AndTheFirstWaveWaitsForEveryMachinesPlayer()
// Setup as OnlineMatchNetworkTests.Hosting_…; heard = new ChangeRecorder(other.Match)
// ReadyUp(0), LoadGameScene, other.Match.ReportLoaded(Game); wait (LOADING) until GAME and MainLoop (the opening cutscene)
Assert.Less(Vector3.Distance(TutorialStarts()[0].transform.position, ScooterIn(0).Sphere.transform.position), 1.5f, "this machine's scooter at the start of its lane");
// wait (60 s) until State() == Tutorial
Order[] lessons = (Order[])Reflect.GetField(OrderManager.Instance, "tutorialOrders");
// wait until heard has Spawn(lessons[0].Key, false) and Spawn(lessons[1].Key, false): both lanes' tutorial orders are out
// The other machine's player finishes; the first wave waits for this machine's
other.Match.ReportLearnt(1);                                        // wait 1 s
Assert.AreEqual(GameState.Tutorial, State(), "this machine's player is still in the tutorial");
other.Match.ReportLearnt(0);                                        // wait 1 s
Assert.AreEqual(GameState.Tutorial, State(), "a report for a seat that isn't theirs counts for nothing");
FinishTutorialInTheCity(0);                                         // wait (WAIT) until Begin
Assert.AreEqual(GameState.Begin, State(), "everyone has finished");
// wait until heard has Erase(lessons[0].Key) and Erase(lessons[1].Key): the tutorial orders go at the first wave
// leave: log.MachinesLeave(), other.Leave(), wait until host.PlayersIn == 1, host.Leave(), Assert.IsEmpty(log.Problems)

[UnityTest] public IEnumerator Hosting_APlayerWhoLeavesMidTutorial_DoesntHoldUpTheOthers()
// the match starts; wait until MainLoop; the same start-point assert; wait until Tutorial; FinishTutorialInTheCity(0); wait 1 s
Assert.AreEqual(GameState.Tutorial, State(), "waits for the other machine's player");
// log.MachinesLeave(); other.Leave(); wait until host.PlayersIn == 1, then (WAIT) until Begin
Assert.AreEqual(GameState.Begin, State(), "everyone left has finished");

[UnityTest] public IEnumerator Joining_ThisMachinesPlayerDoesTheTutorial_AndTheHostHearsWhenTheyFinish()
// Setup as OnlineMatchNetworkTests.Joining_…: the host is a bare session; this machine's player moves to seat 2
// host: RequestLoad(Game), wait for this machine's report, RequestShow(Game), SendState(StartingCutscene), SendState(MainLoop); wait until GAME and MainLoop
Assert.Less(Vector3.Distance(TutorialStarts()[1].transform.position, ScooterIn(1).Sphere.transform.position), 1.5f, "at the start of seat 2's lane");
// host.Match.SendState(Tutorial); wait until State() == Tutorial, then 1 s
Assert.IsFalse(TutorialHandlerOf(1).HasLearnt, "the tutorial runs here");
// learnt = new SeatRecorder(); host.Match.MachineLearnt += learnt.Heard
FinishTutorialInTheCity(1);                                         // wait until learnt.Count == 1
Assert.AreEqual(mine.Network.LocalClientId, learnt.Machine);
Assert.AreEqual(1, learnt.Seat);
Assert.AreEqual(GameState.Tutorial, State(), "the host starts the first wave");
// host.Match.SendState(Begin); wait until Begin; leave as usual
```

The tests that assumed the online skip now play the tutorial:
- **`OnlineMatchNetworkTests`:**
  - `Hosting_TheMatchStarts_…`: the scooter check uses `TutorialStarts()[0]`. The "first wave starts … without the tutorial" block becomes: wait until `Tutorial`, `FinishTutorialInTheCity(0)`, `other.Match.ReportLearnt(1)`, wait until `Begin`. Its `HasLearnt` assert stays.
  - `Hosting_AMachineLeavingDuringTheLoadingScreen_…`: `TutorialStarts()[0]`.
  - `Joining_…`:
    - The scooter check uses `TutorialStarts()[1]`.
    - The host sends `Tutorial`, and the test waits for it and calls `FinishTutorialInTheCity(1)`.
    - Then comes the existing `HasLearnt` assert, and then the host's `Begin`.
- **`OnlineOrdersNetworkTests`:**
  - `Hosting_…` waits for `Tutorial`, calls `FinishTutorialInTheCity(0)` and `other.Match.ReportLearnt(1)`, then waits for `Begin`.
  - The tutorial-orders comment says their orders are out until the first wave. The drop spot is `TutorialStarts()[1]`.
  - `Joining_…`: the host sends `Tutorial`, the test waits for it and calls `FinishTutorialInTheCity(1)`, then the host sends `Begin`. The spot is `TutorialStarts()[1]`.

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "OnlineTutorialNetworkTests|SpawnManagerTests"` (in the background)
Expected: 5 failures:
- The 3 new tests fail on their start-point assert: the scooter starts in the city.
- The 2 online `SpawnManagerTests` cases fail: the players aren't placed without city points.

- [ ] **Step 3: Implement `OnlineTutorial` (reports), the `OnlineGame` changes, and the removals** (and the meta).

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "OnlineTutorialNetworkTests|OnlineMatchNetworkTests|OnlineOrdersNetworkTests|SpawnManagerTests|GameSceneSpawnTests|TutorialManagerTests|LocalMatchSmokeTest"`
Expected: `tests: 25 total, 25 passed` (3 + 4 + 2 + 7 + 1 + 7 + 1).

- [ ] **Step 5: No commit.**

---

### Task 4: Cutout steals online

**Files:**
- Modify: `Assets/Scripts/World/CutoutHandler.cs`, `Assets/Scripts/Management/CutoutManager.cs`, `Assets/Scripts/World/Order.cs` (`i/crlf`), `Assets/Scripts/Online/OnlineTutorial.cs`
- Test: `Assets/Tests/Editor/OnlineTutorialNetworkTests.cs` (steps added to its Hosting and Joining tests)

**Interfaces:**
- Consumes: Task 1 `TutorialSync.AskCutout`/`CutoutAsked`; Task 2 `OnlineMatch.AskCutout`/`CutoutAsked`; Phase 3E `OrderSync.MayChange`, `OrderSync.HandlerIn`.
- Produces:

```csharp
// CutoutHandler
public int Seat { get; }                       // whose lane it's in (-1 until InitCutout)
public bool IsOpen { get; }                    // stolen from: its barrier is down
public static CutoutHandler InSeat(int seat);  // the cutout of a seat's lane this match; null when none (or destroyed)
public void InitCutout(int seat);              // was InitCutout(): also remembers the seat (CutoutManager passes player.Index)
public void StealFor(OrderHandler player);     // authority: the player takes its order (StealActive = false, InitOrder(false), AddOrder), then it opens; nothing once open
```

- **Opening** is today's steps after a steal: `hasStolen = true`, the barrier's collider off, `DissolveOut`, the spin, and the holder's `PlayOrderTheft` (skipped when they have no `SoundPool`).
- **`OnTriggerStay`**, for a boosting scooter at a cutout that isn't open:
  - Another machine's scooter (`RemoteAvatar.IsRemote(player)`): nothing. Its own machine asks.
  - The authority (`OrderSync.MayChange`): `StealFor(player)`, as today.
  - An online client: it opens at once, then `TutorialSync.AskCutout(Seat)`.
  - The `try`/`catch` stays.
- **`Update`:** an initialized cutout (`Seat >= 0`) that isn't open opens when its order has a holder. So the host's steal shows on every machine.
- **`Order.InitOrder`** sets `stealActive = false` inside its change (Ruling 6).
- **`OnlineTutorial`:**
  - Client: `TutorialSync.CutoutAsked(seat)` → `match.AskCutout(seat)`.
  - Host: `match.CutoutAsked(machine, seat)` → when `session.SeatOf(machine) == seat`: `CutoutHandler.InSeat(seat).StealFor(OrderSync.HandlerIn(seat))`, when both exist.

- [ ] **Step 1: Write the failing test steps**
  - Helpers:
    - `CutoutOrder(c)`: `(Order)Reflect.GetField(c, "order")`.
    - `Barrier(c)`: the `BoxCollider` of `(GameObject)Reflect.GetField(c, "barrier")`.
    - `CutoutCenter(c)`: `c.GetComponent<Collider>().bounds.center`.
  - `Hosting_TheTutorialRunsOnline_…`: after the tutorial orders' spawns, before any report:

```csharp
CutoutHandler myCutout = CutoutHandler.InSeat(0), theirCutout = CutoutHandler.InSeat(1);
Order myOrder = CutoutOrder(myCutout), theirOrder = CutoutOrder(theirCutout);
other.Match.AskCutout(0);                                            // wait 1 s
Assert.IsFalse(myCutout.IsOpen, "not their lane's cutout");
// The other machine's player boosts into theirs: their machine asks, and the host hands them its order
other.Match.AskCutout(1);                                            // wait until heard has Pickup(theirOrder.Key, 1)
Assert.Less(heard.Changes.IndexOf(OrderChange.Spawn(theirOrder.Key, false)), heard.Changes.IndexOf(OrderChange.Pickup(theirOrder.Key, 1)));
Assert.AreSame(Handler(1), theirOrder.PlayerHolding, "on the other machine's scooter here");
Assert.IsFalse(Barrier(theirCutout).enabled, "their lane's barrier is down here");
// This machine's player boosts into theirs: the host takes it at once, as in a local match
ScooterIn(0).AutoBoost(); PutBallAt(0, CutoutCenter(myCutout));      // wait until heard has Pickup(myOrder.Key, 0)
Assert.IsTrue(myCutout.IsOpen);
```

  - `Joining_…`: after the `Tutorial` state arrives, before finishing:

```csharp
// (the session variables are host and mine, as in OnlineMatchNetworkTests.Joining_…)
CutoutHandler myCutout = CutoutHandler.InSeat(1), hostsCutout = CutoutHandler.InSeat(0);
Order myOrder = CutoutOrder(myCutout), hostsOrder = CutoutOrder(hostsCutout);
Assert.IsTrue(myOrder.StealActive, "this machine's cutout holds its order");
Assert.IsTrue(Barrier(myCutout).enabled, "its barrier is up");
// asked = new SeatRecorder(); host.Match.CutoutAsked += asked.Heard
ScooterIn(1).AutoBoost(); PutBallAt(1, CutoutCenter(myCutout));      // wait until asked.Count == 1
Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
Assert.AreEqual(1, asked.Seat);
Assert.IsFalse(Barrier(myCutout).enabled, "open at once, before the host answers");
Assert.IsNull(myOrder.PlayerHolding, "the host hands out the order");
host.Match.SendOrder(OrderChange.Spawn(myOrder.Key, false)); host.Match.SendOrder(OrderChange.Pickup(myOrder.Key, 1));
// wait until myOrder.PlayerHolding == Handler(1)
Assert.IsFalse(myOrder.StealActive, "the cutout doesn't hold it any more");
// The host's player steals theirs: that cutout opens here too
host.Match.SendOrder(OrderChange.Spawn(hostsOrder.Key, false)); host.Match.SendOrder(OrderChange.Pickup(hostsOrder.Key, 0));
// wait until hostsCutout.IsOpen
Assert.IsFalse(Barrier(hostsCutout).enabled);
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh OnlineTutorialNetworkTests`
Expected: `NO RESULTS` with `error CS0117`/`CS1061` (`InSeat`, `IsOpen`, `Seat`).

- [ ] **Step 3: Implement the cutout rules, the seat from `CutoutManager`, `InitOrder`'s clear (Edit tool), and the `OnlineTutorial` requests.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "OnlineTutorialNetworkTests|OnlineOrdersNetworkTests|LocalMatchSmokeTest"`
Expected: `tests: 6 total, 6 passed`.

- [ ] **Step 5: No commit.**

---

### Task 5: Docs and the full suite

**Files:**
- Modify: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`

- [ ] **Step 1: `docs/online.md`**
  - A "Phase 3F: the tutorial online" section, in the doc's plain style:
    - Each player starts in their own lane.
    - Tutorial orders are the host's.
    - The cutout steal: the barrier opens at once, and the order comes from the host.
    - The first wave begins once every machine's player has driven into the city. A leaver doesn't hold anyone up.
    - How it works: `TutorialManager`'s finished seats, `TutorialSync`, `OnlineMatch.ReportLearnt`/`AskCutout`, `OnlineTutorial`, `CutoutHandler`.
  - Phase 3C's "no tutorial yet" line and the ParrelSync walkthrough's "(no tutorial online)" now describe the tutorial.
  - Known limits:
    - Remove the tutorial line.
    - Add: "A cutout's order reaches a client's scooter about a round trip after its barrier opens."

- [ ] **Step 2: `docs/testing.md`**
  - `TutorialManagerTests` joins the EditMode list.
  - `TutorialMessagesNetworkTests` (port 7800, empty scene) and `OnlineTutorialNetworkTests` (port 7801, game scene: a minute or two each).
  - `GameSceneSpawnTests` now checks the tutorial start points.

- [ ] **Step 3: The roadmap**
  - Progress: a Phase 3F line. "Next" becomes Tasks 3.6–3.8.
  - Task 3.5 bullet 4 ticked, with a note: each machine reports its player's finish; the host counts seats; cutout steals are asked for (this plan's Rulings 2–4).
  - Task 3.8 bullet 1's note gains: "a player who leaves mid-tutorial doesn't hold up the others (Phase 3F)".

- [ ] **Step 4: `EDITOR-TODO.md`**
  - Import step: new scripts `TutorialSync` and `OnlineTutorial`, the changed scripts, the new tests.
  - A two-editor tutorial check:
    - Both start in their own lanes.
    - In the clone, boost into the cutout: its barrier drops at once, and the order rides on the clone's scooter in both editors.
    - Finish in editor 1: the first wave waits.
    - Finish in the clone: the waves start in both.
  - Optional cleanup: the four city `Spawn 1-4` objects the spawn manager no longer uses.

- [ ] **Step 5: Run the full suite** (in the background)

Run: `bash tools/run-tests.sh`
Expected: 425 + 12 new = `tests: 437 total, 437 passed`.
- The new tests: 7 in Task 1, 2 in Task 2, 3 in Task 3.
- If **Save Network Prefab IDs** still hasn't been run, `OnlinePrefabsTests.SavedPrefabIds_…` is the one red test (436 pass).

- [ ] **Step 6: No commit.** Suggested title: "Phase 3F: the tutorial online".

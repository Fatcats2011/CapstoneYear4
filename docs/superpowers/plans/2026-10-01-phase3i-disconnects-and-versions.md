# Phase 3I — Disconnects and Versions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A machine that leaves or drops mid-match never breaks the match:
- A client's orders, the golden order too, go back to the others, and the match goes on.
- When the host goes, every other machine goes back to the menu and says why, from any point of a match, a load included.
- Builds that can't play together are told apart before they connect.
- The editor's direct sessions survive a long load.

**Architecture:**
- **The host gone means back to the menu.**
  - When a session ends by itself, `OnlineSession.Ended` fires: on a client, the host left or went silent; on the host, its own connection failed.
  - `OnlineGame.ShowEnd` then takes a machine that isn't in its menus back through the local loader (`SceneFlow.Current.ReturnToMenu()`). The reason shows at once, and again when the menu is up.
  - The pause menu closes with the Loading state (Phase 3H), and the audio mix resets with any state.
  - A cutscene that's playing stops when the Loading state comes (`CutsceneManager`). Otherwise, offline now, it would ask for the tutorial during the 8 s trip to the menu.
- **A held load can't be called off:** Unity keeps every later load waiting behind it. So the local loader:
  - shows the held scene as soon as it's loaded;
  - starts nothing in it (`ISceneFlow.LeavingForMenu`, read by `SpawnManager.Start`);
  - then loads the menu.
- **A client that leaves holding the golden order:**
  - The order goes back to its start, at its starting value (`OrderHandler.ReleaseOrders` calls `EraseGoldWithoutDelivering`, then `InitOrder`), and the golden round goes on.
  - Other machines replay those two changes (`EraseGold`, `Spawn`) as they do today.
- **Builds:** the Steam lobby's `build` tag adds the build's Netcode setup to its version (`LobbyRules.BuildTag`, `OnlineSession.NetcodeSetup`). The setup is the hash Netcode compares when a player joins. So a friend on a build Netcode would silently drop is told before they join.
- **Long loads:**
  - Direct (Unity Transport) sessions wait 90 s for a silent machine, not 30 s (`OnlineSession.DIRECT_DISCONNECT_MS`).
  - Every machine logs how long each match load took, and its longest frame (`LoadWatch`). That's how to time a build on a slow PC.
- **Tests "kill" a machine by switching off its Unity Transport** (`session.Direct.enabled = false`). It goes silent as a crashed machine does. The others notice after their timeout, which those tests set to 2 s.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 (its Unity Transport adapter, Unity Transport 1.5.0) · Steamworks.NET 2025.164.1 · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap), Task 3.8:
- "Client leaves → the host drops/erases its orders, despawns its avatar, recomputes placements. *(… Still to do: a leaver holding the golden order ends the golden round as if delivered.)*"
- "Host leaves → clients return to the menu with "Host left the match". *(… Returning to the menu from a match is still to do.)*"
- "Reject mismatched builds (lobby `build` metadata + NGO connection-approval payload). *(Approval half done … A build whose Netcode setup differs is dropped by Netcode before approval, with no reason, so the lobby filter matters …)*"
- "Tests: kill a client mid-delivery; kill the host mid-match."
- "Long loads: in the editor, the first load of the game scene took 31 s. Two test sessions in one Unity process lost their connection during it: Unity Transport's disconnect timeout is 30 s. Time a build's load on a slow PC; if it comes close, raise the timeout while loading."
- The roadmap's Review focus 4: "A client or the host drops mid-delivery → leaver's orders return to the pool; match continues (client) or ends cleanly (host)."
- Also:
  - The Phase 3H ruling: "a client paused or mid-cutscene when the host leaves mid-match … is Task 3.8".
  - The Phase 3C deferred minor: "if the host picks Main Menu within the second a slow client is still activating the game scene, that client's return does nothing … (roadmap Task 3.8)".

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**. A bump is a Steam API change: it goes to `main`, after asking.
- **Players:** online is one player per machine until Task 3.9.
- **Local split-screen must behave exactly as before.** `LocalMatchSmokeTest` passes after every task that changes game code.
- **Online rules** (roadmap Global constraints): no mid-match joining, and if the host leaves, the match ends. There's no host migration. Netcode's scene management stays off.
- **Steam API files stay untouched:** `SteamManager.cs` and `SteamStartup.cs`.
- **The user may have Unity editors open on the real project** (their editor, plus a ParrelSync clone sharing `Assets/`):
  - Never edit an existing scene, prefab or ProjectSettings file.
  - Everything in this plan is code.
- **No git commits:** your human partner commits.
- **Meta files:** every new file under `Assets/` gets a `.meta` (`bash tools/newmeta.sh <path>`). Before a task ends, check `git status` for new files without one.
- **Line endings:** `SceneManager.cs` is `i/crlf`: edit it with the Edit tool only. Never `sed -i` on scripts.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]` (the mirror). Game-scene network tests take a minute or two each, so run long runs in the background.
  - Check `ListAgents` before a long run.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
- **Play Mode tests:**
  - No lambda captures a test method's local: after `EnterPlayMode`, even assigning one throws. Use static helpers, and recorder classes subscribed as method groups.
  - Wait by real time, never by frame count. An EditMode test that enters Play Mode may only `yield return null`.
  - Multi-machine tests call `log.MachinesLeave()` before the first machine leaves or goes silent.
- **Tests can't see `internal` members.** Whatever tests call is `public`; they read private state through `Reflect`.
- **The game has its own global `SceneManager` class:** write `UnityEngine.SceneManagement.SceneManager` for Unity's.
- **Branch:** `steam-phase1a`. Phase 3H is committed (`651311e9`) and the tree is clean.

## Rulings (decided while planning)

1. **When the host goes, the match ends everywhere, and the other machines go back to the title screen.**
   - Each one shows "The host left the match." at once, and again when the menu is up.
   - They're offline by then, so they land where an offline "Main Menu" lands (`MainMenu.Start`). Their player stays joined.
   - The same goes for a host whose own connection fails (`OnlineSession.CONNECTION_LOST`).
   - There's no host migration (the roadmap's "if the host leaves, the match ends").
   - Rejected: landing in player select. That needs a flag carried through the menu load, and the title screen is where an offline game goes back to.
   - Cost if wrong: one more button press to play again.
2. **A session that ends during a load finishes the load, shows nothing of it, and goes to the menu.**
   - Unity can't cancel a held load (`allowSceneActivation = false`). Every later load waits behind it, so the menu can't load first.
   - The held scene is shown behind the loading screen as soon as it's loaded. `SpawnManager.Start` asks for no state while the game is leaving for the menu, so no cutscene, music or spawn starts.
   - The same branch covers the Phase 3C minor: the host's "Main Menu" while a slow client is still activating the game scene. That client now follows to the menu instead of staying in the game alone. There's no separate test: the race needs a frame-exact show and return.
   - Cost if wrong: the rest of the load, waited out behind the loading screen.
3. **A leaver's golden order goes back to its start, at its starting value.**
   - It isn't dropped where they were: that spot can be water, or a respawn in progress. The order would then be unreachable, and the golden round could never end.
   - What it grew to was the leaver's: the golden value only grows while someone holds it.
   - It reuses two existing order changes (`EraseGold`, `Spawn`), so there's no new message.
   - Cost if wrong: the others race back to its start.
4. **The lobby's build tag is the version plus the Netcode setup** (`"1.0.0/" + 16 hex digits`).
   - The direct join's payload stays the plain version (`JoinRules`). A direct join of another Netcode setup still shows "Couldn't reach the host.": Netcode drops it before approval. Direct joins are for the editor and LAN tests only.
   - Cost if wrong: if two copies of one build hashed differently, friends couldn't join. A test pins `NetcodeSetup` to what Netcode itself computes for a session of this build.
5. **Long loads:**
   - Direct sessions wait 90 s for a silent machine. Unity Transport reads its timeout once, when the session starts, so "raise it while loading" can't be done. Direct sessions are the editor's and the tests', so the whole session gets the longer wait.
   - Steam sessions keep Steam's own timeouts. Steam's networking thread keeps the connection alive while the game's main thread is busy. That's expected; the two-PC check confirms it with the new log line.
   - Cost if wrong: a crashed editor is noticed after 90 s instead of 30 s.
6. **The "kill" tests:**
   - A silent machine is one whose `UnityTransport` component is off. Its `Update` stops, so it sends nothing, heartbeats included.
   - The game-level silence tests run in the menu scene. A 2 s timeout can't survive a scene load: both sides of a test share one main thread.
   - The game-scene tests use `Leave`. After the timeout, a silent machine goes through the same Netcode events as one that left: the disconnect callback and its objects' despawn.
7. **A client whose own connection drops is told "The host left the match."** too. Netcode gives no reason either way, so the client can't tell the two apart. It's documented as a known limit.
8. **Not in this phase:** public lobbies, rejoining a match in progress, host migration, and telling the player when their own Wi-Fi dropped.

## Review Focus

- **The host leaves while a client is in the opening cutscene with its pause menu open** → the client lands on the title screen, with the pause menu closed and no errors. (Task 4 `Joining_TheHostLeavesDuringTheOpeningCutscene_…`.)
- **The host leaves while a client's held load is still loading, or already loaded and waiting** → the client never starts the match, and ends on the title screen. (Task 4 `Joining_TheHostLeavesWhileThisMachineLoads_…` and `…WhileThisMachinesLoadWaits_…`.)
- **A client leaves holding the golden order** → it's back at its start for the others, and the round doesn't end. (Task 2 `Hosting_AMachineLeavesHoldingTheGoldenOrder_…`.)
- **A machine goes silent instead of leaving** (a crash, a pulled cable) → the others notice after the timeout and act as if it left. (Task 4 `Hosting_AMachineGoesSilent_…`, `Joining_TheHostGoesSilentMidMatch_…`.)
- **A friend's lobby has this version, but another Netcode setup** → they're told it's another build before joining. (Task 1 `Refusal_SameVersion_AnotherNetcodeSetup_SaysAnotherBuild`.)

## Facts (probed 2026-10-01)

- **How a session ends:**
  - `OnlineSession.End` first calls `SetRole(Offline)`. `OnlineGame.OnRoleChanged(Offline)` then:
    - sets `GameAuthority.Role`;
    - sets `SceneFlow.Current = null`, so the local loader takes over;
    - removes the remote players;
    - sets the seat back to −1.
  - Only then does `End` raise `Ended` (`OnlineGame.ShowEnd`). `Leave()` raises no `Ended`.
  - On a client, every unexpected end after the host let it in is `HOST_LEFT` (`EndReason`). On the host it's `CONNECTION_LOST` (`OnServerStopped`).
- **Today, mid-match,** a client whose session ends plays on alone, as the authority now. In player select it stays there (Phase 3D).
- **The local loader** (`SceneManager`, `i/crlf`):
  - `LoadMenuScene` loads only when the active scene isn't the menu scene. `LoadSceneAsync` asks for Loading first.
  - A held load stops at 0.9 with `allowSceneActivation = false`, and raises `HeldSceneReady`.
  - Nothing shows a held load once the online flow is unlinked. So a client whose session ends mid-load stays on the loading screen for good.
  - Unity: "While isDone is false, the AsyncOperation queue is stalled". A menu load behind a held one never starts.
  - `sceneLoad` is never cleared after a load. `RaiseSceneUp` is `sceneLoaded`, which fires after the new scene's `Awake`/`OnEnable` and before its `Start`.
- **`SpawnManager.Start`** (in the game and golden scenes) asks for StartingCutscene, or GoldenCutscene. That starts the cutscene and the music, and hides the loading screen (`HideLoadingScreen` runs on StartingCutscene, GoldenCutscene and Menu).
- **`MainMenu.Start`** asks for Menu offline, and PlayerSelect online.
- **The trip to the menu is slow.** The menu load waits `loadingScreenDelay` (8 s in the menu scene) before it shows the menu. That's longer than a 5 s hint, and longer than most of the 9.2 s opening cutscene.
- **A cutscene asks for the next state when its timer ends** (`EndCutscene`): the tutorial, or the golden round. A client that's offline by then is the authority, so that request applies.
- **On a client, `SetGameState` does nothing:** states come from the host. During a held load the client is in Loading only because the host's own load sent Loading (`OnStateApplied` → `SendState`), right after `RequestLoad`.
- **Pause:** `ClosePauseForState` closes the pause menu on any state but Tutorial, Begin, MainLoop and FinalPackage, so Loading closes it. The mix goes back to "gameplay" on any state (`SoundManager.ResetSnapshotToGameplay`).
- **The cutscene manager lives in the match scenes,** as a prefab instance in Design Scene(Main), FinalAreaScene and Tutorial scene.
- **`ControllerPrompts`** draws above everything (sorting order 1000). Tests read `IsHintShown` and `HintText`.
- **The golden order:**
  - It exists only in FinalAreaScene.
  - `OrderHandler.ReleaseOrders` runs on every machine, from `PlayerInstantiate.RemoveRemotePlayer`. It calls `ReturnHome` and then `EraseOrder`.
  - For the golden order in FinalPackage, `EraseOrder` gives the holder the bonus and calls `GoldOrderDelivered`, which leads to Results. That's today's "as if delivered".
  - `EraseGoldWithoutDelivering`:
    - resets `FinalOrderValue` to Golden;
    - clears the holder (it checks for a missing one);
    - turns the order off and puts it back at its pickup.
  - `InitOrder` puts it out again (`FinalOrder.Start` does that when the round starts).
  - Both are host-only changes that clients replay by key (`EraseGold`, `Spawn`). On a client, the leaver's despawn and these changes can arrive in either order. Both replays work by key and don't need the leaver's scooter.
  - The golden value grows only while someone holds the order (`OrderManager.Update`).
- **Netcode's join check:**
  - `NetworkConfig.GetConfig(bool cache)` is public. It hashes:
    - the protocol versions;
    - the network prefabs' ids (`ForceSamePrefabs`);
    - tick rate, approval and scene management.
  - A joiner whose hash differs is dropped before approval, with no reason. The client then sees "Couldn't reach the host.".
  - `NetworkPrefabs.Add` registers a prefab's id at once, so a `NetworkConfig` built without a `NetworkManager` hashes the same as a session's.
- **Unity Transport** (`UnityTransport`, inside NGO 1.15.1):
  - `DisconnectTimeoutMS` (default 30 000) is read when the session starts.
  - Its `Update()` is a MonoBehaviour message that sends, beats and receives. So while the component is off, nothing goes out, and the other side drops the connection after its own timeout.
- **The Steam transport** sets no timeouts of its own, so Steam's defaults apply.
- **The game scene's `OrderManager` carries on into the golden round** (Phase 3E: "the order manager outlive[s] scenes").
- **Test recipes:**
  - `OnlineCuesNetworkTests` brings the host to time up with `Reflect.SetField(OrderManager.Instance, "wave", 99)` and `InitWave()`. Without `StopAllCoroutines`, `PostGameClarity` then loads the golden round (`LoadFinalOrderScene`, held online).
  - `OnlineOrdersNetworkTests` has the helpers:
    - `ShareAt(OnlineScooter, Vector3)` moves another machine's scooter;
    - `ScooterInSeat`, `PlayerInSeat`, `Handler(seat)`;
    - `FinishTutorialInTheCity(seat)`.
- **Ports in use:** 7791–7808. This plan adds 7809.

## File Structure

- Create:
  - `Assets/Scripts/Online/LoadWatch.cs`: times a match load (ready time and longest frame).
  - Tests: `Assets/Tests/Editor/LoadWatchTests.cs`; `Assets/Tests/Editor/DisconnectsNetworkTests.cs` (port 7809).
- Modify:
  - `Assets/Scripts/Online/LobbyRules.cs`: build tags.
  - `Assets/Scripts/Online/OnlineSession.cs`: `NetcodeSetup`, `DIRECT_DISCONNECT_MS`.
  - `Assets/Scripts/Online/OnlinePlay.cs`: `BuildTag`, for the lobby.
  - `Assets/Scripts/Online/OnlineLobby.cs`: its third parameter is the build tag (a rename).
  - `Assets/Scripts/Player/OrderHandler.cs`: the golden order's release.
  - `Assets/Scripts/Online/OnlineSceneFlow.cs`: the load watch; `LeavingForMenu`.
  - `Assets/Scripts/Online/OnlineGame.cs`: logs loads; back to the menu after an unexpected end.
  - `Assets/Scripts/Menu/ISceneFlow.cs`: `LeavingForMenu`.
  - `Assets/Scripts/Menu/SceneManager.cs` (`i/crlf`): the menu after a held load.
  - `Assets/Scripts/Menu/CutsceneManager.cs`: a cutscene stops when a load starts.
  - `Assets/Scripts/Player/SpawnManager.cs`: starts nothing while leaving for the menu.
  - Tests:
    - `LobbyRulesTests`, `OnlineSessionTests`, `OnlineSceneFlowTests`.
    - `SceneFlowTests` (`FakeSceneFlow` gains `LeavingForMenu`).
    - `OnlineOrdersNetworkTests` (+2).
    - `OnlinePlayNetworkTests` (one assertion).
- Docs: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`.

---

### Task 1: Builds with another Netcode setup are told apart

**Files:**
- Modify: `Assets/Scripts/Online/LobbyRules.cs`, `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Scripts/Online/OnlinePlay.cs`, `Assets/Scripts/Online/OnlineLobby.cs`
- Test: `Assets/Tests/Editor/LobbyRulesTests.cs`, `Assets/Tests/Editor/OnlineSessionTests.cs`, `Assets/Tests/Editor/OnlinePlayNetworkTests.cs`

**Interfaces:**
- Produces:
  - `LobbyRules.BuildTag(string version, ulong netcodeSetup) : string` gives `version + "/" + netcodeSetup.ToString("x16")`.
  - `LobbyRules.VersionOf(string buildTag) : string` gives the part before the last `/`, or the whole tag when there's no `/`.
  - `LobbyRules.OtherBuild(string version) : string` gives `"That match is on another build of version " + version + ". You both need the same build."`.
  - `LobbyRules.Refusal(lobbyGame, lobbyBuild, myBuild)` keeps its signature, and now compares build tags.
  - `OnlineSession.NetcodeSetup(OnlinePrefabs prefabs) : ulong` is a public static.
  - `OnlinePlay.BuildTag() : string` is a public static.

- [ ] **Step 1: Write the failing tests**

In `LobbyRulesTests` (the two existing `Refusal_…` tests pass plain versions, and stay as they are):

```csharp
[Test]
public void BuildTag_IsTheVersion_ThenTheNetcodeSetupInHex()
{
    Assert.AreEqual("1.2/00000000000000ff", LobbyRules.BuildTag("1.2", 255));
    Assert.AreEqual("1.2", LobbyRules.VersionOf("1.2/00000000000000ff"));
}

[Test]
public void VersionOf_ATagWithoutASetup_IsTheWholeTag()
{
    Assert.AreEqual("1.2", LobbyRules.VersionOf("1.2"));
}

[Test]
public void Refusal_SameVersion_AnotherNetcodeSetup_SaysAnotherBuild()
{
    string refusal = LobbyRules.Refusal("doa", LobbyRules.BuildTag("1.2", 1), LobbyRules.BuildTag("1.2", 2));

    Assert.AreEqual(LobbyRules.OtherBuild("1.2"), refusal);
}

[Test]
public void Refusal_AnotherVersion_NamesBothVersions_WithoutTheirSetups()
{
    string refusal = LobbyRules.Refusal("doa", LobbyRules.BuildTag("1.1", 1), LobbyRules.BuildTag("1.2", 1));

    Assert.AreEqual(JoinRules.Refusal("1.1", "1.2", 0, GameState.Menu), refusal);
}
```

In `OnlineSessionTests`:

```csharp
[Test]
public void NetcodeSetup_IsWhatASessionsNetcodeCompares()
{
    OnlineSession session = NewSession();

    Assert.AreEqual(session.Network.NetworkConfig.GetConfig(false), OnlineSession.NetcodeSetup(OnlinePrefabs.Load()));
}
```

In `OnlinePlayNetworkTests.Hosting_YHostsALobby_FriendsJoin_AndBEndsIt`, once the lobby exists:

```csharp
Assert.AreEqual(OnlinePlay.BuildTag(), lobbies.GetData(OnlinePlay.Instance.Lobby.Current, LobbyRules.BUILD_KEY),
    "tagged with this build's version and Netcode setup");
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash tools/run-tests.sh "LobbyRulesTests|OnlineSessionTests"`
Expected: `NO RESULTS` with `error CS0117` (`LobbyRules` has no `BuildTag`; `OnlineSession` has no `NetcodeSetup`).

- [ ] **Step 3: Implement**
  - **`LobbyRules`:**
    - `BuildTag`, `VersionOf` and `OtherBuild`, as in Interfaces.
    - `Refusal` keeps `NOT_THIS_GAME` first. Equal tags pass.
    - Tags whose versions differ get `JoinRules.Refusal(VersionOf(lobbyBuild), VersionOf(myBuild), 0, GameState.Menu)`. Anything else gets `OtherBuild(VersionOf(myBuild))`.
  - **`OnlineSession`:**
    - A private static `NetworkConfig NewConfig(NetworkTransport transport)` holds the settings `Create` sets today (approval on, scene management off). `Create` uses it, so the two can't drift apart.
    - A private static `GameObject[] NetworkPrefabsOf(OnlinePrefabs prefabs)` lists the player, match and scooter prefabs. `Create` registers those, as today.
    - `NetcodeSetup(prefabs)` builds `NewConfig(null)`, adds `new NetworkPrefab { Prefab = p }` for each with `config.Prefabs.Add`, and returns `config.GetConfig(false)`.
  - **`OnlinePlay`:**
    - `BuildTag()` returns `LobbyRules.BuildTag(Application.version, OnlineSession.NetcodeSetup(OnlinePrefabs.Load()))`.
    - `Use` passes it to `new OnlineLobby(...)`.
  - **`OnlineLobby`:** rename the constructor's `version` parameter and field to `build`, and fix the docs ("this build's tag: `LobbyRules.BuildTag`"). There's no behaviour change.

- [ ] **Step 4: Run them to verify they pass**

Run: `bash tools/run-tests.sh "LobbyRulesTests|OnlineSessionTests|OnlineLobbyTests|OnlinePlayNetworkTests"`
Expected: all pass, with 5 more tests than before.

- [ ] **Step 5: No commit.**

---

### Task 2: A leaver's orders, the golden order too

**Files:**
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`ReleaseOrders`)
- Test: `Assets/Tests/Editor/OnlineOrdersNetworkTests.cs` (port 7799, as the file has)

**Interfaces:**
- Consumes: `Order.EraseGoldWithoutDelivering()`, `Order.InitOrder(bool shouldAdd = true)`, `Order.Value`, and `Order.PickupPoint`, all existing.
- Produces: nothing new. `ReleaseOrders()` keeps its signature.

- [ ] **Step 1: Write the failing tests**

Both start as the file's `Hosting_…` test does: the menu scene, this machine hosting with the game, another machine joining in seat 2, the match loaded, both players through the tutorial, and the first wave begun. Move that opening into a static `IEnumerator` helper the three `Hosting_…` tests share. It fills a small holder class (`host`, `other`, `heard`, `taken`) and captures no locals.

```csharp
[UnityTest]
public IEnumerator Hosting_AMachineLeavesMidDelivery_ItsOrderIsStillErasedAfterItsThrow()
{
    // … the opening; `first` = the first wave's first order, as in Hosting_…
    // Their scooter picks it up, then reaches its dropoff: the host decides the delivery
    ShareAt(ScooterInSeat(other, 1), first.transform.position);
    // … wait for Pickup(first.Key, 1)
    ShareAt(ScooterInSeat(other, 1), first.DropoffPoint.position);
    // … wait until `heard` has Deliver(first.Key, 1), then at once, while the order flies to the customer:
    log.MachinesLeave();
    other.Leave();
    // … wait until host.PlayersIn == 1, then 1 s more (the throw takes 0.25 s)

    Assert.IsFalse(first.IsActive, "erased after its throw: back in the pool");
    Assert.IsNull(first.PlayerHolding);
    Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "their scooter went");
    Assert.IsTrue(OrderManager.Instance.GameStarted, "the waves go on");
    // … host.Leave(); log.Dispose(); Assert.IsEmpty(log.Problems, …)
}

[UnityTest]
public IEnumerator Hosting_AMachineLeavesHoldingTheGoldenOrder_ItGoesBackToItsStart_AndTheRoundGoesOn()
{
    // … the opening
    // Time up: the host's golden round loads on both machines. The other machine reports it loaded when asked
    // (a recorder on other.Match.LoadRequested, then other.Match.ReportLoaded(MatchScene.FinalOrder))
    Reflect.SetField(OrderManager.Instance, "wave", 99);
    OrderManager.Instance.InitWave();
    // … wait (LOADING) for GoldenCutscene, CutsceneManager.Instance.Skip(), wait for FinalPackage
    Order golden = (Order)Reflect.GetField(OrderManager.Instance, "finalOrder");
    Vector3 start = golden.PickupPoint.position;
    ShareAt(ScooterInSeat(other, 1), start);
    // … wait for Pickup(golden.Key, 1)
    Assert.AreSame(Handler(1), golden.PlayerHolding, "the other machine's player holds the golden order");
    OrderManager.Instance.FinalOrderValue = 90; // it grew while they held it

    log.MachinesLeave();
    other.Leave();
    // … wait until host.PlayersIn == 1, then one frame

    Assert.AreEqual(GameState.FinalPackage, State(), "the golden round goes on");
    Assert.IsTrue(golden.IsActive, "out again");
    Assert.IsNull(golden.PlayerHolding);
    Assert.Less(Vector3.Distance(start, golden.transform.position), 0.01f, "back at its start");
    Assert.AreEqual((int)Constants.OrderValue.Golden, OrderManager.Instance.FinalOrderValue, "at its starting value");

    // This machine's player can take it now: move their ball onto its start
    // … wait until golden.PlayerHolding == Handler(0)
    Assert.AreSame(Handler(0), golden.PlayerHolding, "the round goes on for the players still here");
    // … host.Leave(); log.Dispose(); Assert.IsEmpty(log.Problems, …)
}
```

- [ ] **Step 2: Run them to verify they fail** (in the background: three game loads)

Run: `bash tools/run-tests.sh OnlineOrdersNetworkTests`
Expected:
- `…MidDelivery_…` passes at once. It pins today's behaviour: a delivered order has left its holder before the throw ends.
- `…HoldingTheGoldenOrder_…` fails at "out again". Today the leaver "delivers" it: it's erased, and Results follows.

- [ ] **Step 3: Implement `ReleaseOrders`**

For each held order, after `ReturnHome()`:
- the golden order (`order.Value == Constants.OrderValue.Golden`) gets `EraseGoldWithoutDelivering()`, then `InitOrder()`;
- any other order gets `EraseOrder()`, as before.

Update the summary: "the golden order goes back to its start, at its starting value: a player leaving doesn't deliver it". On a client both calls do nothing (`OrderSync.MayChange`): the host's changes replay there.

- [ ] **Step 4: Run them to verify they pass**

Run: `bash tools/run-tests.sh "OnlineOrdersNetworkTests|LocalMatchSmokeTest"`
Expected: `tests: 4 total, 4 passed`.

- [ ] **Step 5: No commit.**

---

### Task 3: Long loads: the direct timeout, and timing every load

**Files:**
- Create: `Assets/Scripts/Online/LoadWatch.cs`, `Assets/Tests/Editor/LoadWatchTests.cs`
- Modify: `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Scripts/Online/OnlineSceneFlow.cs`, `Assets/Scripts/Online/OnlineGame.cs`
- Test: `Assets/Tests/Editor/OnlineSessionTests.cs`, `Assets/Tests/Editor/OnlineSceneFlowTests.cs`

**Interfaces:**
- Produces:
  - **`OnlineSession.DIRECT_DISCONNECT_MS = 90000`** (`public const int`). `Create` sets `Direct.DisconnectTimeoutMS` to it.
  - **`LoadWatch`** (plain C#):
    - `public const int FRAMES_AFTER_UP = 2`: the scene's activation frame, then the frame its scripts' `Start` runs in.
    - `bool Running`.
    - `void Start(MatchScene scene, float now)`: starts over.
    - `void Ready(float now)`: the held scene is loaded.
    - `void Up()`: the scene is shown.
    - `string Tick(float now)`: called each frame. It counts the time since the last tick (or since `Start`) as a frame. It returns the report on the `FRAMES_AFTER_UP`th tick after `Up`, and stops; otherwise it returns null. It returns null while not running.
    - `void Stop()`.
    - `static string Report(MatchScene scene, float readySeconds, float longestFrame)`: `"Online: the " + scene + " scene was ready here after " + ready + " s; its longest frame took " + longest + " s"`. Both numbers use `ToString("0.0", CultureInfo.InvariantCulture)`.
  - **`OnlineSceneFlow`:**
    - The constructor becomes `OnlineSceneFlow(ISceneFlow localFlow, IMatchLoader matchLoader, Func<float> realTime)`.
    - `public void Tick()`.
    - `public event Action<string> LoadTimed`.

- [ ] **Step 1: Write the failing tests**

`LoadWatchTests`:

```csharp
[Test]
public void Report_GivesTheTimeToReady_AndTheLongestFrame_TheSceneShowingIncluded()
{
    LoadWatch watch = new LoadWatch();
    watch.Start(MatchScene.Game, 10f);
    Assert.IsNull(watch.Tick(10.5f));
    Assert.IsNull(watch.Tick(14.5f));       // a 4 s frame
    watch.Ready(15f);                       // ready after 5 s
    Assert.IsNull(watch.Tick(15.2f));
    watch.Up();
    Assert.IsNull(watch.Tick(21.2f));       // the scene's activation: a 6 s frame
    Assert.AreEqual(LoadWatch.Report(MatchScene.Game, 5f, 6f), watch.Tick(21.3f));
    Assert.IsFalse(watch.Running);
    Assert.IsNull(watch.Tick(30f), "it reports once");
}

[Test]
public void Tick_BeforeAnyLoad_ReportsNothing()
{
    Assert.IsNull(new LoadWatch().Tick(1f));
}

[Test]
public void Report_ReadsTheSameEverywhere()
{
    Assert.AreEqual("Online: the Game scene was ready here after 12.3 s; its longest frame took 4.0 s",
        LoadWatch.Report(MatchScene.Game, 12.34f, 4f));
}
```

`OnlineSceneFlowTests`:
- `SetUp` passes a clock: a `float now` field, a `float Now() { return now; }` method, and `new OnlineSceneFlow(local, loader, Now)`. Collect `flow.LoadTimed` in a list, `reports`. The file's other `new OnlineSceneFlow(...)` gets `Now` too.
- Add:

```csharp
[Test]
public void Client_TimesItsLoad_AndReportsItOnceTheSceneIsUp()
{
    LinkAsClient();
    now = 10f; link.RaiseLoadRequested(MatchScene.Game);
    now = 13f; flow.Tick();                         // a 3 s frame
    now = 14f; loader.RaiseHeldSceneReady();        // ready after 4 s
    link.RaiseShowRequested(MatchScene.Game);
    loader.RaiseSceneUp();
    now = 16.5f; flow.Tick();                       // the activation: 3.5 s
    now = 16.6f; flow.Tick();

    CollectionAssert.AreEqual(new[] { LoadWatch.Report(MatchScene.Game, 4f, 3.5f) }, reports);
}

[Test]
public void Host_ReportsHowLongItsOwnLoadTook_NotTheWaitForOthers()
{
    LinkAsHost();
    now = 10f; flow.LoadGameScene();
    now = 11f; flow.Tick();
    now = 12f; loader.RaiseHeldSceneReady();        // ready here after 2 s
    for (now = 12f; now <= 30f; now += 1f)
        flow.Tick();                                // the other machine takes its time
    link.RaiseMachineLoaded(CLIENT, MatchScene.Game);
    loader.RaiseSceneUp();
    now = 31.5f; flow.Tick();
    now = 32f; flow.Tick();

    CollectionAssert.AreEqual(new[] { LoadWatch.Report(MatchScene.Game, 2f, 1.5f) }, reports);
}

[Test]
public void Client_SessionEndsMidLoad_ReportsNothing()
{
    LinkAsClient();
    now = 10f; link.RaiseLoadRequested(MatchScene.Game);
    flow.Unlink();
    now = 20f; loader.RaiseHeldSceneReady();
    loader.RaiseSceneUp();
    now = 21f; flow.Tick();
    now = 22f; flow.Tick();

    CollectionAssert.IsEmpty(reports);
}
```

`OnlineSessionTests`:

```csharp
[Test]
public void Create_DirectSessionsWaitOutALongLoad()
{
    Assert.AreEqual(OnlineSession.DIRECT_DISCONNECT_MS, NewSession().Direct.DisconnectTimeoutMS);
    Assert.GreaterOrEqual(OnlineSession.DIRECT_DISCONNECT_MS, 60000, "well past a cold editor load's 31-41 s");
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `bash tools/run-tests.sh "LoadWatchTests|OnlineSceneFlowTests|OnlineSessionTests"`
Expected: `NO RESULTS` with `error CS0246` (`LoadWatch`) and `error CS1729` (`OnlineSceneFlow` has no 3-argument constructor).

- [ ] **Step 3: Implement**
  - **`LoadWatch`**, as in Interfaces.
  - **`OnlineSession`:** `DIRECT_DISCONNECT_MS`, documented: "A direct session (the editor's and tests') drops a machine it hasn't heard from in 90 s. A cold editor load can stall longer than Unity Transport's own 30 s. Steam sessions keep Steam's timeouts." `Create` sets it.
  - **`OnlineSceneFlow`:**
    - The watch starts in `Begin` (host) and `LoadForHost` (client), with `realTime()`.
    - `HeldSceneReady` and `SceneUp` mark it first, while it's running: ready, then up. Then they do what they do today. Both have early returns: the host's `SceneUp` returns at once because the host is never "changing".
    - `Unlink` stops it.
    - `Tick()` raises `LoadTimed` with a non-null report.
  - **`OnlineGame`:**
    - `Begin` builds the flow with `RealTime`, a static method returning `Time.realtimeSinceStartup`. It subscribes `LoadTimed` to a static `LogLoad(string)` that calls `Debug.Log`.
    - `Update` calls `sceneFlow.Tick()` first, before the `PlayerInstantiate` check.

- [ ] **Step 4: Run them to verify they pass**

Run: `bash tools/run-tests.sh "LoadWatchTests|OnlineSceneFlowTests|OnlineSessionTests|OnlineGameNetworkTests"`
Expected: all pass, with 7 more tests than before.

- [ ] **Step 5: No commit.**

---

### Task 4: When the host goes, everyone goes back to the menu

**Files:**
- Create: `Assets/Tests/Editor/DisconnectsNetworkTests.cs` (port 7809)
- Modify:
  - `Assets/Scripts/Menu/ISceneFlow.cs`
  - `Assets/Scripts/Menu/SceneManager.cs` (`i/crlf`: Edit tool only)
  - `Assets/Scripts/Online/OnlineSceneFlow.cs`, `Assets/Scripts/Online/OnlineGame.cs`
  - `Assets/Scripts/Menu/CutsceneManager.cs`
  - `Assets/Scripts/Player/SpawnManager.cs`
  - `Assets/Tests/Editor/SceneFlowTests.cs` (`FakeSceneFlow`)

**Interfaces:**
- Consumes:
  - `OnlineSession.DIRECT_DISCONNECT_MS` and `UnityTransport.DisconnectTimeoutMS` (the tests set 2000).
  - Task 3's load report, as the log line that test d expects.
- Produces:
  - `ISceneFlow.LeavingForMenu { get; } : bool`: "the game is on its way back to the menu: a match scene shown only to get there starts nothing".
  - `SceneManager.LeavingForMenu`, and `OnlineSceneFlow.LeavingForMenu`, which returns `local != null && local.LeavingForMenu`.
  - `CutsceneManager` ends a playing cutscene on Loading. There's no new member.

- [ ] **Step 1: Write the failing tests**

`DisconnectsNetworkTests`:
- **Constants:** `MENU_SCENE`, `MENU = "Alex Player Testing"`, `GAME = "Design Scene(Main)"`, `THIS_COMPUTER`, `PORT = 7809`, `VERSION = "test"`, `WAIT = 15f`, `LOADING = 180f`, and `SILENT_MS = 2000`.
- **Recorders,** each a class subscribed with a method group:
  - `EndedRecorder` (`Reason`), on `OnlineSession.Ended`.
  - `ReturnRecorder` (`Returns`), on `SceneManager.Instance.OnReturnToMenu`.
  - `StateRecorder` (`States`), on `GameManager.Instance.StateApplied`.
  - `SceneRecorder` (`Loaded` scene names), on `UnityEngine.SceneManagement.SceneManager.sceneLoaded`, with `Dispose` to unsubscribe.
  - `ReportRecorder`, as in `OnlineMatchFlowNetworkTests`.
- **Helpers:** `State()`, `ActiveScene()`, `Slot(i)`, `CutsceneOn()` (as `OnlineMatchFlowNetworkTests` has), and:

```csharp
// A machine that stops answering, as a crashed one does: Unity Transport sends and beats only from its Update
static void GoSilent(OnlineSession session)
{
    session.Direct.enabled = false;
}
```

Every test starts in the menu scene with one test player (`TestPlayers.Add()`), as `OnlineMatchFlowNetworkTests` does.

a. **`Hosting_AMachineGoesSilent_ItsPlayerGoes_AndTheHostPlaysOn`** ("kill a client"):
   - This machine hosts with the game (`OnlineGame.Attach`), then goes to player select (`SetGameState(GameState.PlayerSelect)`, as `OnlineOrdersNetworkTests` does). The other machine is a bare session. Both get `Direct.DisconnectTimeoutMS = SILENT_MS` before `HostDirect` and `JoinDirect`.
   - Wait for `Slot(1)`.
   - `GameManager.Instance.SetGameState(GameState.StartingCutscene)`: mid-match.
   - `log.MachinesLeave(); GoSilent(other);` Then wait up to `WAIT` for `Slot(1) == null`.
   - Assert:
     ```csharp
     Assert.IsNull(Slot(1), "their scooter went, as if they'd left");
     Assert.AreEqual(1, host.PlayersIn);
     Assert.IsTrue(host.IsRunning, "the host plays on");
     Assert.AreEqual(GameState.StartingCutscene, State(), "the match goes on");
     ```
   - End: `other.Direct.enabled = true; other.Leave(); host.Leave();`, then the log check.

b. **`Joining_TheHostGoesSilentMidMatch_ThisMachineHeadsBackToTheMenu_AndSaysWhy`** ("kill the host"):
   - The host is a bare session. This machine joins with the game. Both use `SILENT_MS`.
   - `host.Match.SendState(GameState.StartingCutscene)`, and wait for it here.
   - Subscribe the `ReturnRecorder` and `EndedRecorder`. Then `log.MachinesLeave(); GoSilent(host);` and wait up to `WAIT` for the reason.
   - Assert:
     ```csharp
     Assert.AreEqual(OnlineSession.HOST_LEFT, ended.Reason);
     Assert.AreEqual(1, returns.Returns, "back to the menu");
     Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
     Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);
     Assert.IsTrue(ControllerPrompts.Instance.IsHintShown);
     ```
   - In the menu scene the return loads nothing: the menu is the active scene.
   - End: `host.Direct.enabled = true; host.Leave();`.

c. **`Hosting_ItsConnectionFailsMidMatch_TheHostHeadsBackToTheMenu_AndSaysWhy`:**
   - This machine hosts with the game, alone.
   - `GameManager.Instance.SetGameState(GameState.StartingCutscene)`, then `host.Network.Shutdown()` (Netcode stopping by itself, not `Leave`). Wait for the reason.
   - Assert `CONNECTION_LOST` as the reason and as the hint, and `returns.Returns == 1`.

d. **`Joining_TheHostLeavesDuringTheOpeningCutscene_ThisMachineGoesBackToTheMenu_WithItsPauseClosed`:**
   - The default timeouts apply. Before the load:
     ```csharp
     LogAssert.Expect(LogType.Log, new Regex(@"^Online: the Game scene was ready here after \d+\.\d s; its longest frame took \d+\.\d s$"));
     ```
   - The host is a bare session. This machine joins, and the host loads and shows the game (`RequestLoad`, the report, `RequestShow`), then sends StartingCutscene. Wait for `CutsceneOn()`, then 0.5 s more: the load's report goes out 2 frames after the scene is up.
   - Pause: `PlayerInstantiate.Instance.PlayerPause(Slot(1).Input)`. If the pause menu needs it, first call `SwapMenuType(MenuType.PauseMenu)` on that player's menu, as `OnlinePauseTests` does.
   - Subscribe the `StateRecorder`. Then `log.MachinesLeave(); host.Leave();`, and wait up to `LOADING` for the menu (`ActiveScene() == MENU && State() == GameState.Menu`).
   - Assert:
     ```csharp
     CollectionAssert.DoesNotContain(states.States, GameState.Tutorial, "the cutscene stopped with the load: it asked for nothing");
     Assert.AreEqual(GameState.Menu, State(), "the title screen");
     Assert.IsFalse(PlayerInstantiate.Instance.IsPaused, "its pause menu closed");
     Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);
     Assert.IsTrue(ControllerPrompts.Instance.IsHintShown, "said again once the menu is up");
     Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "this machine's player is still here");
     Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
     ```

e. **`Joining_TheHostLeavesWhileThisMachineLoads_ItGoesBackToTheMenu_WithoutStartingTheMatch`:**
   - `host.Match.RequestLoad(MatchScene.Game); host.Match.SendState(GameState.Loading);` That's what a real host's own load sends. Wait for `State() == GameState.Loading` here.
   - `Assert.IsEmpty(reports.Machines, "still loading");`
   - Subscribe the `StateRecorder` and `SceneRecorder`, then `log.MachinesLeave(); host.Leave();`, and wait up to `LOADING` for the menu.
   - Assert:
     ```csharp
     CollectionAssert.AreEqual(new[] { GAME, MENU }, scenes.Loaded, "the load finished, then the menu");
     CollectionAssert.DoesNotContain(states.States, GameState.StartingCutscene, "the match never started");
     Assert.AreEqual(GameState.Menu, State());
     Assert.AreEqual(OnlineSession.HOST_LEFT, ControllerPrompts.Instance.HintText);
     ```

f. **`Joining_TheHostLeavesWhileThisMachinesLoadWaits_ItGoesBackToTheMenu_WithoutStartingTheMatch`:** as test e, but leave once `reports.Machines` holds this machine: loaded, and held for the host's show. The same assertions.

- [ ] **Step 2: Run them to verify they fail** (in the background: three game loads)

Run: `bash tools/run-tests.sh DisconnectsNetworkTests`
Expected (after the compile check, `FakeSceneFlow` included):
- a passes at once: it pins today's behaviour, now under a real silence.
- b and c fail at `returns.Returns`: expected 1, was 0.
- d times out in Tutorial: the client plays on alone. With everything but the `CutsceneManager` change, d fails at the `Tutorial` assertion instead.
- e and f time out in Loading: stuck behind the loading screen.

- [ ] **Step 3: Implement**

**`ISceneFlow`** gets `LeavingForMenu`. `FakeSceneFlow` returns `false`, and `OnlineSceneFlow` forwards to `local`.

**`SceneManager`** (Edit tool only) gets three new fields:
- `int loadingIndex = -1`: the scene the load in progress goes to.
- `bool holding`: true from a held load's start until its scene is up.
- `bool menuAfterLoad`.

Then:

```csharp
public bool LeavingForMenu { get { return menuAfterLoad || loadingIndex == PlayerSelectScene.BuildIndex; } }

// LoadMenuScene, before its active-scene check:
if (loadingIndex == PlayerSelectScene.BuildIndex)
    return; // on its way already: the host took everyone back, then left
if (holding)
{
    // A held load can't be called off: Unity keeps every later load waiting behind it. It shows as soon as
    // it's loaded, starts nothing (LeavingForMenu), and the menu follows (RaiseSceneUp)
    menuAfterLoad = true;
    if (sceneLoad != null && !sceneLoad.allowSceneActivation)
        ConfirmLoad();
    return;
}
```

- `LoadSceneAsync` starts with `loadingIndex = sceneToLoad; holding = holdForHost; sceneLoad = null;`. Its hold branch calls `ConfirmLoad()` instead of raising `HeldSceneReady` when `menuAfterLoad` is set.
- `RaiseSceneUp` does three things, in order:
  1. If the scene that came up is the one loading: `loadingIndex = -1; holding = false;`.
  2. If `menuAfterLoad` and that scene isn't the menu: clear the flag and call `LoadMenuScene()`. That happens before the shown scene's `Start`.
  3. `SceneUp?.Invoke()`.

**`CutsceneManager.EndForState`** also ends a playing cutscene when the state is `GameState.Loading`, without asking for anything. Today it ends one only on its next state. Its summary says so: "a load means the game is leaving this scene".

**`SpawnManager.Start`** first does:

```csharp
// A scene shown only on the way back to the menu (the session ended during its load) starts nothing
ISceneFlow flow = SceneFlow.Current;
if (flow != null && flow.LeavingForMenu)
    return;
```

**`OnlineGame`:**
- `ShowEnd` shows the reason as today. Then, if `GameManager.Instance` exists and `!LobbyRules.InTheMenus(MainState)`:
  - it stores the reason in `pendingNotice`;
  - it calls `ReturnToMenu()` on `SceneFlow.Current` (null-checked), which is the local loader by now.
- Summary: "the session ended by itself … a match can't go on without its host: a machine in one, or on its way into one, goes back to the menu, where it hears why again".
- `OnStateApplied`: when `pendingNotice != null` and the state is in the menus, show it (`NOTICE_SECONDS`) and clear it. The host's state sending stays as it is.

- [ ] **Step 4: Run them to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "DisconnectsNetworkTests|SceneFlowTests|OnlineSceneFlowTests|OnlinePlayNetworkTests|OnlineMatchFlowNetworkTests|LocalMatchSmokeTest"`
Expected: all pass. The smoke test's menus, loads and pause are unchanged offline.

- [ ] **Step 5: No commit.**

---

### Task 5: Docs and the full suite

**Files:**
- Modify: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`

- [ ] **Step 1: `docs/online.md`**
  - A "Phase 3I: disconnects and versions" section, in the doc's plain style:
    - When the host leaves (or its game stops answering), everyone else goes back to the title screen and sees "The host left the match.", from anywhere in a match: a cutscene, a paused game, or a loading screen. A load that's under way finishes behind the loading screen first.
    - A player who leaves holding the golden order puts it back at its start, at its starting value, and the golden round goes on.
    - A player who leaves while their delivery is in the air: it lands as usual.
    - A friend whose game is another build is told before joining, even with the same version number (the lobby's build tag carries the Netcode setup).
    - In the editor, a machine that stops answering is dropped after 90 s (cold loads can be that slow). Over Steam, Steam's own timeouts apply.
    - Every machine logs each match load: "Online: the Game scene was ready here after … s; its longest frame took … s".
    - How it works: `OnlineGame.ShowEnd`, `ISceneFlow.LeavingForMenu`, the held-load branch in `SceneManager.LoadMenuScene`, a cutscene stopping on Loading (`CutsceneManager`), `OrderHandler.ReleaseOrders`, `LobbyRules.BuildTag`, `OnlineSession.NetcodeSetup`, `DIRECT_DISCONNECT_MS`, `LoadWatch`.
  - Phase 3F's "If the host leaves, a client whose player had finished goes on alone, offline." becomes "If the host leaves, everyone goes back to the title screen (Phase 3I)."
  - The two-editors walkthrough gains: stop Play in editor 1 mid-match, and the clone goes back to the title screen with the message.
  - Known limits:
    - Remove "A player who leaves holding the golden order ends the golden round…" and "If the host leaves mid-match, clients stay where they are…".
    - The Netcode-setup line now covers direct joins only: over Steam the lobby tag tells builds apart.
    - Add: a client whose own connection drops is also told "The host left the match.".
    - Add: a machine that crashes is noticed after the timeout: 90 s in the editor, Steam's over Steam.

- [ ] **Step 2: `docs/testing.md`**
  - The EditMode list gains `LoadWatchTests`.
  - Network tests:
    - `DisconnectsNetworkTests` (port 7809): a machine going silent (hosting, and the host going silent), the host's own connection failing, and the host leaving during a cutscene and during both halves of a load. Three of them load the game scene. "Silent" = its `UnityTransport` switched off, with 2 s timeouts.
    - `OnlineOrdersNetworkTests` gains the mid-delivery and golden-order leavers.

- [ ] **Step 3: The roadmap**
  - Progress: a Phase 3I line. "Next" points to `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`.
  - Task 3.8's five bullets are ticked, each with a short Phase 3I note (Rulings 1–6).
  - Task 3.3's two open boxes are ticked:
    - `OnlinePlayer` and `OnlineScooter` carry everything listed (Phases 3B, 3C, 3E).
    - `RemoteAvatar` and `ScooterLook` cover a remote avatar with no view (Phases 3B, 3C).

- [ ] **Step 4: `EDITOR-TODO.md`** (rewrite it for Phase 3I, and keep what's still open)
  - Import step: the new `LoadWatch`, the changed scripts, and the new tests.
  - A two-editor check:
    - Mid-match, stop Play in editor 1: the clone goes back to the title screen and says "The host left the match.". Again during the opening cutscene with the clone paused, and again on the loading screen.
    - In the golden round, pick up the golden order in the clone, then stop Play in the clone: in editor 1 the golden order is back at its start.
    - Each editor's Console has an "Online: the Game scene was ready here after …" line.
  - Two PCs over Steam: on the slowest PC, start an online match in a build, then read that line in `Player.log` (`%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\Player.log`). Note both numbers.
  - Still open from before, if not done: the Phase 3H, 3G and 3F two-editor checks, the optional "Normal Positions" tidy-up, and the Phase 3D Steam checks.
  - Commit: suggested title "Phase 3I: disconnects and versions".

- [ ] **Step 5: Run the full suite** (in the background; check `ListAgents` first)

Run: `bash tools/run-tests.sh`
Expected: 493 + 20 new = `tests: 513 total, 513 passed`. Task 1 adds 5, Task 2 adds 2, Task 3 adds 7 and Task 4 adds 6.

- [ ] **Step 6: No commit.** Suggested title: "Phase 3I: disconnects and versions".

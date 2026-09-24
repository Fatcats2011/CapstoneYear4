# Phase 3B — Online Player Select Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Machines in an online session share player select. Each machine's player takes the seat the host gives it, and sees every other player's scooter on its podium: company colours, ghost colour, hat and readiness, updating live. Clients follow the host's menus. Starting the match online comes in Phase 3C. Local split-screen stays identical.

**Architecture:**
- The host spawns two kinds of small Netcode objects:
  - **`OnlineMatch`**: the host's game state.
  - **`OnlinePlayer`**, one per machine: its seat, colour, hat and ready.
  - Both are prefabs of their own (not a `NetworkObject` on `PlayerAvatar`), so the player prefabs don't change.
- `OnlineSession` keeps seats (`SeatTable`), spawns these objects and lists them.
- A new game-side component, **`OnlineGame`**, does the rest:
  - It owns `GameAuthority.Role` and the scene flow.
  - It moves this machine's player into its seat.
  - It shows other machines' players as `RemoteAvatar`s: a `PlayerAvatar.prefab` without a view, dressed by `ScooterLook`.
  - It shares this machine's choices.
  - It relays the host's states: ordered RPCs, because the game can switch state twice in a frame.
- Netcode's scene management stays off. While online, `OnlineSceneFlow` stops the match from starting, until Phase 3C.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 · Unity Transport 1.5.0 · Unity Test Framework 1.1.33 · Input System 1.14.0.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- Task 3.2 bullet 2: the lobby screen reuses Player Select. The host gives out seats (a seat decides the company), and players pick their colour and hat.
  - Clarification: this plan does it with the existing player select. The Steam lobby and a Play Online menu stay in 3.2 (Phase 3D).
- Task 3.3 bullet 1: a `OnlinePlayer` with slot, company, colour, hat and ready.
  - Score and the flags byte come with driving (Phase 3C).
  - Ruling 1 below changes where it lives.
- Task 3.3 bullet 5: a remote avatar has no view.
  - Guard the view fields its scripts use.
  - Keep the owner-only scripts off.
  - Put the company look on the avatar.
- Task 3.4 bullet 1: clients follow the host's game state (Ruling 2 changes how).
- Global constraints: online is one player per machine until Task 3.9, and there's no mid-match joining.

## Global Constraints

- Stay on Unity 2022.3 LTS: Netcode for GameObjects **1.x** only.
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`). Online, one player per machine until Task 3.9.
- Local split-screen must behave exactly as before: `LocalMatchSmokeTest` passes after every task. Nothing online runs unless a session is started.
- Online: no mid-match joining (`JoinRules`, Phase 3A). If the host leaves, the session ends (returning to the menu is Task 3.8).
- **Two Unity editors are open on the real project** (the user's editor plus a ParrelSync clone that shares `Assets/`). Never edit an existing scene, prefab or ProjectSettings file.
  - New asset files (Task 2's prefabs and `OnlinePrefabs.asset`) are safe: they're built in the mirror and copied in as new files.
  - `Assets/DefaultNetworkPrefabs.asset` is Netcode's generated list. It's copied from the mirror, and the editors write the same content.
- No git commits: your human partner commits and pushes.
- Every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`, or `bash tools/newmeta.sh <path> folder` for a folder.
  - The assets the mirror builds bring their `.meta` with them.
- Line endings:
  - Files marked `i/crlf` in `git ls-files --eol` must stay CRLF, so edit them with the Edit tool only: `GameManager.cs`, `MainMenu.cs`, `SceneManager.cs`, `PlayerInstantiate.cs`, `PlayerCameraResizer.cs`, `CustomizationSelector.cs`.
  - Never use `sed -i` on scripts.
  - New files may be LF.
- Tests: `bash tools/run-tests.sh [filter]`. It runs in the mirror (`../_doa_test_mirror/CapstoneYear4`) and takes 2–8 minutes, so run it in the background.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
  - Message the other local Claude sessions before long runs (they share the mirror).
- **Play Mode tests:**
  - A test resumes in a reloaded script domain after `yield return new EnterPlayMode()`.
  - Never write a lambda that captures a local of the test method. The closure is allocated when the method starts, so after the reload **even assigning** such a local throws a bare `NullReferenceException` at that line.
  - Use static helper methods with parameters, and recorder classes whose methods are subscribed as method groups.
  - Lambdas that capture nothing (`id => id != 0`) are fine.
- Branch `steam-phase1a` (memory `branch-policy.md`: only the Steam API goes to `main`).

## Rulings (decided while planning)

1. **`OnlinePlayer` is its own network prefab, not a `NetworkObject` on `PlayerAvatar`.**
   - Why:
     - The local player's avatar is nested in `Player - Cinemachine.prefab`. A nested `NetworkObject` gets its own prefab hash and a parent without a `NetworkObject`, which Netcode 1.x can't spawn cleanly.
     - This way local play's prefabs don't change, which matters with two editors open on them.
   - Cost if wrong: one extra object per player, and Phase 3C copies each scooter's pose through it.
2. **Game states travel as ordered `ClientRpc`s, plus a `NetworkVariable` snapshot for players who join later**, not only a `NetworkVariable<GameState>`.
   - Why: the game switches state twice in one frame (`SpawnManager` turns `StartingCutscene` into `MainLoop` at once). A variable only sends its last value, so clients would skip the opening cutscene.
   - Cost if wrong: slightly more traffic than a variable (a few bytes per state change).
3. **A seat is the player's slot on every machine; the host is seat 0.** A machine's player moves to its seat by leaving and rejoining with the same controller.
   - Why: this reuses the tested join code, which sets up layers, cameras, render textures, company and podium, instead of undoing each of those in place.
   - Cost if wrong: the player's colour and hat reset when they move. They join a session from the title screen, before customizing, so nothing is lost there.
4. **`OnlineGame` (the game side), not `OnlineSession`, sets `GameAuthority.Role`.** A session without an `OnlineGame` then stands in for another machine in tests without changing this machine's role. Phase 3A's two `GameAuthority.Role` assertions move to `OnlineGame`'s tests.
   - Cost if wrong: anything that starts a session for the game must add an `OnlineGame` (the Online menu does; the Steam lobby will).
5. **Clients show the host's Options and Credits as the title screen.** These are the host's own menus.
   - Cost if wrong: a client sees the title screen while the host browses options.
6. **Starting a match online is off until Phase 3C.** While online, the ready-up countdown ends with a warning, and nothing loads.
   - Cost if wrong: none now; Phase 3C replaces `OnlineSceneFlow`'s loads.

## Findings (from a throwaway spike in the mirror, 2026-09-24)

- Two sessions (host and client) in one Play Mode process spawn and sync network objects.
  - The host spawns with `Network.SpawnManager.InstantiateAndSpawn(prefab, owner, destroyWithScene: false, isPlayerObject: true)`, which ties each object to that session's `NetworkManager`.
  - The client's copy has `IsOwner` and becomes its `LocalClient.PlayerObject`.
- A `NetworkVariable` the host sets **inside `OnNetworkSpawn`** goes out with the spawn: the client's `OnNetworkSpawn` already sees it, with no change event.
  - Setting one **before** spawning logs a warning: `NetworkVariable is written to, but doesn't know its NetworkBehaviour yet…`.
- `NetworkVariableWritePermission.Owner` values set on the client reached the host in about 0.02 s on 127.0.0.1.
- Two `ClientRpc`s sent in one frame arrived in order. The host is a client too and runs its own `ClientRpc`s, so skip them there with `IsServer`.
- When a client leaves, its player object is destroyed on every machine.
- A `NetworkTransform` subclass whose `OnIsServerAuthoritative()` returns `false` syncs owner to host (for Phase 3C). A 10 m jump converged in about 0.7 s.
- **Scene management on + an unsaved Play Mode scene breaks joining.** Netcode throws `ArgumentOutOfRangeException` in `NetworkSceneManager.GetSceneNameFromPath`, and the client never connects. This plan keeps scene management off.
- Netcode's editor adds every newly imported prefab with a `NetworkObject` to `Assets/DefaultNetworkPrefabs.asset`.
- Batch-mode frames take about 0.2 ms, so wait by time, never by frame count.
- The avatar parts that the view dresses (read from `Player - Cinemachine.prefab`'s serialized fields), relative to the avatar root. `ScooterLook` uses these paths:
  - `PlayerCameraResizer.scooterModel` → `Control/Groundcheck/Basket/SubBasket/ScooterBlockOut_01/Scooter Object/Scooter` (MeshRenderer).
  - `logoDecal[]` → `…/ScooterBlockOut_01/Logo Decal`, `…/Logo Decal 2` (DecalProjector).
  - `CustomizationSelector.ghostModel` → `…/ScooterBlockOut_01/Player Model/Player_Model` (SkinnedMeshRenderer; material 0 = colour, material 1 = horn).
  - `ghostEyelid1/2` → `…/ScooterBlockOut_01/Ghost Model/Eyelid Left`, `Eyelid Right`.
  - `hatModel` → `…/ScooterBlockOut_01/Player Model/Armature.001/Torso/Head/Hat Object`.
  - 6 colours, 8 hats.
- On a view-less avatar these fail, so remote scooters need guards:
  - `PhaseIndicator.Update`: its horn sliders live in the view.
  - `OrderHandler` score and placing updates: `numberHandler`. `ScoreManager.UpdatePlacement` calls them for every handler, and `UpdateScore` runs on every state change.
  - `PlayerCameraResizer.UpdatePlayerObjectLayer` with a null icon camera (from `DrivingIndicators`, on cutscenes).
  - `SkideeSkidoo` unsubscribes with new lambdas, so its game-state handlers outlive a destroyed scooter.

## Review Focus

1. A player from another machine joins while the host's ready-up countdown runs → the countdown stops, as it does for a local joiner. Task 4: `RemotePlayersReadiness_CountsTowardsTheCountdown`.
2. A ready remote player leaves → the count is redone, and if everyone left is ready the countdown starts. Task 4: same test.
3. A second controller is pressed on a machine that's online → it's turned away; one player per machine. Task 5: `ThisMachinesPlayer_MovesToTheSeatTheHostGave_WithTheSameController`.
4. A colour or hat index outside this build's lists arrives (from a hacked or broken peer) → it's ignored with no exception. Task 6: `Pick_OutsideTheList_IsNothing`.
5. The host goes back to the title screen, or into its options → clients show the title screen. Task 6: `ForClient_HostsOwnMenus_ShowAsTheTitleScreen` and `Joining_ThisMachinesPlayer_MovesToItsSeat_AndFollowsTheHost`.

## File map

| File | Task | Responsibility |
|---|---|---|
| `Assets/Scripts/Online/SeatTable.cs` | 1 | Seats 0–3 ↔ Netcode client ids (pure) |
| `Assets/Tests/Editor/SeatTableTests.cs` | 1 | |
| `Assets/Scripts/Player/PlayerRoster.cs`, `PlayerSlot.cs` | 1 | `JoinLocalAt`, `JoinRemoteAt`, `LeaveSlot`; `OwnerClientId` wording |
| `Assets/Tests/Editor/PlayerRosterTests.cs` | 1 | |
| `Assets/Scripts/Online/OnlineSession.cs` | 1, 2, 6 | Seats; registers, spawns and lists the network objects; `RoleChanged`; stops setting `GameAuthority` (6) |
| `Assets/Tests/Editor/OnlineSessionTests.cs` | 1, 2 | |
| `Assets/Scripts/Online/OnlinePlayer.cs`, `OnlineMatch.cs`, `OnlinePrefabs.cs` | 2 | The two network objects; the Resources asset listing what online play spawns |
| `Assets/Prefabs/Online/OnlinePlayer.prefab`, `OnlineMatch.prefab` (+ `.meta`, folder `.meta`) | 2 | Built in the mirror |
| `Assets/Resources/Online/OnlinePrefabs.asset` (+ `.meta`, folder `.meta`) | 2 | Built in the mirror |
| `Assets/DefaultNetworkPrefabs.asset` | 2 | Netcode's list (copied from the mirror) |
| `Assets/Tests/Editor/OnlinePrefabsTests.cs`, `OnlinePlayersNetworkTests.cs` | 2 | EditMode; Play Mode host + client |
| `Assets/Scripts/Management/GameManager.cs` | 3 | `StateApplied` event |
| `Assets/Scripts/Menu/MainMenu.cs` | 3 | Player select's and the title screen's menus follow the game state |
| `Assets/Scripts/Online/OnlineSceneFlow.cs` | 3 | The scene flow while online (loads off until Phase 3C) |
| `Assets/Scripts/Menu/SceneManager.cs` | 3 | The ready-up countdown starts the match through `SceneFlow` |
| `Assets/Tests/Editor/GameManagerTests.cs`, `MainMenuTests.cs`, `OnlineSceneFlowTests.cs`, `SceneFlowTests.cs` | 3 | |
| `Assets/Scripts/Online/ScooterLook.cs`, `RemoteAvatar.cs` | 4 | Dressing a scooter; another machine's player's scooter |
| `Assets/Scripts/Player/PlayerInstantiate.cs` | 4, 5 | Remote players in the roster, their readiness (4); this machine's online seat (5) |
| `Assets/Scripts/Menu/Customization/CustomizationSelector.cs` | 4 | The colours, hats and choices, readable |
| `Assets/Scripts/Player/OrderHandler.cs`, `PlayerCameraResizer.cs`, `SkideeSkidoo.cs` | 4 | Guards for a view-less scooter; handlers that unsubscribe |
| `Assets/Tests/Editor/ScooterLookTests.cs`, `RemoteAvatarTests.cs`, `EventSubscriptionTests.cs`, `TestSupport.cs` | 4 | |
| `Assets/Tests/Editor/OnlineSeatTests.cs` | 5 | Play Mode, menu scene |
| `Assets/Scripts/Online/OnlineGame.cs` | 6 | The game side of a session |
| `Assets/Tests/Editor/OnlineGameTests.cs`, `OnlineGameNetworkTests.cs`, `OnlineSessionNetworkTests.cs` | 6 | |
| `Assets/Scripts/Editor/OnlineTestMenu.cs`, `Assets/Tests/Editor/OnlineTestMenuTests.cs` | 7 | The Online menu plays the game over its session |
| `docs/online.md`, `docs/testing.md`, `docs/player-prefab.md`, roadmap | 7 | |

Test count: 208 now → 219 (Task 1) → 228 (2) → 238 (3) → 244 (4) → 246 (5) → 256 (6) → 257 (7).

---

### Task 1: Seats

**Files:**
- Create: `Assets/Scripts/Online/SeatTable.cs`, `Assets/Tests/Editor/SeatTableTests.cs` (+ `.meta` each)
- Modify: `Assets/Scripts/Player/PlayerRoster.cs`, `Assets/Scripts/Player/PlayerSlot.cs`, `Assets/Scripts/Online/OnlineSession.cs`
- Test: `Assets/Tests/Editor/PlayerRosterTests.cs`, `Assets/Tests/Editor/OnlineSessionTests.cs`

**Interfaces:**
- Consumes: `PlayerRoster`, `PlayerSlot` (Phase 2A); `OnlineSession` (Phase 3A).
- Produces:
  - `SeatTable` with `int Count`, `int Take(ulong clientId)` (the seat, or -1 when full), `int Free(ulong clientId)`, `int SeatOf(ulong clientId)` (-1 = none) and `void Clear()`.
  - `PlayerRoster.JoinLocalAt(PlayerInput input, int slot) → PlayerSlot` (null when the slot is taken or missing, or they're in already).
  - `PlayerRoster.JoinRemoteAt(GameObject player, ulong ownerClientId, int slot) → PlayerSlot`.
  - `PlayerRoster.LeaveSlot(int slot) → bool`.
  - `OnlineSession.SeatOf(ulong clientId) → int`: host only.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/SeatTableTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/SeatTableTests.cs`):

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Online seats: the host sits in seat 0, each joiner in the lowest free seat, 4 at most
    /// </summary>
    public class SeatTableTests
    {
        [Test]
        public void Take_TheHost_GetsSeatZero()
        {
            SeatTable seats = new SeatTable();

            Assert.AreEqual(0, seats.Take(0));
            Assert.AreEqual(1, seats.Count);
        }

        [Test]
        public void Take_Joiners_GetTheLowestFreeSeat_EvenAfterSomeoneLeft()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(7);
            seats.Take(8);
            seats.Free(7);

            Assert.AreEqual(1, seats.Take(9), "the freed seat");
            Assert.AreEqual(3, seats.Take(10), "then the next free one");
            Assert.AreEqual(4, seats.Count);
        }

        [Test]
        public void Take_WhenEverySeatIsTaken_ReturnsMinusOne()
        {
            SeatTable seats = new SeatTable();
            for (ulong player = 0; player < Constants.MAX_PLAYERS; player++)
                seats.Take(player);

            Assert.AreEqual(-1, seats.Take(99));
            Assert.AreEqual(Constants.MAX_PLAYERS, seats.Count);
        }

        [Test]
        public void Take_ASeatedPlayerAgain_KeepsTheirSeat()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(5);

            Assert.AreEqual(1, seats.Take(5));
            Assert.AreEqual(2, seats.Count);
        }

        [Test]
        public void SeatOfAndFree_APlayerWithoutASeat_AreMinusOne()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);

            Assert.AreEqual(-1, seats.SeatOf(3));
            Assert.AreEqual(-1, seats.Free(3));
            Assert.AreEqual(1, seats.Count);
        }

        [Test]
        public void Clear_FreesEverySeat()
        {
            SeatTable seats = new SeatTable();
            seats.Take(0);
            seats.Take(1);

            seats.Clear();

            Assert.AreEqual(0, seats.Count);
            Assert.AreEqual(-1, seats.SeatOf(1));
            Assert.AreEqual(0, seats.Take(4), "seat 0 is free again");
        }
    }
}
```

Add to `Assets/Tests/Editor/PlayerRosterTests.cs`, before the class's closing brace (it already has `using UnityEngine;`):

```csharp
        [Test]
        public void JoinLocalAt_TakesTheGivenSlot_EvenWithLowerOnesFree()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput mine = NewController("Mine");

            PlayerSlot slot = roster.JoinLocalAt(mine, 2);

            Assert.AreEqual(2, slot.Index);
            Assert.IsTrue(slot.IsLocal);
            Assert.AreSame(mine, slot.Input);
            Assert.IsNull(roster[0]);
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void JoinLocalAt_ATakenOrMissingSlot_OrAPlayerAlreadyIn_ReturnsNull()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("First");
            roster.JoinLocalAt(first, 1);

            Assert.IsNull(roster.JoinLocalAt(NewController("Second"), 1), "taken slot");
            Assert.IsNull(roster.JoinLocalAt(first, 3), "already in");
            Assert.IsNull(roster.JoinLocalAt(NewController("Third"), Constants.MAX_PLAYERS), "no such slot");
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void JoinRemoteAt_TakesTheSeatAsAnotherMachinesPlayer()
        {
            PlayerRoster roster = new PlayerRoster();
            GameObject scooter = objects.NewGameObject("Remote");

            PlayerSlot slot = roster.JoinRemoteAt(scooter, 7, 3);

            Assert.AreEqual(3, slot.Index);
            Assert.IsFalse(slot.IsLocal);
            Assert.AreEqual(7UL, slot.OwnerClientId);
            Assert.AreSame(scooter, slot.Player);
            Assert.IsNull(roster.JoinRemoteAt(objects.NewGameObject("Other"), 8, 3), "taken seat");
            Assert.IsNull(roster.JoinRemoteAt(scooter, 7, 2), "already seated");
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void LeaveSlot_FreesIt_Once()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinRemoteAt(objects.NewGameObject("Remote"), 7, 1);

            Assert.IsTrue(roster.LeaveSlot(1));
            Assert.IsNull(roster[1]);
            Assert.AreEqual(0, roster.Count);
            Assert.IsFalse(roster.LeaveSlot(1), "already free");
            Assert.IsFalse(roster.LeaveSlot(-1), "no such slot");
        }
```

Add to `Assets/Tests/Editor/OnlineSessionTests.cs`, after `Approve_PlayersOnTheSameBuild_TakeTheFreeSeats_ThenTheMatchIsFull`:

```csharp
        [Test]
        public void Approve_SeatsPlayersInTheOrderTheyJoin_TheHostFirst()
        {
            OnlineSession session = NewSession();
            Approve(session, NetworkManager.ServerClientId, "1.0.0");
            Approve(session, 5, "1.0.0");
            Approve(session, 9, "1.0.0");

            Assert.AreEqual(0, session.SeatOf(NetworkManager.ServerClientId));
            Assert.AreEqual(1, session.SeatOf(5));
            Assert.AreEqual(2, session.SeatOf(9));
            Assert.AreEqual(-1, session.SeatOf(3), "never joined");
        }
```

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "SeatTableTests|PlayerRosterTests|OnlineSessionTests"`
Expected: `NO RESULTS` with `error CS0246: The type or namespace name 'SeatTable' could not be found`, plus CS1061 for `JoinLocalAt` / `JoinRemoteAt` / `LeaveSlot` / `SeatOf`.

- [ ] **Step 3: Write `SeatTable`**

`Assets/Scripts/Online/SeatTable.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/SeatTable.cs`):

```csharp
/// <summary>
/// Who sits in each of an online match's 4 seats, by Netcode client id. The host takes seat 0 and each joiner the
/// lowest free seat. A seat is that player's slot on every machine: their company, podium, spawn point and place in
/// the results
/// </summary>
public class SeatTable
{
    readonly ulong[] clients = new ulong[Constants.MAX_PLAYERS];
    readonly bool[] taken = new bool[Constants.MAX_PLAYERS];

    /// <summary>How many seats are taken</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Seats a player in the lowest free seat. Returns their seat (the one they have if they're seated already), or -1
    /// when every seat is taken
    /// </summary>
    public int Take(ulong clientId)
    {
        int seat = SeatOf(clientId);
        if (seat >= 0)
            return seat;

        for (int i = 0; i < taken.Length; i++)
        {
            if (!taken[i])
            {
                taken[i] = true;
                clients[i] = clientId;
                Count++;
                return i;
            }
        }
        return -1;
    }

    /// <summary>Frees a player's seat. Returns the seat, or -1 if they had none</summary>
    public int Free(ulong clientId)
    {
        int seat = SeatOf(clientId);
        if (seat >= 0)
        {
            taken[seat] = false;
            Count--;
        }
        return seat;
    }

    /// <summary>A player's seat, or -1 if they have none</summary>
    public int SeatOf(ulong clientId)
    {
        for (int i = 0; i < taken.Length; i++)
        {
            if (taken[i] && clients[i] == clientId)
                return i;
        }
        return -1;
    }

    /// <summary>Frees every seat</summary>
    public void Clear()
    {
        for (int i = 0; i < taken.Length; i++)
            taken[i] = false;
        Count = 0;
    }
}
```

- [ ] **Step 4: Give the roster seats**

In `Assets/Scripts/Player/PlayerRoster.cs`, after `JoinRemote`:

```csharp
    ///<summary>
    /// Online: puts this machine's player in the seat the host gave them. Returns their slot, or null when that slot
    /// is taken or doesn't exist, or they already have one
    ///</summary>
    public PlayerSlot JoinLocalAt(PlayerInput input, int slot)
    {
        if (input == null || !IsFree(slot) || IndexOf(input) >= 0)
            return null;

        return Take(new PlayerSlot(slot, input.gameObject, input, 0));
    }

    ///<summary>
    /// Online: puts another machine's player in their seat. Returns their slot, or null when that slot is taken or
    /// doesn't exist, or they already have one
    ///</summary>
    public PlayerSlot JoinRemoteAt(GameObject player, ulong ownerClientId, int slot)
    {
        if (player == null || !IsFree(slot) || SlotOfPlayer(player) >= 0)
            return null;

        return Take(new PlayerSlot(slot, player, null, ownerClientId));
    }
```

After `Leave(PlayerInput)`:

```csharp
    ///<summary>
    /// Frees a slot, whoever is in it (online: another machine's player left). Returns whether it was taken
    ///</summary>
    public bool LeaveSlot(int slot)
    {
        if (slot < 0 || slot >= slots.Length || slots[slot] == null)
            return false;

        slots[slot] = null;
        Count--;
        return true;
    }
```

Before `FirstFreeSlot()`:

```csharp
    bool IsFree(int slot)
    {
        return slot >= 0 && slot < slots.Length && slots[slot] == null;
    }
```

In `Assets/Scripts/Player/PlayerSlot.cs`, the `OwnerClientId` summary:
- Old: `/// The online client that owns this player (0 when playing offline)`
- New: `/// The online client that owns another machine's player (0 for this machine's players and offline)`

- [ ] **Step 5: Seat players in the session**

In `Assets/Scripts/Online/OnlineSession.cs`:
- Replace `readonly HashSet<ulong> seats = new HashSet<ulong>(); // host: the players it let in, itself included` with `readonly SeatTable seats = new SeatTable(); // host: who sits where, itself included`.
- `PlayersIn` stays `seats.Count`.
- In `Approve`:
  - Old: `if (response.Approved) seats.Add(request.ClientNetworkId); // counted now: …`
  - New: `if (response.Approved) seats.Take(request.ClientNetworkId); // seated now: Netcode lists the player later in the frame` (keep the `else` log).
- In `OnDisconnected`, replace `seats.Remove(clientId)` with `seats.Free(clientId) >= 0`.
- After `EndReason`, add:

```csharp
    /// <summary>
    /// Host: a player's seat, 0-3 (their slot on every machine), or -1 if they have none. The host sits in seat 0
    /// </summary>
    public int SeatOf(ulong clientId)
    {
        return seats.SeatOf(clientId);
    }
```

`seats.Clear()` in `StartHost` and `End` stays as it is.

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "SeatTableTests|PlayerRosterTests|OnlineSessionTests|OnlineSessionNetworkTests"`
Expected: all pass (SeatTable 6, PlayerRoster 4 new, OnlineSession 1 new, and the Phase 3A session tests unchanged).

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 219 total, 219 passed`.

---

### Task 2: Network players and the match

**Files:**
- Create: `Assets/Scripts/Online/OnlinePlayer.cs`, `OnlineMatch.cs`, `OnlinePrefabs.cs` (+ `.meta` each)
- Create (built in the mirror, then copied in):
  - `Assets/Prefabs/Online.meta`
  - `Assets/Prefabs/Online/OnlinePlayer.prefab` (+ `.meta`)
  - `Assets/Prefabs/Online/OnlineMatch.prefab` (+ `.meta`)
  - `Assets/Resources/Online.meta`
  - `Assets/Resources/Online/OnlinePrefabs.asset` (+ `.meta`)
- Modify: `Assets/Scripts/Online/OnlineSession.cs`, `Assets/DefaultNetworkPrefabs.asset` (copied from the mirror)
- Test: `Assets/Tests/Editor/OnlinePrefabsTests.cs`, `Assets/Tests/Editor/OnlinePlayersNetworkTests.cs` (+ `.meta` each), `Assets/Tests/Editor/OnlineSessionTests.cs`

**Interfaces:**
- Consumes: `OnlineSession.SeatOf` (Task 1).
- Produces:
  - **`OnlinePlayer : NetworkBehaviour`:**
    - `int Seat`, `int Colour`, `int Hat`, `bool Ready`.
    - `void Share(int colour, int hat, bool ready)`: owner only; a no-op elsewhere.
    - The host seats it in `OnNetworkSpawn`.
    - It registers with its session.
  - **`OnlineMatch : NetworkBehaviour`:**
    - `GameState State`.
    - `event Action<GameState> StateReceived`: clients only, in order.
    - `void SendState(GameState)`: host only.
  - **`OnlinePrefabs : ScriptableObject`:**
    - `const string RESOURCE = "Online/OnlinePrefabs"` and `static OnlinePrefabs Load()`.
    - `GameObject PlayerPrefab`, `MatchPrefab`, `RemoteAvatarPrefab` (`PlayerAvatar.prefab`), `LocalPlayerPrefab` (`Player - Cinemachine.prefab`).
  - **`OnlineSession` additions:**
    - `IReadOnlyList<OnlinePlayer> Players` and `OnlineMatch Match`.
    - Events `PlayerSpawned(OnlinePlayer)`, `PlayerDespawned(OnlinePlayer)`, `MatchSpawned(OnlineMatch)` and `RoleChanged(NetworkRole)`.
    - `internal` `AddPlayer`, `RemovePlayer`, `SetMatch`, `ClearMatch`.
  - **Host behaviour:**
    - On start it spawns the match, then its own player (seat 0).
    - It spawns a player for each client that connects. Netcode destroys that player when the client leaves.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/OnlinePrefabsTests.cs`:

```csharp
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What online play spawns (Resources/Online/OnlinePrefabs): two network objects that carry data, the scooter shown
    /// for another machine's player, and the local player's prefab, whose player select lists the colours and hats
    /// </summary>
    public class OnlinePrefabsTests
    {
        [Test]
        public void Load_FindsTheOnlinePrefabs()
        {
            Assert.IsNotNull(OnlinePrefabs.Load(), "Resources/" + OnlinePrefabs.RESOURCE);
        }

        [Test]
        public void NetworkPrefabs_AreNetworkObjectsThatCarryData()
        {
            OnlinePrefabs prefabs = OnlinePrefabs.Load();

            AssertNetworkPrefab(prefabs.PlayerPrefab, typeof(OnlinePlayer));
            AssertNetworkPrefab(prefabs.MatchPrefab, typeof(OnlineMatch));
        }

        static void AssertNetworkPrefab(GameObject prefab, System.Type script)
        {
            Assert.IsNotNull(prefab, script.Name);
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            Assert.IsNotNull(networkObject, script.Name + " has a NetworkObject");
            Assert.IsNotNull(prefab.GetComponent(script), script.Name + " script");
            Assert.AreNotEqual(0u, networkObject.PrefabIdHash, script.Name + " has Netcode's prefab id");
            Assert.IsFalse(networkObject.SynchronizeTransform, script.Name + " doesn't move, so its transform isn't sent");
        }

        [Test]
        public void Scooters_AreThePlayerPrefabs()
        {
            OnlinePrefabs prefabs = OnlinePrefabs.Load();

            Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab"), prefabs.RemoteAvatarPrefab);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab"), prefabs.LocalPlayerPrefab);
        }
    }
}
```

`Assets/Tests/Editor/OnlinePlayersNetworkTests.cs`:

```csharp
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Hosts and clients on this computer (127.0.0.1), in one empty Play Mode scene: the host spawns the match and one
    /// OnlinePlayer per machine in its seat, players' choices reach the other machines, the host's states reach clients in
    /// order, and a player who leaves takes their OnlinePlayer with them. Each test enters Play Mode (a few seconds).
    /// No lambda here captures a local: after EnterPlayMode even assigning a captured local throws
    /// </summary>
    public class OnlinePlayersNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7792;
        const float WAIT = 10f;

        /// <summary>
        /// Remembers the states a client heard from the host
        /// </summary>
        class StateRecorder
        {
            public readonly List<GameState> States = new List<GameState>();

            public StateRecorder(OnlineMatch match)
            {
                match.StateReceived += OnState;
            }

            void OnState(GameState state)
            {
                States.Add(state);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
        }

        static OnlinePlayer PlayerInSeat(OnlineSession session, int seat)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.Seat == seat)
                    return player;
            }
            return null;
        }

        static bool Sees(OnlineSession session, int players)
        {
            return session.Match != null && session.Players.Count == players;
        }

        [UnityTest]
        public IEnumerator Hosting_SpawnsTheMatchAndTheHostsPlayer_InSeatZero()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "host started");

            Assert.IsNotNull(host.Match, "the match");
            Assert.AreEqual(1, host.Players.Count, "players");
            Assert.AreEqual(0, host.Players[0].Seat);
            Assert.IsTrue(host.Players[0].IsOwner, "the host's own player");

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Joining_EveryMachineSeesBothPlayers_InTheirSeats()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(Sees(client, 2), "the client sees the match and both players");
            Assert.IsTrue(PlayerInSeat(client, 1).IsOwner, "the client's own player sits in seat 1");
            Assert.IsFalse(PlayerInSeat(client, 0).IsOwner, "seat 0 is the host's");
            Assert.IsTrue(Sees(host, 2), "the host sees both players");
            Assert.IsFalse(PlayerInSeat(host, 1).IsOwner, "on the host, seat 1 is another machine's player");

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayersChoices_ReachTheOtherMachines_OnlyFromTheirOwnMachine()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(client, 2) && Sees(host, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerInSeat(client, 1).Share(2, 5, true);
            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(2, onHost.Colour, "colour");
            Assert.AreEqual(5, onHost.Hat, "hat");
            Assert.IsTrue(onHost.Ready, "ready");

            onHost.Share(0, 0, false); // not the host's player: only its own machine may change it
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(2, PlayerInSeat(client, 1).Colour, "unchanged on its own machine");
            Assert.IsTrue(onHost.Ready, "unchanged on the host");

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostStates_ReachClientsInOrder_EvenTwoInOneFrame()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Match == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            StateRecorder heard = new StateRecorder(client.Match);

            // What the opening cutscene does: the spawn handler switches to the main loop inside the cutscene's own switch
            host.Match.SendState(GameState.StartingCutscene);
            host.Match.SendState(GameState.MainLoop);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.States.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;

            CollectionAssert.AreEqual(new[] { GameState.StartingCutscene, GameState.MainLoop }, heard.States);

            // The latest state, for players who join later: Netcode sends variables at its next tick, after the RPCs
            deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Match.State != GameState.MainLoop && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, client.Match.State, "the latest, for players who join later");

            client.Match.SendState(GameState.Menu); // only the host decides states
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, host.Match.State);

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerLeaving_TheirPlayerGoesEverywhere_AndTheNextJoinerGetsTheirSeat()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession first = OnlineSession.Create("1.0.0");
            first.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(first, 2) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineSession second = OnlineSession.Create("1.0.0");
            second.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(second, 3) && Sees(first, 3)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(PlayerInSeat(second, 2).IsOwner, "the second joiner sits in seat 2");

            first.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Sees(host, 2) && Sees(second, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(PlayerInSeat(host, 1), "gone from the host");
            Assert.IsNull(PlayerInSeat(second, 1), "gone from the other player's machine");

            OnlineSession third = OnlineSession.Create("1.0.0");
            third.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Sees(third, 3) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(PlayerInSeat(third, 1).IsOwner, "the next joiner takes the free seat");

            // One side at a time: closing both ends in one frame makes Windows report the closed port as a socket error
            third.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            second.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
        }
    }
}
```

Add to `Assets/Tests/Editor/OnlineSessionTests.cs`, after `Create_SetsUpNetcodeToApproveJoinsOverUnityTransport`:

```csharp
        [Test]
        public void Create_RegistersThePlayersAndTheMatchWithNetcode()
        {
            OnlineSession session = NewSession();
            OnlinePrefabs prefabs = OnlinePrefabs.Load();

            // Every build must list the same network prefabs, or Netcode drops the joiner before the host hears of them
            Assert.IsTrue(session.Network.NetworkConfig.Prefabs.Contains(prefabs.PlayerPrefab), "players");
            Assert.IsTrue(session.Network.NetworkConfig.Prefabs.Contains(prefabs.MatchPrefab), "the match");
        }
```

`bash tools/newmeta.sh` both new test files.

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "OnlinePrefabsTests|OnlinePlayersNetworkTests|OnlineSessionTests"`
Expected: `NO RESULTS`, `error CS0246` for `OnlinePrefabs`, `OnlinePlayer`, `OnlineMatch`.

- [ ] **Step 3: Write the scripts**

`Assets/Scripts/Online/OnlinePrefabs.cs`:

```csharp
using UnityEngine;

/// <summary>
/// What online play spawns: its two network objects, the scooter shown for another machine's player, and the local
/// player's prefab (its player select lists the colours and hats). The one asset is Resources/Online/OnlinePrefabs
/// </summary>
[CreateAssetMenu(fileName = "OnlinePrefabs", menuName = "Dead on Arrival/Online Prefabs")]
public class OnlinePrefabs : ScriptableObject
{
    /// <summary>Where the asset is, under a Resources folder</summary>
    public const string RESOURCE = "Online/OnlinePrefabs";

    [SerializeField] GameObject playerPrefab;
    [SerializeField] GameObject matchPrefab;
    [SerializeField] GameObject remoteAvatarPrefab;
    [SerializeField] GameObject localPlayerPrefab;

    /// <summary>A OnlinePlayer: the host spawns one per machine</summary>
    public GameObject PlayerPrefab { get { return playerPrefab; } }

    /// <summary>The OnlineMatch: the host spawns one per session</summary>
    public GameObject MatchPrefab { get { return matchPrefab; } }

    /// <summary>PlayerAvatar.prefab: another machine's player's scooter</summary>
    public GameObject RemoteAvatarPrefab { get { return remoteAvatarPrefab; } }

    /// <summary>Player - Cinemachine.prefab: a player on this machine</summary>
    public GameObject LocalPlayerPrefab { get { return localPlayerPrefab; } }

    /// <summary>The asset (null if it's missing)</summary>
    public static OnlinePrefabs Load()
    {
        return Resources.Load<OnlinePrefabs>(RESOURCE);
    }
}
```

`Assets/Scripts/Online/OnlinePlayer.cs`:

```csharp
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One player in an online match, on every machine: their seat, which the host gives, and what they picked in player
/// select (ghost colour, hat, ready), which only their own machine may change. The host spawns one per machine.
/// OnlineGame shows another machine's player as a scooter, and keeps this machine's player's choices up to date.
/// See docs/online.md
/// </summary>
public class OnlinePlayer : NetworkBehaviour
{
    readonly NetworkVariable<int> seat = new NetworkVariable<int>(-1);
    readonly NetworkVariable<int> colour = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> hat = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    OnlineSession session;

    /// <summary>Their seat, 0-3: their slot on every machine (-1 until the host gives one)</summary>
    public int Seat { get { return seat.Value; } }

    /// <summary>Their ghost colour: an index into player select's colours</summary>
    public int Colour { get { return colour.Value; } }

    /// <summary>Their hat: an index into player select's hats</summary>
    public int Hat { get { return hat.Value; } }

    /// <summary>Whether they're ready in player select</summary>
    public bool Ready { get { return ready.Value; } }

    public override void OnNetworkSpawn()
    {
        session = NetworkManager.GetComponent<OnlineSession>();

        // The host seats the player as it spawns them: a value set here goes out with the spawn itself
        if (IsServer && session != null)
            seat.Value = session.SeatOf(OwnerClientId);

        DontDestroyOnLoad(gameObject);

        if (session != null)
            session.AddPlayer(this);
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
            session.RemovePlayer(this);
    }

    /// <summary>
    /// This machine's player: shares what they picked with every machine. Does nothing for another machine's player
    /// </summary>
    public void Share(int chosenColour, int chosenHat, bool isReady)
    {
        if (!IsOwner)
            return;

        if (colour.Value != chosenColour)
            colour.Value = chosenColour;
        if (hat.Value != chosenHat)
            hat.Value = chosenHat;
        if (ready.Value != isReady)
            ready.Value = isReady;
    }
}
```

`Assets/Scripts/Online/OnlineMatch.cs`:

```csharp
using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The host's game state, on every machine. The host sends each state it switches to, in order, and keeps the latest
/// for players who join later. States go one by one because the game sometimes switches twice in a frame (the opening
/// cutscene hands over to the main loop at once), and a shared value would only send the second
/// </summary>
public class OnlineMatch : NetworkBehaviour
{
    readonly NetworkVariable<GameState> state = new NetworkVariable<GameState>(GameState.Default);

    OnlineSession session;

    /// <summary>The host's latest state (Default until it sends one)</summary>
    public GameState State { get { return state.Value; } }

    /// <summary>Clients: each state the host switches to, in order</summary>
    public event Action<GameState> StateReceived;

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

    [ClientRpc]
    void StateClientRpc(GameState newState)
    {
        if (IsServer)
            return; // the host is a client too, and switched already

        StateReceived?.Invoke(newState);
    }
}
```

`bash tools/newmeta.sh` each of the three.

- [ ] **Step 4: Spawn and list them in the session**

In `Assets/Scripts/Online/OnlineSession.cs`:

Fields, after `bool leaving;`:

```csharp
    OnlinePrefabs prefabs; // what the host spawns
    readonly List<OnlinePlayer> players = new List<OnlinePlayer>();
```

Properties and events, after `PlayersIn`:

```csharp
    /// <summary>Every machine's player in this session, as this machine has them (the host spawns them)</summary>
    public IReadOnlyList<OnlinePlayer> Players { get { return players; } }

    /// <summary>The host's game state in this session (null until the host spawns it)</summary>
    public OnlineMatch Match { get; private set; }

    /// <summary>A player's OnlinePlayer arrived on this machine (this machine's own included)</summary>
    public event Action<OnlinePlayer> PlayerSpawned;

    /// <summary>A player's OnlinePlayer went: they left, or the session ended</summary>
    public event Action<OnlinePlayer> PlayerDespawned;

    /// <summary>The host's OnlineMatch arrived on this machine</summary>
    public event Action<OnlineMatch> MatchSpawned;

    /// <summary>The session's part changed (Offline, Host, Client)</summary>
    public event Action<NetworkRole> RoleChanged;
```

In `Create`, before `session.Network = network;`:

```csharp
        // Every build lists the same network prefabs, or Netcode drops a joiner before the host hears of them
        session.prefabs = OnlinePrefabs.Load();
        network.AddNetworkPrefab(session.prefabs.PlayerPrefab);
        network.AddNetworkPrefab(session.prefabs.MatchPrefab);
```

In `StartHost`, after `Debug.Log("Online: hosting version " + Version);`:

```csharp
        Spawn(prefabs.MatchPrefab, NetworkManager.ServerClientId, false);
        Spawn(prefabs.PlayerPrefab, NetworkManager.ServerClientId, true); // seat 0
```

In `OnConnected`, the host branch becomes:

```csharp
        if (Network.IsServer)
        {
            if (clientId != NetworkManager.ServerClientId)
            {
                Debug.Log("Online: player " + clientId + " joined (" + seats.Count + " in)");
                Spawn(prefabs.PlayerPrefab, clientId, true);
            }
            return;
        }
```

`SetRole` becomes:

```csharp
    void SetRole(NetworkRole role)
    {
        bool changed = role != Role;
        Role = role;
        GameAuthority.Role = role;

        if (changed)
            RoleChanged?.Invoke(role);
    }
```

New members, before `SetRole`:

```csharp
    // Host: spawns a network object on every machine (tied to this session's Netcode, which matters when several run in
    // one process, as in tests). Netcode destroys a player's object when that player leaves
    void Spawn(GameObject prefab, ulong owner, bool isPlayer)
    {
        Network.SpawnManager.InstantiateAndSpawn(prefab.GetComponent<NetworkObject>(), owner, false, isPlayer);
    }

    // OnlinePlayer and OnlineMatch report here as they arrive and go
    internal void AddPlayer(OnlinePlayer player)
    {
        players.Add(player);
        PlayerSpawned?.Invoke(player);
    }

    internal void RemovePlayer(OnlinePlayer player)
    {
        if (players.Remove(player))
            PlayerDespawned?.Invoke(player);
    }

    internal void SetMatch(OnlineMatch match)
    {
        Match = match;
        MatchSpawned?.Invoke(match);
    }

    internal void ClearMatch(OnlineMatch match)
    {
        if (Match == match)
            Match = null;
    }
```

(`using System.Collections.Generic;` is already there for the list.)

- [ ] **Step 5: Build the prefabs in the mirror**

1. Sync the mirror and compile the new scripts there: `bash tools/run-tests.sh SeatTableTests`. Expected: 6/6 pass.
2. Write the one-time builder into the **mirror only**, as `../_doa_test_mirror/CapstoneYear4/Assets/Scripts/Editor/OnlinePrefabBuilder.cs`:

```csharp
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

// ONE-TIME BUILDER (Phase 3B, Task 2): run in the test mirror with -executeMethod. Never add it to the project
public static class OnlinePrefabBuilder
{
    public static void Build()
    {
        Folder("Assets/Prefabs", "Online");
        Folder("Assets/Resources", "Online");
        GameObject player = NetworkPrefab<OnlinePlayer>("OnlinePlayer", "Assets/Prefabs/Online/OnlinePlayer.prefab");
        GameObject match = NetworkPrefab<OnlineMatch>("OnlineMatch", "Assets/Prefabs/Online/OnlineMatch.prefab");

        OnlinePrefabs prefabs = ScriptableObject.CreateInstance<OnlinePrefabs>();
        AssetDatabase.CreateAsset(prefabs, "Assets/Resources/Online/OnlinePrefabs.asset");
        SerializedObject fields = new SerializedObject(prefabs);
        fields.FindProperty("playerPrefab").objectReferenceValue = player;
        fields.FindProperty("matchPrefab").objectReferenceValue = match;
        fields.FindProperty("remoteAvatarPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab");
        fields.FindProperty("localPlayerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab");
        fields.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();

        Debug.Log("ONLINE PREFABS BUILT: player " + player.GetComponent<NetworkObject>().PrefabIdHash + ", match " + match.GetComponent<NetworkObject>().PrefabIdHash);
    }

    static GameObject NetworkPrefab<T>(string name, string path) where T : NetworkBehaviour
    {
        GameObject root = new GameObject(name);
        NetworkObject networkObject = root.AddComponent<NetworkObject>();
        networkObject.SynchronizeTransform = false; // data only: nothing to move
        networkObject.AutoObjectParentSync = false;
        root.AddComponent<T>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}
```

3. Run it (about a minute):

```bash
"C:/Program Files/Unity/Hub/Editor/2022.3.62f3/Editor/Unity.exe" -batchmode -quit -projectPath "$(cygpath -w ../_doa_test_mirror/CapstoneYear4)" -executeMethod OnlinePrefabBuilder.Build -logFile "$(cygpath -w Logs/online-prefabs.log)"; grep "ONLINE PREFABS BUILT\|error" Logs/online-prefabs.log
```

Expected: `ONLINE PREFABS BUILT: player <non-zero>, match <non-zero>` and no errors.

4. Copy the results into the project (all new files, except Netcode's list):

```bash
M=../_doa_test_mirror/CapstoneYear4/Assets
mkdir -p Assets/Prefabs/Online Assets/Resources/Online
cp "$M/Prefabs/Online.meta" Assets/Prefabs/Online.meta
cp "$M/Prefabs/Online/OnlinePlayer.prefab" "$M/Prefabs/Online/OnlinePlayer.prefab.meta" "$M/Prefabs/Online/OnlineMatch.prefab" "$M/Prefabs/Online/OnlineMatch.prefab.meta" Assets/Prefabs/Online/
cp "$M/Resources/Online.meta" Assets/Resources/Online.meta
cp "$M/Resources/Online/OnlinePrefabs.asset" "$M/Resources/Online/OnlinePrefabs.asset.meta" Assets/Resources/Online/
cp "$M/DefaultNetworkPrefabs.asset" Assets/DefaultNetworkPrefabs.asset
rm "$M/Scripts/Editor/OnlinePrefabBuilder.cs" "$M/Scripts/Editor/OnlinePrefabBuilder.cs.meta"
git status --short Assets/Prefabs Assets/Resources Assets/DefaultNetworkPrefabs.asset
```

Expected:
- New: `Assets/Prefabs/Online.meta`, `Assets/Prefabs/Online/` (4 files), `Assets/Resources/Online.meta`, `Assets/Resources/Online/` (2 files).
- Modified: `Assets/DefaultNetworkPrefabs.asset`, which now lists the two prefabs.
- The builder is never in the project.

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "OnlinePrefabsTests|OnlinePlayersNetworkTests|OnlineSessionTests|OnlineSessionNetworkTests"`
Expected:
- The new tests pass: OnlinePrefabs 3, OnlinePlayersNetwork 5, OnlineSession 1.
- The Phase 3A session tests still pass. Their hosts now spawn the match and players too.

- [ ] **Step 7: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 228 total, 228 passed`.

---

### Task 3: Following the host (game states, menus, scene flow)

**Files:**
- Create: `Assets/Scripts/Online/OnlineSceneFlow.cs`, `Assets/Tests/Editor/GameManagerTests.cs`, `Assets/Tests/Editor/MainMenuTests.cs`, `Assets/Tests/Editor/OnlineSceneFlowTests.cs` (+ `.meta` each)
- Modify: `Assets/Scripts/Management/GameManager.cs` (i/crlf), `Assets/Scripts/Menu/MainMenu.cs` (i/crlf), `Assets/Scripts/Menu/SceneManager.cs` (i/crlf)
- Test: `Assets/Tests/Editor/SceneFlowTests.cs`

**Interfaces:**
- Consumes: `ISceneFlow` and `SceneFlow` (Phase 2B), and `FakeSceneFlow` (in `SceneFlowTests.cs`, same test assembly).
- Produces:
  - `GameManager.StateApplied` (`event Action<GameState>`): raised for every applied state, right after `MainState` changes and before that state's own `OnSwap*` event.
  - **`MainMenu`:**
    - Opens player select's canvas on `OnSwapPlayerSelect`.
    - Closes the player select, options and credits canvases on `OnSwapMenu`.
    - `SwapToPlayerSelect` and `SwapToMainMenu` only request the state.
  - **`OnlineSceneFlow : ISceneFlow`**, built as `new OnlineSceneFlow(ISceneFlow local)`:
    - `LoadGameScene` and `LoadFinalOrderScene` log `OnlineSceneFlow.NOT_YET` as a warning and load nothing.
    - `ReturnToMenu` and `OnReturnToMenu` go to `local`.
    - `WaitingForConfirm` is false, and `ConfirmLoad` does nothing.
  - `SceneManager`: when the ready-up countdown ends, it calls `SceneFlow.Current.LoadGameScene()`.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/GameManagerTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// StateApplied tells every state the game switches to, in the order it happens, even a state switched to from
    /// inside another state's event. Online, the host sends each one to the clients
    /// </summary>
    public class GameManagerTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<GameState> applied = new List<GameState>();
        GameManager game;

        [SetUp]
        public void SetUp()
        {
            game = objects.Add<GameManager>();
            game.StateApplied += applied.Add;
        }

        [TearDown]
        public void TearDown()
        {
            applied.Clear();
            objects.DestroyAll();
        }

        [Test]
        public void ApplyGameState_TellsStateAppliedBeforeTheStatesOwnEvent()
        {
            bool toldFirst = false;
            game.OnSwapPlayerSelect += () => toldFirst = applied.Contains(GameState.PlayerSelect);

            game.ApplyGameState(GameState.PlayerSelect);

            Assert.IsTrue(toldFirst);
            Assert.AreEqual(GameState.PlayerSelect, game.MainState);
        }

        [Test]
        public void AStateSwitchedToInsideAnother_IsToldAfterIt()
        {
            // What the opening cutscene does: the spawn handler switches to the main loop inside the cutscene's switch
            game.OnSwapStartingCutscene += () => game.SetGameState(GameState.MainLoop);

            game.SetGameState(GameState.StartingCutscene);

            CollectionAssert.AreEqual(new[] { GameState.StartingCutscene, GameState.MainLoop }, applied);
            Assert.AreEqual(GameState.MainLoop, game.MainState);
        }

        [Test]
        public void Tutorial_IsTold_ThoughOnSwapAnythingSkipsIt()
        {
            game.ApplyGameState(GameState.Tutorial);

            CollectionAssert.AreEqual(new[] { GameState.Tutorial }, applied);
        }
    }
}
```

`Assets/Tests/Editor/MainMenuTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The title screen's menus follow the game state, whoever switched it: pressing Play here, or (online) the host
    /// </summary>
    public class MainMenuTests
    {
        readonly TestObjects objects = new TestObjects();
        GameManager game;
        MainMenu menu;
        Canvas playerSelect, options, credits;

        [SetUp]
        public void SetUp()
        {
            game = objects.Add<GameManager>();
            Reflect.SetSingleton(game);
            menu = objects.Add<MainMenu>();
            playerSelect = objects.Add<Canvas>();
            options = objects.Add<Canvas>();
            credits = objects.Add<Canvas>();
            Reflect.SetField(menu, "PlayerSelectCanvas", playerSelect);
            Reflect.SetField(menu, "OptionsCanvas", options);
            Reflect.SetField(menu, "CreditsCanvas", credits);
            playerSelect.enabled = false;
            menu.OnEnable();
        }

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<GameManager>(null);
            objects.DestroyAll();
        }

        [Test]
        public void PlayerSelect_OpensItsMenu_ThoughNobodyPressedPlayHere()
        {
            game.ApplyGameState(GameState.PlayerSelect); // an online client following the host

            Assert.IsTrue(playerSelect.enabled);
        }

        [Test]
        public void TitleScreen_ClosesPlayerSelectOptionsAndCredits()
        {
            playerSelect.enabled = options.enabled = credits.enabled = true;

            game.ApplyGameState(GameState.Menu);

            Assert.IsFalse(playerSelect.enabled, "player select");
            Assert.IsFalse(options.enabled, "options");
            Assert.IsFalse(credits.enabled, "credits");
        }

        [Test]
        public void EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            Reflect.Invoke(menu, "OnDisable");

            Assert.AreEqual(0, Reflect.HandlerCount(game, "OnSwapPlayerSelect", menu));
            Assert.AreEqual(0, Reflect.HandlerCount(game, "OnSwapMenu", menu));
        }
    }
}
```

`Assets/Tests/Editor/OnlineSceneFlowTests.cs`:

```csharp
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The scene flow while online: starting a match online waits for Phase 3C (it says so and loads nothing); going
    /// back to the menu uses the local loader; the loading screen never waits for A
    /// </summary>
    public class OnlineSceneFlowTests
    {
        [Test]
        public void Loads_WaitForPhase3C_AndSaySo()
        {
            FakeSceneFlow local = new FakeSceneFlow();
            OnlineSceneFlow flow = new OnlineSceneFlow(local);
            LogAssert.Expect(LogType.Warning, OnlineSceneFlow.NOT_YET);
            LogAssert.Expect(LogType.Warning, OnlineSceneFlow.NOT_YET);

            flow.LoadGameScene();
            flow.LoadFinalOrderScene();

            Assert.AreEqual(0, local.GameLoads + local.FinalOrderLoads, "nothing loaded");
        }

        [Test]
        public void ReturnToMenu_UsesTheLocalLoader_AndItsListeners()
        {
            FakeSceneFlow local = new FakeSceneFlow();
            OnlineSceneFlow flow = new OnlineSceneFlow(local);
            int heard = 0;
            flow.OnReturnToMenu += () => heard++;

            flow.ReturnToMenu();

            Assert.AreEqual(1, local.MenuReturns);
            Assert.AreEqual(1, heard, "listeners of the online flow hear the local loader");
        }

        [Test]
        public void TheLoadingScreen_NeverWaitsForA()
        {
            FakeSceneFlow local = new FakeSceneFlow { WaitingForConfirm = true };
            OnlineSceneFlow flow = new OnlineSceneFlow(local);

            Assert.IsFalse(flow.WaitingForConfirm);
            flow.ConfirmLoad();
            Assert.AreEqual(0, local.Confirms);
        }
    }
}
```

Add to `Assets/Tests/Editor/SceneFlowTests.cs`, after `LoadingScreen_EveryoneConfirmed_ConfirmsThroughTheSceneFlow`:

```csharp
        [Test]
        public void ReadyUpCountdown_StartsTheMatchThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            SceneManager loader = objects.Add<SceneManager>();

            Reflect.Invoke(loader, "StartMatch"); // what OnReadiedUp calls when the countdown ends

            Assert.AreEqual(1, flow.GameLoads);
        }
```

`bash tools/newmeta.sh` each new test file.

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "GameManagerTests|MainMenuTests|OnlineSceneFlowTests|SceneFlowTests"`
Expected: `NO RESULTS` with `error CS1061` (`StateApplied`) and `CS0246` (`OnlineSceneFlow`).

- [ ] **Step 3: `GameManager.StateApplied`**

In `Assets/Scripts/Management/GameManager.cs` (Edit tool), after `public event Action OnSwapAnything;`:

```csharp

    /// <summary>
    /// Raised for every state switched to, before that state's own event, so a state switched to from inside another
    /// state's event is told after it. Online, the host sends each one to the clients
    /// </summary>
    public event Action<GameState> StateApplied;
```

In `ApplyGameState`, after `mainState = state;`:

```csharp
        StateApplied?.Invoke(state);
```

- [ ] **Step 4: The title screen's menus follow the state**

In `Assets/Scripts/Menu/MainMenu.cs` (Edit tool):

```csharp
    public void OnEnable()
    {
        gameManager = GameManager.Instance;

        // The menus follow the game state, whoever switched it: online, a client follows the host
        if (gameManager != null)
        {
            gameManager.OnSwapPlayerSelect += ShowPlayerSelect;
            gameManager.OnSwapMenu += ShowTitleScreen;
        }
    }

    void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnSwapPlayerSelect -= ShowPlayerSelect;
            gameManager.OnSwapMenu -= ShowTitleScreen;
        }
    }
```

(replacing the old `OnEnable`), and:

```csharp
    public void SwapToPlayerSelect()
    {
        GameManager.Instance.SetGameState(GameState.PlayerSelect);
    }
```

```csharp
    public void SwapToMainMenu()
    {
        GameManager.Instance.SetGameState(GameState.Menu);
    }

    void ShowPlayerSelect()
    {
        PlayerSelectCanvas.enabled = true;
    }

    void ShowTitleScreen()
    {
        PlayerSelectCanvas.enabled = false;
        OptionsCanvas.enabled = false;
        CreditsCanvas.enabled = false;
    }
```

(replacing the old bodies, which set the canvases before requesting the state).

- [ ] **Step 5: `OnlineSceneFlow`, and the countdown starting the match through `SceneFlow`**

`Assets/Scripts/Online/OnlineSceneFlow.cs`:

```csharp
using System;
using UnityEngine;

/// <summary>
/// The scene flow while online (OnlineGame sets it as SceneFlow.Current). Starting a match online, with every machine
/// loading the host's scenes, comes with roadmap Task 3.4 (Phase 3C): until then the ready-up countdown ends with a
/// warning and nothing loads. Going back to the menu uses the local loader
/// </summary>
public class OnlineSceneFlow : ISceneFlow
{
    /// <summary>Logged instead of loading</summary>
    public const string NOT_YET = "Online: starting an online match isn't in yet (roadmap Task 3.4). Leave the session to play a local match.";

    readonly ISceneFlow local;

    /// <param name="localFlow">The local loader, for going back to the menu</param>
    public OnlineSceneFlow(ISceneFlow localFlow)
    {
        local = localFlow;
    }

    public void LoadGameScene()
    {
        Debug.LogWarning(NOT_YET);
    }

    public void LoadFinalOrderScene()
    {
        Debug.LogWarning(NOT_YET);
    }

    public void ReturnToMenu()
    {
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
}
```

In `Assets/Scripts/Menu/SceneManager.cs` (Edit tool):
- In `Start`, change `playerInstantiate.OnReadiedUp += LoadGameScene;` to `playerInstantiate.OnReadiedUp += StartMatch;`.
- In `OnDisable`, change `playerInstantiate.OnReadiedUp -= LoadGameScene;` to `playerInstantiate.OnReadiedUp -= StartMatch;`.
- Add, before `LoadMenuScene`:

```csharp
    ///<summary>
    /// Everyone is ready in player select: the match starts through the scene flow (this loader offline; online, the
    /// online flow)
    ///</summary>
    private void StartMatch()
    {
        SceneFlow.Current.LoadGameScene();
    }
```

- [ ] **Step 6: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "GameManagerTests|MainMenuTests|OnlineSceneFlowTests|SceneFlowTests|EventSubscriptionTests"`
Expected: all pass (the new tests are GameManager 3, MainMenu 3, OnlineSceneFlow 3 and SceneFlow 1).

- [ ] **Step 7: Whole suite, which includes the local smoke test**

Run: `bash tools/run-tests.sh`
Expected: `tests: 238 total, 238 passed`. `LocalMatchSmokeTest` goes through Play → player select → back → countdown exactly as before.

---

### Task 4: Another machine's scooter

**Files:**
- Create: `Assets/Scripts/Online/ScooterLook.cs`, `Assets/Scripts/Online/RemoteAvatar.cs`, `Assets/Tests/Editor/ScooterLookTests.cs`, `Assets/Tests/Editor/RemoteAvatarTests.cs` (+ `.meta` each)
- Modify:
  - `Assets/Scripts/Player/PlayerInstantiate.cs` (i/crlf)
  - `Assets/Scripts/Menu/Customization/CustomizationSelector.cs` (i/crlf)
  - `Assets/Scripts/Player/OrderHandler.cs`
  - `Assets/Scripts/Player/PlayerCameraResizer.cs` (i/crlf)
  - `Assets/Scripts/Player/SkideeSkidoo.cs`
- Test: `Assets/Tests/Editor/EventSubscriptionTests.cs`, `Assets/Tests/Editor/TestSupport.cs` (`Reflect.GetField`)

**Interfaces:**
- Consumes: `OnlinePrefabs` (Task 2); `PlayerRoster.JoinRemoteAt` / `LeaveSlot` (Task 1).
- Produces:
  - **`ScooterLook`** (static, runtime only):
    - Path constants `MODEL`, `SCOOTER_BODY`, `LOGO`, `LOGO_2`, `GHOST`, `EYELID_LEFT`, `EYELID_RIGHT`, `HAT`.
    - `ShowCompany(GameObject avatar, CompanyInformation)`, `ShowColour(GameObject, PlayerColorInformationSO)`, `ShowHat(GameObject, PlayerHatInformationSO)`.
  - **`RemoteAvatar : MonoBehaviour`:**
    - `static RemoteAvatar Create(GameObject avatarPrefab, Transform parent)`.
    - `BallDriving Driving`.
    - `ShowCompany`, `ShowColour`, `ShowHat`.
  - **`PlayerInstantiate`:**
    - `CompanyInformation CompanyForSlot(int)`.
    - `PlayerSlot AddRemotePlayer(GameObject avatar, int seat, ulong ownerClientId)`.
    - `void RemoveRemotePlayer(int seat)`.
    - `bool IsReady(int slot)` and `void SetRemoteReady(int slot, bool ready)`.
  - **`CustomizationSelector`:** `IReadOnlyList<PlayerColorInformationSO> Colours`, `IReadOnlyList<PlayerHatInformationSO> Hats`, `int ColourIndex`, `int HatIndex`.
  - `Reflect.GetField(object target, string name) → object` (tests).

- [ ] **Step 1: Write the failing tests**

Add to `Reflect` in `Assets/Tests/Editor/TestSupport.cs`, after `SetField`:

```csharp
        public static object GetField(object target, string name)
        {
            return FindField(target.GetType(), name).GetValue(target);
        }
```

`Assets/Tests/Editor/ScooterLookTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DoA.Tests
{
    /// <summary>
    /// ScooterLook dresses another machine's scooter by path. The paths must be the very parts the local player's view
    /// dresses (PlayerCameraResizer, CustomizationSelector), so renaming or moving one fails here until ScooterLook follows
    /// </summary>
    public class ScooterLookTests
    {
        const string VIEW = "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab";
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        static Object Link(Component owner, string field, int element = -1)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, owner.GetType().Name + "." + field);
            return element < 0 ? property.objectReferenceValue : property.GetArrayElementAtIndex(element).objectReferenceValue;
        }

        static Transform Part(Transform avatar, string path)
        {
            Transform part = avatar.Find(path);
            Assert.IsNotNull(part, path + " is missing from the scooter");
            return part;
        }

        [Test]
        public void Paths_AreThePartsTheViewDresses()
        {
            GameObject view = AssetDatabase.LoadAssetAtPath<GameObject>(VIEW);
            Transform avatar = view.transform.GetChild(0);
            PlayerCameraResizer resizer = view.GetComponent<PlayerCameraResizer>();
            CustomizationSelector customization = view.GetComponentInChildren<CustomizationSelector>(true);

            Assert.AreSame(Part(avatar, ScooterLook.SCOOTER_BODY).GetComponent<MeshRenderer>(), Link(resizer, "scooterModel"), "company colours");
            Assert.AreSame(Part(avatar, ScooterLook.LOGO).GetComponent<DecalProjector>(), Link(resizer, "logoDecal", 0), "logo");
            Assert.AreSame(Part(avatar, ScooterLook.LOGO_2).GetComponent<DecalProjector>(), Link(resizer, "logoDecal", 1), "second logo");
            Assert.AreSame(Part(avatar, ScooterLook.GHOST).GetComponent<SkinnedMeshRenderer>(), Link(customization, "ghostModel"), "ghost");
            Assert.AreSame(Part(avatar, ScooterLook.EYELID_LEFT).GetComponent<MeshRenderer>(), Link(customization, "ghostEyelid1"), "left eyelid");
            Assert.AreSame(Part(avatar, ScooterLook.EYELID_RIGHT).GetComponent<MeshRenderer>(), Link(customization, "ghostEyelid2"), "right eyelid");
            Assert.AreSame(Part(avatar, ScooterLook.HAT).GetComponent<MeshRenderer>(), Link(customization, "hatModel"), "hat");
        }

        [Test]
        public void Paths_AreOnTheBareScooterToo()
        {
            Transform avatar = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR).transform;

            foreach (string path in new[] { ScooterLook.SCOOTER_BODY, ScooterLook.LOGO, ScooterLook.LOGO_2, ScooterLook.GHOST, ScooterLook.EYELID_LEFT, ScooterLook.EYELID_RIGHT, ScooterLook.HAT })
                Part(avatar, path);
        }

        [Test]
        public void PlayerSelect_ListsColoursAndHats()
        {
            CustomizationSelector customization = AssetDatabase.LoadAssetAtPath<GameObject>(VIEW).GetComponentInChildren<CustomizationSelector>(true);

            Assert.AreEqual(6, customization.Colours.Count, "colours");
            Assert.AreEqual(8, customization.Hats.Count, "hats");
            Assert.IsTrue(customization.Hats.Any(h => h.displayHat), "at least one hat shows");
        }
    }
}
```

`Assets/Tests/Editor/RemoteAvatarTests.cs`:

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Another machine's player on this one, in the real menu scene: their scooter without a view takes their seat,
    /// dressed as them; their readiness counts towards the countdown; and they leave. Enters Play Mode (about 10 s each)
    /// </summary>
    public class RemoteAvatarTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static bool CountingDown(PlayerInstantiate players)
        {
            return Reflect.GetField(players, "readyUpCountdown") != null;
        }

        static PlayerHatInformationSO AShowingHat(CustomizationSelector customization)
        {
            foreach (PlayerHatInformationSO hat in customization.Hats)
            {
                if (hat.displayHat)
                    return hat;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator RemotePlayer_TakesTheirSeat_DressedAsThem_WithoutErrors()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AtTitleScreen(), "the title screen");

            PlayerInstantiate players = PlayerInstantiate.Instance;
            OnlinePrefabs prefabs = OnlinePrefabs.Load();
            RemoteAvatar remote = RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null);
            PlayerSlot slot = players.AddRemotePlayer(remote.gameObject, 1, 5);

            Assert.IsNotNull(slot, "seated");
            Assert.AreEqual(1, slot.Index);
            Assert.IsFalse(slot.IsLocal);
            Assert.AreEqual(5UL, slot.OwnerClientId);
            Assert.AreEqual("P2", remote.name, "named after the seat, like a local player");
            Assert.AreEqual(2, remote.Driving.playerIndex);
            Assert.AreEqual(11, remote.Driving.Sphere.layer, "player 2's ball layer");
            Assert.IsFalse(remote.Driving.enabled, "no controls here");
            Assert.IsTrue(remote.Driving.Sphere.GetComponent<Rigidbody>().isKinematic, "no physics here");
            GameObject[] podiums = (GameObject[])Reflect.GetField(players, "menuSpawnPositions");
            Assert.Less(Vector3.Distance(podiums[1].transform.position, remote.Driving.transform.position), 0.01f, "on seat 2's podium");

            CompanyInformation company = players.CompanyForSlot(1);
            Assert.AreSame(company, slot.Company);
            Assert.AreSame(company.scooterColorMaterial, remote.transform.Find(ScooterLook.SCOOTER_BODY).GetComponent<MeshRenderer>().sharedMaterial, "company colours");
            Assert.AreSame(company.scooterDecalMaterial, remote.transform.Find(ScooterLook.LOGO).GetComponent<DecalProjector>().material, "company logo");

            CustomizationSelector customization = prefabs.LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);
            PlayerColorInformationSO colour = customization.Colours[2];
            remote.ShowColour(colour);
            Assert.AreSame(colour.colorMaterial, remote.transform.Find(ScooterLook.GHOST).GetComponent<SkinnedMeshRenderer>().sharedMaterials[0], "ghost colour");
            Assert.AreSame(colour.colorMaterial, remote.transform.Find(ScooterLook.EYELID_LEFT).GetComponent<MeshRenderer>().sharedMaterial, "eyelids");
            PlayerHatInformationSO hat = AShowingHat(customization);
            remote.ShowHat(hat);
            Transform hatObject = remote.transform.Find(ScooterLook.HAT);
            Assert.IsTrue(hatObject.gameObject.activeSelf, "hat on");
            Assert.AreSame(hat.hatMesh, hatObject.GetComponent<MeshFilter>().sharedMesh, "the hat");

            // Player select and back, driving indicators and placings refreshed: nothing reaches for a view it hasn't got
            GameManager.Instance.ApplyGameState(GameState.PlayerSelect);
            players.PlayerUpdateDrivingIndicators();
            ScoreManager.Instance.UpdatePlacement();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            GameManager.Instance.ApplyGameState(GameState.Menu);
            yield return null;

            players.RemoveRemotePlayer(1);
            yield return null;
            Assert.IsNull(players.Roster[1], "seat free");
            Assert.IsTrue(remote == null, "scooter gone");

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator RemotePlayersReadiness_CountsTowardsTheCountdown()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlinePrefabs prefabs = OnlinePrefabs.Load();
            players.AddRemotePlayer(RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null).gameObject, 1, 5);

            players.ReadyUp(0);
            Assert.IsFalse(CountingDown(players), "waiting for player 2");
            players.SetRemoteReady(1, true);
            Assert.IsTrue(players.IsReady(1));
            Assert.IsTrue(CountingDown(players), "everyone's ready");
            players.SetRemoteReady(1, false);
            Assert.IsFalse(CountingDown(players), "player 2 isn't ready any more");

            // A player from another machine joining mid-countdown stops it, like a local join
            players.SetRemoteReady(1, true);
            players.AddRemotePlayer(RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null).gameObject, 2, 6);
            Assert.IsFalse(CountingDown(players), "player 3 joined");

            // They leave again: everyone left is ready, so it counts down
            players.RemoveRemotePlayer(2);
            Assert.IsTrue(CountingDown(players), "players 1 and 2 are ready");

            players.SetRemoteReady(1, false); // stop before it loads the match
            Assert.IsFalse(CountingDown(players));
            yield return null;
        }
    }
}
```

Add to `Assets/Tests/Editor/EventSubscriptionTests.cs`, after the OrderHandler test:

```csharp
        [Test]
        public void SkideeSkidoo_EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            SkideeSkidoo skids = objects.Add<SkideeSkidoo>();

            Reflect.Invoke(skids, "OnEnable");
            Reflect.Invoke(skids, "OnDisable");

            Assert.AreEqual(0, GameStateHandlersOf(skids));
        }
```

`bash tools/newmeta.sh` each new test file.

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "ScooterLookTests|RemoteAvatarTests|EventSubscriptionTests"`
Expected: `NO RESULTS` with `error CS0103` / `CS0246` for `ScooterLook`, `RemoteAvatar`, `Colours`, `AddRemotePlayer`.

- [ ] **Step 3: Make the choices readable**

In `Assets/Scripts/Menu/Customization/CustomizationSelector.cs` (Edit tool), after `SetDisableOptionsCustomization`:

```csharp

    /// <summary>The ghost colours to choose from</summary>
    public IReadOnlyList<PlayerColorInformationSO> Colours { get { return playerColors; } }

    /// <summary>The hats to choose from</summary>
    public IReadOnlyList<PlayerHatInformationSO> Hats { get { return playerHats; } }

    /// <summary>The chosen colour: an index into Colours</summary>
    public int ColourIndex { get { return currentPlayerColor; } }

    /// <summary>The chosen hat: an index into Hats</summary>
    public int HatIndex { get { return currentPlayerHat; } }
```

(`using System.Collections.Generic;` is already there.)

- [ ] **Step 4: `ScooterLook` and `RemoteAvatar`**

`Assets/Scripts/Online/ScooterLook.cs`:

```csharp
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dresses a scooter (a PlayerAvatar) as a player: their company's scooter colours, logos and indicator icons, their
/// ghost colour and their hat. A player on this machine is dressed by its view (PlayerCameraResizer,
/// CustomizationSelector); another machine's player has no view, so online play dresses its scooter with this. The paths
/// are the parts those view scripts dress (ScooterLookTests checks they still match). At runtime only: renderer
/// materials are copied as in the view scripts
/// </summary>
public static class ScooterLook
{
    public const string MODEL = "Control/Groundcheck/Basket/SubBasket/ScooterBlockOut_01";
    public const string SCOOTER_BODY = MODEL + "/Scooter Object/Scooter";
    public const string LOGO = MODEL + "/Logo Decal";
    public const string LOGO_2 = MODEL + "/Logo Decal 2";
    public const string GHOST = MODEL + "/Player Model/Player_Model";
    public const string EYELID_LEFT = MODEL + "/Ghost Model/Eyelid Left";
    public const string EYELID_RIGHT = MODEL + "/Ghost Model/Eyelid Right";
    public const string HAT = MODEL + "/Player Model/Armature.001/Torso/Head/Hat Object";

    /// <summary>
    /// The company's scooter colour, logos and the icons other players see over the scooter (as PlayerCameraResizer.InitalizeCompanyScooter)
    /// </summary>
    public static void ShowCompany(GameObject avatar, CompanyInformation company)
    {
        Transform root = avatar.transform;
        root.Find(SCOOTER_BODY).GetComponent<MeshRenderer>().material = company.scooterColorMaterial;
        root.Find(LOGO).GetComponent<DecalProjector>().material = company.scooterDecalMaterial;
        root.Find(LOGO_2).GetComponent<DecalProjector>().material = company.scooterDecalMaterial;
        avatar.GetComponentInChildren<DrivingIndicators>(true).SetDrivingIndicatorsToCompany(company.playerIndicatorSprites);
    }

    /// <summary>
    /// The ghost's colour on its body and eyelids (as CustomizationSelector.UpdateGhostColor). The horns keep their
    /// plain material: their glow shows this machine's boost gauge, which another machine's player doesn't have here
    /// </summary>
    public static void ShowColour(GameObject avatar, PlayerColorInformationSO colour)
    {
        Transform root = avatar.transform;
        SkinnedMeshRenderer ghost = root.Find(GHOST).GetComponent<SkinnedMeshRenderer>();
        Material[] materials = ghost.materials;
        materials[0] = colour.colorMaterial;
        ghost.materials = materials;
        root.Find(EYELID_LEFT).GetComponent<MeshRenderer>().material = colour.colorMaterial;
        root.Find(EYELID_RIGHT).GetComponent<MeshRenderer>().material = colour.colorMaterial;
    }

    /// <summary>
    /// The hat, or none (as CustomizationSelector.UpdateHat)
    /// </summary>
    public static void ShowHat(GameObject avatar, PlayerHatInformationSO hat)
    {
        MeshRenderer hatModel = avatar.transform.Find(HAT).GetComponent<MeshRenderer>();
        hatModel.gameObject.SetActive(hat.displayHat);
        if (!hat.displayHat)
            return;

        hatModel.GetComponent<MeshFilter>().mesh = hat.hatMesh;
        hatModel.material = hat.hatMaterial;
        hatModel.transform.localPosition = hat.hatPosition;
        hatModel.transform.localRotation = Quaternion.Euler(hat.hatRotation);
        hatModel.transform.localScale = hat.hatScale;
    }
}
```

`Assets/Scripts/Online/RemoteAvatar.cs`:

```csharp
using UnityEngine;

/// <summary>
/// Another machine's player on this machine: their scooter (PlayerAvatar.prefab) with no view: no cameras, menus or
/// controller. What only the player's own machine runs is off: controls and physics (BallDriving; the ball is
/// kinematic), falling in water (Respawn) and the horn gauge (PhaseIndicator, whose sliders live in a view). It stays
/// where the online game puts it, dressed as the player. See docs/online.md
/// </summary>
public class RemoteAvatar : MonoBehaviour
{
    /// <summary>The scooter's controls (switched off here)</summary>
    public BallDriving Driving { get; private set; }

    /// <summary>
    /// Makes another machine's player's scooter under parent (null = the scene root). It's built switched off, so none
    /// of its scripts start before the parts that belong to the player's own machine are off
    /// </summary>
    public static RemoteAvatar Create(GameObject avatarPrefab, Transform parent)
    {
        GameObject holder = new GameObject("Remote Avatar (being built)");
        holder.SetActive(false);
        GameObject avatar = Instantiate(avatarPrefab, holder.transform);
        RemoteAvatar remote = avatar.AddComponent<RemoteAvatar>();
        remote.TurnOffOwnerParts();
        avatar.transform.SetParent(parent, false);
        Destroy(holder);
        return remote;
    }

    void TurnOffOwnerParts()
    {
        Driving = GetComponentInChildren<BallDriving>(true);
        Driving.enabled = false;
        foreach (Respawn respawn in GetComponentsInChildren<Respawn>(true))
            respawn.enabled = false;
        foreach (PhaseIndicator horns in GetComponentsInChildren<PhaseIndicator>(true))
            horns.enabled = false;

        Rigidbody ball = Driving.Sphere.GetComponent<Rigidbody>();
        ball.isKinematic = true;
        ball.interpolation = RigidbodyInterpolation.None;
    }

    public void ShowCompany(CompanyInformation company)
    {
        ScooterLook.ShowCompany(gameObject, company);
    }

    public void ShowColour(PlayerColorInformationSO colour)
    {
        ScooterLook.ShowColour(gameObject, colour);
    }

    public void ShowHat(PlayerHatInformationSO hat)
    {
        ScooterLook.ShowHat(gameObject, hat);
    }
}
```

`bash tools/newmeta.sh` both.

- [ ] **Step 5: Remote players in the roster**

In `Assets/Scripts/Player/PlayerInstantiate.cs` (Edit tool):

Replace the body of `SetAllPlayerSpawn` with a loop over `PlaceOnPodium`, and add the method:

```csharp
    public void SetAllPlayerSpawn()
    {
        // Loops for all spawned players
        foreach (PlayerSlot slot in roster.Players)
            PlaceOnPodium(slot);
    }

    ///<summary>
    /// Stands a player's scooter on their slot's podium
    ///</summary>
    private void PlaceOnPodium(PlayerSlot slot)
    {
        int i = slot.Index;
        Rigidbody ball = slot.Player.GetComponentInChildren<Rigidbody>();

        // Resets the velocity of the players (another machine's scooter is kinematic: nothing to reset)
        if (!ball.isKinematic)
            ball.velocity = Vector3.zero;

        // reset position and rotation of ball and controller
        ball.transform.position = menuSpawnPositions[i].transform.position;
        ball.transform.rotation = menuSpawnPositions[i].transform.rotation;

        slot.Player.GetComponentInChildren<BallDriving>(true).transform.position = menuSpawnPositions[i].transform.position;
        slot.Player.GetComponentInChildren<BallDriving>(true).transform.rotation = menuSpawnPositions[i].transform.rotation;
    }
```

Split `UnreadyUp`'s countdown half into its own method:

```csharp
    public void UnreadyUp(int playerIndexToReadyUp)
    {
        playerReadyUp[playerIndexToReadyUp] = false;
        roster[playerIndexToReadyUp].Input.GetComponent<PlayerUIHandler>().customizationSelector.SetDisableOptionsCustomization(false);
        StopReadyUpCountdown();
    }

    ///<summary>
    /// Stops the ready-up countdown if it's counting
    ///</summary>
    private void StopReadyUpCountdown()
    {
        if (readyUpCountdown != null)
        {
            PlayerSelectCanvas.Instance.StopCountdown();
            StopCoroutine(readyUpCountdown);
            readyUpCountdown = null;
        }
    }
```

Add, after `ClearPlayerArray`:

```csharp
    ///<summary>
    /// The company a slot plays for
    ///</summary>
    public CompanyInformation CompanyForSlot(int slot)
    {
        return companies[slot];
    }

    ///<summary>
    /// Online: another machine's player takes their seat here. Their scooter (made with RemoteAvatar) stands on the
    /// seat's podium in the seat's company colours, named like a local player. Returns their slot, or null when the seat
    /// is taken
    ///</summary>
    public PlayerSlot AddRemotePlayer(GameObject avatar, int seat, ulong ownerClientId)
    {
        PlayerSlot slot = roster.JoinRemoteAt(avatar, ownerClientId, seat);
        if (slot == null)
            return null;

        slot.Company = companies[seat];
        BallDriving ballDriving = avatar.GetComponentInChildren<BallDriving>(true);
        ballDriving.Sphere.layer = 10 + seat; // their ball's player layer, as for a local player
        ballDriving.playerIndex = seat + 1;
        avatar.name = "P" + (seat + 1);
        avatar.transform.SetParent(playerHolder.transform);
        avatar.GetComponentInChildren<OrderHandler>(true).CompanyInfo = slot.Company;
        ScooterLook.ShowCompany(avatar, slot.Company);
        PlaceOnPodium(slot);

        // Like a local player joining: their seat isn't ready, which stops the countdown
        SetRemoteReady(seat, false);
        PlayerSelectCanvas.Instance.TogglePressButtonTexts(seat, false);
        return slot;
    }

    ///<summary>
    /// Online: another machine's player left. Their seat is free and the ready count is redone
    ///</summary>
    public void RemoveRemotePlayer(int seat)
    {
        PlayerSlot slot = roster[seat];
        if (slot == null || slot.IsLocal)
            return;

        roster.LeaveSlot(seat);
        playerReadyUp[seat] = false;
        PlayerSelectCanvas.Instance.TogglePressButtonTexts(seat, true);
        Destroy(slot.Player);
        ScoreManager.Instance.UpdateOrderHandlers(roster);
        CheckReadyUpCount();
    }

    ///<summary>
    /// Whether a slot's player is ready in player select
    ///</summary>
    public bool IsReady(int slot)
    {
        return playerReadyUp[slot];
    }

    ///<summary>
    /// Online: another machine's player readied up or stopped being ready (they have no menus here)
    ///</summary>
    public void SetRemoteReady(int slot, bool ready)
    {
        playerReadyUp[slot] = ready;
        if (ready)
            CheckReadyUpCount();
        else
            StopReadyUpCountdown();
    }
```

`CheckReadyUpCount` counts `playerReadyUp` against `PlayerCount`, which includes remote players, so it needs no change.

Why `UpdateOrderHandlers` runs after `Destroy`: the handler list is rebuilt from the roster, which no longer has the player, and `UpdateOrderHandlers` doesn't touch the destroyed scooter.

- [ ] **Step 6: Guard what a view-less scooter lacks**

- **`Assets/Scripts/Player/OrderHandler.cs`:**
  - Add a helper after `UpdatePlacement`:

```csharp
    // The score and placing show in this player's HUD; another machine's scooter has none here
    private void ShowScore()
    {
        if (numberHandler != null)
            numberHandler.UpdateScoreUI(score.ToString());
    }
```

  - Replace each of the three `numberHandler.UpdateScoreUI(score.ToString());` lines (in `DeliverOrder`, `ResetHandler` and `UpdateScore`) with `ShowScore();`.
  - `UpdatePlacement()` becomes `if (numberHandler != null) numberHandler.UpdatePlacement(placement);`.
  - Both `if(tutHandler.HasLearnt)` checks become `if (tutHandler != null && tutHandler.HasLearnt)`. The boost modifier is the owner's machine's business, and a remote scooter has no tutorial.
- **`Assets/Scripts/Player/PlayerCameraResizer.cs`** (Edit tool), in `UpdatePlayerObjectLayer`:
  - Old: `iconCamera.cullingMask &= ~(1 << layer);`
  - New: `if (iconCamera != null) iconCamera.cullingMask &= ~(1 << layer);`
  - Keep the comment above it, and add the line `// (another machine's scooter has no icon camera here)`.
- **`Assets/Scripts/Player/SkideeSkidoo.cs`:**
  - Replace the lambda subscriptions with methods, so `OnDisable` really unsubscribes:

```csharp
    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += StartSkidding;
        GameManager.Instance.OnSwapResults += StopSkidding;
    }
    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= StartSkidding;
        GameManager.Instance.OnSwapResults -= StopSkidding;
    }

    private void StartSkidding() { SetPoopy(skidTime); }
    private void StopSkidding() { SetPoopy(0); }
```

- [ ] **Step 7: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "ScooterLookTests|RemoteAvatarTests|EventSubscriptionTests|PlayerPrefabTests"`
Expected: all pass (the new tests are ScooterLook 3, RemoteAvatar 2 and EventSubscription 1).

- [ ] **Step 8: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 244 total, 244 passed`, the smoke test included.

---

### Task 5: This machine's seat

**Files:**
- Modify: `Assets/Scripts/Player/PlayerInstantiate.cs` (i/crlf)
- Create: `Assets/Tests/Editor/OnlineSeatTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `PlayerRoster.JoinLocalAt` (Task 1).
- Produces:
  - `PlayerInstantiate.OnlineSeat` (int; -1 offline).
  - `PlayerInstantiate.SetOnlineSeat(int seat)`: -1 means offline again.
  - With a seat set:
    - This machine's player moves there: they leave and rejoin next frame with the same controller.
    - A player joining later takes that seat.
    - Any other controller is turned away.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/OnlineSeatTests.cs`:

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Online, this machine's one player sits in the seat the host gave them, in the real menu scene: they move there
    /// with their controller, another controller is turned away, and offline again players join as usual. Enters Play
    /// Mode (about 10 s each)
    /// </summary>
    public class OnlineSeatTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static bool LocalIn(int slot)
        {
            PlayerSlot player = PlayerInstantiate.Instance.Roster[slot];
            return player != null && player.IsLocal;
        }

        static void PressA(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator ThisMachinesPlayer_MovesToTheSeatTheHostGave_WithTheSameController()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            Gamepad pad = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
                yield return null;

            players.SetOnlineSeat(2);
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(2) && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerSlot moved = players.Roster[2];
            Assert.IsTrue(LocalIn(2), "in seat 3");
            Assert.IsNull(players.Roster[0], "seat 1 is free");
            Assert.AreEqual(1, players.Roster.LocalCount);
            Assert.IsTrue(moved.Input.devices.Contains(pad), "with the same controller");
            Assert.AreEqual("P3", moved.Player.name);
            BallDriving scooter = moved.Player.GetComponentInChildren<BallDriving>();
            Assert.AreEqual(3, scooter.playerIndex);
            Assert.AreEqual(12, scooter.Sphere.layer, "player 3's ball layer");
            Assert.IsTrue(moved.Input.GetComponent<PlayerUIHandler>().menuInteractions.hostPlayer, "still runs this machine's menus");

            // One player per machine online: another controller is turned away
            TestPlayers.Add();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, players.PlayerCount, "the second controller was turned away");

            // Offline again, a second player joins player select as usual (lowest free slot)
            players.SetOnlineSeat(-1);
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            Gamepad second = TestPlayers.Pads[1];
            deadline = Time.realtimeSinceStartup + 10;
            float nextPress = 0;
            while (!LocalIn(0) && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(second);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.IsTrue(LocalIn(0), "joined seat 1");
            Assert.AreEqual(2, players.PlayerCount);

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Online_APlayerJoiningLater_SitsInTheSeat()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;

            PlayerInstantiate.Instance.SetOnlineSeat(1);
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (!LocalIn(1) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(LocalIn(1), "in seat 2");
            Assert.IsNull(PlayerInstantiate.Instance.Roster[0]);
            MenuInteractions menus = PlayerInstantiate.Instance.Roster[1].Input.GetComponent<PlayerUIHandler>().menuInteractions;
            Assert.IsTrue(menus.hostPlayer, "runs this machine's menus");
            Assert.AreEqual(MenuType.MainMenu, menus.curentMenuType, "on the title screen");
        }
    }
}
```

`Contains` here is LINQ over `ReadOnlyArray<InputDevice>`. The pad argument is a local, but no lambda captures it.

- [ ] **Step 2: Run it to see it fail**

Run: `bash tools/run-tests.sh OnlineSeatTests`
Expected: `NO RESULTS` with `error CS1061: 'PlayerInstantiate' does not contain a definition for 'SetOnlineSeat'`.

- [ ] **Step 3: The online seat**

In `Assets/Scripts/Player/PlayerInstantiate.cs` (Edit tool):

Field, after `CutsceneManager cutsceneManager;`:

```csharp

    // Online: the seat the host gave this machine's one player (-1 offline)
    int onlineSeat = -1;
```

Members, after `IsMissingController`:

```csharp
    ///<summary>
    /// Online: the seat this machine's one player sits in (-1 offline)
    ///</summary>
    public int OnlineSeat { get { return onlineSeat; } }

    ///<summary>
    /// Online: this machine's player sits in the seat the host gave them (-1 = offline again). A player already in
    /// another slot moves there: they leave and join again next frame with the same controller, so every slot-bound
    /// part (layers, cameras, company, podium) is set up as for any join. A player joining later takes the seat
    ///</summary>
    public void SetOnlineSeat(int seat)
    {
        onlineSeat = seat;
        if (seat < 0)
            return;

        foreach (PlayerSlot local in roster.LocalPlayers)
        {
            if (local.Index != seat)
                StartCoroutine(MoveToOnlineSeat(local.Input));
            return; // one player per machine online
        }
    }

    private IEnumerator MoveToOnlineSeat(PlayerInput player)
    {
        Gamepad pad = player.GetDevice<Gamepad>();
        LeaveLocal(player);

        // The old player lets go of the controller when it's destroyed, at the end of this frame
        yield return null;

        if (pad != null && pad.added)
            PlayerInputManager.instance.JoinPlayer(-1, -1, "Gamepad", pad);
    }
```

Split `RemovePlayerRef` so the move can skip its guard:

```csharp
    public void RemovePlayerRef(PlayerInput playerInput)
    {
        // If player spawn is disabled
        if (allowPlayerSpawn == false && Constants.SPAWN_MID_MATCH == false)
        {
            return;
        }

        LeaveLocal(playerInput);
    }

    ///<summary>
    /// A player on this machine leaves: their slot is free and their player object goes
    ///</summary>
    private void LeaveLocal(PlayerInput playerInput)
    {
        int position = roster.Leave(playerInput);
```

The rest of `LeaveLocal` is the old body of `RemovePlayerRef` after the guard, unchanged: the press-button text, `UpdateOrderHandlers`, `Destroy`, the camera rects, the controller prompts and `CheckReadyUpCount`.

In `AddPlayerReference`, from `// If player spawn is disabled` down to `roster.JoinLocal(playerInput);`:

```csharp
        // Online, this machine has one player, in the seat the host gave them
        bool online = onlineSeat >= 0;
        if (online && roster.LocalCount > 0)
        {
            Destroy(playerInput.gameObject);
            return;
        }

        // If player spawn is disabled
        if(!online && allowPlayerSpawn == false && PlayerCount >= 1)
        {

            Destroy(playerInput.gameObject);
            return;
        }


        bool isFirstPlayer = online || PlayerCount == 0;

        // Takes the lowest free slot (online: the seat)
        PlayerSlot slot = online ? roster.JoinLocalAt(playerInput, onlineSeat) : roster.JoinLocal(playerInput);
```

In the `isFirstPlayer` branch:

```csharp
        if (isFirstPlayer)
        {
            MenuInteractions menus = playerInput.gameObject.GetComponent<PlayerUIHandler>().menuInteractions;
            menus.hostPlayer = true;

            // Online, this machine's player can arrive (or move seats) while the host is in player select
            if (online && gameManager.MainState == GameState.PlayerSelect)
                menus.SwapToPlayerSelect();
            else
            {
                menus.SwapMenuType(MenuType.MainMenu);
                MainMenu.Instance.Player1ControllerConnected(playerInput);
            }
        }
```

The `else` branch stays: `SwapToPlayerSelect` for a later local joiner.

- [ ] **Step 4: Run it to see it pass**

Run: `bash tools/run-tests.sh "OnlineSeatTests|RemoteAvatarTests"`
Expected: all pass.

- [ ] **Step 5: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 246 total, 246 passed`, the smoke test included. Local joins take `PlayerCount == 0` and `JoinLocal` exactly as before.

---

### Task 6: The game side of a session

**Files:**
- Create: `Assets/Scripts/Online/OnlineGame.cs`, `Assets/Tests/Editor/OnlineGameTests.cs`, `Assets/Tests/Editor/OnlineGameNetworkTests.cs` (+ `.meta` each)
- Modify: `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Tests/Editor/OnlineSessionNetworkTests.cs`

**Interfaces:**
- Consumes (from Tasks 1–5):
  - `OnlineSession.Players` / `Match` / events / `SeatOf`; `OnlinePlayer`; `OnlineMatch`; `OnlinePrefabs`.
  - `OnlineSceneFlow`; `GameManager.StateApplied`.
  - `RemoteAvatar`; `PlayerInstantiate.AddRemotePlayer` / `RemoveRemotePlayer` / `SetRemoteReady` / `IsReady` / `SetOnlineSeat`; `CustomizationSelector` choices.
- Produces:
  - `static OnlineGame Attach(OnlineSession)`.
  - `static bool CanGoOnline()` and `const string ONE_PLAYER`.
  - `static GameState ForClient(GameState hostState)`.
  - `static T Pick<T>(IReadOnlyList<T> list, int index) where T : class` (null when out of range).
  - `OnlineSession` no longer sets `GameAuthority.Role`: `OnlineGame` does, and the scene flow with it.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/OnlineGameTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// OnlineGame without networking: the role and scene flow follow the session, clients show the host's own menus as
    /// the title screen, and a colour or hat from outside this build's lists is ignored
    /// </summary>
    public class OnlineGameTests
    {
        OnlineSession session;

        [TearDown]
        public void TearDown()
        {
            if (session != null)
                Object.DestroyImmediate(session.gameObject);
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
        }

        [Test]
        public void TheSessionsRole_BecomesThisMachines_WithTheOnlineSceneFlow()
        {
            session = OnlineSession.Create("1.0.0");
            OnlineGame.Attach(session);

            Reflect.Invoke(session, "SetRole", NetworkRole.Host);
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role);
            Assert.IsInstanceOf<OnlineSceneFlow>(SceneFlow.Current);

            Reflect.Invoke(session, "SetRole", NetworkRole.Offline);
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
            Assert.IsNotInstanceOf<OnlineSceneFlow>(SceneFlow.Current);
        }

        [Test]
        public void ASessionWithoutTheGame_LeavesThisMachinesRoleAlone()
        {
            session = OnlineSession.Create("1.0.0");

            Reflect.Invoke(session, "SetRole", NetworkRole.Client);

            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);
        }

        [TestCase(GameState.Options, GameState.Menu)]
        [TestCase(GameState.Credits, GameState.Menu)]
        [TestCase(GameState.Menu, GameState.Menu)]
        [TestCase(GameState.PlayerSelect, GameState.PlayerSelect)]
        [TestCase(GameState.MainLoop, GameState.MainLoop)]
        public void ForClient_HostsOwnMenus_ShowAsTheTitleScreen(GameState host, GameState client)
        {
            Assert.AreEqual(client, OnlineGame.ForClient(host));
        }

        [Test]
        public void Pick_OutsideTheList_IsNothing()
        {
            List<string> colours = new List<string> { "red", "blue" };

            Assert.AreEqual("blue", OnlineGame.Pick(colours, 1));
            Assert.IsNull(OnlineGame.Pick(colours, 2));
            Assert.IsNull(OnlineGame.Pick(colours, -1));
        }
    }
}
```

`Assets/Tests/Editor/OnlineGameNetworkTests.cs`:

```csharp
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The game online, in the real menu scene on this computer (127.0.0.1). This machine hosts or joins with the game,
    /// and another machine is stood in for by a session without the game (it changes nothing here but what it sends).
    /// Enters Play Mode (about 20 s each). No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineGameNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7793;
        const string VERSION = "test";
        const float WAIT = 15f;

        class StateRecorder
        {
            public GameState Last = GameState.Default;

            public StateRecorder(OnlineMatch match)
            {
                match.StateReceived += OnState;
            }

            void OnState(GameState state)
            {
                Last = state;
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

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static OnlinePlayer PlayerInSeat(OnlineSession session, int seat)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.Seat == seat)
                    return player;
            }
            return null;
        }

        static void PressA(Gamepad pad)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator Hosting_AnotherMachinesPlayer_SitsInTheirSeat_DressedAsThem()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role);
            Assert.IsInstanceOf<OnlineSceneFlow>(SceneFlow.Current);
            GameManager.Instance.SetGameState(GameState.PlayerSelect);

            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((Slot(1) == null || other.Match == null || PlayerInSeat(other, 1) == null) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            Assert.IsFalse(Slot(1).IsLocal);
            Assert.AreEqual(NetworkRole.Host, GameAuthority.Role, "their session left this machine's role alone");
            Assert.AreEqual(GameState.PlayerSelect, other.Match.State, "they see where the host is");

            PlayerInSeat(other, 1).Share(2, 3, true);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!players.IsReady(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(players.IsReady(1), "their ready counts here");
            CustomizationSelector customization = OnlinePrefabs.Load().LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);
            Transform scooter = Slot(1).Player.transform;
            Assert.AreSame(customization.Colours[2].colorMaterial, scooter.Find(ScooterLook.GHOST).GetComponent<SkinnedMeshRenderer>().sharedMaterials[0], "their colour");
            Assert.AreEqual(customization.Hats[3].displayHat, scooter.Find(ScooterLook.HAT).gameObject.activeSelf, "their hat");

            StateRecorder heard = new StateRecorder(other.Match);
            GameManager.Instance.SetGameState(GameState.Menu);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Last != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Menu, heard.Last, "the host's switch reached them");

            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(1) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(Slot(1), "they left: their seat is free");

            host.Leave();
            yield return null;
            yield return null;
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "local again");
            Assert.IsNotInstanceOf<OnlineSceneFlow>(SceneFlow.Current, "the local loader again");
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_ThisMachinesPlayer_MovesToItsSeat_AndFollowsTheHost()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            Gamepad pad = TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
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
            Assert.IsTrue(Slot(1).Input.devices.Contains(pad), "with their controller");
            Assert.IsNotNull(Slot(0), "the host's scooter is in seat 1");
            Assert.IsFalse(Slot(0).IsLocal);
            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role);

            host.Match.SendState(GameState.PlayerSelect);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameManager.Instance.MainState != GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.PlayerSelect, GameManager.Instance.MainState, "followed the host into player select");
            Assert.IsTrue(((Canvas)Reflect.GetField(MainMenu.Instance, "PlayerSelectCanvas")).enabled, "player select's menu is open");

            // Ready up here (menus ignore buttons for a moment after they open, so keep pressing): the host hears it
            OnlinePlayer onHost = PlayerInSeat(host, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            float nextPress = 0;
            while (!onHost.Ready && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= nextPress)
                {
                    PressA(pad);
                    nextPress = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }
            Assert.IsTrue(onHost.Ready, "the host sees this player ready");

            // The host looks at its options: here that's the title screen
            host.Match.SendState(GameState.Options);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (GameManager.Instance.MainState != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Menu, GameManager.Instance.MainState);

            mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Slot(0) != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNull(Slot(0), "the host's scooter went with the session");
            Assert.IsTrue(Slot(1).IsLocal, "this machine's player stays");
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role);

            // One side at a time: closing both ends in one frame makes Windows report the closed port as a socket error
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

In `Assets/Tests/Editor/OnlineSessionNetworkTests.cs`, delete two assertions, since `OnlineGame` now owns the role and `OnlineGameTests` covers it:
- `Assert.AreEqual(NetworkRole.Client, GameAuthority.Role, "this machine's part (the client set it last)");`
- `Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "this machine is local again");`

`bash tools/newmeta.sh` both new test files.

- [ ] **Step 2: Run them to see them fail**

Run: `bash tools/run-tests.sh "OnlineGameTests|OnlineGameNetworkTests"`
Expected: `NO RESULTS`, `error CS0103: The name 'OnlineGame' does not exist`.

- [ ] **Step 3: Write `OnlineGame`**

`Assets/Scripts/Online/OnlineGame.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's side of an online session:
/// - this machine's role (GameAuthority) and the scene flow;
/// - this machine's player in the seat the host gave them;
/// - other machines' players as scooters in their seats;
/// - everyone's colour, hat and readiness;
/// - the host's game states.
/// Whatever starts a session for the game adds it: the Online menu now, the Steam lobby later (roadmap Task 3.2).
/// See docs/online.md
/// </summary>
public class OnlineGame : MonoBehaviour
{
    /// <summary>Why a machine with more than one player can't go online</summary>
    public const string ONE_PLAYER = "Online: online play is one player per machine (until roadmap Task 3.9). Let the other players leave player select first.";

    // What this machine shows of another machine's player
    class RemotePlayer
    {
        public RemoteAvatar Avatar;
        public int Seat;
        public int Colour = -1;
        public int Hat = -1;
        public bool Ready;
    }

    OnlineSession session;
    OnlinePrefabs prefabs;
    OnlineSceneFlow sceneFlow;
    CustomizationSelector choices; // player select's colours and hats, from the local player's prefab
    OnlinePlayer localPlayer;     // this machine's player
    readonly List<OnlinePlayer> waiting = new List<OnlinePlayer>(); // other machines' players, until their seat is free here
    readonly Dictionary<OnlinePlayer, RemotePlayer> remotes = new Dictionary<OnlinePlayer, RemotePlayer>();

    /// <summary>
    /// Plays the game over a session (adds an OnlineGame to its object)
    /// </summary>
    public static OnlineGame Attach(OnlineSession session)
    {
        OnlineGame game = session.gameObject.AddComponent<OnlineGame>();
        game.Begin(session);
        return game;
    }

    /// <summary>
    /// Whether this machine can go online: at most one player (online play is one player per machine until Task 3.9)
    /// </summary>
    public static bool CanGoOnline()
    {
        return PlayerInstantiate.Instance == null || PlayerInstantiate.Instance.Roster.LocalCount <= 1;
    }

    /// <summary>
    /// The state a client shows for the host's: the host's own menus (options, credits) show as the title screen
    /// </summary>
    public static GameState ForClient(GameState hostState)
    {
        return hostState == GameState.Options || hostState == GameState.Credits ? GameState.Menu : hostState;
    }

    /// <summary>
    /// An item of player select's list by the index another machine sent, or null when this build's list has no such
    /// item
    /// </summary>
    public static T Pick<T>(IReadOnlyList<T> list, int index) where T : class
    {
        return list != null && index >= 0 && index < list.Count ? list[index] : null;
    }

    void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        prefabs = OnlinePrefabs.Load();
        sceneFlow = new OnlineSceneFlow(SceneFlow.Current);
        choices = prefabs.LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);

        session.RoleChanged += OnRoleChanged;
        session.PlayerSpawned += OnPlayerSpawned;
        session.PlayerDespawned += OnPlayerDespawned;
        session.MatchSpawned += OnMatchSpawned;
        if (GameManager.Instance != null)
            GameManager.Instance.StateApplied += OnStateApplied;

        // A session that's running already
        foreach (OnlinePlayer player in session.Players)
            OnPlayerSpawned(player);
        if (session.Match != null)
            OnMatchSpawned(session.Match);
        if (session.Role != NetworkRole.Offline)
            OnRoleChanged(session.Role);
    }

    void OnDestroy()
    {
        if (session != null)
        {
            session.RoleChanged -= OnRoleChanged;
            session.PlayerSpawned -= OnPlayerSpawned;
            session.PlayerDespawned -= OnPlayerDespawned;
            session.MatchSpawned -= OnMatchSpawned;
            if (session.Match != null)
                session.Match.StateReceived -= FollowHost;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.StateApplied -= OnStateApplied;
    }

    // This machine's role and scene flow follow the session's
    void OnRoleChanged(NetworkRole role)
    {
        GameAuthority.Role = role;

        if (role != NetworkRole.Offline)
        {
            SceneFlow.Current = sceneFlow;
            return;
        }

        SceneFlow.Current = null;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        foreach (RemotePlayer remote in remotes.Values)
        {
            if (players != null)
                players.RemoveRemotePlayer(remote.Seat);
        }
        remotes.Clear();
        waiting.Clear();
        localPlayer = null;
        if (players != null)
            players.SetOnlineSeat(-1);
    }

    void OnPlayerSpawned(OnlinePlayer player)
    {
        if (player.IsOwner)
        {
            localPlayer = player;
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.SetOnlineSeat(player.Seat);
        }
        else
            waiting.Add(player);
    }

    void OnPlayerDespawned(OnlinePlayer player)
    {
        if (player == localPlayer)
            localPlayer = null;
        waiting.Remove(player);

        RemotePlayer remote;
        if (remotes.TryGetValue(player, out remote))
        {
            remotes.Remove(player);
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.RemoveRemotePlayer(remote.Seat);
        }
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        if (match.IsServer)
        {
            // Players who join later start from where the host is
            if (GameManager.Instance != null)
                match.SendState(GameManager.Instance.MainState);
            return;
        }

        match.StateReceived += FollowHost;
        if (match.State != GameState.Default)
            FollowHost(match.State);
    }

    // Host: each state the game switches to goes to the clients
    void OnStateApplied(GameState state)
    {
        if (session.Match != null && session.Match.IsServer)
            session.Match.SendState(state);
    }

    // Client: the host switched state
    void FollowHost(GameState hostState)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ApplyGameState(ForClient(hostState));
    }

    void Update()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        SeatWaitingPlayers(players);
        ShareLocalChoices(players);
        ShowRemoteChoices(players);
    }

    // Other machines' players take their seats here once they're free (this machine's player may be moving out of one)
    void SeatWaitingPlayers(PlayerInstantiate players)
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            OnlinePlayer player = waiting[i];
            int seat = player.Seat;
            if (seat < 0 || seat >= Constants.MAX_PLAYERS || players.Roster[seat] != null)
                continue;

            RemoteAvatar avatar = RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null);
            if (players.AddRemotePlayer(avatar.gameObject, seat, player.OwnerClientId) == null)
            {
                Destroy(avatar.gameObject);
                continue;
            }

            remotes[player] = new RemotePlayer { Avatar = avatar, Seat = seat };
            waiting.RemoveAt(i);
        }
    }

    // This machine's player's colour, hat and readiness go to every machine
    void ShareLocalChoices(PlayerInstantiate players)
    {
        if (localPlayer == null || localPlayer.Seat < 0 || localPlayer.Seat >= Constants.MAX_PLAYERS)
            return;

        PlayerSlot slot = players.Roster[localPlayer.Seat];
        if (slot == null || !slot.IsLocal)
            return;

        CustomizationSelector picked = slot.Input.GetComponent<PlayerUIHandler>().customizationSelector;
        localPlayer.Share(picked.ColourIndex, picked.HatIndex, players.IsReady(slot.Index));
    }

    // Other machines' players' colour, hat and readiness show here as they change
    void ShowRemoteChoices(PlayerInstantiate players)
    {
        foreach (KeyValuePair<OnlinePlayer, RemotePlayer> pair in remotes)
        {
            OnlinePlayer player = pair.Key;
            RemotePlayer shown = pair.Value;

            if (player.Colour != shown.Colour)
            {
                shown.Colour = player.Colour;
                PlayerColorInformationSO colour = Pick(choices.Colours, player.Colour);
                if (colour != null)
                    shown.Avatar.ShowColour(colour);
            }

            if (player.Hat != shown.Hat)
            {
                shown.Hat = player.Hat;
                PlayerHatInformationSO hat = Pick(choices.Hats, player.Hat);
                if (hat != null)
                    shown.Avatar.ShowHat(hat);
            }

            if (player.Ready != shown.Ready)
            {
                shown.Ready = player.Ready;
                players.SetRemoteReady(shown.Seat, player.Ready);
            }
        }
    }
}
```

`bash tools/newmeta.sh Assets/Scripts/Online/OnlineGame.cs`.

- [ ] **Step 4: The session stops setting the role**

In `Assets/Scripts/Online/OnlineSession.cs`:
- In `SetRole`, delete the line `GameAuthority.Role = role;`.
- The class summary:
  - Old: `…and keeps GameAuthority.Role in step (Host or Client while the session runs, Offline once it ends).`
  - New: `…and reports its role (Host or Client while the session runs, Offline once it ends); OnlineGame makes that this machine's GameAuthority.Role.`
- The summary of `Role`, if it mentions `GameAuthority`, likewise.

- [ ] **Step 5: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "OnlineGameTests|OnlineGameNetworkTests|OnlineSessionNetworkTests|OnlineSessionTests|OnlinePlayersNetworkTests"`
Expected: all pass. The new tests are OnlineGame 8 (5 TestCases + 3) and OnlineGameNetwork 2.

- [ ] **Step 6: Whole suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 256 total, 256 passed`.

---

### Task 7: The Online menu, docs and roadmap

**Files:**
- Modify: `Assets/Scripts/Editor/OnlineTestMenu.cs`, `Assets/Tests/Editor/OnlineTestMenuTests.cs`, `docs/online.md`, `docs/testing.md`, `docs/player-prefab.md`, the roadmap

**Interfaces:**
- Consumes: `OnlineGame.Attach` / `CanGoOnline` / `ONE_PLAYER` (Task 6); `MainMenu.SwapToPlayerSelect`.
- Produces:
  - **Tools → Dead on Arrival → Online:**
    - Plays the game over its session.
    - **Host** goes into player select.
    - **Host** and **Join** refuse a machine with more than one player.

- [ ] **Step 1: Write the failing test**

Add to `Assets/Tests/Editor/OnlineTestMenuTests.cs`:

```csharp
        [Test]
        public void Prepare_PlaysTheGameOverTheSession()
        {
            session = OnlineTestMenu.Prepare();

            Assert.IsNotNull(session.GetComponent<OnlineGame>());
            Assert.AreSame(session, OnlineTestMenu.Prepare(), "the same session next time");
            Assert.AreEqual(1, session.GetComponents<OnlineGame>().Length, "played once");
        }
```

It assigns the fixture's `session` field, which the existing `TearDown` destroys. The menu's static session then reads as destroyed (null), and the next `Prepare` makes a new one.

- [ ] **Step 2: Run it to see it fail**

Run: `bash tools/run-tests.sh OnlineTestMenuTests`
Expected: `Prepare_PlaysTheGameOverTheSession` fails (`Expected: not null`).

- [ ] **Step 3: The menu**

In `Assets/Scripts/Editor/OnlineTestMenu.cs`:

```csharp
    [MenuItem(MENU + "Host", false, 1)]
    static void Host()
    {
        if (!OnlineGame.CanGoOnline())
        {
            Debug.LogWarning(OnlineGame.ONE_PLAYER);
            return;
        }

        if (!Prepare().HostDirect(THIS_COMPUTER, OnlineSession.DIRECT_PORT))
        {
            Debug.LogWarning("Online: couldn't host. Is another editor hosting on port " + OnlineSession.DIRECT_PORT + "?");
            return;
        }

        // The online lobby is player select
        if (GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu)
            MainMenu.Instance.SwapToPlayerSelect();
    }

    [MenuItem(MENU + "Join This Computer", false, 2)]
    static void Join()
    {
        if (!OnlineGame.CanGoOnline())
        {
            Debug.LogWarning(OnlineGame.ONE_PLAYER);
            return;
        }

        Prepare().JoinDirect(THIS_COMPUTER, OnlineSession.DIRECT_PORT);
    }
```

In `Prepare`:

```csharp
        if (session == null)
        {
            session = OnlineSession.Create(Application.version);
            OnlineGame.Attach(session);
        }
```

Update the class summary: "…hosts (and goes into player select, the online lobby) or joins a session between two editors…".

- [ ] **Step 4: Docs**

**`docs/online.md`:**
- Replace "What's there so far (Phase 3A)" with "What's there so far". Keep the Phase 3A bullets, and add a **Phase 3B** list:
  - **Seats:** the host sits in seat 0 and joiners take the lowest free seat. A seat is the player's slot on every machine: company, podium, name P1–P4.
  - **This machine's player:** moves into its seat with their controller. Online is one player per machine, so any other controller is turned away.
  - **Player select is the online lobby:**
    - Every other player's scooter stands on its podium, in its company's colours, with their ghost colour and hat, which update as they change them.
    - Ready counts on every machine.
  - **Clients follow the host:** they follow its menus. The host's options and credits show as the title screen.
  - **The match doesn't start online yet** (Phase 3C). When the countdown ends, the Console says so. Leave the session to play locally.
  - **How it works:**
    - Two small network objects: `OnlineMatch` (the host's game state) and a `OnlinePlayer` per machine (seat, colour, hat, ready). They're in `Assets/Prefabs/Online/`, listed in `Assets/Resources/Online/OnlinePrefabs.asset`.
    - `OnlineGame` connects them to the game.
    - Another machine's scooter is a `PlayerAvatar` with its controls off (`RemoteAvatar`), dressed by `ScooterLook`.
- In "Two editors on one computer", replace the host and join steps:
  - Press Play in both editors, and press A on a controller in each: each editor needs its player 1.
  - Editor 1: **Online → Host**. It goes into player select.
  - Editor 2: **Online → Join This Computer**.
    - Editor 2's player moves to seat 2: second podium, second company.
    - Each editor shows the other's scooter.
  - Change colour and hat in one editor, and watch the other.
    - A controller only drives the editor that has focus, so click into an editor before pressing buttons.
  - Ready up in both. The countdown runs and ends with `Online: starting an online match isn't in yet…`.
- Known limits, add:
  - One player per machine online.
  - The match can't start online yet (3C).
  - An empty seat still shows "press A" on every machine.
  - Without an Online → Leave, a player who readied up stays ready.
  - When the host leaves, clients stay in player select with their own player (returning to the menu is Task 3.8).

**`docs/testing.md`:** extend the network-test bullet:
- `OnlinePlayersNetworkTests` (127.0.0.1, port 7792) uses sessions only.
- `RemoteAvatarTests`, `OnlineSeatTests` and `OnlineGameNetworkTests` (port 7793) play in the real menu scene, about 10–20 s each.

**`docs/player-prefab.md`:** under "Editing", add a bullet:
- Online, another machine's player is `PlayerAvatar.prefab` dressed by path (`ScooterLook`).
- Renaming or moving the scooter body, logos, ghost, eyelids or hat means updating `ScooterLook`'s paths. `ScooterLookTests` fails until then.

**Roadmap** (`2026-09-22-steam-split-screen-and-online.md`):
- Phase 3 progress line: add `Phase 3B (2026-09-24-phase3b-online-player-select.md) — online player select: seats, OnlinePlayer, remote scooters, clients follow the host's states.`
- Task 3.2 bullet 2: stays `[ ]`. Note: *(Phase 3B: player select is the online lobby. Seats give companies; colour, hat and ready sync. The Steam lobby screen is still to do.)*
- Task 3.3 bullet 1: stays `[ ]`. Note: *(Phase 3B: `OnlinePlayer` has seat (company follows), colour, hat and ready. It's its own network prefab, not on `PlayerAvatar`: see the Phase 3B plan's Ruling 1. Score and the flags byte come with driving.)*
- Task 3.3 bullet 5: stays `[ ]`. Note: *(Phase 3B: `RemoteAvatar` turns off `BallDriving`, `Respawn` and `PhaseIndicator` and makes the ball kinematic. `ScooterLook` dresses it. `OrderHandler` and `PlayerCameraResizer.UpdatePlayerObjectLayer` are guarded. Still to do for the match: `Order.EraseOrder` and `OrderBeacon`'s `Compass` lookups.)*
- Task 3.4 bullet 1: `[x]`. Note: *(`OnlineMatch`: ordered `ClientRpc`s plus a snapshot variable, because states can switch twice in a frame. Clients show the host's options and credits as the title screen.)*
- The §R checklist's Run All count: `208 passed` → `257 passed`, or whatever the whole suite reports.

- [ ] **Step 5: Run the tests to see them pass**

Run: `bash tools/run-tests.sh OnlineTestMenuTests`
Expected: all pass.

- [ ] **Step 6: Whole suite and the player build's scripts**

Run: `bash tools/run-tests.sh`
Expected: `tests: 257 total, 257 passed`.

Check that the Windows player's scripts compile (they're runtime code). Put a one-off into the **mirror only**, as `../_doa_test_mirror/CapstoneYear4/Assets/Scripts/Editor/PlayerCompileCheck.cs`:

```csharp
using UnityEditor;
using UnityEditor.Build.Player;
using UnityEngine;

// ONE-OFF (Phase 3B, Task 7): compiles the Windows player's scripts in the test mirror. Never add it to the project
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

Expected: `PLAYER COMPILE: <about 33> assemblies` and no `error CS` lines. (The mirror syncs from the project first, so run the whole suite before this.)

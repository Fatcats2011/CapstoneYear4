# Phase 3J — Online Plus Couch Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** several players on one machine can play an online match together, with up to 4 players in all.
- Each player on a machine gets their own seat, scooter and split-screen view there.
- Every other machine sees them as separate players.

**Architecture:**
- **Seats belong to machines, several each.**
  - On the host, `SeatTable` maps each seat to a Netcode client id, and one client may hold several.
  - The host spawns one `OnlinePlayer` and one `OnlineScooter` per seat, owned by that seat's machine. Each carries the seat it was spawned for (`OnlineSession.SpawningSeat`).
  - Every host-side check of the form "this request names the sender's own seat" becomes `session.Owns(machine, seat)`.
- **A machine joins with one seat, as today, and asks for each further one.**
  - A client calls `OnlineSession.AskSeat`, which sends a ServerRpc on `OnlineMatch`. The host applies the join rules (full, or the match has started), then seats and spawns, or answers with a refusal. The host's own extra players are seated at once, with no RPC.
  - Giving a seat back (`FreeSeat`) despawns that seat's player and scooter everywhere.
  - The join payload and `JoinRules` don't change.
- **This machine's players move into the seats it holds.**
  - `PlayerInstantiate` keeps the set of seats this machine holds. A pure planner (`CouchSeats.Plan`) decides which local player goes to which seat, the menu player (`hostPlayer`) first.
  - A local player without a seat is *unseated*: they stay where they joined. `OnlineGame` asks for one seat per unseated player (`SeatAsker` counts the asks in flight).
  - Moves reuse today's leave-and-rejoin-next-frame (`MoveToOnlineSeat`), now with a target seat per device.
- **Everything else is per seat already:**
  - driving (`OnlineDriving`), hits, respawns and cues (`RemoteAvatar.IsRemote`);
  - the tutorial (seats);
  - split-screen (`roster.LocalPlayers`) and `GraphicsQuality` (`LocalCount`).

  The few places that assumed "this machine's one player" are listed in Task 4.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 · Unity Transport 1.5 · Steamworks.NET 2025.164.1 · Unity Test Framework 1.1.33.

**Spec:**
- The roadmap, Task 3.9 (`docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md:299-301`): "Several local players per client: each local `PlayerInput` requests its own `NetworkPlayer` owned by that client; that client split-screens only its local slots."
- The handoff (`2026-10-01-remaining-work-handoff.md`, "Optional, after launch"): "Each local `PlayerInput` gets its own online player, and each machine split-screens only its own players."
- The user, 2026-10-06: "continue with the next phase". Task 3.9 is the only code phase not waiting on art, the App ID or the user's editor steps.

## Global Constraints

- **Stay on Unity 2022.3 LTS** and Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**.
- **Never edit a scene, prefab, `.inputactions` asset or ProjectSettings file.** No new network prefab: the three in `OnlinePrefabs` stay, so `NetcodeSetup` and `OnlinePrefabsTests` are unchanged.
- **`SteamManager.cs` and `SteamStartup.cs` stay untouched.** The embedded Steam transport under `Packages/` stays untouched too.
- **Local play must behave exactly as before.** `LocalMatchSmokeTest` and every online test pass after every task.
- **Online safety stays** (`docs/online-safety.md`):
  - every new ServerRpc starts with `Allowed(sender, RpcKind.Seat)`;
  - the host checks `Owns` before acting on a seat;
  - a client ignores a seat outside 0–3.
- **Branch:** `steam-phase1a`. **No git commits:** the user commits.
- **Meta files:** run `bash tools/newmeta.sh <path>` for every new file under `Assets/`.
- **Line endings:** check with `git ls-files --eol`. Edit `i/crlf` files with the Edit tool only, never `sed -i`.
- **Tests:**
  - Use `bash tools/run-tests.sh [filter]`. Run long runs in the background, one run at a time (one mirror).
  - Play Mode rules: recorder classes, not captured locals; wait by real time; `EnterPlayMode` yielded by the test itself; `log.MachinesLeave()` before a machine leaves.
  - Network ports in use run to 7812. This phase uses **7813**. Update `docs/testing.md`'s "next free port" to 7814.
- **Subagents:** Sonnet agents are allowed "as you see fit", but never two test runs at once.

## Rulings (decided while planning)

1. **One seat at join, more by asking.** The join payload stays the version alone.
   - A machine with 3 players joining a match with 1 free seat gets in; its other 2 players are turned away with "That match is full.".
   - Why: one path for every extra seat (at join, or a pad pressing A later), and `JoinRules` and the approval path, which 4C hardened, stay as they are.
   - Cost if wrong: a round trip before the 2nd–4th players sit down.
2. **Extra seats only while the host is in the menus.** `AskSeat` uses `JoinRules.Refusal(Version, Version, seats.Count, HostState())`, so the same "full" and "started" rules apply. Nobody joins mid-match, as offline.
3. **An unseated player can't follow the match out of the menus.** When this machine's state leaves the menus (`LobbyRules.InTheMenus` false), every unseated local is turned away with `JoinRules.STARTED`.
   - The host never counted them, so the match would have had a player no other machine sees.
4. **B in player select:**
   - This machine's menu player (`hostPlayer`) still leaves the session, as today.
   - Any other local player leaves their slot, as offline, and gives their seat back (`SeatGivenUp` → `FreeSeat`).
   - The host ignores a request to free a machine's last seat, so a connected machine always has a player.
5. **Achievements:** a feat by any of this machine's seats unlocks for this machine's Steam account, as in local split-screen. `MatchFeats.UnlocksHere` takes the machine's seats.
6. **Rate limits scale with players.** `RateGate.Allow(sender, kind, players)` multiplies the burst and the refill by the seats the sender holds (at least 1).
   - Four players on one machine send four players' cues.
   - Kicking is unchanged: 200 drops in 10 s.
7. **A seat given back mid-load isn't the machine leaving.** `OnlineSceneFlow.MachineLeft` is called only when the last `OnlinePlayer` of that machine goes (`OnlineGame.MachineGone`).
8. **Remote sounds** fade with the distance to the *nearest* of this machine's players.
9. **Gone:** the one-player-per-machine checks and messages (`OnlineGame.CanGoOnline`, `OnlineGame.ONE_PLAYER`, `LobbyRules.ONE_PLAYER`, the `localPlayers` parameter of `LobbyRules.Busy`), and `PlayerInstantiate.SetOnlineSeat` / `OnlineSeat`.

## Review Focus

1. **Two players on a client joining at once:** both end up seated, with their own controllers. Neither is lost in the leave-and-rejoin frame, and the menu player stays `hostPlayer`. *(Task 3: `TwoPlayersHere_MoveIntoTheSeatsTheyAreGiven_MenuPlayerFirst`.)*
2. **A seat arriving for a player who left meanwhile:** it's given back, not left empty and counted as a player who never readies. *(Task 3: `GiveSeat_WithNobodyToSit_ReturnsFalse`. Task 4: `OnlineGame` frees it; the two-editor check.)*
3. **A modified client:**
   - freeing another machine's seat or its own last one is ignored;
   - asking for seats in a flood is capped by `RpcKind.Seat`;
   - a machine with several seats can't name a seat it doesn't hold.

   *(Task 1: `FreeingAnotherMachinesSeat_OrItsLastSeat_IsIgnored` and the `Owns` grep. Task 2: `RateGateTests`.)*
4. **A client leaving with 3 seats** frees all three on the host, and the next joiner gets the lowest. *(Task 1: `AMachineLeaving_FreesAllItsSeats`.)*
5. **The countdown with an unseated player here:** the host starts the match, and this machine's unseated player is turned away instead of driving unseen. *(Task 4: `OnlineGame` on leaving the menus; Task 3: `TurningAwayUnseated_RemovesThemWithTheReason`.)*

---

### Task 1: Seats per machine on the host

**Files:**
- Modify:
  - `Assets/Scripts/Online/SeatTable.cs`
  - `Assets/Scripts/Online/OnlineSession.cs`
  - `Assets/Scripts/Online/OnlineMatch.cs`
  - `Assets/Scripts/Online/OnlinePlayer.cs`
  - `Assets/Scripts/Online/OnlineScooter.cs`
  - `Assets/Scripts/Online/RateGate.cs`: only the new `RpcKind.Seat` (5 burst, 1/s: the default branch).
- Test:
  - `Assets/Tests/Editor/SeatTableTests.cs`
  - create `Assets/Tests/Editor/CouchSeatsNetworkTests.cs` (port **7813**, empty scene, styled like `OnlinePlayersNetworkTests`).

**Interfaces:**
- Produces:
  - `SeatTable`:
    - `int Take(ulong clientId)`: always a *new* seat, the lowest free one, or -1 when full.
    - `bool Free(ulong clientId, int seat)`: frees one seat if that client holds it.
    - `int FreeAll(ulong clientId)`: how many seats it freed.
    - `bool Owns(ulong clientId, int seat)`: false for a seat outside 0–3.
    - `int CountOf(ulong clientId)`
    - `List<int> SeatsOf(ulong clientId)`, ascending.
    - `Count` and `Clear` stay. `SeatOf` is removed.
  - `OnlineSession`:
    - `public bool Owns(ulong clientId, int seat)` (host)
    - `public int SeatsHeldBy(ulong clientId)` (host)
    - `internal int SpawningSeat`: the seat being spawned, -1 otherwise.
    - `public void AskSeat()`:
      - host: seats another player of its own at once and spawns them, or raises `SeatRefused`;
      - client: `Match.AskSeat()`, and nothing while `Match` is null.
    - `public void FreeSeat(int seat)`: host: `FreeSeatOf(ServerClientId, seat)`; client: `Match.FreeSeat(seat)`.
    - `public event Action<string> SeatRefused`: this machine's ask was refused; carries `JoinRules.FULL` or `JoinRules.STARTED`.
    - `SeatOf(ulong)` is removed.
  - `OnlineMatch`:
    - `public void AskSeat()` → `SeatServerRpc()`
    - `public void FreeSeat(int seat)` → `FreeSeatServerRpc(int seat)`
    - `public event Action<ulong> SeatAsked`
    - `public event Action<ulong, int> SeatFreed`
    - `public void RefuseSeat(ulong machine, bool full)` → a targeted `SeatRefusedClientRpc(bool full, ClientRpcParams)`
    - `public event Action<string> SeatRefused` on the client, which maps `full` to `JoinRules.FULL`, else `JoinRules.STARTED`.
  - `RpcKind.Seat`, appended last.

- [ ] **Step 1: Write the failing tests.**

  `SeatTableTests`:
  - Replace `Take_ASeatedPlayerAgain_KeepsTheirSeat` with `Take_TheSameMachineAgain_GetsAnotherSeat`. The host takes 0; client 5 takes 1, then 2.
  - `Owns_OnlyTheMachinesOwnSeats`: client 5 owns 1 and 2, not 0, -1 or 4.
  - `Free_OneSeat_KeepsTheMachinesOthers`: free (5, 1), and 2 is still owned; `CountOf(5)` is 1; `Free(7, 2)` is false.
  - `FreeAll_FreesEveryOneOfAMachinesSeats`: returns 2; `Count` is 1; the next `Take` gets 1.
  - Update the other tests from `SeatOf` to `Owns`/`SeatsOf`.

  `CouchSeatsNetworkTests`: host direct on 7813 and a client, as in `OnlinePlayersNetworkTests`:
  - `ClientAskingASeat_SpawnsAnotherPlayerAndScooterForIt_Everywhere`:
    - after `client.AskSeat()`, both machines see 3 players and 3 scooters;
    - the client owns 2 players, in seats 1 and 2;
    - `host.Owns(clientId, 2)` and `host.SeatsHeldBy(clientId) == 2`.
  - `HostAskingASeat_SeatsItsSecondPlayerAtOnce`: in the same frame, `host.Players.Count == 2`, and the new one is the host's, in seat 1.
  - `FreeingASeat_DespawnsItEverywhere_AndTheNextAskGetsIt`.
  - `FreeingAnotherMachinesSeat_OrItsLastSeat_IsIgnored`: the client frees 0, then its only seat 1, and both machines still see 2 players after 1 s.
  - `AskingWhenFull_IsRefused_WithTheReason`:
    - the host asks twice, so it holds seats 0, 2 and 3, and the client holds 1;
    - the client asks;
    - a recorder on `client.SeatRefused` hears `JoinRules.FULL`, and there are still 4 players.
  - `AMachineLeaving_FreesAllItsSeats`: a client holding 2 seats leaves; the host sees 1 player; a new client gets seat 1.

  Every network test ends with `log.MachinesLeave()` and an empty `log.Problems`.

- [ ] **Step 2: Run them, and see them fail.**

  Run: `bash tools/run-tests.sh "SeatTableTests|CouchSeatsNetworkTests"`
  Expected: compile errors (`Owns`, `AskSeat` not defined), so `NO RESULTS`.

- [ ] **Step 3: Implement.**
  - **`Approve`:** still calls `seats.Take` once.
  - **`OnConnected`:** spawns a player and scooter for each of `seats.SeatsOf(clientId)`. The first is spawned as the player object; the others aren't.
  - **`Spawn`:** gains a `seat` parameter, and sets `SpawningSeat` around `InstantiateAndSpawn`. `OnlinePlayer` and `OnlineScooter` set `seat.Value = session.SpawningSeat` in `OnNetworkSpawn` on the server.
  - **`OnDisconnected`:** calls `seats.FreeAll`.
  - **`SetMatch` on the server:** subscribes to `SeatAsked` and `SeatFreed` (unsubscribe in `ClearMatch`). `SeatAsked` refuses through `Match.RefuseSeat` when `JoinRules.Refusal` gives a reason.
  - **`FreeSeatOf(machine, seat)`:**
    - does nothing unless `Owns(machine, seat) && seats.CountOf(machine) > 1`;
    - otherwise frees the seat, and despawns (`NetworkObject.Despawn(true)`) that seat's `OnlinePlayer` and `OnlineScooter`.
  - **Logs:**
    - `"Online: player " + id + " took another seat (" + seats.Count + " in)"`
    - `"Online: player " + id + " gave a seat back (" + seats.Count + " in)"`
  - **Client side:** `SetMatch` on a client subscribes to `Match.SeatRefused` and raises the session's `SeatRefused`. The refusal is just two fixed texts.
  - **"Its own seat" means any of its seats:**
    - `session.SeatOf(machine) == seat` becomes `session.Owns(machine, seat)`, and `!=` becomes `!Owns`;
    - the call sites are `OnlineCues.cs:98`, `OnlineOrders.cs:150`, `OnlineRespawns.cs:71`, `OnlineSteals.cs:72` and `OnlineTutorial.cs:68,83`;
    - verify that `grep -rn "SeatOf(machine\|SeatOf(Owner" Assets/Scripts` prints nothing.

- [ ] **Step 4: Run the tests.**

  Run: `bash tools/run-tests.sh "SeatTableTests|CouchSeatsNetworkTests|Online.*NetworkTests|MessageLimitsNetworkTests|NonsenseNetworkTests"`
  Expected: all pass. In particular, the existing "only for the sender's own seat" tests still refuse other seats.

### Task 2: Rate limits per player

**Files:**
- Modify:
  - `Assets/Scripts/Online/RateGate.cs`
  - `Assets/Scripts/Online/OnlineMatch.cs` (`Allowed`)
- Test: `Assets/Tests/Editor/RateGateTests.cs`

**Interfaces:**
- Consumes: `OnlineSession.SeatsHeldBy(ulong)` (Task 1).
- Produces: `public bool Allow(ulong sender, RpcKind kind, int players = 1)`. The burst and the refill are each multiplied by `Math.Max(1, players)`.

- [ ] **Step 1: Write the failing test.** `RateGateTests.Allow_AMachineWithThreePlayers_GetsThreeTimesTheAllowance`: with 3 players, 60 cues pass at once and the 61st is refused; 0.1 s later 3 more pass.
- [ ] **Step 2: Run it, and see it fail.** Run: `bash tools/run-tests.sh RateGateTests`. Expected: compile error, so `NO RESULTS`.
- [ ] **Step 3: Implement.** `Allowed` passes `session.SeatsHeldBy(sender)` (1 when there's no session).
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "RateGateTests|MessageLimitsNetworkTests"`. Expected: all pass, and the flood test still disconnects its one-seat sender.

### Task 3: This machine's players in the seats it holds

**Files:**
- Create: `Assets/Scripts/Online/CouchSeats.cs`, plus its `.meta`.
- Modify:
  - `Assets/Scripts/Player/PlayerInstantiate.cs`: replace `onlineSeat`/`SetOnlineSeat`/`OnlineSeat`, `AddPlayerReference`'s online branch, `MoveToOnlineSeat` and `RemovePlayerRef`.
  - `Assets/Scripts/Menu/MenuInteractions.cs:265`: B leaves the session only for the `hostPlayer`.
- Test:
  - create `Assets/Tests/Editor/CouchSeatsTests.cs` (pure);
  - modify `Assets/Tests/Editor/OnlineSeatTests.cs` and `Assets/Tests/Editor/KeyboardPlayerTests.cs:179,189`.

**Interfaces:**
- Produces:
  - `public static class CouchSeats`, with `public static int[] Plan(IReadOnlyList<int> localSlots, IReadOnlyCollection<int> heldSeats)`:
    - `localSlots` lists this machine's players' current slots, the menu player first, then slot order;
    - it returns each player's target seat, in the same order, -1 for unseated;
    - a player already in a held seat keeps it;
    - the rest take the remaining held seats in ascending order, in list order.
  - `PlayerInstantiate`:
    - `public bool IsOnline`: true from the first `GiveSeat` until `GoOffline`.
    - `public IReadOnlyCollection<int> OnlineSeats`
    - `public bool GiveSeat(int seat)`:
      - adds the seat, plans, and starts the moves;
      - returns whether a local player will sit in it;
      - if none will, the seat isn't kept, so the caller gives it back;
      - a seat outside 0–3 is false.
    - `public void LoseSeat(int seat)`: the seat's local player, if any, becomes unseated where they are.
    - `public void GoOffline()`
    - `public int UnseatedLocalCount`: local players in the roster outside a held seat. Players mid-move aren't in the roster, so they aren't counted.
    - `public int TurnAwayUnseated(string reason, int count)`:
      - removes up to `count` unseated local players, highest slot first;
      - shows `reason` with `ControllerPrompts.Instance.ShowHint(reason, OnlineGame.NOTICE_SECONDS)`;
      - returns how many it removed.
    - `public event Action<int> SeatGivenUp`: a local player in a held seat left with B. The seat is no longer held when it's raised.
  - **Joins while online (`IsOnline`), in `AddPlayerReference`:**
    - **A device moving seats** (`rejoinSeats: Dictionary<InputDevice, (int seat, bool host)>`): it joins at its seat (`JoinLocalAt`), and is the `hostPlayer` if it was before.
    - **Otherwise, outside `GameState.PlayerSelect`:** it's turned away silently.
    - **Otherwise, with 4 players in the roster:** it's turned away with the hint `JoinRules.FULL`.
    - **Otherwise:** it joins the lowest free slot (`JoinLocal`), unseated. The usual player-select setup applies; it's `hostPlayer` only if `LocalCount` was 0.
  - The `rejoining` field stays as the device mid-join, for the keyboard check.

- [ ] **Step 1: Write the failing tests.**

  `CouchSeatsTests`:
  - `Plan_PlayersAlreadyInHeldSeats_Stay`: slots [1, 3], held {1, 3} → [1, 3].
  - `Plan_TheMenuPlayerTakesTheFirstFreeSeat`: slots [2, 0], held {1, 3} → [1, 3].
  - `Plan_MorePlayersThanSeats_LeavesTheLastUnseated`: [0, 1], {2} → [2, -1].
  - `Plan_AStayingPlayersSeatIsntGivenToAnother`: [0, 2], {2, 3} → [3, 2].

  `OnlineSeatTests`, in the real menu scene, styled like the existing tests:
  - Update the existing tests: `SetOnlineSeat(n)` → `GiveSeat(n)`, `SetOnlineSeat(-1)` → `GoOffline()`.
    - The "another controller is turned away" assertion stays: the state is the title screen there. Its message says "outside player select".
  - `TwoPlayersHere_MoveIntoTheSeatsTheyAreGiven_MenuPlayerFirst`:
    1. Pad A joins at the title screen; go to player select; pad B presses A.
    2. `GiveSeat(2)`:
       - A is in 2 with pad A and is `hostPlayer`;
       - B is local and unseated (`UnseatedLocalCount == 1`).
    3. `GiveSeat(3)`:
       - B is in 3 with pad B and isn't `hostPlayer`;
       - `LocalCount == 2`;
       - A's camera rect equals `SplitScreenLayout.CalculateRects(2)[0]`.
  - `Online_ASecondPlayerLeaving_GivesTheirSeatBack`:
    1. With A in 1 and B in 3, B presses East in player select.
    2. A recorder on `SeatGivenUp` hears 3.
    3. `Roster[3]` is null, and `OnlineSeats` is {1}.
  - `GiveSeat_WithNobodyToSit_ReturnsFalse`: with A in 1, `GiveSeat(2)` is false, and `OnlineSeats` is {1}.
  - `TurningAwayUnseated_RemovesThemWithTheReason`:
    1. A is in 1, and B joins unseated in player select.
    2. `TurnAwayUnseated(JoinRules.FULL, 1)` returns 1; B's slot is free; the hint is `JoinRules.FULL`; A stays.

  `KeyboardPlayerTests`: `SetOnlineSeat(2)` → `GiveSeat(2)`; `SetOnlineSeat(-1)` → `GoOffline()`.

- [ ] **Step 2: Run them, and see them fail.** Run: `bash tools/run-tests.sh "CouchSeatsTests|OnlineSeatTests|KeyboardPlayerTests"`. Expected: compile errors, so `NO RESULTS`.

- [ ] **Step 3: Implement.**
  - `GiveSeat` and `LoseSeat` both re-plan.
  - A move:
    1. `LeaveLocal`: no `SeatGivenUp` (a move never leaves a held seat).
    2. Next frame, it rejoins with its device in `rejoinSeats`.
  - `RemovePlayerRef` (B) raises `SeatGivenUp` when the slot was held, after removing it from the held set.

- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "CouchSeatsTests|OnlineSeatTests|KeyboardPlayerTests|PlayerSlotTests|LocalMatchSmokeTest"`. Expected: all pass. The smoke test proves offline joins and leaves are unchanged.

### Task 4: The game asks for seats, and the one-player rules go

**Files:**
- Create: `Assets/Scripts/Online/SeatAsker.cs`, plus its `.meta`.
- Modify:
  - `Assets/Scripts/Online/OnlineGame.cs`
  - `Assets/Scripts/Online/OnlineAchievements.cs`
  - `Assets/Scripts/Steam/MatchFeats.cs:84`
  - `Assets/Scripts/Online/RemoteSound.cs:51-63`
  - `Assets/Scripts/Online/LobbyRules.cs` (drop `ONE_PLAYER`; `Busy(GameState state, bool online)`)
  - `Assets/Scripts/Online/OnlinePlay.cs` (`PlayOnline`, `AnswerRequest`, and the comment at `:128`)
  - `Assets/Scripts/Editor/OnlineTestMenu.cs:32-35,52-55`
- Test:
  - create `Assets/Tests/Editor/SeatAskerTests.cs`;
  - modify `AchievementsTests`, `RemoteSoundTests`, `LobbyRulesTests`, `OnlineGameTests` and `OnlinePlayNetworkTests`.

**Interfaces:**
- Consumes:
  - Task 1: `AskSeat`, `FreeSeat`, `SeatRefused`.
  - Task 3: `GiveSeat`, `LoseSeat`, `GoOffline`, `UnseatedLocalCount`, `TurnAwayUnseated`, `SeatGivenUp`.
- Produces:
  - `public class SeatAsker`:
    - `int Asking`
    - `int ToAsk(int unseated)`: returns `max(0, unseated - Asking)`, and adds it to `Asking` *before* returning, because the host's asks answer at once.
    - `void Answered()`: never below 0.
    - `void Reset()`
  - `public static bool MatchFeats.UnlocksHere(bool online, int seat, ICollection<int> ownSeats)`
  - `public static float RemoteSound.NearestDistance(Vector3 where, IEnumerable<Vector3> listeners)`: `float.PositiveInfinity` when empty. `VolumeAt` uses this machine's local players' balls, and gives 1 when there are none.
  - `public static bool OnlineGame.MachineGone(IEnumerable<ulong> ownersLeft, ulong machine)`
  - **`OnlineGame` behaviour:**
    - `localPlayer` becomes `List<OnlinePlayer> localPlayers`. `ShareLocalChoices` shares each one's choices from the roster slot at its seat.
    - An owned player spawning:
      - calls `asker.Answered()` if `Asking > 0`, then `players.GiveSeat(seat)`;
      - if that's false, `session.FreeSeat(seat)`.
    - An owned player despawning: `players.LoseSeat(seat)`.
    - `sceneFlow.MachineLeft(owner)` runs only when `MachineGone` says so, over the session's remaining players' owners.
    - **`Update`:** when `session.Match != null && players.IsOnline`, it calls `session.AskSeat()` `asker.ToAsk(players.UnseatedLocalCount)` times.
    - **`SeatRefused(reason)`:** `asker.Answered()`, then `players.TurnAwayUnseated(reason, 1)`.
    - **`SeatGivenUp(seat)`:** `session.FreeSeat(seat)`.
    - **`OnStateApplied`,** when the state isn't `LobbyRules.InTheMenus`: `players.TurnAwayUnseated(JoinRules.STARTED, Constants.MAX_PLAYERS)`.
    - **`OnRoleChanged(Offline)`:** `players.GoOffline()`, `asker.Reset()`, `localPlayers.Clear()`.
    - **`OnDestroy`:** unsubscribes all of the above.
  - **Removed:** `OnlineGame.CanGoOnline`, `OnlineGame.ONE_PLAYER` and `LobbyRules.ONE_PLAYER`.

- [ ] **Step 1: Write the failing tests.**
  - `SeatAskerTests`:
    - `ToAsk_OncePerUnseatedPlayer_NotAgainWhileWaiting`: `ToAsk(2)` 2; `ToAsk(2)` 0; `Answered()`; `ToAsk(1)` 0; `ToAsk(2)` 1.
    - `Answered_WithNothingAsked_StaysAtZero`.
    - `Reset_ForgetsTheAsks`.
  - `AchievementsTests.UnlocksHere_OfflineEverySeat_OnlineAnyOfThisMachines`:
    - offline, seat 3 with {} is true;
    - online, {1, 3}: seat 3 true, seat 0 false.
  - `RemoteSoundTests.NearestDistance_IsToTheClosestListener`:
    - listeners (0,0,0) and (100,0,0), where (90,0,0): 10;
    - no listeners: +∞.
  - `OnlineGameTests.MachineGone_OnlyWhenNoneOfItsPlayersRemain`:
    - owners {2, 5}: machine 5 is false, machine 7 is true.
  - `LobbyRulesTests`: the two-player busy assertion becomes `Assert.IsNull(LobbyRules.Busy(GameState.PlayerSelect, false))`; update the other `Busy` calls.
  - `OnlinePlayNetworkTests` (`:105-125`): with two local players, Y now hosts.
    - `lobbies.Creates` has 1.
    - The host session has 2 players, both the host's, in seats 0 and 1.
    - Both locals are in the roster at 0 and 1 (wait up to 5 s).
    - Remove the "Player 2 leaves" part.
- [ ] **Step 2: Run them, and see them fail.** Run: `bash tools/run-tests.sh "SeatAskerTests|AchievementsTests|RemoteSoundTests|OnlineGameTests|LobbyRulesTests|OnlinePlayNetworkTests"`. Expected: compile errors, so `NO RESULTS`.
- [ ] **Step 3: Implement**, as in Interfaces above. Then `grep -rn "ONE_PLAYER\|CanGoOnline\|SetOnlineSeat\|OnlineSeat\b" Assets docs` finds no code (docs are Task 5's).
- [ ] **Step 4: Run the tests.** Run: `bash tools/run-tests.sh "SeatAskerTests|AchievementsTests|RemoteSoundTests|OnlineGameTests|LobbyRulesTests|OnlinePlayNetworkTests|OnlineTestMenuTests|Online.*NetworkTests"`. Expected: all pass.

### Task 5: Docs, editor chores, full suite

**Files:**
- `docs/online.md`:
  - a "Phase 3J: online plus couch" section (what players see, then "How it works", as in 3G–3I);
  - Phase 3B's "One player per machine" bullets updated;
  - Phase 3D's "One player per machine: with two players here, Y says…" removed;
  - Known limits: drop "only a machine's first controller can take a seat online". Add:
    - a 2nd–4th player sits down a round trip after joining (on the host, at once);
    - nobody joins mid-match;
    - a machine's other players are turned away when the match is full.
  - The two-editor section: add "two pads in one editor: both get seats".
- `docs/testing.md`: the new test classes, and next free port 7814.
- `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md:301`: tick Task 3.9 with a Phase 3J note.
- The handoff: status, the "What's left" item 3 gone, and the deferred minors.
- `EDITOR-TODO.md`:
  - a short section: import Phase 3J;
  - the two-editor check: two pads in the client editor, both seated, both drive, B on the second gives the seat back;
  - the two-PC check with two pads on one PC.
- Memory: `doa-steam-revival.md`, a Phase 3J block.

- [ ] **Step 1: Write the docs.**
- [ ] **Step 2: Run the full suite in the background.** Run: `bash tools/run-tests.sh`. Expected: all pass (629 plus this phase's new tests). Then the final review: a fresh Sonnet reviewer over the uncommitted diff, with this plan, its Rulings and Review Focus.

# Phase 3G — Steals, Clashes and Respawns Online Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Online players steal from and clash with each other as in a local match, and the host decides every hit, so every machine sees the same result. Two players who fall in the water never rise on the same respawn point.

**Architecture:**
- **The machine that drives the boosting scooter sees the hit, and the host decides it.**
  - A scooter asks when it boosts into another machine's scooter, or starts a boost while touching one. A client asks through `StealSync` → `OnlineMatch.AskSteal`; the host's own player goes straight to the host's judge.
  - The host checks that the asker speaks for its own seat. Then it judges with its own view (`StealRules`): both players are there and not respawning, they're within reach, and that pair hasn't been hit in the last 2 s. So when both machines ask about one bump, the first request wins and the second does nothing.
  - It's a steal when the host sees the victim not boosting, a clash when it sees them boosting, as in a local match.
- **A steal's effects on orders travel as Phase 3E order changes.** The stolen order moving to the thief is one new change (`OrderChange.Steal`). The victim dropping the rest and spinning out is Phase 3E's Drop. Every client replays both (`OnlineOrders`).
- **Bounces go to their owners.** The host sends each hit to every machine (`PlayerHit`), and each machine bounces only its own player: a robbed player away from the thief, both players in a clash.
- **Respawn points are the host's.**
  - A client whose player falls in the water asks (`RespawnSync` → `OnlineMatch.AskRespawn`), then waits, hidden, for the host's point. After 2 s without one, it picks its own as today.
  - The host picks the nearest point nobody is rising from and holds it for the respawn. It drops the player's orders on the point's spots and answers (`OnlineMatch.SendRespawn`).
  - The host's own player picks as today, from the points nobody holds.
- **`OnlineSteals`** and **`OnlineRespawns`** are the glue. `OnlineGame` adds them, like `OnlineTutorial`. On a client, hits and respawn answers wait with the host's other messages (`HostQueue`).

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 (ServerRpc, ClientRpc) · Unity Transport 1.5.0 (its simulator adds the 150 ms) · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- **Task 3.6:**
  - "Steal: the attacker's client detects the overlap while boosting → `RequestStealServerRpc(victimSlot)`; the host checks distance ≤ trigger radius + lag tolerance, attacker boosting, victim not boosting, per-pair cooldown → first valid request wins → result replicates."
  - "Clash: host → `ClientRpc` to both owners → each calls `BounceOff` locally."
  - "Respawn: the owner detects water → `ServerRpc`; the host drops that player's orders and picks a `RespawnPoint` → `ClientRpc` to the owner runs the respawn animation. (Moves `RespawnManager.GetRespawnPoint` and `RespawnPoint.InUse` to the host.)" Its Phase 3E note: the host already drops the orders; "Picking the point stays here."
  - "Tests: EditMode tests for the steal arbitration rules (pure C# class); 2 clients stealing simultaneously at 150 ms → one steal, same holder everywhere."
- **Review focus 5:** "Two players steal from each other at the same moment online → exactly one steal wins, everyone sees the same holder."

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**. A bump is a Steam API change: it goes to `main`, after asking.
- **Players:** online is one player per machine until Task 3.9.
- **Local split-screen must behave exactly as before,** apart from the two 2024 bugs in Ruling 8. `LocalMatchSmokeTest` passes after every task.
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
- **Line endings:** `Order.cs` is `i/crlf`: edit it with the Edit tool only. Never `sed -i` on scripts.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]` (the mirror). Game-scene network tests take a minute or two each, so run long runs in the background.
  - Check `ListAgents` before a long run.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
- **Play Mode tests:**
  - No lambda captures a test method's local: after `EnterPlayMode` even assigning one throws. Use static helpers, and recorder classes subscribed as method groups.
  - Wait by time, never by frame count.
  - Two-machine tests call `log.MachinesLeave()` before the first machine leaves.
- **Tests can't see `internal` members.** Whatever tests call is `public`; they read private state through `Reflect`.
- **The game has its own global `SceneManager` class:** write `UnityEngine.SceneManagement.SceneManager` for Unity's.
- **Branch:** `steam-phase1a`. Phase 3F is committed (`5e9804d1`). The saved network prefab IDs aren't yet, nor are three files the editor re-saved. Commit them first, so Phase 3G stays one commit.

## Rulings (decided while planning)

1. **The host trusts the asker's own boost, and checks the victim's.**
   - The roadmap has the host check "attacker boosting". But the asker's flags byte travels apart from its request and can arrive after it, so the host would refuse real steals.
   - A machine already decides its own scooter's driving, and it asks only while its player boosts.
   - Cost if wrong: a modified client could steal without boosting.
2. **One hit per pair per 2 s, whichever machine asks first** (`StealRules.PAIR_COOLDOWN`).
   - Two machines asking about one bump make one hit: the first request the host judges wins, and the other does nothing. That's the roadmap's "first valid request wins".
   - A boost lasts 1.6 s, and a robbed player spins out for 1 s.
   - Cost if wrong: online, a second hit between the same two players within 2 s doesn't count.
3. **Reach: 50 m between the two balls, as the host sees them** (`StealRules.REACH`).
   - The trigger reaches about 3.2 m. But on a 150 ms connection, two scooters meeting head-on at top speed can be 35 m apart on the host when the request lands.
   - The check stops stale requests, such as one about a scooter that has since respawned.
   - Cost if wrong: a modified client could steal from up to 50 m away.
4. **Steal or clash follows the host's view of the victim.** If the victim started boosting less than a round trip before the bump, the host may call it a steal where a local match would call a clash. Every machine sees the same result.
5. **A steal is two order changes plus a hit.**
   - The order moving to the thief is one new change, `OrderChange.Steal`, and the grab animation and whoosh come with it.
   - The victim dropping the rest and spinning out is Phase 3E's Drop.
   - The bounce isn't an order change. A `PlayerHit` reaches every machine, and each bounces only its own player: the roadmap's "ClientRpc to both owners".
   - Rejected: replaying the whole steal on clients, because the drop's heights must be the host's.
6. **The host picks respawn points, and a client waits for one.**
   - A client's scooter hides and freezes at once, as today. It waits up to 2 s (`Respawn.HOST_ANSWER_WAIT`) before its wisp rises.
   - With no answer (the host left, or the answer is very late), it picks its own point. It asks the host to drop its orders there, as in Phase 3E.
   - The host picks for its own player at once.
   - Rejected: each machine picks and tells the others. Two players falling within a round trip could still take one point.
   - Cost if wrong: on a client, the rise starts a round trip later.
7. **The host holds another machine's player's point for the respawn, plus 1 s** (`Respawn.Duration` + `OnlineRespawns.HOLD_MARGIN`). The host doesn't hear when the respawn ends. Cost if wrong: a point stays taken up to a second longer than it needs to.
8. **Two 2024 bugs are fixed, in local play too:**
   - `GetRespawnPoint` handed out the first point even while someone rose from it, so two players falling near it rose on top of each other.
   - A player robbed of the golden order stayed slowed (×0.95) for the rest of the golden round, because a steal never cleared their golden flag.
   - Cost if wrong: none expected. Only those two cases change.
9. **Not in this phase:**
   - Other machines don't hear the thief's whoosh or see a respawn's gravestone. Those are one-shots (Task 3.7).
   - No predicted bounce: on a client, a clash's bounce comes a round trip after the bump. The balls' own collision is immediate.

## Review Focus

- **Two machines ask to steal from each other at the same moment** → one steal, the same on every machine. (Task 4 `Hosting_…`; Task 1 `TheSamePairAgainSoon_…`.)
- **A steal request for a seat that isn't the sender's** → nothing happens. (Task 4 `Hosting_…`.)
- **A steal against a player who is respawning** → nothing happens: their scooter is hidden and holds nothing. (Task 1 `ARespawningPlayer_…`; Task 4 `Hosting_…`, with a hidden scooter.)
- **Two players fall in the water at the same place and time** → they rise on different points, each with their orders on their own point. (Task 5 `APointHeld…`; Task 6 `Hosting_…`.)
- **The host never answers a respawn** (it left, or the answer is late) → the player isn't stuck in the water: they rise on their own point after 2 s. (Task 6 `Joining_…`.)

## Facts (probed 2026-09-29)

- **The scooter** (`PlayerAvatar.prefab`):
  - "Control" (layer 7) holds `OrderHandler`, `BallDriving` and a trigger `CapsuleCollider`: radius 2.15 m, height 4.3 m, vertical, centre (0, 1.62, −0.42). It has no Rigidbody. `BallDriving` keeps it 0.97 m below the ball's centre.
  - "Ball Of Fun" (layer 10 + seat) holds a `SphereCollider` (radius 1 m), a Rigidbody (mass 300, drag 1.4) and `Respawn`.
  - When a Control's trigger meets another scooter's ball, that Control's `OrderHandler.OnTriggerEnter` fires; the balls are then up to about 3.2 m apart. Another machine's ball is kinematic, and triggers still fire.
- **The local rules today:**
  - `OnTriggerEnter` notes the touch on the other scooter (`PlayerTouching`). A boosting scooter steals from one that isn't boosting, and clashes with one that is. A boost that starts while touched (`AttemptSteal`, on `BallDriving.OnBoostStart`) does the same.
  - A steal (`StealOrder`): the thief gets the victim's best order if they have room, with a grab animation and a whoosh. The victim drops the rest, spins out for 1 s (`DropEverything` → `GotHit` → `SpinOut`) and bounces away from the thief.
  - A clash: each scooter bounces off the other's ball, unless it's phasing (`BallClash`).
  - `BounceOff` always pushes with `clashForce`, an impulse of 4000, whatever force it's given.
- **Online today (Phase 3C):**
  - `OnTriggerEnter` and `AttemptSteal` return on a client and for another machine's scooter, so nobody steals across machines. `RemoteAvatar.IsRemote` tells which parts belong to another machine's scooter.
  - Another machine's scooter shows its owner's `Boosting`, `Phasing` and hidden rider (`DriveFlags`, applied each frame by `OnlineDriving` → `BallDriving.ShowRemote`). The flags travel in an owner-written NetworkVariable, apart from RPCs, so they can arrive after a request sent later.
  - While another machine's rider is hidden, its ball's collider is off (`Respawn.ShowRemote`).
- **Boosts:** a boost lasts 1.6 s (`boostDuration`). `BallDriving.AutoBoost()` starts one from code (for tests), without `OnBoostStart`.
- **Respawns** (`Respawn`, on the ball):
  - Water (the trigger, or `BallDriving`'s raycast) calls `StartRespawnCoroutine`. The death sound plays and the scooter freezes. The rider hides (`RiderHidden`) and the ball's collider goes off; `IsRespawning` stays true until the end.
  - `RespawnPlayer` picks a point (`RespawnManager.GetRespawnPoint(lastGroundedPos)`, then `InUse = true`). It drops the orders on the point's two spots; since Phase 3E, a client asks the host to (`AskDrop`).
  - It then turns the scooter to face the point and places a gravestone. The wisp and ball go to the casket, 3 m under the point, and the ball lifts to 3 m above it. Finally `InitPoint` frees the point.
  - The scooter's timings: `wispRiseTime` 0.5 s + `wispToCasketTime` 0.8 s + `liftDuration` 0.7 s = 2.0 s.
- **`RespawnManager`** (one per match scene):
  - It keeps its points in child order: 63 in "Design Scene(Main)", 67 in "FinalAreaScene".
  - `closeEnough` is 0 in the game scene (10 by default).
  - `GetRespawnPoint` returns the nearest point not in use. But it starts from the first point without checking whether it's in use (a 2024 bug).
- **The golden order slows its holder** (`HasGoldenOrder` → `goldenOrderMultiplier` 0.95):
  - `Order.Pickup` sets the flag.
  - A drop's landing, a delivery and `EraseGoldWithoutDelivering` clear it.
  - A steal (`LoseOrder` → `RemovePlayerHolding`) doesn't clear it (a 2024 bug).
- **Another machine's `SoundPool` is off** and plays nothing, so the thief's whoosh is heard only on the thief's own machine.
- **Test tools:**
  - `session.Direct.SetDebugSimulatorParameters(delayMs, jitterMs, dropPercent)`, called before Host or Join, adds delay in the editor, as Bad Connection does.
  - `OnlinePlayersNetworkTests` already runs four sessions in one process.

## File Structure

- Create in `Assets/Scripts/Online/`:
  - `StealRules.cs`: the host's rules for a hit (`HitVerdict`).
  - `StealSync.cs`: a machine's steal requests on their way out of the game code.
  - `PlayerHit.cs`: a steal or clash as it travels (`HitKind`).
  - `OnlineSteals.cs`: the glue for steals and clashes.
  - `RespawnSync.cs`: a client's respawn requests on their way out of the game code.
  - `OnlineRespawns.cs`: the glue for respawns.
- Modify:
  - `Assets/Scripts/Player/OrderHandler.cs`: online hits; `StealFrom`, `TakeOrderFrom`, `BounceFrom`, `ClashWith`.
  - `Assets/Scripts/World/Order.cs` (`i/crlf`): `RemovePlayerHolding` clears the golden flag.
  - `Assets/Scripts/Online/OrderChange.cs`: the `Steal` kind and `Seat2`.
  - `Assets/Scripts/Online/OnlineOrders.cs`: replays a `Steal`.
  - `Assets/Scripts/Online/OnlineMatch.cs`: steal requests, hits, respawn requests and answers.
  - `Assets/Scripts/Online/OnlineGame.cs`: adds `OnlineSteals` and `OnlineRespawns`; hits and respawn answers wait with the host's messages.
  - `Assets/Scripts/Online/RemoteAvatar.cs`: the `IsRemote` summary.
  - `Assets/Scripts/Player/Respawn.cs`: online, a client asks the host; `RiseAt`, `Duration`.
  - `Assets/Scripts/Management/RespawnManager.cs`: skips the first point when it's in use; `IndexOf`, `PointAt`.
  - `Assets/Scripts/World/RespawnPoint.cs`: `HoldFor`.
- Tests, all under `Assets/Tests/Editor/`:
  - Create:
    - `StealRulesTests`, `PlayerHitTests`, `RespawnPointsTests`.
    - `StealMessagesNetworkTests` (port 7802) and `RespawnMessagesNetworkTests` (port 7803).
    - `OnlineStealsNetworkTests` (port 7804) and `OnlineRespawnsNetworkTests` (port 7805).
  - Modify: `OrderChangeTests`, `AuthorityGateTests`, `RemoteScooterRulesTests`, `OrderRulesTests`, and a comment in `OnlineOrdersNetworkTests`.
- Docs: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`.

---

### Task 1: The host's steal rules

**Files:**
- Create: `Assets/Scripts/Online/StealRules.cs`
- Test: `Assets/Tests/Editor/StealRulesTests.cs`

**Interfaces:**
- Produces:

```csharp
public enum HitVerdict { None, Steal, Clash }

/// The host's rules for one player boosting into another, online (a plain C# class)
public class StealRules
{
    public const float PAIR_COOLDOWN = 2f; // s: one hit per pair, whichever machine asks first (Ruling 2)
    public const float REACH = 50f;        // m between the two balls, as the host sees them (Ruling 3)

    public HitVerdict Judge(int attacker, int victim, float now, float distance, bool victimBoosting, bool eitherRespawning);
    public static bool Counts(GameState state); // true in Tutorial, Begin, MainLoop and FinalPackage
}
```

- `Judge` returns `None` in these cases, and a `None` never records anything:
  - the same seat twice, or a seat outside `0..Constants.MAX_PLAYERS - 1`;
  - `eitherRespawning`;
  - `distance > REACH`;
  - that pair (either way round) was judged `Steal` or `Clash` less than `PAIR_COOLDOWN` before `now`.
- Otherwise it records the pair's time. It returns `Clash` when `victimBoosting`, and `Steal` otherwise.

- [ ] **Step 1: Write the failing tests** (`SetUp`: `rules = new StealRules()`)

```csharp
[Test] public void BoostingIntoAPlayerWhoIsnt_Steals()
{ Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 1, 10f, 3f, false, false)); }

[Test] public void BoostingIntoABoostingPlayer_Clashes()
{ Assert.AreEqual(HitVerdict.Clash, rules.Judge(0, 1, 10f, 3f, true, false)); }

[Test] public void TheSamePairAgainSoon_CountsForNothing_EitherWayRound()
{   // both machines asking about one bump
    rules.Judge(0, 1, 10f, 3f, false, false);
    Assert.AreEqual(HitVerdict.None, rules.Judge(1, 0, 10.2f, 3f, false, false), "the other machine's request");
    Assert.AreEqual(HitVerdict.None, rules.Judge(0, 1, 10f + StealRules.PAIR_COOLDOWN - 0.1f, 3f, true, false));
}

[Test] public void TheSamePairAfterTheCooldown_CountsAgain()
{
    rules.Judge(0, 1, 10f, 3f, false, false);
    Assert.AreEqual(HitVerdict.Steal, rules.Judge(1, 0, 10f + StealRules.PAIR_COOLDOWN, 3f, false, false));
}

[Test] public void AnotherPair_IsntHeldUp()
{
    rules.Judge(0, 1, 10f, 3f, false, false);
    Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 2, 10.1f, 3f, false, false));
    Assert.AreEqual(HitVerdict.Steal, rules.Judge(2, 1, 10.2f, 3f, false, false));
}

[Test] public void TooFarApartForTheHost_CountsForNothing_AndDoesntHoldThePairUp()
{
    Assert.AreEqual(HitVerdict.None, rules.Judge(0, 1, 10f, StealRules.REACH + 1f, false, false));
    Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 1, 10.1f, 3f, false, false));
}

[Test] public void ARespawningPlayer_CantBeHit()
{
    Assert.AreEqual(HitVerdict.None, rules.Judge(0, 1, 10f, 3f, false, true));
    Assert.AreEqual(HitVerdict.Steal, rules.Judge(0, 1, 10.1f, 3f, false, false), "not held up");
}

[Test] public void NobodyHitsThemselves_OrAnEmptySeat()
{
    Assert.AreEqual(HitVerdict.None, rules.Judge(1, 1, 10f, 0f, false, false));
    Assert.AreEqual(HitVerdict.None, rules.Judge(-1, 1, 10f, 3f, false, false));
    Assert.AreEqual(HitVerdict.None, rules.Judge(0, Constants.MAX_PLAYERS, 10f, 3f, false, false));
}

[Test] public void HitsCount_OnlyWhilePlayersDrive()
{
    GameState[] driving = { GameState.Tutorial, GameState.Begin, GameState.MainLoop, GameState.FinalPackage };
    foreach (GameState state in (GameState[])System.Enum.GetValues(typeof(GameState)))
        Assert.AreEqual(System.Array.IndexOf(driving, state) >= 0, StealRules.Counts(state), state.ToString());
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh StealRulesTests`
Expected: `NO RESULTS` with `error CS0246` (`StealRules`, `HitVerdict`).

- [ ] **Step 3: Implement `StealRules`** (and the meta). Store each pair's time under an order-free key, e.g. `min * Constants.MAX_PLAYERS + max`.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh StealRulesTests`
Expected: `tests: 9 total, 9 passed`.

- [ ] **Step 5: No commit.**

---

### Task 2: Steals in the game code

**Files:**
- Create: `Assets/Scripts/Online/StealSync.cs`
- Modify:
  - `Assets/Scripts/Online/OrderChange.cs`
  - `Assets/Scripts/Player/OrderHandler.cs`
  - `Assets/Scripts/World/Order.cs` (`i/crlf`: Edit tool only)
  - `Assets/Scripts/Online/RemoteAvatar.cs` (summary only)
- Test: modify `OrderChangeTests`, `AuthorityGateTests`, `RemoteScooterRulesTests` and `OrderRulesTests`.

**Interfaces:**
- Consumes: `OrderSync.SeatOf`, `OrderSync.MayChange`, `OrderSync.Change` (Phase 3E), and `GameAuthority`.
- Produces:

```csharp
public static class StealSync
{
    public static event Action<int, int> Asked; // online: this machine's player (the attacker's seat), boosting, hit another (the victim's seat)
    public static void Ask(int attacker, int victim);
    public static void Reset();                 // tests
}

// OrderChangeKind gains Steal (added last)
// OrderChange gains:
public int Seat2;                                            // Steal: the victim's seat. -1 in every other change (each factory sets it)
public static OrderChange Steal(int order, int thief, int victim); // Seat = the thief, Seat2 = the victim

// OrderHandler
public void StealFrom(OrderHandler victim);                 // offline and on the host only (GameAuthority.IsAuthority), in this order:
                                                            // TakeOrderFrom the victim's best order if this player has room;
                                                            // victim.DropEverything at the victim's racks (they spin out);
                                                            // SetDrivingIndicators
public void TakeOrderFrom(OrderHandler victim, Order order); // gated on OrderSync.MayChange, as one Steal change: the steal animation
                                                             // trigger, PlayOrderTheft, victim.LoseOrder(order), then AddOrder(order)
public void BounceFrom(OrderHandler thief);                 // this player's BallDriving.BounceOff(thief.transform.position, ClashForce * 0.5f)
public void ClashWith(OrderHandler other);                  // Clash?.Invoke(other)
```

- **`OnTriggerEnter`:**
  - It finds the other handler as today, and notes the touch (`otherHandler.PlayerTouching = this`) on every machine.
  - Then it returns for another machine's scooter (`RemoteAvatar.IsRemote(this)`).
  - A boosting scooter hits (`Hit(otherHandler)`), then both touches clear, as today.
  - No authority gate.
- **`AttemptSteal`:** it returns for another machine's scooter. With someone touching, it calls `Hit(playerTouching)`. No authority gate.
- **`private void Hit(OrderHandler other)`:**
  - Online (`GameAuthority.IsOnline`): `StealSync.Ask(OrderSync.SeatOf(this), OrderSync.SeatOf(other))`, and nothing else.
  - Offline: today's rules. `StealOrder(other)` when the other player isn't boosting; otherwise `ClashWith(other); other.ClashWith(this)`.
- **`StealOrder`** (offline) becomes `StealFrom(victim)`, then `victim.BounceFrom(this)`.
- **`Order.RemovePlayerHolding`:** when the holder loses the golden order, it sets `playerHolding.HasGoldenOrder = false`, before clearing `playerHolding`.
- **`RemoteAvatar.IsRemote`'s summary** drops "until steals are shared". Another machine's scooter doesn't fall in water, steal or clash here: its own machine asks the host (`OnlineSteals`).

- [ ] **Step 1: Write the failing tests**

`OrderChangeTests`: `EveryKind` gains `OrderChange.Steal(11, 0, 2)`. A new test:

```csharp
[Test] public void Steal_CarriesTheOrder_TheThief_AndTheVictim()
{
    OrderChange steal = OrderChange.Steal(11, 0, 2);
    Assert.AreEqual(OrderChangeKind.Steal, steal.Kind);
    Assert.AreEqual(11, steal.Order);
    Assert.AreEqual(0, steal.Seat, "the thief");
    Assert.AreEqual(2, steal.Seat2, "the victim");
    Assert.AreEqual(-1, OrderChange.Pickup(11, 0).Seat2, "only a steal has a second seat");
}
```

`AuthorityGateTests` (`TearDown` also calls `StealSync.Reset()`):
- `OrderHandler_TouchingAnotherPlayer_IsNoted_OnlyWhereTheRulesAreDecided` becomes `OrderHandler_TouchingAnotherPlayer_IsNoted_OnEveryMachine`. It keeps the three roles and asserts `Assert.AreSame(me, other.PlayerTouching)`.
- The clash test becomes:

```csharp
[TestCase(NetworkRole.Offline, 2, 0)]
[TestCase(NetworkRole.Host, 0, 1)]
[TestCase(NetworkRole.Client, 0, 1)]
public void OrderHandler_BoostingIntoABoostingPlayer_ClashesAtOnceOffline_OnlineAsksTheHost(NetworkRole role, int expectedClashes, int expectedAsks)
// as today, plus: int asks = 0; StealSync.Asked += (attacker, victim) => asks++;
// me.AttemptSteal(); Assert.AreEqual(expectedClashes, clashes); Assert.AreEqual(expectedAsks, asks);
```

`RemoteScooterRulesTests`:
- **`SetUp`:** a `PlayerInstantiate` from `objects`, set with `Reflect.SetSingleton`, so `OrderSync.SeatOf` has seats.
- **`TearDown`** also sets that singleton back to `null`, sets `GameAuthority.Role = Offline` and calls `StealSync.Reset()`.
- **`Scooter(bool remote)`:**
  - Control also gets a `BallDriving`, set as the `OrderHandler`'s `ball` (`Reflect.SetField(handler, "ball", …)`).
  - The root joins the roster with `players.Roster.JoinRemoteAt(root, (ulong)seat, seat)`: seat 0 when remote, seat 1 otherwise (a client's view). No test builds two of one kind.
- **Helpers:** `Boost(GameObject scooter)`: `Reflect.SetField(scooter.GetComponentInChildren<BallDriving>(), "boosting", true)`. And `List<Vector2Int> asks`, filled by `StealSync.Asked += (a, v) => asks.Add(new Vector2Int(a, v))` in `SetUp`.
- **`AnotherMachinesScooter_AndALocalOne_DontTouchEachOtherHere`** becomes:

```csharp
[Test] public void AnotherMachinesScooter_TouchingThisMachinesOne_IsNotedHere_ForThisOnesBoost()
{
    GameObject remote = Scooter(true), local = Scooter(false);
    OrderHandler remoteHandler = remote.GetComponentInChildren<OrderHandler>();
    Reflect.Invoke(remoteHandler, "OnTriggerEnter", local.GetComponentInChildren<SphereCollider>());
    Assert.AreSame(remoteHandler, local.GetComponentInChildren<OrderHandler>().PlayerTouching,
        "its own machine never tells this one: this machine notes it");
}
```

- **New tests:**

```csharp
[TestCase(NetworkRole.Host)]
[TestCase(NetworkRole.Client)]
public void ThisMachinesScooter_BoostingIntoAnotherMachines_AsksTheHost_WithBothSeats(NetworkRole role)
{
    GameAuthority.Role = role;
    GameObject remote = Scooter(true), local = Scooter(false);
    Boost(local);
    Reflect.Invoke(local.GetComponentInChildren<OrderHandler>(), "OnTriggerEnter", remote.GetComponentInChildren<SphereCollider>());
    CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, asks, "this machine's player hit seat 0's: the host decides");
}

[Test] public void ThisMachinesScooter_StartingABoostWhileTouched_AsksTheHost()
{
    GameAuthority.Role = NetworkRole.Client;
    GameObject remote = Scooter(true), local = Scooter(false);
    OrderHandler localHandler = local.GetComponentInChildren<OrderHandler>();
    localHandler.PlayerTouching = remote.GetComponentInChildren<OrderHandler>();
    localHandler.AttemptSteal();
    CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, asks);
}

[Test] public void AnotherMachinesScooter_BoostingIntoThisMachinesOne_AsksNothingHere()
{
    GameAuthority.Role = NetworkRole.Client;
    GameObject remote = Scooter(true), local = Scooter(false);
    Boost(remote); // its owner boosts (ShowRemote)
    Reflect.Invoke(remote.GetComponentInChildren<OrderHandler>(), "OnTriggerEnter", local.GetComponentInChildren<SphereCollider>());
    Assert.IsEmpty(asks, "its own machine asks");
}
```

- The class summary: another machine's scooter doesn't fall in water, steal or clash here. A scooter driven here asks the host when it hits one.

`OrderRulesTests` (`TearDown` also sets the `PlayerInstantiate` singleton back to `null`):

```csharp
[Test] public void TakeOrderFrom_OnAClient_WaitsForTheHost()
{
    GameAuthority.Role = NetworkRole.Client;
    OrderHandler thief = NewPlayer("Player 1"), victim = NewPlayer("Player 2");
    Order order = objects.Add<Order>();
    Reflect.SetField(victim, "order1", order);
    thief.TakeOrderFrom(victim, order);
    Assert.AreSame(order, Reflect.GetField(victim, "order1"), "the host's Steal moves it");
    Assert.IsFalse(thief.HasOrder);
}

[Test] public void RemovePlayerHolding_TheGoldenOrder_NoLongerSlowsItsLastHolder()
{   // a steal takes it (2024: the robbed player stayed slowed for the rest of the golden round)
    Reflect.SetSingleton(objects.Add<PlayerInstantiate>()); // the compass markers look for this machine's players
    OrderHandler robbed = NewPlayer("Player 1");
    robbed.HasGoldenOrder = true;
    Order golden = GoldenOrderHeldBy(robbed);
    golden.RemovePlayerHolding();
    Assert.IsFalse(robbed.HasGoldenOrder);
}
```

- `GoldenOrderHeldBy(OrderHandler player)` builds an `Order` with:
  - `value` = `Golden`;
  - an `arrow` and an `orderMeshObject` (bare GameObjects);
  - a `compassMarker` (a `CompassMarker` from `objects`);
  - `ogMeshRot` = `Quaternion.identity` (`Awake` sets these in a real order);
  - `playerHolding` = the player.

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "OrderChangeTests|AuthorityGateTests|RemoteScooterRulesTests|OrderRulesTests"`
Expected: `NO RESULTS` with `error CS0117`/`CS1061` (`Steal`, `Seat2`, `StealSync`, `TakeOrderFrom`).

- [ ] **Step 3: Implement `StealSync`** (and the meta), **`OrderChange.Steal`, the `OrderHandler` changes, the golden flag (Edit tool) and the `RemoteAvatar` summary.** `ToString` for a steal: `"Steal " + Order + " seat " + Seat + " from seat " + Seat2`.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "OrderChangeTests|AuthorityGateTests|RemoteScooterRulesTests|OrderRulesTests|LocalMatchSmokeTest"`
Expected: `tests: 53 total, 53 passed` (10 + 22 + 11 + 9 + the smoke test).

- [ ] **Step 5: No commit.**

---

### Task 3: The match carries steals, hits and respawns

**Files:**
- Create: `Assets/Scripts/Online/PlayerHit.cs`
- Modify: `Assets/Scripts/Online/OnlineMatch.cs`
- Test:
  - `Assets/Tests/Editor/PlayerHitTests.cs` (EditMode).
  - `Assets/Tests/Editor/StealMessagesNetworkTests.cs` (port 7802) and `Assets/Tests/Editor/RespawnMessagesNetworkTests.cs` (port 7803). Both use an empty Play Mode scene, like `TutorialMessagesNetworkTests`.

**Interfaces:**
- Produces:

```csharp
public enum HitKind : byte { Steal, Clash }

public struct PlayerHit : INetworkSerializable
{
    public HitKind Kind;
    public int Attacker; // who boosted in: a steal's thief
    public int Victim;
    public static PlayerHit Steal(int thief, int victim);
    public static PlayerHit Clash(int attacker, int victim);
}

// OnlineMatch
public event Action<ulong, int, int> StealAsked;       // host: a client's player hit another (the client's id, the attacker's seat, the victim's seat)
public event Action<PlayerHit> HitReceived;            // clients: a steal or clash the host decided
public event Action<ulong, int, Vector3> RespawnAsked; // host: a client's player fell in the water (the client's id, their seat, where they were last on the ground)
public event Action<int, int> RespawnReceived;         // clients: where the host says a player rises (their seat, the respawn point's index)
public void AskSteal(int attacker, int victim);        // client -> StealServerRpc (RequireOwnership = false); nothing on the host, which judges its own at once
public void SendHit(PlayerHit hit);                    // host -> HitClientRpc, which the host skips; nothing on a client
public void AskRespawn(int seat, Vector3 lastGrounded); // client -> RespawnServerRpc (RequireOwnership = false); nothing on the host, which picks its own at once
public void SendRespawn(int seat, int point);          // host -> RespawnClientRpc, which the host skips; nothing on a client
```

- The `OnlineMatch` summary gains "- Steals, clashes and respawn points: clients' requests and the host's answers (OnlineSteals, OnlineRespawns)."

- [ ] **Step 1: Write the failing tests**
  - **`PlayerHitTests`:** `AHit_ArrivesAsItWasSent`, over `PlayerHit.Steal(2, 3)` and `PlayerHit.Clash(0, 1)` (`TestCaseSource`), round-tripped through `FastBufferWriter`/`FastBufferReader` as in `OrderChangeTests`.
  - **Setup and leaving** as in `TutorialMessagesNetworkTests`:
    - The host runs `HostDirect("127.0.0.1", port)` and the client `JoinDirect`. Wait until both see 2 players.
    - At the end: `log.MachinesLeave()`, the client leaves, then the host, and `Assert.IsEmpty(log.Problems)`.
  - **Recorders** (each subscribed as a method group):
    - `StealRecorder` (`Machine`, `Attacker`, `Victim`, `Count`) and `RespawnAskRecorder` (`Machine`, `Seat`, `LastGrounded`, `Count`), on the host.
    - `HitRecorder` (`List<PlayerHit> Hits`) and `PointRecorder` (`Seat`, `Point`, `Count`), on whichever match they're built with.

```csharp
// StealMessagesNetworkTests
[UnityTest] public IEnumerator AClientsStealRequest_ReachesTheHost_WithWhoSentIt()
// client.Match.AskSteal(1, 0); wait (10 s) until asked.Count == 1
Assert.AreEqual(client.Network.LocalClientId, asked.Machine);
Assert.AreEqual(1, asked.Attacker);
Assert.AreEqual(0, asked.Victim);
// host.Match.AskSteal(0, 1); wait 1 s
Assert.AreEqual(1, asked.Count, "the host judges its own player's at once: it never asks");

[UnityTest] public IEnumerator TheHostsHits_ReachEveryClient_InOrder()
// hits on client.Match, hostHeard on host.Match; host.Match.SendHit(PlayerHit.Steal(0, 1)); host.Match.SendHit(PlayerHit.Clash(1, 0)); wait until 2 hits
CollectionAssert.AreEqual(new[] { PlayerHit.Steal(0, 1), PlayerHit.Clash(1, 0) }, hits.Hits);
// client.Match.SendHit(PlayerHit.Clash(0, 1)); wait 1 s
Assert.AreEqual(2, hits.Hits.Count, "a client sends none");
Assert.IsEmpty(hostHeard.Hits, "the host skips its own");

// RespawnMessagesNetworkTests
[UnityTest] public IEnumerator AClientsRespawnRequest_ReachesTheHost_WithWhoSentItAndWhere()
// client.Match.AskRespawn(1, new Vector3(10, 2, -30)): the host hears its id, seat 1 and (10, 2, -30)
// host.Match.AskRespawn(0, Vector3.zero); wait 1 s: still 1 ("the host picks its own at once: it never asks")

[UnityTest] public IEnumerator TheHostsRespawnPoints_ReachEveryClient()
// host.Match.SendRespawn(1, 17): the client hears seat 1, point 17
// client.Match.SendRespawn(0, 3); wait 1 s: the client still heard 1, and a recorder on the host heard none
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "PlayerHitTests|StealMessagesNetworkTests|RespawnMessagesNetworkTests"`
Expected: `NO RESULTS` with `error CS0246`/`CS1061` (`PlayerHit`, `AskSteal`, `SendHit`, `AskRespawn`, `SendRespawn`).

- [ ] **Step 3: Implement `PlayerHit`** (and the meta) **and the `OnlineMatch` members.** `PlayerHit.ToString`: `"Steal 0 from 1"` / `"Clash 1 with 0"`.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "PlayerHitTests|StealMessagesNetworkTests|RespawnMessagesNetworkTests|OrderMessagesNetworkTests"`
Expected: `tests: 9 total, 9 passed`.

- [ ] **Step 5: No commit.**

---

### Task 4: Steals and clashes online

**Files:**
- Create: `Assets/Scripts/Online/OnlineSteals.cs`
- Modify: `Assets/Scripts/Online/OnlineOrders.cs`, `Assets/Scripts/Online/OnlineGame.cs`
- Test: `Assets/Tests/Editor/OnlineStealsNetworkTests.cs` (port 7804; the menu scene, like `OnlineOrdersNetworkTests`)

**Interfaces:**
- Consumes:
  - Task 1: `StealRules`, `HitVerdict`.
  - Task 2: `StealSync.Asked`; `OrderHandler.StealFrom`, `TakeOrderFrom`, `BounceFrom`, `ClashWith`; `OrderChange.Steal`, `Seat2`.
  - Task 3: `PlayerHit`; `OnlineMatch.AskSteal`, `StealAsked`, `SendHit`, `HitReceived`.
- Produces: `public class OnlineSteals : MonoBehaviour`, with `public void Begin(OnlineSession session)` and `public void Show(PlayerHit hit)`.
  - **This machine's player's requests** (`StealSync.Asked`): a client sends them (`match.AskSteal`); the host judges them itself.
  - **Host:** `match.StealAsked(machine, attacker, victim)` is judged only when `session.SeatOf(machine) == attacker`.
  - **Judging** (host):
    - It needs both seats' `OrderSync.HandlerIn` and `StealRules.Counts(GameManager.Instance.MainState)`.
    - Then `rules.Judge(attacker, victim, Time.time, <distance between the two balls (BallDriving.Sphere)>, victim.IsBoosting, <either respawning>)`.
    - Respawning means the ball's `Respawn.IsRespawning || Respawn.RiderHidden`. Another machine's scooter only ever has the second.
    - A `Steal`: `thief.StealFrom(victim)`, then the hit. A `Clash`: the hit.
    - The hit: `Show(hit)` here, then `match.SendHit(hit)`.
  - **`Show`:**
    - A `Steal`: the victim calls `BounceFrom(thief)`, if this machine drives them (`!RemoteAvatar.IsRemote`).
    - A `Clash`: each of the two that this machine drives calls `ClashWith` on the other.
    - A seat that isn't here: nothing happens.
  - **Subscriptions** follow `OnlineTutorial`: the match's event is subscribed on `MatchSpawned`, and at `Begin` for a match already there. All are undone in `OnDestroy`.
- **`OnlineOrders`** replays a `Steal`: `thief.TakeOrderFrom(victim, order)`, with the thief from `HandlerIn(Seat)` and the victim from `HandlerIn(Seat2)`. It replays only when the order, the thief and the victim are all here.
- **`OnlineGame`:**
  - It adds `OnlineSteals` after `OnlineTutorial`, and keeps it in a field.
  - A client's `match.HitReceived` → `hostMessages.Add(() => steals.Show(hit))`, so hits wait with the host's states and order changes. `OnDestroy` unsubscribes.
  - The summary gains "- steals and clashes between players (OnlineSteals);".

**Test helpers:**
- Copied from `OnlineOrdersNetworkTests`/`OnlineTutorialNetworkTests`: `State`, `ActiveScene`, `Slot`, `ScooterIn`, `Handler`, `PutBallAt`, `CitySpot`, `FinishTutorialInTheCity`, `ScooterInSeat`, `FirstNewSpawn`, `ChangeRecorder`, `ReportRecorder`.
- New:
  - `ShareAt(OnlineScooter scooter, Vector3 spot, DriveFlags flags)`, plus the flagless one (`default(DriveFlags)`).
  - `static readonly DriveFlags BOOSTING = new DriveFlags(true, false, false, 0, true, false)` and `HIDDEN = new DriveFlags(false, false, false, 0, true, false, true)`.
  - `BallOf(OrderHandler player)`: `player.GetComponent<BallDriving>().Sphere.transform.position`.
  - `RespawnOf(int seat)`: `ScooterIn(seat).Sphere.GetComponent<Respawn>()`.
  - `OfKind(ChangeRecorder recorder, OrderChangeKind kind)`: that kind's changes, in order.
  - `HitRecorder` (`List<PlayerHit> Hits`), and `StealRecorder` as in Task 3.

- [ ] **Step 1: Write the failing tests**

```csharp
[UnityTest] public IEnumerator Hosting_TheHostDecidesStealsAndClashes_AndOneStealWinsWhenTwoMachinesTryAtOnce()
// This machine hosts with the game, as in OnlineOrdersNetworkTests.Hosting. Two other machines, x and y (sessions without the game),
// each call Direct.SetDebugSimulatorParameters(150, 0, 0) before JoinDirect; wait until they sit in seats 1 and 2 here,
// with their matches and scooters. ChangeRecorder and HitRecorder on each of x.Match and y.Match.
// To the first wave: FinishTutorialInTheCity(0); x.Match.ReportLearnt(1); y.Match.ReportLearnt(2).
// Seat 1 picks up the first new order, seat 2 the second (ShareAt their pickups; wait for each Pickup).
// Up in the air, away from every beacon and this machine's player: air = CitySpot() + Vector3.up * 30f;
// x's scooter at air, y's at air + Vector3.right * 2f; wait until the host shows both there.
x.Match.AskSteal(2, 1);                         // wait 1 s
Assert.IsEmpty(hitsX.Hits, "not the sender's seat");
// ShareAt(y's scooter, same spot, HIDDEN); wait until RespawnOf(2).RiderHidden; x.Match.AskSteal(1, 2); wait 1 s
Assert.IsEmpty(hitsX.Hits, "a respawning player can't be hit");
// ShareAt(y's scooter, same spot); wait until !RespawnOf(2).RiderHidden
// At once, both ways:
x.Match.AskSteal(1, 2); y.Match.AskSteal(2, 1); // wait until both heard a hit, then 1 s more
Assert.AreEqual(1, OfKind(heardX, OrderChangeKind.Steal).Count, "one steal");
CollectionAssert.AreEqual(OfKind(heardX, OrderChangeKind.Steal), OfKind(heardY, OrderChangeKind.Steal), "the same everywhere");
// steal = that change: seat 1 robbing seat 2, or seat 2 robbing seat 1
CollectionAssert.AreEqual(new[] { PlayerHit.Steal(steal.Seat, steal.Seat2) }, hitsX.Hits);
CollectionAssert.AreEqual(hitsX.Hits, hitsY.Hits);
Assert.AreSame(Handler(steal.Seat), first.PlayerHolding, "the winner holds both orders here");
Assert.AreSame(Handler(steal.Seat), second.PlayerHolding);
Assert.IsFalse(Handler(steal.Seat2).HasOrder);
// A clash: wait StealRules.PAIR_COOLDOWN + 0.5 s; both scooters ShareAt their spots with BOOSTING;
// wait until Handler(1).IsBoosting && Handler(2).IsBoosting here; x.Match.AskSteal(1, 2); wait until x heard 2 hits
Assert.AreEqual(PlayerHit.Clash(1, 2), hitsX.Hits[1]); // and y heard the same
Assert.AreEqual(1, OfKind(heardX, OrderChangeKind.Steal).Count, "a clash moves no order");
// This machine's player boosts into the winner: the host steals at once.
// winner = steal.Seat; ShareAt the winner at its spot (not boosting), the loser at air + Vector3.right * 30f;
// wait until !Handler(winner).IsBoosting and the loser is there; Order best = Handler(winner).GetBestOrder();
ScooterIn(0).AutoBoost(); PutBallAt(0, BallOf(Handler(winner)) + Vector3.forward);
// wait until x heard OrderChange.Steal(best.Key, 0, winner)
Assert.AreSame(Handler(0), best.PlayerHolding);
Assert.AreEqual(PlayerHit.Steal(0, winner), hitsX.Hits[hitsX.Hits.Count - 1]);
// Leave as in OnlineOrdersNetworkTests: log.MachinesLeave(), x and y leave, then the host; Assert.IsEmpty(log.Problems)

[UnityTest] public IEnumerator Joining_ThisMachinesPlayerAsksToSteal_AndTheHostsStealsAndClashesShowHere()
// This machine joins a host without the game and plays in seat 1, to the first wave as in OnlineOrdersNetworkTests.Joining.
// spot = CitySpot(); hosts = ScooterInSeat(host, 0); asked = new StealRecorder(host.Match)
// It asks: ShareAt(hosts, spot + Vector3.up * 30f); wait until the host's scooter is there here;
ScooterIn(1).AutoBoost(); PutBallAt(1, spot + Vector3.up * 30f + Vector3.forward); // wait until asked.Count == 1
Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
Assert.AreEqual(1, asked.Attacker);
Assert.AreEqual(0, asked.Victim);
// The host's player robs this machine's. First an order: order = normalOrders[0]; the host sends Spawn(order.Key, true) and
// Pickup(order.Key, 1); wait until it's on Handler(1). Then ShareAt(hosts, spot + Vector3.right * 4f); PutBallAt(1, spot);
// wait until the host's scooter is there here.
host.Match.SendOrder(OrderChange.Steal(order.Key, 0, 1));
host.Match.SendOrder(OrderChange.Drop(1, OrderBook.NONE, spot, 1f, OrderBook.NONE, spot, 1f, true));
host.Match.SendHit(PlayerHit.Steal(0, 1));      // wait until order.PlayerHolding == Handler(0)
Assert.IsFalse(Handler(1).HasOrder);
Assert.IsTrue((bool)Reflect.GetField(ScooterIn(1), "spinningOut"), "it spins out");
// yield return new WaitForFixedUpdate() twice
Assert.Less(ScooterIn(1).Sphere.GetComponent<Rigidbody>().velocity.x, -1f, "it bounces away from the thief");
// A clash: wait 1.5 s (the spin-out ends); PutBallAt(1, spot); host.Match.SendHit(PlayerHit.Clash(0, 1));
// wait (WAIT) until the ball's velocity.x < -1: it bounces off the host's scooter
// host.Match.SendHit(PlayerHit.Clash(0, 3)): nobody in seat 3, so nothing happens (and no error)
// Leave; Assert.IsEmpty(log.Problems)
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh OnlineStealsNetworkTests`
Expected: 2 failures. `Hosting_…` hears no hit, because nobody judges yet. `Joining_…`'s host never hears the request.

- [ ] **Step 3: Implement `OnlineSteals`** (and the meta), **the `Steal` replay and the `OnlineGame` wiring.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "OnlineStealsNetworkTests|OnlineOrdersNetworkTests|LocalMatchSmokeTest"`
Expected: `tests: 5 total, 5 passed`.

- [ ] **Step 5: No commit.**

---

### Task 5: Respawn points the host can hold

**Files:**
- Modify: `Assets/Scripts/Management/RespawnManager.cs`, `Assets/Scripts/World/RespawnPoint.cs`
- Test: `Assets/Tests/Editor/RespawnPointsTests.cs`

**Interfaces:**
- Produces:

```csharp
// RespawnManager
public RespawnPoint GetRespawnPoint(Vector3 lastGrounded); // (exists) the nearest point not in use, the first one included; a free one
                                                           // within closeEnough still does, as today. Every point in use: the nearest. None: null
public int IndexOf(RespawnPoint point); // its place among the points, the same on every machine; -1 when it isn't one
public RespawnPoint PointAt(int index); // null outside the range
// RespawnPoint
public bool InUse { get; set; }         // (exists) also true while held
public void HoldFor(float seconds);     // online host: another machine's player rises here, so it's in use that long (Time.time)
// InitPoint (exists) also ends a hold
```

- [ ] **Step 1: Write the failing tests**
  - Fixture:
    - A `RespawnManager` from `objects`, with three points at x = 0, 50 and 100 under one parent.
    - Each point has its two order spots (`Reflect.SetField` on `order1Spawn` and `order2Spawn`).
    - Then `Reflect.Invoke(manager, "InitRespawnPoints", parent)`.

```csharp
[Test] public void TheNearestFreePoint_IsPicked()
{ Assert.AreSame(points[1], manager.GetRespawnPoint(new Vector3(48, 0, 0))); }

[Test] public void APointInUse_IsSkipped_EvenTheFirst()
{   // 2024: the first point was handed out even while someone rose from it
    points[0].InUse = true;
    Assert.AreSame(points[1], manager.GetRespawnPoint(Vector3.zero));
}

[Test] public void EveryPointInUse_TheNearestAnyway()
{
    foreach (RespawnPoint point in points)
        point.InUse = true;
    Assert.AreSame(points[2], manager.GetRespawnPoint(new Vector3(90, 0, 0)));
}

[Test] public void APointHeldForAnotherMachinesPlayer_IsInUse_UntilANewRound()
{
    points[1].HoldFor(5f);
    Assert.IsTrue(points[1].InUse);
    Assert.AreNotSame(points[1], manager.GetRespawnPoint(new Vector3(50, 0, 0)));
    points[1].InitPoint();
    Assert.IsFalse(points[1].InUse);
}

[Test] public void PointsGoByTheirPlace_TheSameOnEveryMachine()
{
    Assert.AreEqual(2, manager.IndexOf(points[2]));
    Assert.AreSame(points[2], manager.PointAt(2));
    Assert.IsNull(manager.PointAt(-1));
    Assert.IsNull(manager.PointAt(3));
    Assert.AreEqual(-1, manager.IndexOf(null));
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh RespawnPointsTests`
Expected: `NO RESULTS` with `error CS1061` (`IndexOf`, `PointAt`, `HoldFor`).

- [ ] **Step 3: Implement.** `GetRespawnPoint` keeps two answers as it goes: the nearest point of all, and the nearest free one.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "RespawnPointsTests|LocalMatchSmokeTest"`
Expected: `tests: 6 total, 6 passed`.

- [ ] **Step 5: No commit.**

---

### Task 6: Respawns online

**Files:**
- Create: `Assets/Scripts/Online/RespawnSync.cs`, `Assets/Scripts/Online/OnlineRespawns.cs`
- Modify: `Assets/Scripts/Player/Respawn.cs`, `Assets/Scripts/Online/OnlineGame.cs`
- Test:
  - `Assets/Tests/Editor/OnlineRespawnsNetworkTests.cs` (port 7805; the menu scene).
  - In `OnlineOrdersNetworkTests`, the comment "as Respawn does" becomes "as Respawn does without the host's answer".

**Interfaces:**
- Consumes:
  - Task 3: `OnlineMatch.AskRespawn`, `RespawnAsked`, `SendRespawn`, `RespawnReceived`.
  - Task 5: `GetRespawnPoint`, `IndexOf`, `PointAt`, `HoldFor`.
- Produces:

```csharp
public static class RespawnSync
{
    public static event Action<int, Vector3> Asked; // online client: its player in this seat fell in the water, last on the ground here
    public static void Ask(int seat, Vector3 lastGrounded);
    public static void Reset();                     // tests
}
// Respawn
public const float HOST_ANSWER_WAIT = 2f;  // online client: how long its player waits for the host's point before picking its own
public float Duration { get; }             // wispRiseTime + wispToCasketTime + liftDuration (2.0 s on the scooter)
public void RiseAt(RespawnPoint point);    // online client: the host's point for the respawn that's waiting for one; ignored otherwise
// OnlineRespawns : MonoBehaviour
public const float HOLD_MARGIN = 1f;
public void Begin(OnlineSession session);
public void Show(int seat, int point);     // client: the host's point for this machine's player in that seat (Respawn.RiseAt)
```

- `RespawnPlayer` starts like this. The rest is today's code without its pick-and-drop lines, from the wisp's setup on:

```csharp
RespawnPoint rsp = null;
if (GameAuthority.IsOnline && !GameAuthority.IsAuthority)
{
    // The host picks where every player rises (never two on one point) and drops the orders there
    risePoint = null;
    awaitingPoint = true;
    RespawnSync.Ask(OrderSync.SeatOf(this), lastGroundedPos);
    float giveUp = Time.time + HOST_ANSWER_WAIT;
    while (risePoint == null && !GameAuthority.IsAuthority && Time.time < giveUp) // offline again: the host left
        yield return null;
    awaitingPoint = false;
    rsp = risePoint;
}
if (rsp == null)
{
    // Offline, on the host, or with no answer: this machine picks. A client with no answer asks the host to drop the orders there
    rsp = RespawnManager.Instance.GetRespawnPoint(lastGroundedPos);
    rsp.InUse = true;
    orderHandler.DropEverything(rsp.Order1Spawn, rsp.Order2Spawn, false);
}
```

- `StopRespawnCoroutine` also stops waiting (`awaitingPoint = false`).
- **`OnlineRespawns`:**
  - Client: `RespawnSync.Asked` → `match.AskRespawn`.
  - Host: `match.RespawnAsked(machine, seat, lastGrounded)` counts only when `session.SeatOf(machine) == seat`, and there's a `RespawnManager` and a handler in that seat. Then, in order:
    - `point = GetRespawnPoint(lastGrounded)`;
    - `point.HoldFor(respawn.Duration + HOLD_MARGIN)`, with the seat's ball's `Respawn`;
    - `handler.DropEverything(point.Order1Spawn, point.Order2Spawn, false)`;
    - `match.SendRespawn(seat, IndexOf(point))`.
  - `Show` (client): if this machine drives the seat's player (`!RemoteAvatar.IsRemote`), their `Respawn.RiseAt(RespawnManager.Instance.PointAt(point))`, when both exist.
  - Subscriptions as in `OnlineSteals`.
- **`OnlineGame`:**
  - It adds `OnlineRespawns` after `OnlineSteals`, and keeps it in a field.
  - A client's `match.RespawnReceived` → `hostMessages.Add(() => respawns.Show(seat, point))`. `OnDestroy` unsubscribes.
  - The summary gains "- where players who fall in the water rise (OnlineRespawns);".

- [ ] **Step 1: Write the failing tests**
  - Helpers as in Task 4, plus `RespawnAskRecorder` and `PointRecorder` (Task 3), and `DropRecorder` (from `OnlineOrdersNetworkTests`).
  - Neither test uses a new member, so each fails on behaviour, not on compiling.

```csharp
[UnityTest] public IEnumerator Hosting_TheHostPicksWhereEveryPlayerRises_NeverTwoOnOnePoint()
// This machine hosts with the game; another machine (a session without the game) sits in seat 1; to the first wave.
// Seat 1 picks up an order (first). spot = CitySpot(); RespawnPoint mine = RespawnManager.Instance.GetRespawnPoint(spot);
// This machine's player falls in the water: RespawnOf(0).LastGroundedPos = spot; RespawnOf(0).StartRespawnCoroutine();
Assert.IsTrue(mine.InUse, "this machine's player rises there");
// The other machine's player falls in at the same place: rise = new PointRecorder(other.Match); other.Match.AskRespawn(1, spot);
// wait until rise.Count == 1
Assert.AreEqual(1, rise.Seat);
RespawnPoint theirs = RespawnManager.Instance.PointAt(rise.Point);
Assert.IsNotNull(theirs);
Assert.AreNotSame(mine, theirs, "never the point this machine's player rises from");
// the last Drop change the other machine heard for seat 1:
Assert.AreEqual(first.Key, drop.Order2, "their order lands on their point");
Assert.AreEqual(theirs.Order2Spawn, drop.Spot2);
Assert.IsFalse(drop.Flag, "no spin-out");
// At once again: other.Match.AskRespawn(1, spot); wait until rise.Count == 2
RespawnPoint next = RespawnManager.Instance.PointAt(rise.Point);
Assert.IsTrue(next != mine && next != theirs, "the host holds a point while its player rises");
// Another seat: other.Match.AskRespawn(0, spot); wait 1 s
Assert.AreEqual(2, rise.Count, "not the sender's seat");
// Leave; Assert.IsEmpty(log.Problems)

[UnityTest] public IEnumerator Joining_ThisMachinesPlayerRisesWhereTheHostSays_OrOnItsOwnPointWithoutAnAnswer()
// This machine joins a host without the game, in seat 1; to the first wave. spot = CitySpot(); PutBallAt(1, spot);
// asked = new RespawnAskRecorder(host.Match); drops = new DropRecorder(host.Match); Respawn respawn = RespawnOf(1)
respawn.LastGroundedPos = spot; respawn.StartRespawnCoroutine();   // wait until asked.Count == 1
Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
Assert.AreEqual(1, asked.Seat);
Assert.AreEqual(spot, asked.LastGrounded);
// wait 1 s
Assert.IsTrue(respawn.IsRespawning && respawn.RiderHidden, "hidden, waiting for the host's point");
// far = the index of the point farthest from spot (PointAt from 0 until null); host.Match.SendRespawn(1, far);
// wait (WAIT) until !respawn.IsRespawning
Assert.Less(Flat(BallOf(Handler(1)), RespawnManager.Instance.PointAt(far).PlayerSpawn), 2f, "it rose where the host said");
Assert.AreEqual(0, drops.Count, "the host drops the orders");
// No answer: respawn.LastGroundedPos = spot; respawn.StartRespawnCoroutine(); wait (WAIT) until asked.Count == 2, then until !respawn.IsRespawning
RespawnPoint own = RespawnManager.Instance.GetRespawnPoint(spot);
Assert.Less(Flat(BallOf(Handler(1)), own.PlayerSpawn), 2f, "it rose on its own point after a short wait");
Assert.AreEqual(1, drops.Count, "and asked the host to drop its orders there");
Assert.AreEqual(own.Order1Spawn, drops.Spot1);
// Leave; Assert.IsEmpty(log.Problems)
// Flat(a, b): the distance between a and b, ignoring height
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh OnlineRespawnsNetworkTests`
Expected: 2 failures. `Hosting_…` hears no point, because nobody picks for another machine yet. `Joining_…`'s host never hears the request.

- [ ] **Step 3: Implement `RespawnSync` and `OnlineRespawns`** (and their metas), **the `Respawn` changes and the `OnlineGame` wiring.**

- [ ] **Step 4: Run to verify they pass** (in the background)

Run: `bash tools/run-tests.sh "OnlineRespawnsNetworkTests|OnlineOrdersNetworkTests|LocalMatchSmokeTest"`
Expected: `tests: 5 total, 5 passed`.

- [ ] **Step 5: No commit.**

---

### Task 7: Docs and the full suite

**Files:**
- Modify: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`

- [ ] **Step 1: `docs/online.md`**
  - A "Phase 3G: steals, clashes and respawns" section, in the doc's plain style:
    - Boosting into another machine's scooter steals or clashes, as in a local match, and every machine sees the same.
    - Two machines trying at once make one steal.
    - The golden order can be stolen online, and the robbed player isn't slowed any more.
    - Two players falling in the water never rise on the same point.
    - How it works: `StealSync` and `AskSteal`, `StealRules`, `OrderChange.Steal`, `PlayerHit`, `OnlineSteals`; `RespawnSync` and `AskRespawn`, `OnlineRespawns`, `RespawnPoint.HoldFor`.
  - Phase 3E's water bullet: since Phase 3G, the host also picks the respawn point.
  - The ParrelSync walkthrough gains steals, clashes and respawns.
  - Known limits:
    - Remove "Steals and clashes are the host's alone…".
    - Add: on a client, a steal or clash takes effect a round trip after the bump, because the host decides. Near the boundary, the host may call a steal where a local match would call a clash (Ruling 4).
    - Add: a client whose player falls in the water rises a round trip later than offline.
    - Add: another machine's scooter can be bumped during the last 0.7 s of its rise from the grave. Its collider comes on when its rider shows.
    - The silent-scooters line: no whoosh and no gravestone on other machines either (Task 3.7).

- [ ] **Step 2: `docs/testing.md`**
  - The EditMode list gains `StealRulesTests`, `PlayerHitTests` and `RespawnPointsTests`.
  - Network tests:
    - `StealMessagesNetworkTests` (port 7802) and `RespawnMessagesNetworkTests` (port 7803): empty scene.
    - `OnlineStealsNetworkTests` (port 7804): two other machines at 150 ms, and one steal wins. `OnlineRespawnsNetworkTests` (port 7805). Both load the game scene: a minute or two each.

- [ ] **Step 3: The roadmap**
  - Progress: a Phase 3G line. "Next" becomes Tasks 3.7–3.8.
  - Task 3.6's four bullets are ticked, each with a short Phase 3G note (Rulings 1–3 for the steal; `PlayerHit` for the clash; `OnlineRespawns` for the respawn; the tests).
  - Task 3.7 gains: other machines don't yet hear the thief's whoosh or see a respawn's gravestone (Phase 3G).

- [ ] **Step 4: `EDITOR-TODO.md`** (rewrite it for Phase 3G, and keep what's still open)
  - Import step: the new scripts, the changed scripts and the new tests.
  - A two-editor check, then again with Bad Connection in the clone:
    - In the clone, pick up an order. In editor 1, boost into the clone's scooter: the order jumps to editor 1's scooter in both editors, and the clone's scooter spins out and bounces away.
    - Boost into each other at the same time: both bounce, and no order moves.
    - In the golden round, steal the golden order: afterwards the robbed player isn't slower.
    - Drive both scooters into the water at one spot: they rise on different points, each with their own orders.
  - Still open from before, if not done: the Phase 3F two-editor check, the optional "Normal Positions" tidy-up, and the Phase 3D Steam checks.
  - Commit: suggested title "Phase 3G: steals, clashes and respawns online".

- [ ] **Step 5: Run the full suite** (in the background)

Run: `bash tools/run-tests.sh`
Expected: 438 + 32 new = `tests: 470 total, 470 passed`.
- The new tests: Task 1 adds 9, Task 2 adds 8, Task 3 adds 6, Task 4 adds 2, Task 5 adds 5 and Task 6 adds 2.
- The prefab IDs are saved now, so `OnlinePrefabsTests.SavedPrefabIds_…` passes.

- [ ] **Step 6: No commit.** Suggested title: "Phase 3G: steals, clashes and respawns online".

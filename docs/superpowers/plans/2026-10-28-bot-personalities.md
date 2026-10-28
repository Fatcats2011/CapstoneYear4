# Bot Personalities Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give online CPU drivers three readable personalities (thief, hunter, courier) that react to the scoreboard.

**Architecture:** Pure decision code (new `BotPersonality.cs`, `BotTactics.cs`) scores every option a bot has — free
orders, steals, delivering — from plain data, with a personality's weights shifted by the scoreboard. `BotDriver`
gathers the scene into that data, follows the choice (chasing a victim and boosting into them for a steal, dodging for
the courier) and logs per-bot stats. `OnlineGame.SeatHostBots` hands each bot its personality by seat rank.

**Tech Stack:** Unity 2022.3, C#, NUnit tests in `Assets/Tests/Editor` (namespace `DoA.Tests`),
`python tools/compile-check.py`, `bash tools/run-tests.sh <filter>`.

**Spec:** `docs/superpowers/specs/2026-10-28-bot-personalities-design.md`

## Global Constraints

- At most one thief. 1 bot = Thief; 2 = Thief, Hunter; 3 = Thief, Hunter, Courier.
- No label, name, colour or hat reveals a personality; bots keep the "CPU" name (`OnlineGame.BOT_NAME`).
- Online-only, host-only; steals go through the existing `OrderHandler` hit path. No new network messages.
- Steals off (`HouseRules.Playing.Steals == false`) → no bot ever chooses a steal.
- A bot holding 2 orders only delivers. Holding 1, it detours only for an option within `DETOUR` metres.
- Leader = highest `OrderHandler.Score` over all seated players, the bot included; a tie for top counts as leading.
- Desperation = gap to the leader in points, 0 at level/leading, 1 at `FULL_GAP` and beyond.
- Boost-to-hit may skip `BotDriver.BOOST_EVERY` spacing but still only presses the boost button (the game's boost rules apply).
- Pure decisions live in static/plain classes with EditMode tests, matching `BotBrain` / `BotStuck`.
- Don't change `Constants.OrderValue` numbers; money comes from `Order.Worth` / `StealValueRules`.

## Review Focus

1. **Victim leaves mid-chase** (delivers, drops the order, respawns, or is destroyed): the bot must re-score next tick,
   not drive at a stale position or throw. → Task 4 test `Thief_VictimWithNothing_GoesBackToOrders`.
2. **Bot robbed while carrying:** held count drops to 0; it must pick a fresh target, not keep heading to the old
   dropoff. → Covered by `Choose` being stateless over `Held` (Task 2 test `Choose_NothingHeld_NeverDelivers`).
3. **Steals switched off mid-lobby:** the thief must still collect orders. → Task 2 test `Choose_StealsOff_ThiefCollectsOrders`.
4. **Two near-equal options flicking every frame** (bot wobbling between two orders). → Task 2 test `Choose_Stickiness_KeepsCurrentOnNearTie`.
5. **Fewer seats than expected / a bot whose seat is unknown (-1):** assignment must not throw and must still give at
   most one thief. → Task 1 test `RankOf_UnknownSeat_IsCourierNotThief`.

Parallel-session note: another Claude session may share this folder's test mirror and network ports 7791-7835. Check
with the user before running network tests; a private copy of `_doa_test_mirror` avoids compile clashes.

---

### Task 1: Personalities and their weights

**Files:**
- Create: `Assets/Scripts/Player/BotPersonality.cs`
- Test: `Assets/Tests/Editor/BotPersonalityTests.cs`

**Interfaces:**
- Produces:
  - `public enum BotKind { Thief, Hunter, Courier }`
  - `public readonly struct BotProfile` with public readonly fields: `float StealAppetite, DesperateSteal, ValuePerPoint, GoldenBonus, DistancePerMetre, ChaseSeconds, DesperateChase, BoostEvery; int MinStealWorth; bool Dodges`
  - `public static class BotPersonality`:
    - `BotKind ForRank(int rank)` — 0 Thief, 1 Hunter, anything else (2+, negative) Courier
    - `int RankOf(int seat, IEnumerable<int> botSeats)` — how many distinct bot seats ≥0 are lower than `seat`; `seat < 0` → 2
    - `BotProfile For(BotKind kind)`

Values (from the spec's "deliberately extreme" baselines; tune later in this file only):

| | StealAppetite | DesperateSteal | ValuePerPoint | GoldenBonus | DistancePerMetre | ChaseSeconds | DesperateChase | BoostEvery | MinStealWorth | Dodges |
|---|---|---|---|---|---|---|---|---|---|---|
| Thief | 3 | 2 | 1 | 0 | 0.6 | 8 | 6 | 2.5 | 0 | false |
| Hunter | 0 | 1.5 | 3 | 120 | 0.2 | 5 | 5 | 2.5 | 80 (`StealValueRules.HARD`) | false |
| Courier | 0 | 0 | 1 | 0 | 1.2 | 0 | 0 | 1.2 | int.MaxValue | true |

(Golden's `Worth` is 50, below Easy; `GoldenBonus` is how the hunter still prizes it.)

- [ ] **Step 1: Write the failing tests** in `BotPersonalityTests`:
  - `ForRank_OneThiefThenHunterThenCourier`: `ForRank(0)==Thief`, `ForRank(1)==Hunter`, `ForRank(2)==Courier`, `ForRank(3)==Courier`, `ForRank(-1)==Courier`.
  - `RankOf_LowestBotSeatFirst`: seats `{3,1,2}` → `RankOf(1,..)==0`, `RankOf(2,..)==1`, `RankOf(3,..)==2`; duplicates in the list don't change it.
  - `RankOf_UnknownSeat_IsCourierNotThief`: `RankOf(-1, {1,2}) == 2`.
  - `Profiles_AreDistinct`: thief `StealAppetite` > 0, hunter and courier `StealAppetite == 0`; hunter `ValuePerPoint` > the others'; courier `DistancePerMetre` > the others' and `Dodges`; courier `BoostEvery` < the others'.
- [ ] **Step 2:** Run `python tools/compile-check.py` → expect a compile failure naming `BotPersonality`.
- [ ] **Step 3:** Implement `BotPersonality.cs` with the interfaces and table above. Summary comments in the `BotBrain` style.
- [ ] **Step 4:** Run `python tools/compile-check.py` → `COMPILES`; `bash tools/run-tests.sh BotPersonalityTests` → all pass.
- [ ] **Step 5: Commit** — `Bot personalities: thief, hunter and courier weights, assigned by seat rank`.

---

### Task 2: Choosing what to do (scoring)

**Files:**
- Create: `Assets/Scripts/Player/BotTactics.cs`
- Test: `Assets/Tests/Editor/BotTacticsTests.cs`

**Interfaces:**
- Consumes: `BotProfile`, `BotPersonality.For` (Task 1).
- Produces:
  - `public enum BotOptionKind { Order, Steal }`
  - `public struct BotOption { public BotOptionKind Kind; public int Worth; public bool Golden; public float Distance; public bool VictimLeads; public bool VictimBoosting; }`
  - `public struct BotSituation { public BotProfile Profile; public int MyScore; public int LeaderScore; public int Held; public bool StealsOn; }`
  - `public static class BotTactics`:
    - `const int FULL_GAP = 200; const float DETOUR = 25f; const float STICKY = 1.25f; const float LEADER_BONUS = 1.5f; const float BOOSTING_VICTIM = 0.2f;`
    - `float Desperation(int myScore, int leaderScore)` — `Clamp01((leader - mine) / FULL_GAP)`
    - `bool Leads(int myScore, int leaderScore)` — `myScore >= leaderScore`
    - `float Score(BotSituation s, BotOption o)` — `float.NegativeInfinity` when the option isn't allowed
    - `int Choose(BotSituation s, IReadOnlyList<BotOption> options, int current)` — index of the option to go for, or -1 = deliver what it holds (or idle when it holds nothing and nothing scores)
    - `float ChaseSeconds(BotSituation s)` — `ChaseSeconds + DesperateChase·d`, halved when leading

Scoring (d = desperation; leading = `Leads`):
```
value    = Worth·ValuePerPoint·(1 + d) + (Golden ? GoldenBonus : 0)
distance = Distance·DistancePerMetre·(1 − 0.5·d)·(leading ? 1.5 : 1)
Order:  value − distance
Steal:  not allowed unless StealsOn and appetite > 0 and Worth ≥ MinStealWorth, where appetite = StealAppetite + DesperateSteal·d;
        value·appetite·(VictimLeads ? LEADER_BONUS : 1)·(VictimBoosting ? BOOSTING_VICTIM : 1) − distance
Not allowed at all when Held ≥ 2. When Held == 1, not allowed beyond DETOUR metres.
```
`Choose`: the best allowed score, with `options[current]`'s score ×`STICKY` when positive. With `Held ≥ 1`, the best
must also be > 0 or it returns -1 (deliver). With `Held == 0` it returns the best allowed option, or -1 when none is.

- [ ] **Step 1: Write the failing tests** in `BotTacticsTests` (build profiles with `BotPersonality.For`):
  - `Desperation_ZeroWhenLeadingOrLevel_OneAtFullGap`: `(100,100)→0`, `(150,100)→0`, `(0,200)→1`, `(0,400)→1`, `(0,100)→0.5`.
  - `Leads_TieCountsAsLeading`.
  - `Thief_PrefersAStealOverAnEqualOrder`: one order Worth 65 at 30 m, one steal Worth 65 at 30 m → picks the steal.
  - `Courier_NeverSteals`: courier, steal Worth 500 at 5 m, desperate → `Score` is −∞.
  - `Hunter_StealsBigPackagesOnlyWhenBehind`: steal Worth 80 at 20 m → −∞ when level; allowed when `MyScore` 0, `LeaderScore` 200; steal Worth 65 → −∞ even then.
  - `Hunter_DrivesFarForAHardOrder`: Easy (55) at 20 m vs Hard (80) at 120 m → Hard; a courier given the same picks Easy.
  - `Hunter_PrizesGolden`: Golden (Worth 50, `Golden=true`) at 60 m vs Hard at 60 m → Golden.
  - `Steal_LeaderPreferred`: two equal steals, one `VictimLeads` → that one.
  - `Steal_BoostingVictimScoresLower`.
  - `Choose_StealsOff_ThiefCollectsOrders`: `StealsOn=false`, a steal and an order → the order.
  - `Choose_HoldingTwo_AlwaysDelivers`: `Held=2` → -1.
  - `Choose_HoldingOne_DetoursOnlyWhenClose`: order at 20 m → its index; same order at 40 m → -1.
  - `Choose_NothingHeld_NeverDelivers`: `Held=0`, one order far away with a negative score → still that order's index; no options → -1.
  - `Choose_Stickiness_KeepsCurrentOnNearTie`: two orders scoring within 10% → returns `current` whichever it is.
  - `ChaseSeconds_LongerWhenDesperate_ShorterWhenLeading`.
- [ ] **Step 2:** Compile-check → fails naming `BotTactics`.
- [ ] **Step 3:** Implement `BotTactics.cs` exactly as the formulas above.
- [ ] **Step 4:** Compile-check → `COMPILES`; `bash tools/run-tests.sh "BotTacticsTests|BotPersonalityTests"` → all pass.
- [ ] **Step 5: Commit** — `Bot personalities: score orders and steals by personality and scoreboard`.

---

### Task 3: Chases, boost-to-hit and dodging (pure helpers)

**Files:**
- Modify: `Assets/Scripts/Player/BotTactics.cs`
- Create: `Assets/Scripts/Player/BotChase.cs`
- Test: `Assets/Tests/Editor/BotTacticsTests.cs`, `Assets/Tests/Editor/BotChaseTests.cs`

**Interfaces:**
- Produces:
  - `BotTactics.HIT_DISTANCE = 12f; HIT_ANGLE = 20f; DANGER_RADIUS = 15f;`
  - `bool BotTactics.BoostToHit(float bearing, float distance)` — `|bearing| ≤ HIT_ANGLE && distance ≤ HIT_DISTANCE`
  - `Vector3 BotTactics.Dodge(Vector3 position, Vector3 aim, IReadOnlyList<Vector3> threats)` — for each threat within `DANGER_RADIUS` on the ground, push the aim directly away from it, more the closer it is (up to `DANGER_RADIUS` metres sideways at contact); returns `aim` unchanged with no threats near. Height ignored.
  - `public class BotChase` (like `BotStuck`):
    - `const float FORGET_VICTIM = 6f;` (seconds before the same victim may be chased again)
    - `bool Tick(int victimSeat, float limitSeconds, float deltaTime)` — true when this chase is over (time spent on this victim ≥ limit); then that seat is barred for `FORGET_VICTIM` s. A different seat starts a fresh timer; `victimSeat < 0` resets.
    - `bool Barred(int seat)` — still within its `FORGET_VICTIM` cool-off
    - `void Advance(float deltaTime)` — runs down the cool-offs (call once a frame)

- [ ] **Step 1: Write the failing tests:**
  - `BoostToHit_CloseAndLinedUp_Only`: (5°, 8 m) → true; (30°, 8 m) → false; (5°, 20 m) → false.
  - `Dodge_NoThreats_AimUnchanged`; `Dodge_ThreatOnTheRight_AimMovesLeft` (threat at (5,0,10), aim (0,0,20) → returned x < 0); `Dodge_FarThreat_Ignored` (threat 40 m away).
  - `BotChaseTests.GivesUpAfterTheLimit_ThenBarsThatVictim`: `Tick(2, 5, 4)` false, `Tick(2, 5, 2)` true, `Barred(2)` true, after `Advance(7)` `Barred(2)` false.
  - `BotChaseTests.NewVictim_FreshTimer`: `Tick(2,5,4)`, `Tick(3,5,4)` false.
- [ ] **Step 2:** Compile-check → fails.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Compile-check; `bash tools/run-tests.sh "BotTacticsTests|BotChaseTests"` → pass.
- [ ] **Step 5: Commit** — `Bot personalities: chase limits, boost-to-hit and the courier's dodge`.

---

### Task 4: BotDriver follows its personality

**Files:**
- Modify: `Assets/Scripts/Player/BotDriver.cs` (`PickTarget` at 228-249, boost block at 151-159, fields)
- Test: `Assets/Tests/Editor/BotDriverPlayTests.cs`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces:
  - `public BotKind Kind { get; set; }` on `BotDriver` (default `Courier`, the mildest — so the F7 autopilot is unchanged in spirit; it may set any kind)
  - `public OrderHandler Victim { get; private set; }` — who it's chasing now, else null (tests)
  - `internal BotStats Stats` — see Task 5

Behaviour each frame (replacing `PickTarget`):
1. Gather options: each free order (as today) → `BotOption{Order, Worth, Golden, Distance}`; each other seated player's
   `OrderHandler` (from `PlayerInstantiate.Instance.Roster.Players`) holding an order, not `chase.Barred(seat)` →
   `BotOption{Steal, victim.GetBestOrder().Worth, Golden, Distance, VictimLeads, victim.IsBoosting}`. Keep a parallel
   list of the targets (Order or OrderHandler) so `current` maps across frames by reference.
2. `BotSituation`: `Held` = orders held (0-2), `MyScore`/`LeaderScore` from roster scores, `StealsOn =
   HouseRules.Playing.Steals`.
3. `Choose` → -1 with something held → target the held best order's dropoff (as today); -1 with nothing → idle; an
   order → its position; a steal → victim's scooter position, `Victim` set, `chase.Tick(seat, ChaseSeconds, dt)`; when
   it returns true, drop the victim and re-choose next frame.
4. Boost: personality `BoostEvery` replaces `BOOST_EVERY`. While chasing, `BoostToHit(bearing to victim, distance)`
   boosts regardless of `sinceBoost` (still never while `boostHeld ≥ 0` or reversing).
5. Courier with `Held ≥ 1`: `Aim = Dodge(position, Aim, positions of boosting rivals)` before steering.
6. `chase.Advance(dt)` every frame.

Keep `OnlyInRound`, pathing, stuck handling as they are. Rescore at most every `REPATH_EVERY` (0.5 s) — keep the last
choice between rescores — so the scan cost stays small.

- [ ] **Step 1: Write the failing PlayMode tests** in `BotDriverPlayTests`, using its existing scene setup:
  - `Thief_ChasesARivalCarryingAnOrder`: a second player holds an order, a free order is equally far → after a few frames the thief's `Victim` is that player and `Target` is near their scooter.
  - `Courier_IgnoresTheCarrier_TakesTheOrder`: same setup, `Kind = Courier` → `Victim` null, `Target` at the order.
  - `Thief_VictimWithNothing_GoesBackToOrders`: the victim's orders are released mid-chase → within 1 s `Victim` null and `Target` at a free order; no exception.
  - `Thief_StealsOff_CollectsOrders`: `HouseRules.Playing` with steals off → `Victim` stays null (restore `HouseRules.Playing` in teardown).
- [ ] **Step 2:** Run `bash tools/run-tests.sh BotDriverPlayTests` → new tests fail.
- [ ] **Step 3:** Implement the behaviour above in `BotDriver`.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "BotDriverPlayTests|BotBrainTests|BotTacticsTests"` → all pass.
- [ ] **Step 5: Commit** — `Bot personalities: bots chase, steal, hunt and dodge by personality`.

---

### Task 5: Seating the personalities, and a per-bot stats log

**Files:**
- Modify: `Assets/Scripts/Online/OnlineGame.cs` (`SeatHostBots`, ~line 739)
- Create: `Assets/Scripts/Player/BotStats.cs`
- Test: `Assets/Tests/Editor/BotStatsTests.cs`; extend `Assets/Tests/Editor/OnlineBotsNetworkTests.cs` if cheap

**Interfaces:**
- Consumes: `BotPersonality.RankOf/ForRank`, `BotDriver.Kind`, `BotDriver.Stats`.
- Produces:
  - `public class BotStats { int StealTries, Steals, Pickups; float TotalValue, TotalPickupDistance; void Tried(); void Stole(); void PickedUp(int worth, float distance); string Line(BotKind kind, int seat); }`
    `Line` → e.g. `"Bot seat 2 Thief: 5 steal tries, 3 steals, 4 pickups, avg value 62, avg pickup 48 m"` (avg 0 with none).
- In `SeatHostBots`: before `bot.Drive`, `bot.Kind = BotPersonality.ForRank(BotPersonality.RankOf(seat, <seats of every bot OnlinePlayer in botsToSeat and seatedBots>))`. Bots spawn in seat order (`OnlineSession.AddBots` takes seats in turn), so lowest bot seat = first created = thief.
- `BotDriver` counts: a steal try each boost-to-hit; a steal when its held count rises while `Victim` was set; a pickup when held count rises otherwise (worth, distance it travelled since choosing it — distance at choice time is fine). Logs `Stats.Line` with `Debug.Log` when `OnlyInRound` goes from in-round to out-of-round.

- [ ] **Step 1: Write the failing tests:** `BotStatsTests.Line_AveragesAndCounts` (two pickups 55@20 m and 80@60 m, one try, one steal → `"... 1 steal tries, 1 steals, 2 pickups, avg value 68, avg pickup 40 m"` — round to nearest int), `Line_NothingYet_ZeroAverages`. If `OnlineBotsNetworkTests` can reach the seated `BotDriver`s cheaply, add `ThreeBots_OneThiefOneHunterOneCourier` (ask the user before running network tests — see Review Focus note).
- [ ] **Step 2:** Compile-check → fails.
- [ ] **Step 3:** Implement `BotStats`, the counting in `BotDriver`, and the assignment in `SeatHostBots`.
- [ ] **Step 4:** Compile-check; `bash tools/run-tests.sh "BotStatsTests|BotFillTests"` (+ the network test if added) → pass.
- [ ] **Step 5: Commit** — `Bot personalities: seat the thief first, and log each bot's play`.

---

### Task 6: Full run, docs and the playtest hand-off

**Files:**
- Modify: `docs/superpowers/specs/2026-10-23-modern-features-handoff.md` only if it tracks bot phases (one line: personalities done); `EDITOR-TODO.md` Bots section if it lists bot behaviour.

- [ ] **Step 1:** `bash tools/run-tests.sh` → all pass (several minutes; `Logs/test-results.xml`). Report any failure with output.
- [ ] **Step 2:** Code review per the user's preference (4 Sonnet reviewers, medium effort); fix findings one at a time.
- [ ] **Step 3:** Hand the user a short playtest checklist (short bullets): host an online match alone (3 bots); watch for one bot hounding whoever leads, one driving far for big/golden packages, one quick bot that swerves away from boosters; check the stats lines in the log after the round; try steals off.
- [ ] **Step 4: Commit** docs — `Bot personalities: docs`.

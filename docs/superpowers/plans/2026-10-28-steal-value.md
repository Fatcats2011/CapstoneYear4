# Steals Make Orders Worth More Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each steal doubles a normal order's worth, a thief earns a small once-per-order payment for taking a package, and the tier prices follow the time an order takes to score.

**Architecture:** One pure rules class (`StealValueRules`) holds every number and rule: prices, doubling, the cap against overflow, the thief's payment, the look. `Order` keeps a steal count that every machine raises when it replays the existing Steal change, so nothing in the network format changes. `OrderHandler` scores an order's current worth and pays the thief on the host, where scores are already decided and shared.

**Tech Stack:** Unity 2022.3, C#, NUnit EditMode tests in `Assets/Tests/Editor` (namespace `DoA.Tests`), `tools/compile-check.py`, `tools/run-tests.sh`.

**Spec:** `docs/superpowers/specs/2026-10-28-steal-value-design.md`

## Global Constraints

- Prices: Easy 55, Medium 65, Hard 80. Golden stays 50, and its growth over time stays +1 per second held.
- Each steal doubles a normal order's worth (an Easy stolen twice is worth 4× its price). A steal never changes the golden order's worth.
- The thief's payment is 7, added to their score on the host (or offline) when a package is actually taken, once per order per thief seat. A golden steal pays too. A bump that takes nothing pays nothing and doubles nothing.
- Match Rules STEALS OFF: no doubling and no payment (nothing changes hands).
- Do NOT change the numeric values of `Constants.OrderValue` (Easy 40, Medium 60, Hard 80, Golden 50): scenes and prefabs store them as raw numbers, and the enum names the tier everywhere. Money comes from `StealValueRules`, never from the enum's number.
- No change to the network format. Only the host changes scores; clients follow them as today.
- The steal count resets whenever an order returns to the pool or spawns again.
- Every number lives in `StealValueRules` as a named constant, to tune in playtests.
- Git: work on `steam-phase1a`. Stage only the files named in each task; the working tree has unrelated modified fonts and `FinalAreaScene.unity`.
- Commit messages end with `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.

## Review Focus

1. A thief with both slots full bumps a rival who carries orders: the orders fall, nothing is taken, so no payment and no doubling (checked in the playtest in Task 4; the count and the payment live only inside the "takes a package" branch).
2. A robbed player steals the same order back: they are paid once per order, and the order still doubles (`PayForSteal_PaysOncePerOrderPerThief`, Task 2).
3. An order that returns to the pool and spawns again starts at its price with an empty payment record (`ResetSteals_BackToThePriceAndForgetsThieves`, Task 2).
4. A long run of steals cannot overflow a score (`Worth_ManyStealsIsCappedNotOverflowed`, Task 1).
5. The golden order never doubles but still pays its thief (`Worth_GoldenNeverDoubles`, Task 1; `PayForSteal_PaysForTheGoldenOrderToo`, Task 2).

---

## File Structure

- Create `Assets/Scripts/World/StealValueRules.cs`: the rules (pure, static).
- Modify `Assets/Scripts/World/Order.cs`: the steal count, `Worth`, the payment record, the look.
- Modify `Assets/Scripts/Player/OrderHandler.cs`: score by worth, best order by worth, pay the thief.
- Create `Assets/Tests/Editor/StealValueRulesTests.cs` and `Assets/Tests/Editor/OrderStealValueTests.cs`.
- Modify `docs/controls.md`, `docs/testing.md`.

### Task 1: The rules

**Files:**
- Create: `Assets/Scripts/World/StealValueRules.cs`
- Test: `Assets/Tests/Editor/StealValueRulesTests.cs`

**Interfaces:**
- Produces (all `public static` on `public static class StealValueRules`):
  - `const int EASY = 55, MEDIUM = 65, HARD = 80, GOLDEN = 50`
  - `const int STEAL_PAY = 7`
  - `const int MAX_COUNTED_STEALS = 16` (a guard against overflow, not a design cap: 80 × 2^16 fits an `int`)
  - `int Price(Constants.OrderValue tier)`
  - `int Worth(Constants.OrderValue tier, int steals)`
  - `int Pay(HashSet<int> alreadyPaid, int thiefSeat)`
  - `float Swell(int steals)`

- [ ] **Step 1: Write the failing tests** in `StealValueRulesTests` (follow `StealRulesTests`' style). Tests and assertions:
  - `Prices_AreTheTimeBasedOnes`: `Price(Easy)==55`, `Price(Medium)==65`, `Price(Hard)==80`, `Price(Golden)==50`.
  - `Worth_DoublesPerSteal`: `Worth(Easy,0)==55`, `Worth(Easy,1)==110`, `Worth(Easy,2)==220`, `Worth(Hard,2)==320`.
  - `Worth_GoldenNeverDoubles`: `Worth(Golden,0)==50` and `Worth(Golden,3)==50`.
  - `Worth_ManyStealsIsCappedNotOverflowed`: `Worth(Hard,100)==80*65536` and is positive; `Worth(Hard,-3)==80`.
  - `Pay_FirstTimePerThiefPays_ThenNothing`: with one `HashSet<int>`, `Pay(set,0)==7`, `Pay(set,0)==0`, `Pay(set,1)==7`.
  - `Pay_UnknownSeat_PaysNothing`: `Pay(set,-1)==0` and the set stays empty.
  - `Swell_GrowsPerStealThenStops`: `Swell(0)==1f`, `Swell(1)` ≈ 1.15f, `Swell(4)` ≈ 1.6f, `Swell(10)` ≈ 1.6f (`Assert.AreEqual(expected, actual, 0.001f)`).

- [ ] **Step 2: Run to verify they fail**

Run: `python tools/compile-check.py`
Expected: errors that `StealValueRules` does not exist.

- [ ] **Step 3: Implement `StealValueRules`** with the signatures above in `Assets/Scripts/World/StealValueRules.cs`. `Worth` for a non-golden tier is `Price × 2^min(max(steals,0), MAX_COUNTED_STEALS)` (use a shift); for `Golden` it is `GOLDEN`. `Pay` returns 0 for a negative seat or a seat already in the set, else adds the seat and returns `STEAL_PAY`. `Swell` is `1 + 0.15 × min(max(steals,0), 4)`. Put a summary on the class in the project's comment style (what, why, where it is used), noting the enum's numbers are only identifiers.

- [ ] **Step 4: Run to verify they pass**

Run: `python tools/compile-check.py` → `COMPILES`, then `bash tools/run-tests.sh StealValueRulesTests`
Expected: all 7 pass.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/World/StealValueRules.cs Assets/Scripts/World/StealValueRules.cs.meta Assets/Tests/Editor/StealValueRulesTests.cs Assets/Tests/Editor/StealValueRulesTests.cs.meta
git commit -m "Steal value: the rules (prices, doubling, thief payment)"
```

### Task 2: Orders count steals and scoring uses worth

**Files:**
- Modify: `Assets/Scripts/World/Order.cs` (the `value` field around line 12; `InitOrder` ~159; `EraseOrder` ~404)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`order2` ~line 18; delivery `score +=` lines ~169 and ~183; `GetBestOrder` ~330; `StealFrom` ~400; `TakeOrderFrom` ~436)
- Test: `Assets/Tests/Editor/OrderStealValueTests.cs`

**Interfaces:**
- Consumes: `StealValueRules.Worth`, `StealValueRules.Pay` (Task 1).
- Produces:
  - `Order`: `public int Steals`, `public int Worth`, `internal readonly HashSet<int> PaidThieves`, `internal void CountSteal()`, `internal void ResetSteals()`.
  - `OrderHandler`: `internal int PayForSteal(Order stolen, int thiefSeat)` (adds to `score`, returns what was paid); `order2` becomes `internal`.

- [ ] **Step 1: Write the failing tests** in `OrderStealValueTests` (use `TestObjects` as in `OrderHandlerResetTests`; set `order.value` directly, it is `internal`). Tests and assertions:
  - `CountSteal_RaisesWorthByDoubling`: a Hard order; `CountSteal()` twice → `Steals==2`, `Worth==320`.
  - `ResetSteals_BackToThePriceAndForgetsThieves`: after two `CountSteal()` and a `PaidThieves.Add(0)`, `ResetSteals()` → `Steals==0`, `Worth==80`, `PaidThieves.Count==0`.
  - `Golden_WorthStaysAtItsPrice`: a Golden order after `CountSteal()` ×3 → `Worth==50`.
  - `PayForSteal_PaysOncePerOrderPerThief`: handler `Score` 0; `PayForSteal(order,0)` returns 7 and `Score==7`; again returns 0 and `Score==7`; `PayForSteal(order,1)` returns 7 and `Score==14`.
  - `PayForSteal_PaysForTheGoldenOrderToo`: a Golden order, `PayForSteal(order,0)` returns 7.
  - `PayForSteal_UnknownSeat_PaysNothing`: `PayForSteal(order,-1)` returns 0, `Score` unchanged.
  - `GetBestOrder_ChoosesByWorth`: `order1` an Easy order stolen twice (worth 220), `order2` a Hard order (worth 80) → `GetBestOrder()` returns the Easy one; with no steals, the Hard one wins.

- [ ] **Step 2: Run to verify they fail**

Run: `python tools/compile-check.py`
Expected: errors for the missing members.

- [ ] **Step 3: Implement in `Order`:** a private `int steals`; `public int Steals => steals;` `public int Worth => StealValueRules.Worth(value, steals);` `internal readonly HashSet<int> PaidThieves = new HashSet<int>();` `internal void CountSteal() { steals++; }` `internal void ResetSteals()` sets `steals = 0` and clears `PaidThieves`. Call `ResetSteals()` at the top of `InitOrder` (after its `MayChange` guard) and in `EraseOrder` (after its guard), so every machine resets when it replays a spawn or an erase.

- [ ] **Step 4: Implement in `OrderHandler`:**
  - Make `order2` `internal`.
  - Delivery: `score += order1.Worth` and `score += order2.Worth` replace the `(int)order.Value` additions. The golden order's delivery still adds its price (50), and `AwardGoldenBonus` is unchanged.
  - `GetBestOrder` compares `Worth` instead of `Value`. `SetDrivingIndicators` keeps using `.Value` (the tier picks the icon).
  - `internal int PayForSteal(Order stolen, int thiefSeat)`: `int paid = StealValueRules.Pay(stolen.PaidThieves, thiefSeat); score += paid; return paid;`.
  - `TakeOrderFrom`: call `order.CountSteal()` right after `AddOrder(order)`, so every machine that replays the Steal change counts it.
  - `StealFrom`: inside the existing branch that takes the order (after `TakeOrderFrom`, before `ReportSteal`), call `PayForSteal(newOrder, OrderSync.SeatOf(this))`, then `ShowScore()` and `ScoreManager.Instance.UpdatePlacement()` as the delivery code does. The slots-full case and STEALS OFF never reach this branch, so they pay and double nothing.

- [ ] **Step 5: Run to verify they pass**

Run: `python tools/compile-check.py` → `COMPILES`, then `bash tools/run-tests.sh OrderStealValueTests`
Expected: all 7 pass.

- [ ] **Step 6: Run the existing order and steal tests** for regressions.

Run: `bash tools/run-tests.sh "OrderHandlerResetTests|OrderChangeTests|OrderSyncTests|OrderRulesTests|StealRulesTests"`
Expected: all pass.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/World/Order.cs Assets/Scripts/Player/OrderHandler.cs Assets/Tests/Editor/OrderStealValueTests.cs Assets/Tests/Editor/OrderStealValueTests.cs.meta
git commit -m "Steal value: orders count steals, score their worth, pay the thief"
```

### Task 3: Show the worth on the order

**Files:**
- Modify: `Assets/Scripts/World/Order.cs` (`InitOrder` switch ~line 179; `CountSteal`)

**Interfaces:**
- Consumes: `StealValueRules.Swell` (Task 1), `Order.CountSteal`/`ResetSteals` (Task 2).
- Produces: `private int PackageType()` (0 Easy, 1 Medium, 2 Hard, 3 Golden) and `private void ApplyStealLook()`.

- [ ] **Step 1: Implement `PackageType()`** and use it in `InitOrder` in place of its local `packageType` switch (same mapping as today).

- [ ] **Step 2: Implement `ApplyStealLook()`:** do nothing if `orderMeshObject` is null or the order is Golden; else set `orderMeshObject.transform.localScale` to `meshScale[PackageType()] × StealValueRules.Swell(steals)` on all three axes. Call it at the end of `CountSteal()` and `ResetSteals()`. `InitOrder` still sets the base scale, so a respawned order starts at its normal size.

- [ ] **Step 3: Verify the edit-mode tests still pass** (a test order has no mesh object, so `ApplyStealLook` must return early).

Run: `python tools/compile-check.py` → `COMPILES`, then `bash tools/run-tests.sh "OrderStealValueTests|StealValueRulesTests"`
Expected: all pass.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/World/Order.cs
git commit -m "Steal value: a stolen order swells with each steal"
```

### Task 4: Docs, full test run and a playtest

**Files:**
- Modify: `docs/controls.md` (Match rules section), `docs/testing.md`

- [ ] **Step 0: Check the two open questions from the spec** by reading code, and record each answer in the commit message:
  - Joining mid-match: `Constants.SPAWN_MID_MATCH` is `false`, and `docs/online.md` says whether a machine can rejoin a running match. If nobody can join or rejoin mid-match, no machine can miss a steal count and nothing more is needed; if a rejoin exists, make its replay of the order state carry each order's `Steals` (add a task before shipping).
  - The tutorial: confirm the tutorial's steal lesson does not leave points in the match score (`TutorialManager`, `OrderHandler.ResetHandler`). If it does, skip `PayForSteal` while `GameManager.Instance.MainState == GameState.Tutorial`.
  - Awards, Steam feats and the highlight reel keep counting tiers and steals as today; no change.

- [ ] **Step 1: Document the rules** in `docs/controls.md` under **Match rules**: a bullet that each steal doubles a normal order's worth (not the golden order's), the thief is paid 7 once per order for taking a package, STEALS: OFF turns both off, and the prices are Easy 55, Medium 65, Hard 80 (golden 50, +$1 a second). Add `StealValueRulesTests` (prices, doubling, the cap, the payment, the look) and `OrderStealValueTests` (an order's count, its reset, scoring by worth, the thief's payment) to `docs/testing.md` in the same style as the entries around them.

- [ ] **Step 2: Run the whole EditMode suite**

Run: `bash tools/run-tests.sh`
Expected: all pass (the run takes several minutes; results in `Logs/test-results.xml`).

- [ ] **Step 3: Playtest in the editor** with the dev hotkeys (`docs/dev-hotkeys.md`; drive with the arrows) on a local match with two players. Check each of these and note the result:
  - Delivering an Easy, a Medium and a Hard order scores 55, 65 and 80.
  - Player 2 boosts into player 1, who carries one order: player 2 gets it, their score rises by 7 at once, and the order looks larger. Player 1 steals it back: they are paid 7 once, and delivering it scores the tier price × 4.
  - Player 2 carries two orders and boosts into player 1, who carries one: player 1's order falls, player 2 takes nothing and their score does not change.
  - Match Rules STEALS: OFF: a bump takes nothing, pays nothing, and an order keeps its look.
  - A delivered order that spawns again is its normal size and price.
  - The golden order changes hands without growing larger, and its thief is still paid 7.

- [ ] **Step 4: Commit**

```bash
git add docs/controls.md docs/testing.md
git commit -m "Steal value: document the rules and tests"
```

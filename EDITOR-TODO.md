# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-09-29 (Phase 3E: orders online).

## 1. Save the online prefabs' network IDs (1 minute, main editor only)

- Still to do from the code review: the prefab files still hold the old IDs.
- In the **main editor**, not in Play Mode (the item is greyed out there): **Tools → Dead on Arrival → Online → Save Network Prefab IDs**.
- `git status` then shows 3 changed prefabs in `Assets/Prefabs/Online/`, each with one changed line (`GlobalObjectIdHash`).
- Until then `OnlinePrefabsTests.SavedPrefabIds_AreUnique_AndMatchTheIdNetcodeComputes` fails on purpose. It's the only red test.

## 2. Let both editors import Phase 3E (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New scripts: `OrderBook`, `OrderChange`, `OrderSync`, `OnlineOrders`, `HostQueue`.
  - Changed: `Order`, `OrderHandler`, `OrderBeacon`, `OnlineMatch`, `OnlinePlayer`, `OnlineGame`, `PlayerInstantiate`, and comments in `RemoteAvatar` and `TutorialManager`.
  - New tests: `OrderBookTests`, `OrderBookSceneTests`, `OrderChangeTests`, `OrderSyncTests`, `OrderRulesTests`, `HostQueueTests`, `OrderMessagesNetworkTests`, `OnlineOrdersNetworkTests`.
- The Console should end with no red errors.

## 3. Orders in two editors (10 minutes)

- Play in both editors. Editor 1: **Tools → Dead on Arrival → Online → Host**. The clone: **Join This Computer** (`docs/online.md`, Two editors).
- Ready up in both: the match starts in the city.
- Both editors show the same orders appearing.
- Drive the clone's scooter into an order's light: the order rides on it in both editors (a moment later in the clone).
- Deliver it: the same score in both HUDs, and the same placings.
- In the clone, fall in the water holding an order: it drops at the respawn point in both.
- Play on to the golden round: its value climbs the same in both. Deliver it: the results match.
- Then again with **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)** ticked in the clone before **Join**: pickups show a little later there, and everything else still matches.
- Anything odd? Note it, plus each editor's Console lines starting `Online:`.

## 4. From Phase 3D, if not done yet: Steam (20 minutes)

- One PC, Steam running: **Play**, A, **Play** → player select. **Y** hosts (the line along the top changes). **B** goes back offline.
- Two PCs, two Steam accounts that are friends: follow `docs/online.md`, Two PCs over Steam. Orders now show on both PCs too.

## 5. Review and commit (your call)

- Not committed: Phase 3E on `steam-phase1a`, on top of your "Phase 3E" commit (Phase 3D and this phase's plan).
- How it works: `docs/online.md` (Phase 3E: orders online).
- Suggested commit title: "Phase 3E: orders online".

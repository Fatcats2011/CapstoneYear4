# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-09-29 (Phase 3G: steals, clashes and respawns online).

## 1. Let both editors import Phase 3G (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New scripts: `StealRules`, `StealSync`, `PlayerHit`, `OnlineSteals`, `RespawnSync`, `OnlineRespawns`.
  - Changed: `OrderHandler`, `Order`, `OrderChange`, `OnlineOrders`, `OnlineMatch`, `OnlineGame`, `HostQueue`, `RemoteAvatar`, `Respawn`, `RespawnManager`, `RespawnPoint`.
  - New tests: `StealRulesTests`, `PlayerHitTests`, `RespawnPointsTests`, `StealMessagesNetworkTests`, `RespawnMessagesNetworkTests`, `OnlineStealsNetworkTests`, `OnlineRespawnsNetworkTests`.
- The Console should end with no red errors.

## 2. Steals, clashes and respawns in two editors (15 minutes)

- Play in both editors. Editor 1: **Tools → Dead on Arrival → Online → Host**. The clone: **Join This Computer** (`docs/online.md`, Two editors).
- Ready up in both, and drive out of the tutorial into the first wave in both.
- In the clone, pick up an order. In editor 1, boost into the clone's scooter:
  - The order jumps to editor 1's scooter in both editors.
  - The clone's scooter spins out and bounces away.
- Boost into each other at the same time: both scooters bounce, and no order moves.
- In the clone, steal it back: the same shows in both.
- In the golden round, steal the golden order: afterwards the robbed player isn't slower than before.
- Drive both scooters into the water at one spot: they rise on different respawn points, each with their own orders.
- Then again with **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)** ticked in the clone before **Join**. In the clone, hits land a moment after the bump.
- Anything odd? Note it, plus each editor's Console lines starting `Online:`.

## 3. From Phase 3F, if not done yet: the tutorial and orders in two editors (15 minutes)

- Play in both editors, host in editor 1 and join in the clone, and ready up in both. After the opening cutscene, each player is at the start of their own tutorial lane.
- In the clone: pick up the tutorial order, then boost into the cardboard cutout.
  - Its barrier drops at once.
  - The order rides on the clone's scooter in both editors.
- In editor 1 only, finish the tutorial and drive out into the city: the first wave waits.
- Drive out in the clone: the waves start in both.
- Orders:
  - Both editors show the same orders.
  - Deliver one in the clone: the same score in both HUDs.
  - The golden round's value climbs in both, and the results match.

## 4. Optional: tidy the game scene (2 minutes, main editor only)

- The spawn manager no longer uses the city start points: players start in their tutorial lanes.
- In the main editor, not in Play Mode: open `Design Scene(Main)`, delete **Spawning Manager → Normal Positions** (the four city `Spawn 1-4` objects), and save the scene.
- Nothing else refers to them.

## 5. From Phase 3D, if not done yet: Steam (20 minutes)

- One PC, Steam running: **Play**, A, **Play** → player select. **Y** hosts (the line along the top changes). **B** goes back offline.
- Two PCs, two Steam accounts that are friends: follow `docs/online.md`, Two PCs over Steam. The tutorial, orders, steals and respawns play on both PCs too.

## 6. Review and commit (your call)

- Not committed: Phase 3G on `steam-phase1a`, on top of "Online Prefabs".
- How it works: `docs/online.md` (Phase 3G: steals, clashes and respawns).
- Suggested commit title: "Phase 3G: steals, clashes and respawns online".

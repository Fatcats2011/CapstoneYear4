# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-09-29 (Phase 3F: the tutorial online).

## 1. Save the online prefabs' network IDs (1 minute, main editor only)

- Still to do from the code review: the prefab files still hold the old IDs.
- In the **main editor**, not in Play Mode (the item is greyed out there): **Tools → Dead on Arrival → Online → Save Network Prefab IDs**.
- `git status` then shows 3 changed prefabs in `Assets/Prefabs/Online/`, each with one changed line (`GlobalObjectIdHash`).
- Until then `OnlinePrefabsTests.SavedPrefabIds_AreUnique_AndMatchTheIdNetcodeComputes` fails on purpose. It's the only red test.

## 2. Let both editors import Phase 3F (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New scripts: `TutorialSync`, `OnlineTutorial`.
  - Changed: `TutorialManager`, `SpawnManager`, `CutoutHandler`, `CutoutManager`, `Order`, `OnlineMatch`, `OnlineGame`.
  - New tests: `TutorialManagerTests`, `TutorialMessagesNetworkTests`, `OnlineTutorialNetworkTests`.
- The Console should end with no red errors.

## 3. The tutorial and orders in two editors (15 minutes)

- Play in both editors. Editor 1: **Tools → Dead on Arrival → Online → Host**. The clone: **Join This Computer** (`docs/online.md`, Two editors).
- Ready up in both. After the opening cutscene, each player is at the start of their own tutorial lane.
- In the clone: pick up the tutorial order, then boost into the cardboard cutout.
  - Its barrier drops at once.
  - The order rides on the clone's scooter in both editors.
- In editor 1 only, finish the tutorial and drive out into the city: the first wave waits.
- Drive out in the clone: the waves start in both.
- Orders, if you haven't checked them since Phase 3E:
  - Both editors show the same orders.
  - Deliver one in the clone: the same score in both HUDs.
  - Fall in the water holding one: it drops at the respawn point in both.
  - The golden round's value climbs in both, and the results match.
- Then again with **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)** ticked in the clone before **Join**.
- Anything odd? Note it, plus each editor's Console lines starting `Online:`.

## 4. Optional: tidy the game scene (2 minutes, main editor only)

- The spawn manager no longer uses the city start points: players start in their tutorial lanes again.
- In the main editor, not in Play Mode: open `Design Scene(Main)`, delete **Spawning Manager → Normal Positions** (the four city `Spawn 1-4` objects), and save the scene.
- Nothing else refers to them.

## 5. From Phase 3D, if not done yet: Steam (20 minutes)

- One PC, Steam running: **Play**, A, **Play** → player select. **Y** hosts (the line along the top changes). **B** goes back offline.
- Two PCs, two Steam accounts that are friends: follow `docs/online.md`, Two PCs over Steam. The tutorial and orders now play on both PCs too.

## 6. Review and commit (your call)

- Not committed: Phase 3F on `steam-phase1a`, on top of "Phase 3E: orders online".
- How it works: `docs/online.md` (Phase 3F: the tutorial online).
- Suggested commit title: "Phase 3F: the tutorial online".

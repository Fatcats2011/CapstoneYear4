# Editor to-do (master list)

Things only you can do in the Unity editor, top to bottom. Updated 2026-09-28 (online start fixes).

## 1. Let both editors import the fixes (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. Scripts only, no scene or prefab changes:
  - `SpawnManager`, `TutorialManager`, `PlayerInstantiate`.
  - Tests: `SpawnManagerTests`, `OnlineMatchNetworkTests`, and the new `GameSceneSpawnTests`.
- The Console should end with no red errors.

## 2. Check the start of an online match (5 minutes)

- Start a two-editor match as before: **Play** in both, **Host** in editor 1, **Join This Computer** in editor 2, ready up in both.
- After the opening cutscene, both players start **in the city**, side by side (the four *Normal Positions* under *Spawning Manager*), not on the tutorial road.
  - Each editor shows the other's scooter beside its own.
- Optional: go back to the menu, ready up again, and pick **Online → Leave** in editor 2 while the loading screen shows. Editor 1's match still starts, with its player in the city.
- Offline split screen is unchanged: the tutorial still plays, from the tutorial road.
- Anything odd? Note it, plus both editors' Console lines starting `Online:`.

## 3. Review and commit (your call)

- Not committed: working-tree changes on `steam-phase1a`, on top of your Phase 3C commit.
- How it works: `docs/online.md`.

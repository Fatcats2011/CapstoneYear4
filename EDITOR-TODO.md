# Editor to-do (master list)

Things only you can do in the Unity editor, top to bottom. Updated 2026-09-28 (code review fixes).

## 1. Let both editors import the fixes (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. Scripts only, no scene or prefab changes:
  - `OnlineGame`, `PlayerInstantiate`, `RemoteAvatar`, `Respawn`, `BallDriving`, `DriveFlags`, `OnlineScooter`, `OnlineDriving`.
  - New editor command: `NetworkPrefabIds` (step 2).
  - Tests: `OnlinePrefabsTests`, `RemoteAvatarTests`, `OnlineGameNetworkTests`, `OnlineSeatTests`, `DriveFlagsTests`, `RemoteDrivingTests`, `RemoteScooterRulesTests`, `OnlineScootersNetworkTests`.
- The Console should end with no red errors.

## 2. Save the online prefabs' network IDs (1 minute, main editor only)

- In the **main editor**, not in Play Mode: **Tools → Dead on Arrival → Online → Save Network Prefab IDs**.
- `git status` then shows 3 changed prefabs in `Assets/Prefabs/Online/`: `OnlineMatch`, `OnlinePlayer`, `OnlineScooter`.
  - Each has one changed line, `GlobalObjectIdHash`.
- Why: `OnlineMatch` and `OnlinePlayer` saved the same placeholder ID, which could break online play in a build. The editor hid it.
- Until you do this, `OnlinePrefabsTests.SavedPrefabIds_AreUnique_AndMatchTheIdNetcodeComputes` fails on purpose. It's the only red test.

## 3. Check the fixes in two editors (10 minutes)

- **Host leaves in player select:**
  - **Play** in both, **Host** in editor 1, **Join This Computer** in editor 2.
  - Ready up in editor 2 only, then **Online → Leave** in editor 1.
  - Editor 2 stays in player select, **not ready**, and no countdown starts. Before the fix it started a solo match.
- **Respawn:**
  - Host and join again, ready up in both, start the match.
  - In editor 2, drive into the water (or press **R**).
  - In editor 1, editor 2's scooter vanishes at once: no ghost sitting in the water, no sliding across the map. It reappears rising from its grave.
- Offline split screen is unchanged: water respawns and the ready countdown work as before.
- Anything odd? Note it, plus both editors' Console lines starting `Online:`.

## 4. Review and commit (your call)

- Not committed: the code review fixes on `steam-phase1a`, on top of your "Online Support" commit.
- How it works: `docs/online.md`.

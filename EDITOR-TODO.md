# Editor to-do (master list)

Things only you can do in the Unity editor, top to bottom. Updated 2026-09-28 (Phase 3C).

## 1. Let both editors import Phase 3C (2 minutes)

- Click into the **main editor**. It imports the new scripts and prefab, and recompiles:
  - `Assets/Scripts/Online/`: `OnlineScooter`, `OnlineDriving`, `DriveFlags`, `ScooterPose`, `OwnerNetworkTransform`, `LoadRound`, `MatchClock`.
  - `Assets/Prefabs/Online/OnlineScooter.prefab`.
- Then click into the **ParrelSync clone**. It shares `Assets/`, so it imports the same files.
- If Unity says `OnlinePrefabs.asset` or `DefaultNetworkPrefabs.asset` changed on disk, reload them.
- The Console should end with no red errors.

## 2. Play an online match in two editors (10 minutes)

- If your controller doesn't respond even offline, sort that out first. It's the Steam controller-support issue, looked at in its own session: turn off Steam's controller support (Steam → Settings → Controller) and try again.
- Press **Play** in both editors. In each, wait for the title screen and press **A**.
- Editor 1: **Tools → Dead on Arrival → Online → Host**. Editor 2: **Tools → Dead on Arrival → Online → Join This Computer**.
- **Ready up** in both. Click into an editor before pressing its buttons.
- What you should see:
  - After the countdown, both show the loading screen. The game appears in both at about the same time: editor 1 waits for editor 2.
  - The opening cutscene plays in each, then driving starts. There's no tutorial online.
  - Drive in one editor and watch the other: your scooter moves there, with its boost trail, skid marks and drift sparks.
  - The match clock shows the same time in both.
  - Orders are editor 1's alone for now. Editor 2 doesn't see them (roadmap Task 3.5).
- **Pause** in editor 1 and pick **Main Menu**: both go back to the menu, still in the session.
- Optional: start another match, then **Online → Leave** in editor 2 mid-match. Its scooter disappears from editor 1, and editor 1's match goes on.
- Anything odd? Note it, plus both editors' Console lines starting `Online:`.

## 3. Review and commit (your call)

- Phase 3C is **not committed**. Everything is on `steam-phase1a` as working-tree changes, together with the 2026-09-24 slider fix.
- Plan: `docs/superpowers/plans/2026-09-27-phase3c-driving-together.md`.
- How it works: `docs/online.md`.

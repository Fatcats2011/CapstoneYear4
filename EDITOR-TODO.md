# Editor to-do (master list)

Things only you can do in the Unity editor, top to bottom. Written while you were away, 2026-09-24.

## 1. Let both editors import Phase 3B (2 minutes)

- Click into the **main editor**. It imports the new scripts and prefabs and recompiles:
  - `Assets/Scripts/Online/*`
  - `Assets/Prefabs/Online/OnlinePlayer.prefab`, `OnlineMatch.prefab`
  - `Assets/Resources/Online/OnlinePrefabs.asset`
- Then click into the **ParrelSync clone**. It shares `Assets/`, so it imports the same files. No package changes this time.
- If Unity says `DefaultNetworkPrefabs.asset` changed on disk, reload it. Netcode itself lists the two new prefabs there.
- The Console should end with no red errors.

## 2. Try online player select in two editors (5 minutes)

- Press **Play** in both editors. In each, wait for the title screen and press **A** on a controller.
  - A controller only drives the editor that has focus: click into an editor before pressing buttons.
- Editor 1: **Tools → Dead on Arrival → Online → Host**. It jumps into player select.
  - Host and Join are greyed out until the title screen is up.
- Editor 2: **Tools → Dead on Arrival → Online → Join This Computer**.
- What you should see:
  - Editor 2's player moves to the **second podium**, in the second company's colours.
  - Each editor shows the **other's scooter** on its podium.
  - Change **colour or hat** in one editor, and the other updates within a moment.
  - Editor 2 follows editor 1: if editor 1 backs out to the title screen, editor 2 goes too.
  - **Ready up** in both. The countdown runs, then the Console says `Online: starting an online match isn't in yet…`. Nothing loads (that's Phase 3C).
- **Online → Leave** in editor 2: its scooter disappears from editor 1's podium.
- Anything odd? Note it, plus both editors' Console lines starting `Online:`.

## 3. Review and commit (your call)

- Phase 3B is **not committed**. Everything is on `steam-phase1a` as working-tree changes.
- Plan: `docs/superpowers/plans/2026-09-24-phase3b-online-player-select.md`.
- How it works: `docs/online.md`.
- A suggested commit message is in my final message.

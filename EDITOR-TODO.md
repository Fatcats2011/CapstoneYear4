# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-10-01 (Phase 3H: one-shots, cutscenes and pause online).

## 1. Let both editors import Phase 3H (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New scripts: `ScooterCue`, `ClockCue`, `CueSync`, `RemoteSound`, `OnlineCues`, `PausePolicy`.
  - Changed: `OnlineMatch`, `OnlineGame`, `RemoteAvatar`, `SoundPool`, `PhaseIndicator`, `Respawn`, `OrderManager`, `CutsceneManager`, `PlayerInstantiate`, `BallDriving`, `BallCollision`.
  - New tests: `ScooterCueTests`, `RemoteSoundTests`, `PausePolicyTests`, `CueMessagesNetworkTests`, `OnlineCuesNetworkTests`, `OnlineMatchFlowNetworkTests`, `OnlinePauseTests`.
- The Console should end with no red errors.

## 2. One-shots, cutscenes and pause in two editors (20 minutes)

- Play in both editors. Editor 1: **Tools → Dead on Arrival → Online → Host**. The clone: **Join This Computer** (`docs/online.md`, Two editors).
- Ready up in both. During the opening cutscene, press **L** in the clone: nothing happens. Press **L** in editor 1: both skip to the tutorial.
- Drive out of the tutorial into the first wave in both, then:
  - Drive the two scooters side by side and boost in the clone: editor 1 hears it. Drive apart: it fades, and it's silent from about 60 m.
  - After a boost in the clone, wait for its horn to fill: editor 1 sees the clone's horns flash, and hears it if close.
  - Steal from each other: the robbed editor hears the thief's whoosh.
  - Drive the clone's scooter into the water: editor 1 sees its wisp, then its gravestone, then it rises from it with a sparkle.
  - Drive the clone's scooter through pedestrians and cans in editor 1's view: they react in editor 1. Tail it closely: editor 1's scooter gets the slipstream.
  - At each new wave, the bells ring in both. At the whistle, both scooters stop.
- Pause in the clone: editor 1 drives on, and the clone shows "Online: the match goes on while you're paused. Main Menu leaves it."
  - Leave it paused until the main game ends: its pause menu closes for the golden cutscene.
  - Pause in editor 1: its hint says Main Menu ends the match for everyone.
- Then again with **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)** ticked in the clone before **Join**. In editor 1, the clone's sounds come a moment late.
- Anything odd? Note it, plus each editor's Console lines starting `Online:`.

## 3. From Phases 3F and 3G, if not done yet: the tutorial, orders, steals and respawns in two editors (20 minutes)

- Play in both editors, host in editor 1 and join in the clone, and ready up in both. After the opening cutscene, each player is at the start of their own tutorial lane.
- In the clone: pick up the tutorial order, then boost into the cardboard cutout. Its barrier drops at once, and the order rides on the clone's scooter in both editors.
- In editor 1 only, finish the tutorial and drive out into the city: the first wave waits. Drive out in the clone: the waves start in both.
- Orders: both editors show the same orders. Deliver one in the clone: the same score in both HUDs. The golden round's value climbs in both, and the results match.
- In the clone, pick up an order. In editor 1, boost into the clone's scooter: the order jumps to editor 1's scooter in both editors, and the clone's scooter spins out and bounces away.
- Boost into each other at the same time: both scooters bounce, and no order moves.
- In the golden round, steal the golden order: afterwards the robbed player isn't slower than before.
- Drive both scooters into the water at one spot: they rise on different respawn points, each with their own orders.

## 4. Optional: tidy the game scene (2 minutes, main editor only)

- The spawn manager no longer uses the city start points: players start in their tutorial lanes.
- In the main editor, not in Play Mode: open `Design Scene(Main)`, delete **Spawning Manager → Normal Positions** (the four city `Spawn 1-4` objects), and save the scene.
- Nothing else refers to them.

## 5. From Phase 3D, if not done yet: Steam (20 minutes)

- One PC, Steam running: **Play**, A, **Play** → player select. **Y** hosts (the line along the top changes). **B** goes back offline.
- Two PCs, two Steam accounts that are friends: follow `docs/online.md`, Two PCs over Steam. Everything above plays on both PCs too.

## 6. Review and commit (your call)

- Not committed: Phase 3H on `steam-phase1a`, on top of "Phase 3G: steals, clashes and respawns online".
- How it works: `docs/online.md` (Phase 3H: one-shots, cutscenes and pause).
- Suggested commit title: "Phase 3H: one-shots, cutscenes and pause online".

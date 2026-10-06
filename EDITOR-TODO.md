# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-10-06 (Phase 4A: Steam features in the game).

## 1. Let both editors import Phase 4A (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New folder `Assets/Scripts/Steam`: `Achievement`, `IAchievementStore`, `SteamAchievementStore`, `MatchFeats`, `IPresence`, `SteamPresence`, `PresenceRules`, `FeatSync`, `SteamFeatures`, `SessionLog`, `SessionLogWriter`.
  - New script: `OnlineAchievements`. Changed: `OnlineMatch`, `OnlineGame`, `OrderHandler`.
  - New tests: `AchievementsTests`, `PresenceRulesTests`, `SteamFeaturesTests`, `AchievementMessagesNetworkTests`, `SessionLogTests`.
- The Console should end with no red errors.

## 2. Optional now: Phase 4A with Steam running (10 minutes)

- Steam running, one offline match in the editor. A friend's Steam friends list shows you in Spacewar, with the raw token (`#DeliveringSolo`) or nothing: the texts arrive with the `.vdf` upload (section 9).
- The first delivery logs `Steam: this app has no achievement FIRST_DELIVERY` once: normal on app 480.
- A build launched with `-sessionlog`: open `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\logs\`. There's a `session-….log` with a header, and the `Online:` lines of any online match.

## 3. From Phase 3I, if not done yet: disconnects in two editors (15 minutes)

- Play in both editors. Editor 1: **Tools → Dead on Arrival → Online → Host**. The clone: **Join This Computer** (`docs/online.md`, Two editors).
- Each editor's Console shows an `Online: the Game scene was ready here after …` line once the match is up.
- Mid-match, stop Play in editor 1: the clone goes back to the title screen and says "The host left the match.".
- Again, three more times, stopping Play in editor 1:
  - during the opening cutscene, with the clone paused;
  - on the loading screen, before the game appears;
  - on the loading screen, just before the game appears (the clone's load is done, and it waits for editor 1).
  - Each time the clone ends on the title screen with the message, and its match never starts.
- In the golden round, pick up the golden order in the clone, then stop Play in the clone: in editor 1 the golden order is back at its start, worth its starting value, and the round goes on. Pick it up in editor 1: its value climbs.
- Anything odd? Note it, plus each editor's Console lines starting `Online:`.

## 4. From Phase 3I, if not done yet: two PCs over Steam, time a load (10 minutes)

- On the slowest PC, start an online match in a build (`docs/online.md`, Two PCs over Steam).
- Open `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\Player.log` and find `Online: the Game scene was ready here after … s; its longest frame took … s`.
- Note both numbers, and whether the other PC stayed in the match.

## 5. From Phase 3H, if not done yet: one-shots, cutscenes and pause in two editors (20 minutes)

- Host in editor 1, join in the clone, and ready up in both. During the opening cutscene, press **L** in the clone: nothing happens. Press **L** in editor 1: both skip to the tutorial.
- Drive out of the tutorial into the first wave in both, then:
  - Boost in the clone beside editor 1's scooter: editor 1 hears it. Drive apart: it fades, and it's silent from about 60 m.
  - After a boost in the clone, wait for its horn to fill: editor 1 sees the clone's horns flash.
  - Steal from each other: the robbed editor hears the thief's whoosh.
  - Drive the clone's scooter into the water: editor 1 sees its wisp, then its gravestone, then it rises from it with a sparkle.
  - Drive the clone's scooter through pedestrians and cans in editor 1's view: they react. Tail it closely: editor 1's scooter gets the slipstream.
  - At each new wave, the bells ring in both. At the whistle, both scooters stop.
- Pause in the clone: editor 1 drives on, and the clone's hint says Main Menu leaves the match. Pause in editor 1: its hint says Main Menu ends it for everyone.
- Then again with **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)** ticked in the clone before **Join**.

## 6. From Phases 3F and 3G, if not done yet: the tutorial, orders, steals and respawns in two editors (20 minutes)

- Host in editor 1, join in the clone, and ready up in both. After the opening cutscene, each player is at the start of their own tutorial lane.
- In the clone: pick up the tutorial order, then boost into the cardboard cutout. Its barrier drops at once, and the order rides on the clone's scooter in both editors.
- Finish the tutorial in editor 1 only: the first wave waits. Drive out in the clone: the waves start in both.
- Deliver an order in the clone: the same score in both HUDs. The golden round's value climbs in both, and the results match.
- In the clone, pick up an order. In editor 1, boost into the clone's scooter: the order jumps to editor 1's scooter in both, and the clone's scooter spins out.
- Boost into each other at the same time: both bounce, and no order moves.
- In the golden round, steal the golden order: the robbed player isn't slower afterwards.
- Drive both scooters into the water at one spot: they rise on different respawn points.

## 7. Optional: tidy the game scene (2 minutes, main editor only)

- Not in Play Mode: open `Design Scene(Main)`, delete **Spawning Manager → Normal Positions** (the four city `Spawn 1-4` objects), and save the scene.
- Nothing refers to them any more: players start in their tutorial lanes.

## 8. From Phase 3D, if not done yet: Steam (20 minutes)

- One PC, Steam running: **Play**, A, **Play** → player select. **Y** hosts (the line along the top changes). **B** goes back offline.
- Two PCs, two Steam accounts that are friends: follow `docs/online.md`, Two PCs over Steam.

## 9. At Phase 4B, when the App ID exists: the Steamworks dashboard

- Stats & Achievements: create `FIRST_DELIVERY` ("First Delivery") and `GOLDEN_WIN` ("Golden Finish"), with the descriptions in `docs/steam/in-game-features.md`. Each needs two 256×256 JPG icons (unlocked and locked).
- Community → Rich Presence: upload `docs/steam/rich-presence-english.vdf`.

## 10. Review and commit (your call)

- Not committed: Phase 4A on `steam-phase1a`, on top of "Phase 3I".
- How it works: `docs/steam/in-game-features.md`.
- What's next: `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`.
- Suggested commit title: "Phase 4A: Steam features in the game".

# Gold Rush Implementation Plan (plan 2 of 5)

> Built inline on 2026-11-04 right after plan 1 (`2026-11-04-modes-framework-and-mutators.md`); this records the decisions.

**Goal:** Gold Rush as a playable mode: lots of packages, a score that drains while you're not delivering, steals that count double in the last 30 seconds, offline and online.

**Spec:** `docs/game-modes-ideas.md` → Gold Rush; per-mode table (Steals locked on, waves and golden order shown, no round timer).

## Decisions (all numbers in `Assets/Scripts/Management/GoldRushRules.cs`)

- **Packages:** each wave's authored quotas × `QUOTA_MULTIPLIER` 3; spawn cooldown `SPAWN_COOLDOWN` 1 s (never slower than the scene's).
- **Drain:** `DRAIN_PER_SECOND` 2 points/s after `GRACE_SECONDS` 5 s without a delivery (the match's start counts as one). Never below 0. Only while the waves run (Begin/MainLoop, not the golden round).
- **Rush hour:** the last `RUSH_HOUR_SECONDS` 30 s of the waves. Each steal counts twice: the order doubles twice and the thief's pay doubles. A hint ("RUSH HOUR! STEALS COUNT DOUBLE") shows on every machine.
- **Online:** nothing new is sent. The host spawns, drains and pays; scores already reach clients through `OnlinePlayer.score`. Clients replay steals through `OrderHandler.TakeOrderFrom`, reading rush hour from their own copy of the host's clock, so a steal right at the 30 s mark may show one doubling different on a client for display only (the score is the host's).

## Tasks (done)

1. `GoldRushRules` (pure) + `GoldRushRulesTests`.
2. `OrderManager`: quotas and cooldown through `GoldRushRules`; `DrainGoldRush` (authority) each frame; `StartDrainClock` for every player at `InitGame`; `WatchRushHour` hint on every machine.
3. `OrderHandler`: `StartDrainClock` on each delivery (authority), `Drain(deltaTime)`, `PayForSteal` × `StealCounts()`, `CountSteal` `StealCounts()` times.
4. `GameModes.Playable` += GoldRush (the MODE row now appears); string `goldrush.rushhour` + translations; `MatchRulesPlayTests.GoldRush_AScoreDrains_UntilADelivery`.

## Not covered by tests (playtest, EDITOR-TODO 16e)

- Rush hour's double steal in a real match (the clock can't be set from a test without a seam).
- Whether the order pool (28 in the game scene) runs dry at triple quotas with four players.
- Bots: they deliver and steal as usual; check they don't sit still and bleed points.

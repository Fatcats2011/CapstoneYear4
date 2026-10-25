# End-of-match awards: design

**Date:** 2026-10-24
**Branch:** `steam-phase1a`
**Status:** the user handed the open questions to me ("I trust your judgement"), so every choice below marked **(my call)** is mine, not agreed. Feature 8 of `2026-10-23-modern-features-handoff.md`.

## Intent

At the results, every player gets a card that says something they did, so the one who came last still has something to grin about. It costs one tally and one message: most of the counting is already raised for achievements (`FeatSync`).

## Goals

- One award card per player, above their score, shown with the scores.
- The same cards on every machine online.
- Nobody goes without: a player who did nothing gets a fallback card.

## Non-goals

- Phase 2 awards (Drift King, Wall Walker: counted on the driver's machine). Later.
- Awards in the menus, Steam stats or a history.
- Animating the cards. They appear with the scores.

## Decisions

- **One award per player (my call).** Two or three big ones would leave the rest of the table out, which is the opposite of why we're adding them.
- **Joke names with a plain line under them (my call),** as the handoff's mock: `KLEPTOGHOST` / `3 steals`. The line is always a count, never "most", because the second pass (below) can hand an award to someone who wasn't top.
- **The awards, in priority order (best first):**

  | Award | Counts | Line |
  |---|---|---|
  | GOLDEN GRIP | seconds holding the golden order | `held the gold 12s` |
  | KLEPTOGHOST | steals | `3 steals` / `1 steal` |
  | BUSY BEE | deliveries | `9 deliveries` / `1 delivery` |
  | HIGH ROLLER | hard deliveries | `2 hard orders` / `1 hard order` |
  | BUTTERFINGERS | times stolen from | `robbed 5 times` / `robbed once` |
  | FISH FOOD | falls in the water | `4 swims` / `1 swim` |
  | TOURIST | fallback | `enjoyed the view` |

  High Roller counts hard deliveries, not money: a count reads plainer, and money is already the score. (my call)
- **Picking (`AwardRules`), deterministic:**
  1. A player who is the only top (value > 0) of an award holds it outright. Each player takes their best outright award (priority order).
  2. Players still without one take the best of what's left: walk the awards by priority; for each award nobody has, the player still without one who has the highest count (> 0) takes it, ties to the lower seat.
  3. Anyone still without gets TOURIST (it can repeat).
- **Nothing in the tutorial counts** (state `Tutorial`), as `MatchFeats`. The rematch skips the tutorial anyway.
- **Counted where the rules run** (this PC offline, the host online):
  - deliveries, hard deliveries, steals: `FeatSync.Delivered`, `FeatSync.Stole`;
  - stolen from: a new `FeatSync.Robbed(victimSeat)`, raised by `OrderHandler.StealFrom` when it takes an order;
  - falls: a new `FeatSync.Sank(seat)`, raised once per respawn by `Respawn` when it starts on the rules' machine, and by `OnlineRespawns.PickFor` on the host when another machine's player asks where to rise (that's their fall; only their own machine can say so);
  - golden time: the rules' machine adds the frame time for each seat whose handler `HasGoldenOrder` in `FinalPackage`.
- **Online:** at `Results` the host picks and shares one `AwardBoard` (an award and count per seat, 0–3) through `OnlineMatch.ShareAwards` → `AwardsClientRpc`. Clients check it (`NetChecks.Sane(AwardBoard)`: defined awards, seats 0–3, counts up to 9999) and keep the last one (`AwardSync.Last`), so a late results screen still finds it. Host → client only: no new ServerRpc, nothing to rate-limit.
- **Showing:** `ResultsMenu.UpdateResults` builds the cards (`ResultsAwardsView`, by code under the results canvas, in the scores' font) above each score, for the seat that score shows, from `AwardSync.Last`. No board: no cards. They hide with the scores (`ClearResultsView`).
- **A new match** (`GameManager.OnNewMatch`) clears the tally and `AwardSync.Last`.

## Approaches

- **A. A pure tally + rules, a persistent `MatchAwards` behaviour that feeds them, and a small sync seam (chosen).** The same shape as `MatchFeats`/`SteamFeatures` and the rematch/skip seams; everything but the wiring is edit-mode tested.
- B. Fold the tally into `SteamFeatures`. Fewer files, but it mixes Steam with something that has nothing to do with Steam, and its tests with Steam fakes.
- C. Each client counts its own and sends it at the results. Clients would decide what they show others; the host already sees every event, so there's no reason to trust them.

## Left to the builder

- The card's exact size, colours and offset above the score.
- The message's field layout.

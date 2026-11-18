# Pack G: results breakdown and first-time hints (design)

**Date:** 2026-11-14
**Branch:** `steam-phase1a`
**Status:** approved by the user 2026-11-14; built the same day
**From:** `2026-11-13-player-feedback-handoff.md`, pack G

## Why

Players should leave a match knowing what paid, and learn the less obvious rules the first time they meet them. Two separate parts; they share nothing but this document.

## What is already in the game (checked in the code, not seen running)

- Results cards (`ResultsAwardsView`): 300 x 250 at 1080p, full: picture, name, score, then the award.
- `MatchStats` counts deliveries, steals and so on for the awards: counts, not money. The host shares the awards with clients as an `AwardBoard`.
- Money is added in four places in `OrderHandler`: a delivery, the golden bonus, steal pay, and mode points (crown seconds, time free, nuggets). Nothing takes money away.
- Settings are saved per machine in `settings.cfg`. There are no player profiles.
- Standard's tutorial teaches steals; the other modes have no tutorial.

## Goals

1. Each results card shows where its player's money came from.
2. A rule a player has not met yet is explained once, in a few words, when it first matters.

**Not in this pack:** changes to the awards, a statistics screen, per-player profiles, hints for the menus.

**Would make it a failure:** parts that don't add up to the score; a hint that covers the view, repeats, or shows to someone it didn't happen to.

## Part 1: the money breakdown

### Approaches

| Approach | Trade-off | |
|---|---|---|
| Taller card with numbers | Plain amounts teach what pays and need no colour; the cards grow a little | **Picked by the user** |
| A four-colour bar under the score | Smallest, but relies on colour and shows shares, not amounts | |
| A separate page behind a button | Nothing added to the first view, but rarely opened | |

### Design

- **Four sources**, each with a short label:
  - **Deliveries:** what delivered orders paid at their plain price (the Mother Lode's $250 included).
  - **Steals:** steal pay, plus the extra a stolen order paid on delivery over its plain price **(my call)**. A stolen Hard order delivered for $160 counts $80 under Deliveries and $80 under Steals, so the card shows that stealing pays.
  - **Golden:** everything the golden order paid.
  - **Mode:** crown seconds, time free, time in, catches, nuggets.
- **They always add up to the score.** Every place that adds money says which source it is, the tutorial's money included (it carries into the match).
- **On the card:** a block of small lines between the header and the award, two per row, label left and amount right (`Deliveries  $520`). A source that paid nothing is left out; with one or two sources there is one row. The card grows 22 px per row: up to 44 px taller (250 to 294); with nothing to show (a score of 0) it stays 250.
- **Size:** text about 16 px at 1080p, the card's existing money colour.
- **Online:** only the host counts. The four amounts per seat ride in the awards' own message (`AwardBoard`), so no new message is needed; a client's cards show them when the awards arrive.
- **A new match** (and a rematch) starts the counts at zero, as `MatchStats`.

## Part 2: first-time hints

One sensible way: the existing callout line. A new HUD element would break "small UI", and a pause-and-explain box would stop the match.

### Design

- **Where:** the player's own callout line, in the plain tone, held for 4 seconds. A hint waits for the line to be free, so it never cuts an event line; a newer event line replaces it.
- **Once per machine:** each hint shows the first time it happens to any local player, then never again on that machine. Saved in `settings.cfg`. Online, each machine keeps its own.
- **Not in the tutorial**, which teaches these itself.
- **The hints** (short enough for the line; in all 16 languages):

| When it first happens to you | Hint |
|---|---|
| You bump a rival without boosting | `BOOST INTO RIVALS TO STEAL` |
| You are robbed | `BOOST TO BLOCK A THIEF` |
| You steal an order | `STOLEN ORDERS PAY DOUBLE` |
| You first pick up the golden order | `HOLD GOLD LONGER, EARN MORE` |

- **Text fit:** hints use the callout's own size; the line is widened only if a language needs it.
- **Left out (my call):** an Options row to show hints again. Until one exists, removing the `hints=` line from `settings.cfg` (or setting it to 0) brings them back.

## Structure

- `MoneySource` (enum), `MoneyTally` (pure: per-seat amounts, `Add`, `Of`, `Total`, `NewMatch`), kept by `MatchAwards` and carried in `AwardBoard`. `OrderHandler` reports each gain to the tally where it adds to `score`.
- `ResultsAwardsView` gains the block; `BreakdownRules` (pure) decides the lines and their order.
- `HintRules` (pure: which hint an event earns, whether it has been seen) and `Hints` (the seen set, loaded and saved with the settings); `StealCallout.HearHint`.
- Strings: four source labels and four hints, 16 languages.

## Testing

- Edit-mode tests: the tally adds up to the score for every source; the split of a stolen delivery; the board survives a network round trip; the card's lines for 0, 1, 2 and 4 sources; each hint fires once and is remembered; a hint waits for a busy line.
- Playtest note in `EDITOR-TODO.md` 16l: results cards in split screen and online (a client sees the host's amounts), at the smallest view; each hint appears once, to the right player.

## Documents

- GDD: the results cards' breakdown and the hints. TDD: the new classes, the message, the saved setting, the tests.

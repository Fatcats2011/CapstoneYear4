# Pack D: orders and where to go (design)

**Date:** 2026-11-14
**Branch:** `steam-phase1a`
**Status:** approved by the user 2026-11-14; built the same day
**From:** `2026-11-13-player-feedback-handoff.md`, pack D

## Why

Players should see what an order pays, where each held order goes, and when a rival is about to take one. Packs A to C say what just happened; this pack says what is about to.

## What is already in the game (checked in the code, not seen running)

- Each tier has its own beam colour, floating package icon (`BeconIndicator`), compass icon and package model.
- A carried order's beacon moves to its drop-off in the drop-off colour, seen only by the carrier, with a compass mark for the carrier, an arrow over the package and a customer ghost.
- Nothing warns a carrier about a rival.

So this pack adds three small things, not the handoff's full list. Tier shapes and a drop-off ring are dropped: both already exist in another form.

## Goals

1. A carrier knows a thief is coming, and from which side.
2. A player can read an order's worth at its beacon.
3. A player holding several orders can tell which drop-off is which.

**Not in this pack:** warnings for the crown, infection tags or the cursed package; any new sound; changes to beam colours or icons; scene or prefab edits.

**Would make it a failure:** a warning that covers the view, fires all the time in a crowd, or shows for a rival who can't steal.

## Approaches for the rival warning

| Approach | Trade-off | |
|---|---|---|
| Edge mark: a small chevron at the side of the view the rival is on | Gives direction at a glance; one new small HUD element | **Picked by the user** |
| A rival icon on the compass | No new element, but at the top of the screen and easy to miss in a chase | |
| Heartbeat and rumble only | Nothing on screen, no direction, needs a sourced sound | |

## Design

### 1. Rival warning (edge mark)

- **Who sees it:** a player carrying at least one order. Their own view only.
- **Which rivals count:** one who could steal now: steals are on (`HouseRules.Playing.StealsOn`), the rival has a free slot, neither is respawning, and the match is being played. A steal needs the thief to boost into a carrier who isn't boosting, so:
  - **near** (within 30 m **(my call)**): the chevron shows, steady and dim;
  - **boosting and closing** (within 30 m, boosting, and getting nearer): the chevron pulses bright, with one short rumble when this state begins.
- **Where:** at the edge of the player's view, on the side the rival is on relative to the camera: left, right, or bottom middle for behind. A rival in front is on screen already and gets no mark.
- **How many:** one mark, for the most dangerous rival (a boosting one before a near one, then the nearest).
- **Size:** about 40 px tall at 1080p, red, no words. With ReduceMotion (`Comfort`) it doesn't pulse; it shows at full brightness instead.
- **Carrier boosting:** the mark stays, since a boost runs out.
- **Online and split screen:** every machine works it out from what it already has (positions, the boost flag in `DriveFlags`, held orders). No new network messages. Each split-screen view has its own mark.

### 2. Price at the beacon

- A small price under the package icon that floats over a pickup beacon: `$55`, `$65`, `$80`.
- It is the order's current worth (`Order.Worth`), so a stolen order reads `$130` and the Mother Lode `$250`. It updates when the order is stolen and dropped.
- The golden order shows no price: its pay grows with the time it is held.
- It turns to each view and scales with distance as the icon does, and fades with it close up.

### 3. Which drop-off is which

- A drop-off's compass mark shows its package's own icon (today every drop-off uses the same one) with the price in small text above the distance.
- Only the carrier sees it, as today.

### Structure

- `RivalWarningRules` (pure): which rival is the threat, its level (none / near / charging) and its side, from positions and flags. Edit-mode tests.
- `RivalWarning` (per local player, built in code on the Driving Canvas beside `StealCallout`): draws the mark, rumbles through `Rumbler`.
- `PriceTag` (world-space text added to `BeconIndicator`'s per-view icons at runtime) and a price line added to the compass icon at runtime. Words through `OrderFeedbackRules.PriceText`.
- No new strings: prices are numbers.

### Left to the builder

- Exact pixel positions and the pulse's timing; how the chevron sprite is drawn (in code, as `Colourblind` draws its badges).

## Testing

- Edit-mode tests for the rules (threat choice, side, levels, price text), the mark's state, and the price labels.
- Playtest note in `EDITOR-TODO.md` 16k: 4 players in split screen and online; check the mark's side is right, it never shows for a full-handed rival, and the prices read at the smallest view.

## Documents

- GDD: the warning, beacon prices and drop-off marks (HUD and Orders). TDD: the new classes and tests.

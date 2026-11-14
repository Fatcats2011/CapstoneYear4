# Score gains with reasons (Pack B): design

**Date:** 2026-11-14
**Branch:** `steam-phase1a`
**Status:** waiting for the user's approval.
**From:** `2026-11-13-player-feedback-handoff.md`, Pack B. Builds on Pack A (`2026-11-13-steal-feedback-design.md`).

Lines marked **(my call)** were chosen by Claude, not the user.

## Why

Money arrives silently, so players can't tell what pays. The scoreboard's number changes and its rows slide, but nothing says how much was earned or for what.

## Goals

- A player who earns money reads how much and why, in their own view.
- Everyone can see whose money just went up, and by how much, on the scoreboard.
- A change of first place is called out to the two players it concerns.

## Not in this pack

- New sounds. Deliveries already have theirs, and the user asked for none here.
- Mode event calls such as "Crown stolen" (Pack C).
- A per-source tally for the results screen (Pack G).

## What would make it a failure

- New UI that takes screen space (the user's rule: added UI stays small). This pack adds no new area: it reuses Pack A's line and writes inside the scoreboard's rows.
- Lines that spam: a line every second in a timed mode, or for every $10 nugget.
- Amounts that differ between machines online.

## Approaches

| # | Approach | Trade-off |
|---|---|---|
| 1 | **A reason in the player's own line, plus a "+$" on the scoreboard** (picked by the user) | Says how much and why; no new screen area |
| 2 | Scoreboard only | Smallest, but never says why |
| 3 | "+$65" floating in the world at the drop-off | No HUD space, but tiny in 4-way split, easily hidden, no reason |

## What changed from the handoff

- "Flying into the score HUD, with the HUD counting up": the per-player score HUD no longer exists. The scoreboard's own number counts up instead.
- "Golden +$1/s": the golden order pays once, on delivery. Its line shows the whole amount then.
- The 4-digit HUD limit no longer applies.

## The design

### 1. The reason, in the player's own line

Pack A's callout line (bottom middle of each player's own view) also reports that player's gains. Same size, same 1.5 s life, in gold.

| When | Line |
|---|---|
| Delivers an order | `DELIVERED +$65` |
| Delivers a stolen order | `DELIVERED ×2 +$130` (the stamp from Pack A, so doubling is seen to pay) **(my call)** |
| Delivers the golden order | `GOLDEN ORDER +$212` (everything it paid) |
| Delivers the Mother Lode | `MOTHER LODE +$250` |

- A steal's `STOLE FROM <name> +$7` (Pack A) is unchanged.
- **No line** for per-second points (Kings Crown, Infection) or for a $10 Gold Rush nugget **(my call)**: they would never stop. The scoreboard covers them.
- Two gains within half a second become one line with the sum **(my call)**.
- The tutorial's deliveries show their line too: that money carries into the match.
- Translated in every language in `strings.csv` **(my call: Claude writes the translations)**.

### 2. The amount, on the scoreboard

- When a player's money goes up, a small `+$65` appears beside their money on their row for about 1.2 s, and the number counts up to the new total over about 0.4 s **(my call on both timings)**.
- A further gain while it is showing adds to it and restarts its time, so nuggets read as one growing `+$30`.
- It sits inside the row: the board's size and place don't change.
- Only where the row shows money (Standard, Gold Rush). Kings Crown shows time held and the other two show nothing, so they get no `+$`.
- A score that goes down or resets (a new match) shows nothing.

### 3. Lead change

- When first place changes hands, the new leader reads `YOU TOOK THE LEAD` (gold) and the player who lost it reads `<name> TOOK THE LEAD` (red). Nobody else gets a line.
- Not for the first money of a match, when nobody led yet **(my call)**.
- It never cuts a gain line short: if both happen together, the gain shows first and the lead line follows it **(my call)**.
- In the modes where the scoreboard ranks players by money or by time held (Standard, Gold Rush, Kings Crown). Not Infection or Hot Potato.
- No sound.

### Online and split screen

- Every machine already replays each delivery and knows the order's worth, so each shows the reason and amount for the players it drives.
- The scoreboard's `+$` comes from the change in each player's total, which every machine already receives from the host, so all machines show the same amount.
- The golden order's line uses the value that machine shows for it; a client can be a dollar or two behind the host for a moment. The scoreboard's `+$` is exact.
- Split screen: each player's line is their own; the scoreboard is shared.

## Left to the builder

- Generalising Pack A's callout so it carries gain and lead lines, with the "gain first, lead after" order.
- The pure rules class (which line for which gain, merging, when a lead change counts) with edit-mode tests.
- Where exactly the `+$` sits in a row, and its size.

## Documents and testing

- `DoA GDD.docx`: the gain lines, the scoreboard `+$` and count-up, the lead-change lines. `DoA TDD.docx`: the new classes.
- `EDITOR-TODO.md`: a playtest note, next to Pack A's (16h).

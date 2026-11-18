# Split-screen scoreboard — short design (2026-11-17)

## Why
In local play the live scoreboard sits middle-left, so it covers the left-hand players' views and not the others'.
The board should move with the number of people on the screen: on the centre where the views meet, so it takes
the same share of every view, or in the empty black space when the layout has some, so it takes none.

## Goals
- Where the board sits and how it is arranged follow the local player count and the two-player split option.
- The board covers every player's view equally, or not at all.
- In the centre, the board has a different, compact look: picture, money and held packages, with no name.

## Non-goals
- No change for one local player, whether offline with bots or online alone: the board stays middle-left.
- No change to what the board tracks: ranking, money, packages, flames, crowns, badges, "+$" pop and lead calls.

## Anti-goals
- A centre board big enough to block the road ahead in any view.
- Duplicating the row code.

## Approaches
- **A. One board that rearranges itself (picked).** A layout step picks the board's anchor and its arrangement
  (column, strip or 2×2 grid), and rows gain a compact form. Everything else is kept. The layout step is pure numbers and is tested.
- B. A second board just for split screen. Rejected: it would copy the row code, so every change would be made twice.
- C. A board inside each player's view. Rejected: it isn't in the middle.

## Layout per screen (as agreed)
| Local players | Split | Where | Arrangement | Rows |
|---|---|---|---|---|
| 1 | — | middle-left (today) | column | full |
| 2 | Side-by-side | screen centre, straddling the vertical line | column | compact |
| 2 | Stacked | screen centre, straddling the horizontal line | strip, ranked left to right | compact |
| 2 | Boxed | centred in the empty left bar (x 0–0.25) | column | full *(my call: the bar is wide enough for names)* |
| 3 | quarters | centred in the empty bottom-left corner (x 0–0.25, y 0–0.5) | column | full *(my call, same reason)* |
| 4 | quarters | screen centre, on the cross | 2×2 grid, one cell per quarter | compact |

## Decided by me (open to correction)
- Compact row = picture, money (with its "+$"), and two package slots. The name is dropped because the picture
  (Steam picture or ghost) already says who it is.
- First place is still drawn bigger and rows still slide when the ranking changes. In the grid, rank fills the cells in
  reading order, with first place at top-left.
- The text size setting still scales the board.
- The count used is this machine's local players (`roster.LocalCount`), so couch players online get the same layouts.
- The board re-lays itself when the player count or the split option changes mid-session.

## Left to the builder
- Exact compact sizes, the centre-row banner shape (it shouldn't taper to one side when centred), and checking that the
  centre board doesn't collide with the other centre HUD pieces (steal callout, mode info box, reconnect prompts).
- EditMode tests for the layout step (anchor + arrangement + cell positions for each row of the table).
- GDD (UI / HUD section) and TDD (Scoreboard) updates, with a Revision History line and a minor version bump in each.
- A playtest entry in EDITOR-TODO for each split layout.

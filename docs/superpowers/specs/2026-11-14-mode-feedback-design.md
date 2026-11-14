# Mode goals and event calls (Pack C): design

**Date:** 2026-11-14
**Branch:** `steam-phase1a`
**Status:** waiting for the user's approval.
**From:** `2026-11-13-player-feedback-handoff.md`, Pack C. Builds on Packs A and B (the callout line).

Lines marked **(my call)** were chosen by Claude, not the user.

## Why

`ModeInfoBox` explains a mode once, at the start. After that a player has to remember their goal, and nothing tells them when their role changes. The Hot Potato playtest (2026-11-04) "didn't work" because players couldn't tell the cursed package had reached them: a rule the player can't see feels broken.

## Goals

- A player can read their goal for their current role, in their own view.
- A player is told, in words, when a mode event happens to them.
- Everyone gets a warning before a timed round ends.

## Not in this pack

- Changes to Standard or Gold Rush beyond the 30-second warning's absence there (they use waves; rush hour and the Mother Lode keep their bottom-bar messages).
- New visuals in the world (the crown, beacon, flame and fuse number stay as they are).
- Results breakdown and first-time hints (Pack G).

## What would make it a failure

- UI that takes space (the user's rule: added UI stays small). No boxes, no centre-screen banners, no full-screen flash.
- A goal tag that sits on screen all match.
- Lines or tags that differ between machines online.

## Approaches

| # | Approach | Trade-off |
|---|---|---|
| 1 | **Event lines plus a tiny goal tag** (picked by the user, with the tag disappearing after 25 seconds) | Says the goal and what just happened; a few small words, gone after 25 s |
| 2 | Event lines only | Smaller, but the role is only stated at the moment it changes |
| 3 | Centre-screen announcements for everyone | Ruled out: breaks the small-UI rule |

## The design

### 1. The goal tag

- Two or three small words under the compass, in each player's own view. Text only, no box, with a dark outline. About the callout line's size or smaller **(my call)**.
- It appears when the round starts and whenever the player's role changes, and **disappears 25 seconds later** (the user's call; my reading: it is the goal tag that goes after 25 seconds, and the event lines keep their 1.5 seconds). A role change shows the new tag and starts the 25 seconds again.
- It fades in and out, hides while paused, and goes when the match ends or the player is out.

| Mode | Role | Tag |
|---|---|---|
| Kings Crown | Has the crown | `KEEP THE CROWN` |
| Kings Crown | Doesn't | `GET THE CROWN` |
| Infection | Infected | `CATCH THEM` |
| Infection | Free | `DON'T GET CAUGHT` |
| Hot Potato | Has the cursed package | `PASS IT!` |
| Hot Potato | Doesn't | `STAY AWAY` |
| Hot Potato | Out | none |

- Standard and Gold Rush have no tag.
- At the start of a round the tag waits until `ModeInfoBox` has gone (5 s), so the two never overlap under the compass **(my call)**.

### 2. Event lines

In the player's existing callout line, for 1.5 s, only for the players involved (except where "everyone" is stated).

| Mode | When | Who | Line | Colour |
|---|---|---|---|---|
| Kings Crown | Picks the crown up, or takes it | The new holder | `YOU HAVE THE CROWN` | gold |
| Kings Crown | Has it taken | The old holder | `<name> TOOK YOUR CROWN` | red |
| Infection | Is caught | The caught player | `YOU'RE INFECTED` | red |
| Infection | Is the first hunter | That player, at the start | `YOU'RE INFECTED` | red |
| Infection | A player is caught while one hunter exists | That hunter | `CAUGHT <name>` | gold |
| Infection | One free player is left | That player | `YOU'RE THE LAST ONE FREE` | white |
| Hot Potato | The package reaches them (lit or passed) | The new holder | `YOU HAVE THE CURSED PACKAGE` | red |
| Hot Potato | Passes it on | The passer | `PASSED TO <name>` | gold |
| Hot Potato | A player is out | Everyone else | `<name> IS OUT` | white |

- `CAUGHT <name>` is shown only when there was exactly one infected player before the catch **(my call)**: the other machines learn only who is infected now, not who made the catch, so with two or more hunters it can't be attributed and nobody gets the line.
- A lead-change line (Pack B) still waits for these, as it does for any line.
- The Infection catch already pays 50 points; no `+$` is shown, since that mode's scoreboard shows no money.

### 3. Round ending

- **30 seconds left:** in Kings Crown and Infection (the modes with a round timer), everyone reads `30 SECONDS LEFT` in their callout line, in white, and the timer's digits pulse once **(my call on the pulse: a short grow-and-shrink)**. No screen flash.
- Hot Potato has no round timer of its own (its clock is the fuse), so it gets no 30-second line.
- **The last-10-seconds countdown** reads "Golden Order In…" in every mode today. In modes with no golden round it reads `TIME'S UP IN…` instead **(my call)**.

### 4. Sounds

- Two new sounds, from the Kenney CC0 packs already downloaded (never made by Claude):
  - the cursed package lands on you;
  - 30 seconds left.
- The user hears candidates and approves each before it goes in. Each is recorded in `LICENSES.md`, and its volume is set against the music as in Pack A.
- No other new sounds: the crown keeps its pickup sound and infection its whoosh.

### Online and split screen

- Other machines receive only the mode's current state (`ModeSnapshot`), not its events. Each machine compares the state it last saw with the new one and works out the events itself, the same way on the host, on clients and offline. No network messages are added.
- Two changes that arrive in one update are both worked out from the before and after.
- Each player's tag and lines are their own; split screen shows four.

## Left to the builder

- A pure rules class that turns "state before, state after" into the events, lines and tags, with edit-mode tests.
- The tag's exact position, size and fades, clear of the compass and `ModeInfoBox`.
- How the timer digits pulse without touching the Driving Canvas prefab.

## Documents and testing

- `DoA GDD.docx`: the tags, the event lines, the 30-second warning, the countdown text, the two sounds. `DoA TDD.docx`: the new classes.
- `EDITOR-TODO.md`: a playtest note (16j), next to Packs A and B.

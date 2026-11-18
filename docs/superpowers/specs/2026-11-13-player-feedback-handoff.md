# Player feedback: handoff

**Date:** 2026-11-13
**Branch:** `steam-phase1a`
**Status:** a handoff, not an approved design. The user asked for ideas to help players understand what is happening in a match. Nothing here is agreed yet. Each pack still goes through its own brainstorm, spec and plan before any code.

> **For the next session:**
> 1. Read this, then `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md` (how to work in this repo).
> 2. Ask the user which packs they want, then take one pack at a time. Brainstorm it with the user (superpowers:brainstorming), starting from that pack's "Open questions", then write its spec and plan.
> 3. Check "Already in the game" against the code before building: it was a quick survey, not an audit.
> 4. UI is built by code at runtime; scene, prefab and ProjectSettings edits are `EDITOR-TODO.md` chores. Rules go in small pure classes with edit-mode tests (`StealValueRules`, `GoldRushRules`).
> 5. Anything that changes the HUD or rules updates `docs/design/DoA GDD.docx`; new systems update `docs/design/DoA TDD.docx` (see `CLAUDE.md`).
> 6. Every new effect must work online (host decides, every machine replays) and in 4-way split screen.

Lines marked **(my call)** are suggestions nobody has agreed to yet.

## The problem

Players miss what just happened to them and why the scores moved. The clearest evidence:
- The steal-value spec (`2026-10-28-steal-value-design.md`): steals were rare, and doubling only pays off if players can see an order's worth.
- The Hot Potato playtest (2026-11-04): the mode "didn't work" because the cursed package couldn't be seen. A rule the player can't see feels broken.

## Already in the game (quick survey, check before building)

| What | Where |
|---|---|
| A stolen order's model gets larger with each steal | `Order.ApplyStealLook`, `StealValueRules.Swell` |
| A purple flame round a stolen package on the scoreboard | `PurpleFlame`, `Scoreboard` |
| Scoreboard rows slide to their new rank; first place is drawn larger | `Scoreboard.Ranking`, `RowTops` |
| Pickup, drop-off and steal sounds (the steal's "whoosh" has no known licence) | `OrderHandler`, `SoundPool` |
| Steal callout line, rumble, camera kick, `×2` stamp (Pack A) | `StealCallout`, `StealFeedbackRules`, `WorthStamp` |
| The per-player score HUD is gone: the live scoreboard replaced it | `Scoreboard` |
| What to do in each mode, shown once at the start (not Standard) | `ModeInfoBox` |
| Standard's tutorial | `TutorialHandler`, `TutorialManager` |
| Beacons, compass marks | `BeconIndicator`, `Compass`, `CompassMarker` |
| Score HUD (4 digits, caps at 9999) | `NumberHandler` |

## The packs

| # | Pack | Value | Size (my call) |
|---|---|---|---|
| A | Steals and bumps: **built 2026-11-14** (`2026-11-13-steal-feedback-design.md`, plan `2026-11-13-steal-feedback.md`; playtest in `EDITOR-TODO.md` 16h) | highest | medium |
| B | Score gains with reasons: **built 2026-11-14** (`2026-11-14-score-gain-feedback-design.md`, plan `2026-11-14-score-gain-feedback.md`; playtest in `EDITOR-TODO.md` 16i) | high | small–medium |
| C | Mode goals and event calls: **built 2026-11-14** (`2026-11-14-mode-feedback-design.md`, plan `2026-11-14-mode-feedback.md`; playtest in `EDITOR-TODO.md` 16j) | high | medium |
| D | Orders and where to go: **built 2026-11-14** (`2026-11-14-orders-feedback-design.md`, plan `2026-11-14-orders-feedback.md`; playtest in `EDITOR-TODO.md` 16k). Tier beacons and the drop-off beacon already existed; built: rival warning, beacon prices, drop-off marks | medium | small |
| E | Event sounds | medium, cheap | small |
| F | Driving and respawn: **built 2026-11-16** (`2026-11-16-driving-feedback-design.md`, plan `2026-11-16-driving-feedback.md`; playtest in `EDITOR-TODO.md` 16m). The respawn takes about 2 seconds, so its countdown is built but shows no numbers | low–medium | small |
| G | Results breakdown and first-time hints: **built 2026-11-14** (`2026-11-14-results-and-hints-design.md`, plan `2026-11-14-results-and-hints.md`; playtest in `EDITOR-TODO.md` 16l) | medium | medium |

## Build order (my call)

1. **A** first: the steal-value feature only works if players notice steals and doubled orders.
2. **B** next: it answers "why did my score change" everywhere, in every mode.
3. **C**: the Hot Potato lesson says hidden mode rules feel broken.
4. **E** alongside any of these: each pack adds its own sounds.
5. **D, G, F** after a playtest shows what is still unclear.

---

## A. Steals and bumps

**Why:** a steal is the game's biggest swing, and today it is easy to miss, mainly in a pile-up or split screen.

**Ideas**
- A short callout to both players: the thief sees "STOLEN from <name> +$X", the victim sees "<name> took your order".
- A camera punch or a short hit-stop when a package changes hands, plus rumble for both (`Rumbler`).
- The order's worth shown on the package: a "×2 / ×4" stamp or a price tag over it and its beacon, as well as the swell.
- A bump that takes nothing says why: "Slots full" or "Nothing to steal".

**Open questions**
- Is a hit-stop fair online, or local only?
- Is a price tag readable at 4-way split-screen size?
- Show the worth to everyone, or only the carrier?

## B. Score gains with reasons

**Why:** money arrives silently, so players can't tell what pays.

**Ideas**
- A floating "+$65" where the money was earned, flying into the score HUD, with the HUD counting up.
- A short reason with each gain: "Delivered", "Steal bonus", "Golden +$1/s", or the mode's own reasons.
- A "Lead change!" callout when first place changes (the rows already slide).

**Open questions**
- One popup per gain, or merged gains when several land at once (the golden order pays every second)?
- Does the 4-digit HUD limit matter here (open question from the steal-value work)?

## C. Mode goals and event calls

**Why:** `ModeInfoBox` explains the mode once at the start; after that, players have to remember.

**Ideas**
- A small goal line that stays on screen for the round: "Hold the crown", "Pass the potato", "You're infected: tag others".
- Mode events announced in the middle of the screen: "<name> is infected", "Crown stolen", "Potato: 5s".
- Phase changes (golden round, last 30 seconds) marked with a sound and a screen pulse, not only the timer.

**Open questions**
- Where does the goal line fit next to the scoreboard (middle left) and the compass?
- Does each mode's call list come from `ModeReferee` events, or a new event list?

## D. Orders and where to go

**Ideas**
- Beacons in a distinct colour and shape for each tier, so Easy, Medium, Hard and Golden can be told apart from far away (and in colourblind mode: `Colourblind`).
- A drop-off ring and compass mark while carrying a package.
- A warning when a rival closes in on a valuable package: a red glow on that side of the screen, or a rising heartbeat sound.

**Open questions**
- Is a rival warning too strong a help for the carrier? Should it depend on the package's worth?

## E. Event sounds

**Ideas**
- One distinct sound each for: steal, being stolen from, delivery, value doubled, golden spawned, lead change, mode event.

**Open questions**
- New sounds or reuse existing ones? They go through `SoundPool` / `SoundManager` and the audio volume options.

## F. Driving and respawn

**Ideas**
- Boost pad or kick: speed lines and a sound that rises in pitch.
- Respawn says what happened ("Fell in the river") with a short countdown at the gravestone (`RespawnGravestone`, `RespawnManager`).

## G. Results breakdown and first-time hints

**Ideas**
- The results screen shows each player's money split by source (deliveries / steals / golden / mode), so players see what pays. It needs a per-source tally during the match.
- One-time hints the first time something happens: the first bump, "Bump rivals to steal their orders". Saved per profile, shown once.

**Open questions**
- Does the split fit on the new player cards (`ResultsMenu`, `CardPanel`), or does it go in the awards?
- Hints per player or per machine? Shared split-screen players have no profiles of their own.

## Testing

Each pack ends with a playtest note in `EDITOR-TODO.md`: play a 4-player match (bots are fine) in split screen and online, and check every new callout can be read at the smallest screen size.

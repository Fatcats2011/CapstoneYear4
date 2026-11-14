# Steal and bump feedback (Pack A): design

**Date:** 2026-11-13
**Branch:** `steam-phase1a`
**Status:** waiting for the user's approval.
**From:** `2026-11-13-player-feedback-handoff.md`, Pack A.

Lines marked **(my call)** were chosen by Claude, not the user.

## Why

A steal is the game's biggest swing, and players miss it. Today the thief gets a grab animation and a whoosh; the victim spins out. Nobody is told who took what, why a bump took nothing, or what a stolen order is now worth beyond its size.

## Goals

- The thief and the victim each know, at once, that a steal happened and who the other player was.
- A bump that takes nothing says why.
- Anyone can read a stolen order's worth on the package.

## Not in this pack

- Score popups for deliveries, and a counting-up HUD (Pack B).
- Crown, potato and infection calls (Pack C). A hit that a mode takes over (`ModeReferee.Strike`) shows nothing here.
- Price tags on beacons or the compass.

## What would make it a failure

- New UI that covers the road or crowds the HUD. The user's rule: added UI stays small.
- A callout that can't be read in a 4-way split-screen view.
- An effect that differs between machines online.

## Approaches

| # | Approach | Trade-off |
|---|---|---|
| 1 | **One small line in each player's own view** (picked by the user) | Says who and why; one line of space, only when something happens |
| 2 | Words floating over the scooters | No HUD space, but tiny at 4-way split and hidden behind scenery |
| 3 | No words: sound, rumble and a flash only | Smallest, but never says who or why |

## The design

### 1. The callout line

- One line of text at the bottom middle of the player's own view, just over the horn charge bars, in that player's view only. (Changed while building, 2026-11-14: the design said "under the player's own score", but that HUD was removed when the live scoreboard came in.) Bots and other machines' players have no line.
- It fades in, holds, and fades out in about 1.5 s **(my call)**. A new line replaces the old one at once.
- Text is small **(my call)**: about the size of the placement suffix ("st", "nd"), never wider than the score block. Long names are cut with "…".
- It follows the Text Size option (`TextSize`) and is translated (`strings.csv`, all languages) **(my call: Claude writes the translations)**.

| Who | When | Line |
|---|---|---|
| Thief | Took an order, paid | `STOLE from <name> +$7` |
| Thief | Took an order, already paid for it | `STOLE from <name>` |
| Victim | Lost an order | `<name> took your order` |
| Attacker | Bump, own slots full | `Slots full` |
| Attacker | Bump, victim held nothing | `Nothing to steal` |
| Attacker | Bump, Steals rule off | `Steals are off` **(my call)** |

- Names are the scoreboard's (`Scoreboard.ShownName`).
- The thief's line is gold, the victim's red, the "nothing" lines white **(my call)**. Colourblind mode needs no change: the words carry the meaning.
- The tutorial shows the lines too, without the `+$7` (steals don't pay there).

### 2. Feel

- **Rumble:** a short pulse for the thief (light) and the victim (strong), through `Rumbler`, so the Rumble option still switches it off.
- **Camera kick:** a quick nudge of the victim's camera, and a smaller one of the thief's **(my call)**. It follows the Camera Effects comfort setting (`Comfort`), like the boost's wide view.
- **No hit-stop** (agreed): time never freezes.
- Both happen only on the machine that drives that player.

### 3. Worth on the package

- A stolen order shows a small `×2`, `×4`, `×8`… stamp floating just over the package, seen by everyone, turned to face each view's camera.
- It appears with the first steal and goes when the order is delivered or respawns. The swell stays.
- The scoreboard shows the same stamp beside the purple flame of the player holding it.
- The golden order and the Mother Lode never show one (they don't double).
- From `×64` up it reads `×64+` **(my call)**, so the stamp never grows wide.

### 4. Sounds

- **Steal** (thief): keeps the "whoosh" (the user's call, 2026-11-14). Its licence is still unknown (`LICENSES.md`).
- **Robbed** (victim): a new, lower sound, only for the victim.
- **Nothing taken:** a soft dull thud **(my call)**.
- All three come from copyright-free sources online (CC0 only), never made by Claude. The user approves each file before it is downloaded. Each is recorded in `LICENSES.md` with its source page and licence.
- Other machines hear the steal sound at a distance, as they hear the whoosh today.
- **(my call)** The clips load by code from `Assets/Resources`, so no scene or prefab changes.

### Online and split screen

- The host decides every steal, as now. Every machine already replays the steal as an order change, and hears the hit (`PlayerHit`). The callout, kick and rumble hang off those on each machine.
- A bump that took nothing needs its reason on the attacker's machine: the host's hit message carries it.
- Split screen: each player's line, kick and rumble are their own.

## Left to the builder

- The line's exact position, font size and timings, checked at 4-way split.
- How the stamp faces each camera in split screen.
- The pure rules class (which line for which outcome, the stamp's text) and its edit-mode tests, in the style of `StealValueRules`.
- Rumble and kick strengths.

## Documents and testing

- `DoA GDD.docx`: the callout lines, the stamp, the sounds. `DoA TDD.docx`: the new classes and the hit message's reason.
- `EDITOR-TODO.md`: a playtest note. Play 4 players (bots are fine) in split screen and online; check each line and the stamp at the smallest view.

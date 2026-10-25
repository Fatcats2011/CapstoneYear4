# Cutscene skip for players: short design

**Date:** 2026-10-23
**Branch:** `steam-phase1a`
**Status:** waiting for the user's review
**Source:** feature 9 of `2026-10-23-modern-features-handoff.md` (pack C). The user accepted the handoff's suggestion: everyone holds A, on both cutscenes.

## Why

The opening and golden-round cutscenes play every match. Now that a rematch is one button, the opening cutscene comes round again and again. The only skip today is the developer key (L, `CutsceneManager.Skip`).

## What players get

- **The prompt (my calls):**
  - A **HOLD A TO SKIP** prompt shows bottom right, one second into a cutscene, so an A still held from the loading screen doesn't skip by accident.
  - The A uses the ButtonGlyph art (`ButtonArtSet.Results().confirm…`), so it shows as A, cross or Space.
  - The prompt shows a count, e.g. `1/3`.
- **Voting:**
  - Each player holds A for **0.5 s** to vote. Letting go sooner does nothing.
  - A vote can't be taken back (my call: the cutscene is short, and taking it back adds a button for little).
  - Once every player in the match has voted, the cutscene ends, exactly as the developer skip ends it.
- **Both cutscenes** work the same way: the opening and the golden round.
- **Online:**
  - Each machine's players vote on their own screens, and the votes go to the host.
  - The host skips once every seated player has voted. A machine that leaves stops counting.
  - The host shares the count, so every machine shows the same `n/m`.

## Approaches

**Picked (the only sensible one):** a small pure rules class (`SkipVote`) with Edit Mode tests, a prompt built by code, and the rematch vote's online pattern. That means a static seam, a relay component and two RPCs, with a `RateGate` allowance and the host's own-seat check.

**Not taken:**
- **Host-only skip:** the user chose everyone.
- **Reusing `RematchVote` itself:** a skip has no countdown and no taking back, so a separate small class is clearer.

## How it fits

- **Skipping:** `CutsceneManager.Skip()` already skips only where states are decided. Online, the host's next state ends the cutscene on every client (`EndForState`). Only the vote is new.
- **Input:**
  - During a cutscene, each player's `MenuInteractions` is in `MenuType.Cutscene` (`PlayerInstantiate.SwapForCutscene`).
  - The hold has to be timed from the player's own UI South action. Read the action's pressed state from their `PlayerInput`: `PlayerUIHandler.SouthFaceValue` is set only on a press, not a release.
- **Which seats vote:** every seat in the roster, the same as the rematch vote.
- **The online pieces:**
  - **Client → host:** "seat n voted".
  - **Host → clients:** the voted and seated masks.
  - **Safety:** add a new `RpcKind` with the default allowance. Clients check the masks (`NetChecks`). Add a row to `docs/online-safety.md`.

## Non-goals

- Skipping the loading screen, the tutorial or the results.
- Taking a vote back.
- One player skipping alone.

## Anti-goals

- **An accidental skip:** an A held from the loading screen, or a quick tap.
- **Machines out of step online:** one machine ending its cutscene before the host does.
- **A client forcing a skip:** voting for another seat, or flooding.

## Constraints

- No scene or prefab edits. The prompt is built by code at runtime.
- Edit Mode tests for the rules, plus one network test (a client's vote counts only for its own seat, and the host skips when everyone has voted).
- Leave everything uncommitted.

## Left to the builder

- The prompt's exact look and position, and where the per-player hold timer lives (in `MenuInteractions`' Cutscene mode, or a small component).
- Docs: `docs/controls.md`, `docs/online.md`, `docs/online-safety.md` and `docs/testing.md`.

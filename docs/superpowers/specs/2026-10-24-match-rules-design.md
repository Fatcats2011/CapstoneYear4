# Match rules the host sets: design

**Date:** 2026-10-24
**Branch:** `steam-phase1a`
**Status:** the user is away ("I trust your judgement for how these are to be done"): every choice here is **my call**. Feature 3 (pack D) of `2026-10-23-modern-features-handoff.md`.

## Intent

Let a group shape the match: a quicker or longer game, a friendly game with no stealing, or no golden round. Normal stays today's game, so nobody who never opens the panel sees a change.

## The rules

| Rule | Choices | Normal | What it does |
|---|---|---|---|
| LENGTH | SHORT · NORMAL · LONG | NORMAL | Each wave lasts 0.75×, 1× or 1.5× as long; the number of waves stays the same |
| STEALS | ON · OFF | ON | Off: boosting into a rival still bumps them, but takes nothing and drops nothing. The tutorial's steal lesson still works |
| GOLDEN ORDER | ON · OFF | ON | Off: after the last wave, the golden scene loads straight into the results (its podium), no golden round. Golden Win, Golden Steal and Last to First can't be earned; the panel says so |

Catch-up (the handoff's fourth) is left out: it needs tuning the user hasn't asked for.

## Where

- **Player select (local) and Dispatch (online host): Start** opens **MATCH RULES**, a panel built by code over the screen. Only the host player (the one who works the menus) can open it.
- Up / down pick a rule, left / right change it, **A or B** close it. Changes apply at once and are saved in `settings.cfg` (`length`, `steals`, `golden`), so they're remembered.
- **Online:** the host's rules run the match: the host runs the waves, judges steals and loads the scenes, and clients follow its states, so nothing new is sent. A public lobby (matchmaking, or the host's open lobby) always plays the normal rules.

## Shape

- `HouseRules` (pure): the three rules, `WaveFactor`, `Normal`, `ForMatch(publicLobby)`, `Summary` ("SHORT · NO STEALS · NO GOLDEN ORDER", or "" for normal). `HouseRules.Current` is what the match uses, set from the settings and the panel.
- `OrderManager` uses `WaveLength` (the serialized wave length × the factor) everywhere it used the wave length.
- `OrderHandler.StealFrom` takes nothing when steals are off (outside the tutorial).
- `SpawnManager.Start` in the golden scene goes to Results when the Golden Order is off; the loading screen also hides on Results.
- `RulesPanel` (view, code-built overlay canvas) shows an `OptionsBook` page of the three rules, like the Options pages' rows.

## Approaches

- **A. Rules applied where the host already decides (chosen):** waves, steals and scene loads already run on the host only, so no message is needed.
- B. Send the rules to every client: needed only for showing them; left for later (the Dispatch board line).

## Left for later

- Clients seeing the host's rules (a Dispatch board line, from the Steam lobby data). Until then a client finds out by playing.
- Catch-up.

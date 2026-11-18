# Pack F: driving and respawn (design)

**Date:** 2026-11-16
**Branch:** `steam-phase1a`
**Status:** approved by the user 2026-11-16 ("go with 1": the design, and sound pick 1); built the same day, with the correction below
**From:** `2026-11-13-player-feedback-handoff.md`, pack F

## Why

Two moments say nothing today: crossing a boost pad, and waiting to come back after falling in the water.

## What is already in the game (checked in the code, not seen running)

- Speed lines show at boost strength while boosting and faintly above about 23 m/s (`BallDriving.ControlSpeedLines`). Reduce motion turns them off.
- A boost pad (ground tagged `Speed`) adds force every physics step the scooter is on it (`groundBoostFlag`). It has no sound and no effect of its own.
- A respawn has one cause in a match: the water. A death sound plays, the orders drop at the respawn point, and the player waits with nothing on screen. **Correction (found while building):** the wait is about 2 seconds, not 7. The 7 came from the script's defaults; the player prefab sets wisp 0.5 + 0.8 and lift 0.7.
- "Kick" in the handoff is for props (`Kickable`), not players: nothing to do.

## Goals

1. Crossing a boost pad feels like something happened.
2. A player in the water knows how long until they drive again.

**Not in this pack:** changes to boost pad force or respawn time; a number over the gravestone (the camera moves through the respawn; it can't be placed well unseen); anything for props.

**Would make it a failure:** a pad sound that repeats every physics step or grates when pads come in a row; a countdown that covers the view or is wrong by more than a second.

## Approaches

One sensible way for each, both reusing what exists: the pad drives the speed lines and the feedback sound player already there; the countdown uses the callout line. A new HUD element for either would break "small UI".

## Design

### 1. Boost pad

- **When:** the scooter comes onto a pad, counted once per crossing: a new crossing needs 0.75 s off pads first **(my call)**, so a row of pads or a long pad is one moment, not a stutter.
- **Speed lines:** full boost strength for 0.5 s, then back to what the speed gives. Off with Reduce motion, as today.
- **Rumble:** one short light pulse (0.2 / 0.4 for 0.15 s). Off with rumble off, as today.
- **Sound:** one short rising sweep, for the player on the pad only, pitched a little higher the faster they are going (1.0 to 1.25). The file is the user's pick: `maximize_006.ogg` from Kenney Interface Sounds (CC0), the rising twin of the robbed sound, as `pad.ogg`. Volume set against the music as the other feedback sounds, to tune in the playtest.
- **Online and split screen:** each machine does this for the players it drives. Nothing is sent, and other players hear nothing.

### 2. Respawn countdown

**As built:** the `FELL IN THE RIVER` line shows. The countdown below is built, but only counts a wait of 3 seconds or more (`DrivingFeedbackRules.MIN_COUNTED_WAIT`), so with today's 2-second respawn no numbers show. The user was told on 2026-11-16.

- **When the player goes in the water:** their callout line reads `FELL IN THE RIVER`.
- **Then:** `BACK IN 6`, `5` ... `1`, from the whole seconds left of the respawn (7 seconds from when the rise begins, counted on the game's clock so a pause holds it), a new line each second, in the plain tone. It clears as the player can drive again.
- **Other lines:** an event line that arrives meanwhile (a steal elsewhere can't involve a respawning player, but a lead change or mode line can) shows for its usual time, then the countdown carries on from the right number.
- **The tutorial's respawn:** it shows there too, since the wait is the same.
- **Online:** a client waits up to 2 s for the host's respawn point first; the count starts from the real time left once the rise begins, so it may open at `FELL IN THE RIVER` a little longer.
- **Strings:** `respawn.fell` and `respawn.back` (`BACK IN {0}`), 16 languages.

## Structure

- `DrivingFeedbackRules` (pure): pad crossing (`PAD_GAP`, whether a touch starts a new crossing), the burst (`BURST_SECONDS`), the pitch from speed, and the countdown number from time left. Edit-mode tests.
- `BallDriving`: tells a crossing from its ground check; `ControlSpeedLines` holds the burst; `PadJolt()` for the rumble.
- `FeedbackSounds.PAD` and its volume; `SoundPool.PlayFeedback` gains an optional pitch.
- `StealCallout.HearRespawn(secondsLeft)` / a small tick that `Respawn` feeds through a static hub (`RespawnFeedback`), as the other packs' hubs.

## Testing

- Edit-mode tests for the rules, the callout's countdown (its numbers, yielding to an event line and resuming), the pad's once-per-crossing, and the two strings in every language.
- Playtest note in `EDITOR-TODO.md` 16m: pads singly and in a row, the sound against the music, the countdown reaching 1 as control returns, online as a client.

## Documents

- GDD: the pad moment and the countdown (HUD, sound list). TDD: the new classes. `LICENSES.md`: the sound, once picked.

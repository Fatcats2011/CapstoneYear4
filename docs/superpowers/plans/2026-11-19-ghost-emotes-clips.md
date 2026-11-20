# Ghost emotes, part 2: hand clips

Spec: `docs/superpowers/specs/2026-11-19-ghost-animations-design.md`. Part 1: `2026-11-19-ghost-emotes-core.md`. Branch `steam-phase1a`. Nothing is committed.

Scope of this part:
- hand clips authored in Blender from tables, previewed from the game camera, and exported as one FBX
- an upper-body Animator layer that plays them
- `GhostEmotes` playing a version's clip together with its procedural body part
- reworking 10b and 22b, which don't read because the scooter hides the tail

Not in this part:
- triggers in the game, and online (part 3)
- the parcel or crown following the hand (part 3, since it touches `OrderHandler` and the results)

## Pipeline
1. **Clip tables.** `tools/ghost-clips/clips.py` holds one table per clip. Keys are poses for `Head`, `Hand.Left/Right` and `Finger.Left/Right` in the ghost's own frame:
   - pos `(fwd, left, up)` in U
   - rot `(roll, pitch, yaw)` in degrees
   - Every clip starts and ends at rest. Clip names are `GE_<specid>_<Name>`.
2. **Build and export.** `tools/ghost-clips/ghost_clips.py` builds the Blender actions, renders review sheets to `../GhostEmoteCaptures/blender/`, and exports `Assets/Animations/Character Animations/GhostEmotes.fbx`. The export holds the armature and the mesh as two roots, like `Official_Anim_4.fbx`, so clip paths come out as `Armature.001/...`. It has one take per clip and runs in a live Blender over MCP, or headless with `blender -b -P tools/ghost-clips/ghost_clips.py`.
   - `GE_0_RestCheck` is not an emote. It is the rest pose, used to check that the round trip keeps the bones' orientation.
3. **Unity import.** `GhostEmotes.fbx` is imported as Generic, with no Avatar, one clip per take, and loop off.
4. **The Animator layer.** `GhostEmoteLayerSetup` (menu `DoA/Setup/Ghost Emote Layer`, plus `BuildFromCommandLine`) is idempotent:
   - It sets the FBX import settings.
   - It creates `GhostUpperBody.mask`, a transform mask with only `Armature.001`, `Armature.001/Torso`, `Torso/Head`, the hands and the fingers enabled. Every other path is disabled.
   - It adds or updates the `Emotes` layer in `PlayerAnimator.controller`: override blending, the mask, default weight 0, and an `Empty` default state.
   - It adds one state per `GE_` clip (not `GE_0_RestCheck`), with Write Defaults off.
   - It runs in the test mirror, and the changed assets are copied back, because the user's editor is open on the real project.
5. **Playback.** `GhostMove` gets an optional `Clip` (the state name; null = none). `GhostEmotes.Play` does two things:
   - It calls `animator.Play(clip, emotesLayer, 0)` when the layer and state exist.
   - It eases the `Emotes` layer's weight up to 1 over 0.12 s, and back to 0 over the last 0.15 s of the move.
   - The procedural body part runs as before. A move's `Length` must equal its clip's length, and a test checks this.

## Authoring rules
- **Readability:** from behind, a hand only reads above the horns (up ≥ about 2.2 U from its rest spot) or out past the body's sides (|left| ≥ about 1.3 U). Check every clip on its `behind` sheet.
- **Body moves stay procedural:** lunges, bows, spins and bounces go in the version's `GhostMoves` curve, not in the clip.
- **The hands are floating cartoon hands:** they can go anywhere, with no arms to break.
- **Holding a parcel or crown:** hands go together, as if holding a box between them. The object itself follows in part 3.

## Tasks
1. **Unity pipeline** (one agent):
   - the import settings and `GhostEmoteLayerSetup`
   - `GhostMove.Clip` and layer playback in `GhostEmotes`
   - tests: the round trip (`GE_0_RestCheck` poses the bones like the bind pose), the layer and mask exist, a state per clip, and a clip version playing raises the layer weight then lowers it
   - wire the three proof clips (2a, 11a, 14a) into `GhostMoves`
   - capture them
2. **Clip batches** (one agent per batch, one at a time, because Blender and `clips.py` are shared). Each batch writes its tables, renders, iterates until every `behind` sheet reads, adds the matching `GhostMoves` versions, re-exports, and reruns the setup and capture.
   - A: match reactions (1b 1d 2b 2c 2d 3a 3b 3c 3d 4a 4b 4c 5a 5c)
   - B: match, other (7a 7b 7d 8b 9a 9b 9c 10a 10c 10d 11b 11c, plus the reworked 10b and 22b)
   - C: lobby and countdown (12a 13b 14c 15c 19a 19b 19c 20c 21a 21b 21c 22a 22c 23a 23b 24a)
   - D: results (16a 16b 16c 16d 17a 17b 17c 25a 25b)
3. **Review** all `behind` sheets and fix any that don't read.
4. **Docs:** TDD, spec and memory.

## Body parts for the clip versions
These are the procedural `GhostMoves` curves that go with each clip, from the authors' notes. Shares are fractions of the clip's length. "Left" and "right" are the ghost's own sides. A version's `Length` = its clip length (see `clips.py`). "Turn round" = the part 1 turn (Yaw s·100, HeadYaw s·60).

| Id | Name | Body part |
|---|---|---|
| 1b | Parcel Shake | Roll ±10 in time with the shake (side flips at .27/.42/.57/.72) |
| 1d | Parcel Spin | small lean away from the raised right hand (Roll −8·Hold) |
| 2a | Fist Pump | Lift 0.12·Bounces(t,3) |
| 2b | Arm Flail | Lift bob 0.1·Bounces(t,4) over .1–.8 |
| 2c | Hat Throw | Roll +15 to the right over .4–.55, small Lift bump; the hat pops off with the throw (HatLift / HatFade in code) |
| 2d | Lunge Grab | Pitch +20 over .1–.5, hold to .6, back to 0 by .9 |
| 3a | Sweeping Bow | Pitch +25 over .4–.7, slight Roll right, upright by .95 |
| 3b | High Fist | hop: Lift 0.3·Arc around .2–.45 |
| 3c | Hat Lift | lean back (Pitch −10) over .3–.5; the hat follows the hand: HatLift up to ~2.5 over .2–.6, back by .8 |
| 3d | Brush Off | tiny Lift bumps at .2 and .4 |
| 4a | Hat Clamp | Pitch −8 lean back, Roll ±8 buffeting |
| 4b | Plane Arms | Roll ±20 banking (left up at .3, right up at .5) |
| 4c | Superhero | Pitch +10 over .15–.3, held; Lift 0.2 held |
| 5a | Arms Up | Lift 0.15·Bounces(t,3) |
| 5c | Starfish | hop: Lift 0.5 over .12–.3, land ~.85 (squash via Land) |
| 7a | Parcel Overhead | Lift 0.08 bob over .15–.8 |
| 7b | Parcel Hug | Yaw ±25 twist over .15–.65, still from .7 |
| 7d | Presenting | Yaw towards left (+30) over .15–.5, across to right (−30) over .5–.8, hold to .92 |
| 8b | Hat Grab | Lift 0.6 pop over 0–.2, settle by .6; the hat pops up (HatLift 1.5 over 0–.25) and is caught back by .8 |
| 9a | Big Stretch Up | Pitch −10 over .2–.55, then Pitch +15 slump over .7–.85, back by 1 |
| 9b | Slump Over | Roll +25 to the right over .15–.85 with ±4 sway, recover by 1 |
| 9c | Shrug | turn round over 0–.2, hold, back over .85–1 |
| 10a | Ear Waggle | HeadRoll ±8 bob |
| 10b | Bum Wiggle | Roll ±15 in time with the hands (left peaks .27/.57, right .42/.72) over .15–.85, plus the old TailWag |
| 10c | Point Back | Yaw −20 towards the right + Roll −8 over .15–.8 |
| 10d | Come At Me | Pitch +10 + HeadRoll 12 over .15–.9 |
| 11b | Turn And Wave | turn round over 0–.2, hold, back over .85–1 |
| 11c | High Five | Pitch −10 and Roll −10 towards the hand over .15–.7 |
| 22b | Tail Point | Roll +8 away from the tail, HeadRoll 10, over .15–.8 |
| 12a | Hat Twirl | the hat follows the right hand: HatLift ~2.5 and HatSpin 720 over .2–.75, back on by .85 |
| 13b | Ta Da | Lift 0.25 hop over .1–.3 (it's an entrance: Fade 1 at t=0 like 13c) |
| 14c | Shadow Box | Yaw ±12 twist towards each punch (left .1/.34/.58, right .22/.46/.7) |
| 15c | Race Starter | Pitch +15 over .5–.7, back by .9 |
| 19a | Hand Rub | Pitch +12 hunch over .1–.85 |
| 19b | Sly Rub | turn round over 0–.2, hold, back over .9–1 |
| 19c | Excellent | none |
| 20c | Parcel Clutch | Yaw +25 and Roll +10 away to the left over .3–.8, back by .85 |
| 21a | Drum Solo | Lift 0.08 bob on each stroke over .1–.9 |
| 21b | Watch Check | HeadRoll +10 towards the left wrist over .15–.75 |
| 21c | Big Yawn | Pitch −12, HeadPitch −25 over .1–.45, back by .6 |
| 22a | Thumbs In Ears | turn round over 0–.12, hold, back over .9–1 |
| 23a | Admire | Pitch −8 over .15–.85 |
| 23b | Hand Mirror | HeadRoll ±10 over .3–.75 |
| 24a | Lace And Push | Lift 0.05 bumps at .4/.54/.68 |
| 16a | Crown Overhead | none (crown follows the hands' midpoint .18–.78, back to the head .78–.9: part 3) |
| 16b | Arm Pump Dance | Lift 0.12 bounce per pump over .1–.9, Roll ±6 sway |
| 16c | Arms Out Spin | Yaw 360·Smooth over .15–.85 (exactly 0 at 1, like 12b) + Lift 0.2·Arc |
| 16d | Raise The Roof | Lift 0.1 bob per push over .15–.8 |
| 17a | Fist Pound | Lift 0.12 hop per pound over .12–.9 |
| 17b | Flop Back | Pitch −35 over .15–.5, hold to .8, up by 1 |
| 17c | Sob | Tall 1±0.06 heave and Pitch +10 over .2–.85 (step ~.08) |
| 25a | Polish | none (crown follows Hand.Left .22–.78, back to the head by .9: part 3) |

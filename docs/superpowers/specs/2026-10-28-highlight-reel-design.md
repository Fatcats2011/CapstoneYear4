# Highlight reel: design

**Date:** 2026-10-28
**Branch:** `steam-phase1a`
**Status:** waiting for the user's review

## Why

At the end of the main round, players see a short reel of the round's best moments, filmed from invisible cameras that pick the best angle on each one. It gives the match a payoff before the golden round. It also replays the moments players talk about, which are mostly the ones where they affected each other: steals, clashes, bumps that knock someone into the water.

## What players get

- **When:** the main round's clock runs out → the existing slow-motion linger → **the reel plays full screen** (not split) in the town they just drove through → the golden round loads as it does today. If the match rules turn the golden round off, the reel still plays, and then the results load as they do today. The tutorial never counts. There's no reel for the golden round.
- **Length:** 10 to 20 seconds, set by how good the round was. Clips are added best first until the next one isn't good enough or the reel would pass 20 s. A round too quiet to fill about 10 s gets a shorter reel. A round with nothing worth showing gets no reel at all. There's never any padding.
- **Clips:** about 3 to 6 s each. A clip starts a beat before the action, so you see it coming, and ends just after the payoff (the splash, the delivery).
- **Everyone appears:** the reel tries to give every player at least one clip, as long as they did something worth showing (the awards' "everyone gets a card" spirit).
- **Skipping:** the same **HOLD A TO SKIP n/m** as the cutscenes. The reel ends once every player still in has voted. Online, the host counts the votes, and a machine that leaves stops counting.
- **Look: hidden-camera footage (style C).** A REC dot, a camera number for each shot (`CAM 07`), a timestamp from the match clock, light scanlines, a faint frame, and a dry caption on the key moment (`THEFT DETECTED`, `SUBJECT ENTERED WATER`, `DELIVERY CONFIRMED`). The colour is only lightly faded, so the art still reads. Captions go through `Loc` like every other string.
  - **The user may switch styles later** (the mockups also had A, clean with cartoon words, and B, a sports broadcast). The look is a separate overlay layer from the director, so swapping it means replacing that layer only.
- **Cameras are invisible.** There are no props in the world. Each shot is placed by the director for that clip.

## Scoring: what makes the reel

The user asked for skill **and** chaos, with **player interaction weighted most**.

- **Moments** the scorer hears about, with a starting weight (my proposal, to be tuned by playtesting):

  | Moment | Kind | Start weight |
  |---|---|---|
  | Steal of the golden order (if it ever happens in the main round) | interaction | 10 |
  | Steal | interaction | 8 |
  | Clash (boost into someone, no steal) | interaction | 6 |
  | Bump between scooters, scaled by how hard | interaction | 1–4 |
  | Hard delivery | skill | 5 |
  | Delivery in the last 10 s of the round | skill | +3 on top |
  | Other delivery | skill | 2 |
  | Fell in the water alone | chaos | 3 |

- **Combos stack.** Moments within a few seconds of each other that share a player merge into one clip. The clip scores more than the sum, for example ×1.5 for each extra moment. A steal, then a bump, then the victim in the water becomes one big clip. **Any moment involving two players** (a fall right after a bump or clash, a steal straight back) gets an interaction bonus. That's how interaction rises to the top without skill plays disappearing.
- **Picking the reel:**
  1. Score every clip.
  2. Give each player their best clip first, if it clears a minimum score. This is the fairness pass.
  3. Fill with the best remaining clips while the total stays ≤ 20 s and each clip clears the "good enough" bar.
  4. Play in **match order**, so the reel tells the round's story. My call: playing best-last is the alternative, but match order reads better with timestamps on screen.
- **A clip** knows its start and end time, the players in it, its key moment (time and place), and its caption.

## Approaches

- **Picked: A. Record poses, replay with stand-in copies.** During the round, a recorder saves each scooter's pose (the `ScooterPose` data online play already sends), plus the order bags, boosts and splashes, into a rolling buffer about 20 times a second. At the reel, the real players are hidden and frozen. Stand-in scooters in each player's colour and hat act out the recorded poses, and the director's cameras film them. The real players, physics, respawns and online sync are never touched, so the reel can't break the round or anything after it. The costs are building the stand-ins and a few MB of memory.
- **Not taken: B. Replay with the real players in "puppet mode".** It's less to build, but it means switching off and cleanly restoring driving, physics, respawns and online sync for every player, including after a skip halfway through. That's the kind of state that has caused bugs before.
- **Not taken: C. Record real video as it happens.** It costs frame rate during play (worst in split-screen and on the Steam Deck), can't choose angles after the fact, and online each machine would film different footage.
- **Ruled out: record inputs and re-run the round.** Unity physics doesn't replay the same way twice, and online it would drift right away.

## How it works

### 1. Recording (every machine)

- From `MainLoop` until time up, a recorder samples every seat's scooter about 20 times a second. It records the pose (ball, heading, model position and rotation, which already include lean, drift, hop and wheelie), whether the player is boosting, and whether they're carrying an order and of what value. It also records when players hide and reappear for respawns.
- Order bags lying in the world are **not** replayed at first (planning call, 2026-10-28). The bags **carried** on scooters are, from the recorded "carrying" value, so steals and deliveries still show the bag changing hands or leaving.
- Effects to replay, from the recorded flags (`BallDriving.Flags`: boosting, hidden): a boost trail while boosting, and the scooter vanishing while it's hidden (a fall or respawn). Nothing else at first (planning call).
- **Not recorded at first:** pedestrians, kicked cans, other physics props. They're simply absent from the replay. Adding them is a possible later extra.
- **When it stops (my call):** recording runs for the whole main round, with no cap, and stops at time up (`OrderManager.OnMainGameFinishes`). Orders erased or re-parented at time up (`MasterSkywalker`, `Order.EraseWhenSwappingToGold`) are never recorded as picked up. A delivery before time up counts.
- **Time base (my call):** every sample and moment is stamped with a monotonic **round time**. Offline that's seconds since `MainLoop` began (scaled game time). Online it's Netcode server time, which already means the same moment on every machine (as `MatchClock` uses it). Separately, the rules machine stamps each moment with the **time left on the round's clock**, the number the HUD shows. That feeds the "last 10 s" bonus (`FeatSync.Delivered` doesn't carry it, so read it from `OrderManager` when the delivery is heard) and the `CAM` timestamp text. A clock that stops or jumps never breaks the replay, because playback only uses round time.
- **Budget (my calls):** recording must not show up in the frame time. Use fixed-size arrays, nothing allocated per frame, and check it on the Steam Deck and in 4-player split-screen with `PerfOverlay`. A round's worth of samples for 4 players is about 5 minutes × 20 Hz × 4 seats, a few MB at most. The whole round is kept (any moment can make the reel).
- **Online:** each machine records what it sees. Other machines' scooters are the smoothed remote poses (`RemoteAvatar` / `ScooterPose`), which is fine: they're a hair off at most, the same as during play.

### 2. Scoring (the machine that runs the rules: this PC offline, the host online)

- It listens to the same sources as the awards (`MatchAwards`): `FeatSync.Delivered`, `Stole`, `Robbed`, `Sank`. Those carry only one seat, but steals and clashes need the **pair** (attacker and victim). Online, `PlayerHit` (Steal / Clash, attacker and victim) carries it. Offline, the steal/clash decision is made in `OrderHandler`. **New events to add:** a steal-with-victim and a **clash** event, raised where the rules machine decides them, both offline and online. Clash has no event today, and its `PlayerHit` is online only.
- **Bumps:** online, `OnlineBumps` / `BumpRules` already pass bumps through the host with a strength. Offline there's no bump event yet, so add one where scooters collide on the deciding machine, filtered as `BumpRules` does (pair cooldown, strength from closing speed).
- **Falls online:** already covered. A client's fall reaches the host as its respawn request, and the host raises `FeatSync.Sink` for that seat (`OnlineRespawns.PickFor`). Nothing to add.
- Each moment is stored with its time, seat(s) and place (the place comes from the pose recording at that time).
- **Bots:** these are only the developer autopilot (`BotAutopilot`, F7), which drives a player's seat. That seat is still the player's and scores like any other. A seat that leaves mid-round keeps the clips it was already in.
- It's pure rules code: a plain C# class with no Unity objects, so it can have Edit Mode tests (scoring, combos, the fairness pass, the 10–20 s fill, a quiet round with no reel), like `AwardRules`.

### 3. Directing (the rules machine picks, everyone films)

- For each chosen clip, the director tries several **shot types** around the action:
  - **low:** near the ground as the scooters pass
  - **high wide:** shows the chase
  - **side track:** moves alongside
  - **behind:** follows the attacker / main player
  - **fixed pan:** a spot ahead of the action that turns to follow, sports-camera style
- **Rejecting bad shots:** for each candidate, sample a few times across the clip. Line-of-sight raycasts to the players in the clip (and the key moment's place) must be clear of buildings, trees and bridges, and the players must stay in frame. Drop any candidate that fails.
- **Choosing:** of the survivors, take the best framed, but **never the same shot type twice in a row** across the reel.
- **Peak cut:** on the big clips, cut to a second, closer angle at the key moment, with a short slow-down (playback speed only, since it's a replay).
- **Beauty spots (optional, later):** a scene marker the user can drop at places like the waterfall or the drawbridge. When action passes near one, the director prefers it.
- **Shots aim at the stand-ins, not at fixed points (my call).** A shot stores where the camera sits (or how it moves, for side track and behind, relative to its subject) and which seats it frames. Each frame, the camera looks at those seats' **stand-ins** as this machine plays them. So if a client's recording of a remote scooter is a little off, the framing still follows it. Only the camera's placement is shared, never a fixed aim point.
- **Online:** the host runs the director, using its own recording and its own world. It sends every machine the **reel list**: for each clip, start/end round time, players, caption key, and each shot's type, placement, framed seats and cut time. That's small: a few clips × a few shots. Each machine plays that list against its own recording, so everyone sees the same plays from the same angles. This follows the awards' pattern (`AwardSync` / `OnlineAwards`): a static seam plus a relay component and an RPC.

### 4. Playback (every machine)

- **Offline:** it runs in `OrderManager.PostGameClarity(false)`, between the slow-motion linger and `SceneFlow.Current.LoadFinalOrderScene()`. The load follows when the reel ends or is skipped.
- **Online (my calls):** `PostGameClarity` and the load run only on the host (`InitWave` is host-only). Clients just get the `ClockCue.TimeUp` cue, then the host's scene load. `SetTimeScale` does nothing online, so there's no slow motion there.
  - **Start:** after the linger, the host sends the reel list **with a start time in server time**, a little in the future (for example 0.5 s) so the list reaches everyone first. Every machine starts the reel at that server time. A client whose list arrives late starts at the matching offset, a little way in, so all machines stay on the same frame.
  - **End:** the host waits for the reel's length, or the skip vote, then starts the load as it does today. It never waits on any client. A client still showing the reel when the host's load arrives just ends it.
  - **No list:** a client that never got the list (a late join, a drop) shows no reel. It sees the post-round view until the host's load comes, as it does today.
  - **No reel:** if the host decides the round has no reel, it sends nothing and loads as it does today.
- **Stand-ins:** one per seat in the clip, built from that seat's look (colour and hat, as `PlayerColorInformationSO` / `PlayerHatInformationSO` and the customization code use). They have no physics, input or networking. Each frame they're set to the recorded pose, interpolated between samples. Bags and effects are replayed from the recorded events.
- **Real players:** hidden and left frozen for the reel's length. Nothing on them is switched off or restored. They're already stopped at time up.
- **Camera and screen:** one full-screen reel camera replaces the split-screen player cameras for the reel. This interacts with `SplitScreenLayout` / `PlayerCameraResizer`. The player HUDs (compass, minimap, indicators) are hidden. The style-C overlay goes on top, with the cutscene skip prompt bottom right.
- **Skip:** reuse `SkipVote` and `CutsceneSkip`'s prompt, hold and online vote path. The seats are every roster player, as `CutsceneManager.OpenSkip` builds them. A seat under the developer autopilot is still a person's seat. Whether `CutsceneSkip` is opened by the reel directly or generalized beyond `CutsceneManager` is left to the builder.
- **Sound:** the existing post-round snapshot ("paused") carries on. Replay the recorded effect sounds lightly or not at all. Music and stings are left to the builder and the user's ear.

## Goals

- A 10–20 s reel at the end of every main round that has moments worth showing, weighted towards player interaction.
- Good, varied, unblocked angles with no hand-placed cameras.
- The same reel on every machine online.
- A skip vote that works like the cutscene skip.
- A look (style C) that can be swapped later.

## Non-goals

- A reel for the golden round.
- Saving, exporting or sharing clips (Steam, files, video).
- Replaying pedestrians and physics props, at first.
- Visible cameras in the world.
- Letting players pick or rewatch clips.

## Anti-goals (a failure even if it works)

- **Any frame-rate drop during the round** from recording.
- **The reel breaking what comes after it:** the golden round, results, rematch, disconnects, or a skip halfway through.
- **A reel that's all one player** while others did things worth showing.
- **Boring padding** to reach 10 s.
- **Shots staring at a wall:** a blocked or empty frame.
- **Machines online seeing different plays.**

## Constraints

- Unity 2022.3, Netcode for GameObjects over Steam, local split-screen up to 4, online up to 4 seats, the developer bot autopilot.
- No Cinemachine in the project. Hard cuts don't need it. Adding it is the builder's call, only if blends are wanted.
- Online messages from clients go through `RateGate` with the host's own-seat checks, like the skip and rematch votes.
- Tests: Edit Mode tests for the scoring/picking rules and the director's shot choice (variety rule, rejecting blocked shots, against simple stand-in geometry if practical). Add one network test: a client receives the host's reel list and plays it, and the skip vote ends it.
- Strings go in the localization CSV for every language.
- Branch policy: work on `steam-phase1a`, never `main`.

## Left to the builder

- Exact sample rate, buffer layout and interpolation.
- Starting weights, combo window and multiplier, the "good enough" bar, and the per-player minimum, tuned by playtest.
- Shot distances, heights and framing scores, and how many candidates to try per clip.
- How the stand-ins are built (a stripped copy of the scooter prefab, or a dedicated light prefab).
- How the reel camera takes over from the split-screen cameras, and how the overlay is built (code-built like the skip prompt, or a prefab).
- Peak slow-down speed and length.
- Sound treatment.

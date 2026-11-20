# Boost ring: design

2026-11-17 · branch `steam-phase1a`

## Why
New players don't notice the horn glow. They can't tell when boost will be back, and they may not realise boost does anything. We need a charge readout they can't miss, that teaches itself and doesn't rely on them remembering the tutorial.

## Goals
- A ring on the ground under each scooter that fills as boost recharges (3s, or 8s carrying a package: the player prefab sets 8s).
- It explains itself: the same colours as the horns, the boost button showing when it's full, and a visible "spent" burst when used.
- Small and quiet while charging, loudest when full (fits "added UI stays small").

## Non-goals
- No HUD bar. The existing `hornSliderLeft/Right` sliders are left as they are.
- No change to boost rules or timings.
- Online: other machines' scooters get no ring. Their charge isn't synced, and their horns already flash through `CueKind.HornReady`. Online CPU bots get no ring either (nothing drives their gauge).

## Anti-goals
- A ring that looks like a pickup zone, an area attack or a boost pad.
- Prompts that clutter four-player split-screen for experienced players.

## Approaches considered
1. **Flat ring mesh + radial-fill shader (picked).** Cheap, no renderer changes, safe on Metal/Linux and in split-screen. It may dip slightly into bumpy terrain.
2. URP decal projector. Wraps terrain perfectly, but no renderer uses decals today. It would need adding to every per-player renderer, with a split-screen cost and Metal risk.
3. LineRenderer arc. Fastest, but thin and flat, and hard to make glow and pulse like the horns.

## Behaviour (what was agreed)
| Moment | Ring |
|---|---|
| Charging | Fills around the ring in the horn gradient, dim |
| Carrying a package | Dashed/heavier look so the slower 8s fill reads as deliberate |
| Full | Bright, gentle pulse, flashes with the horns; boost button prompt in front |
| Used | Bursts outward behind the scooter, then refills from empty |

- **Button prompt:** the device-correct icon (existing `ButtonGlyph`). Only the owning player's camera sees it (that player's world layer, 10–13).
- **Prompt rule, local (couch, and solo with bots):** the prompt shows on every full ring, always. Couch players can't be told apart between sessions, so there's nothing to count.
- **Solo with bots** is hosted online under the hood, but counts as local (user, 2026-11-17): the online rule applies only when another machine's player is in the match.
- **Prompt rule, online:** per player. The prompt shows on every full ring for that player's first **3 online matches**. After that it appears only once the ring has sat full for a few seconds. One machine is one online player, so the count is saved in that player's `settings.cfg` next to `hints=`, and only online matches count.
- **New one-time hint:** "ring full and unused for ~15s" is added to `HintKind`/`HintRules` with a strings.csv line. It's saved in the hints mask and not shown in the tutorial (same as the other hints).

## How it's built (decided)
- The ring mesh sits under the scooter, lifted slightly above the ground, and tilts to the ground normal from `BallDriving`'s existing ground raycast.
- `PhaseIndicator` drives the ring from the same ratio it uses for the horns, so the two always agree. The package state comes from `BallDriving` (`SetBoostModifier`).
- Couch: every local player's ring is visible to all cameras. Only the prompt is per-player.
- GDD (HUD/feedback, controls teaching) and TDD (PhaseIndicator, the new shader/component, settings key) are updated in the same piece of work, with Revision History lines and a version bump.
- A playtest item goes into EDITOR-TODO: a fresh player, no explanation, asked afterwards "what's the ring?".

## Left to the builder
- Exact ring radius, thickness, lift height, pulse speed, burst look and the "full for a few seconds" delay. All are tunable serialized values.
- Whether the "dashed" package look is dashes or a darker/thicker band, whichever reads better at the ring's size.
- Which object in the prefab the ring hangs from, so it follows position but not the scooter's wheelie/lean.
- Phasing: the ring stays visible while phasing unless it looks wrong in practice.

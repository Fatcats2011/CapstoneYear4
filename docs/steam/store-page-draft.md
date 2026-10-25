# Steam Store Page — Draft

Built from what the game actually contains (updated 2026-10-20: online play, matchmaking, keyboard, Steam features). Edit freely; items marked ⚠️ depend on open legal checks in `LICENSES.md`.

## Basics

- **Title:** Dead on Arrival
- **Developer / Publisher:** The Boo Crew
- **Players:** 1–4. Local split-screen, online over Steam (friends or matchmaking), or both at once: two players on one PC can join an online match together. Remote Play Together once it's enabled in Steamworks.
- **Controller:** Full controller support. One player can use the keyboard (button prompts show controller buttons until the keyboard prompt art lands).
- **Steam features:** 9 achievements, Steam Cloud (settings), Rich Presence, invites from the friends list.
- **Suggested tags:** Racing, Party Game, Local Multiplayer, Split Screen, Online PvP, PvP, Multiplayer, Arcade, Driving, Casual, Funny, Cartoony, Colorful, Stylized
- **Steamworks checkboxes:** Single-player, Online PvP, Shared/Split Screen PvP, Remote Play Together, Steam Achievements, Steam Cloud, Full controller support

## Short description (≤ 300 characters)

> Ghost couriers race through a haunted town on scooters in this 1–4 player party racer, split-screen or online. Grab orders, phase-boost through buildings, drift for speed and steal packages from rivals: the richest ghost after the final Golden Order wins.

(255 characters.)

## About this game

**Deliveries are to die for.** You're a ghost courier with a scooter, a haunted town full of hungry customers, and up to three friends trying to take your tips.

**Grab, deliver, get paid.** Orders pop up all over town. Grab them, carry up to two at a time, and race them to the waiting customer for cash. Harder orders pay more.

**Phase-Boost.** Hit boost to wheelie forward and turn intangible — blast straight through buildings to cut corners, or boost into a rival to steal their package.

**Drive with style.** Drift to charge three tiers of spark boosts, tuck in behind a rival to catch their slipstream, and watch out for the water.

**The Golden Order.** After three escalating waves, one Golden Order appears in the final area. It gets more valuable the longer someone holds it — so everyone is coming for it.

**Make it yours.** Pick your ghost's color and hat, send emotes mid-race, and see who tops the podium.

**Couch or online.** Play split-screen on one screen, invite friends over Steam, or let matchmaking find a lobby. Bring a friend on the couch into an online match too.

## Features

- 1–4 player local split-screen chaos
- Online play over Steam: invite friends or find a match, with couch players joining together
- Phase-Boost through buildings and into rivals to steal their deliveries
- Drift boosts, slipstreams, pedestrians and props to knock around
- Three waves of rising order difficulty, then a Golden Order showdown
- Ghost colors, hats and emotes
- In 16 languages: English, French, German, Italian, Dutch, Spanish, Latin American Spanish, Portuguese, Brazilian Portuguese, Polish, Turkish, Czech, Romanian, Hungarian, Swedish and Danish
- Rematch with one button, and an award for every player at the results (Kleptoghost, Butterfingers, Fish Food...)
- Four rival delivery companies ⚠️ (names pending legal review)

## Content survey notes

- Cartoon ghosts; death is played for laughs (you respawn from a gravestone after falling in water).
- Mild slapstick: pedestrians and props get bumped or flung, then pop back — no blood or gore.
- No chat (text or voice), no purchases and no user-generated content. Online, other players' Steam names are shown on the lobby board.

## System requirements

To be measured during Phase 1 performance work (4-player split-screen at 1080p). Until then: Windows 10/11 64-bit, DirectX 11 GPU.

## Screenshot / trailer shot list

1. Four-way split-screen mid-race.
2. A Phase-Boost straight through a building.
3. A steal: boosting into a rival and taking their package.
4. Drift sparks at tier 3.
5. The Golden Order finale.
6. Character customization (colors and hats).
7. The results podium.
8. The Dispatch lobby: friends' tickets filling up before an online match.

## Achievements (paste into Steamworks → Stats & Achievements)

The API names, display names and descriptions are in `docs/steam/in-game-features.md` (Achievements). Each needs two 256×256 JPG icons (unlocked and locked), so 18 icons in all.

## Rich Presence

Upload `docs/steam/rich-presence-english.vdf` as it is (checked 2026-10-20: its 8 tokens match `PresenceRules`).

# Steam Store Page — Draft

Built from what the game actually contains (updated 2026-11-03: bots, steal value, live scoreboard, highlight reel, match rules; tutorial removed). Edit freely; items marked ⚠️ depend on open legal checks in `LICENSES.md`.

## Basics

- **Title:** Dead on Arrival
- **Developer / Publisher:** The Boo Crew
- **Players:** 1–4. Local split-screen, online over Steam (friends or matchmaking), or both at once: two players on one PC can join an online match together. Online, empty seats fill with CPU rivals unless the host turns Bots off. Remote Play Together once it's enabled in Steamworks.
- **Controller:** Full controller support. One player can use the keyboard (button prompts show controller buttons until the keyboard prompt art lands).
- **Steam features:** 9 achievements, Steam Cloud (settings), Rich Presence, invites from the friends list.
- **Suggested tags:** Racing, Party Game, Local Multiplayer, Split Screen, Online PvP, PvP, Multiplayer, Arcade, Driving, Casual, Funny, Cartoony, Colorful, Stylized
- **Steamworks checkboxes:** Online PvP, Shared/Split Screen PvP, Remote Play Together, Steam Achievements, Steam Cloud, Full controller support, Single-player. Solo play is online-only: Online Play hosts a friends-only lobby, a lone ready player starts the match (`DispatchRules.EveryoneReady`), and the three empty seats fill with bots (Bots rule on by default; `OnlineBotsNetworkTests` covers it over a direct connection). Offline has no bots. ⚠️ Play one solo match through Steam by hand before ticking it.

## Short description (≤ 300 characters)

> Dead on Arrival is a high-stakes race between daredevil ghosts to deliver the most grub and make the most cash! Up to four players speed through the undead city of Necropolis on souped-up scooters, PhaseBoost through walls and slam into rivals to steal their deliveries for themselves!

(285 characters. Shortened from the team's original description.)

## About this game

**Deliveries are to die for.** You're a ghost courier with a scooter, a haunted town full of hungry customers, and up to three rivals trying to take your tips.

**Grab, deliver, get paid.** Orders pop up all over town. Grab them, carry up to two at a time, and race them to the waiting customer for cash. Harder orders pay more.

**Phase-Boost.** Hit boost to wheelie forward and turn intangible — blast straight through buildings to cut corners, or boost into a rival to steal their package.

**Hot goods.** Every steal makes a package worth more, and the thief gets paid. A package that's changed hands burns with purple flame, so everyone can see what's worth chasing.

**Drive with style.** Drift to charge three tiers of spark boosts, tuck in behind a rival to catch their slipstream, and watch out for the water.

**The Golden Order.** After three escalating waves, one Golden Order appears in the final area. It gets more valuable the longer someone holds it — so everyone is coming for it.

**Rivals with personality.** Online, CPU couriers fill the empty seats, so you can also play solo against three of them. One loves to steal, one hunts the big packages, one just wants to deliver — and they all keep an eye on the scoreboard.

**Make it yours.** Pick your ghost's color and hat, send emotes mid-race, set the match length and rules, then watch the highlight reel and see who tops the podium.

**Couch or online.** Play split-screen on one screen, invite friends over Steam, or let matchmaking find a lobby. Bring a friend on the couch into an online match too.

## Features

- 1–4 player local split-screen chaos
- Online play over Steam: invite friends or find a match, with couch players joining together
- CPU rivals with their own personalities fill empty online seats, or play solo against three of them (online)
- Phase-Boost through buildings and into rivals to steal their deliveries
- Stolen packages grow in value with every steal
- Live scoreboard showing everyone's money and what they're carrying
- Drift boosts, slipstreams, pedestrians and props to knock around
- Three waves of rising order difficulty, then a Golden Order showdown
- Match rules: length, steals, Golden Order and bots
- An end-of-round highlight reel of the best moments
- Ghost colors, hats and emotes
- In 16 languages: English, French, German, Italian, Dutch, Spanish, Latin American Spanish, Portuguese, Brazilian Portuguese, Polish, Turkish, Czech, Romanian, Hungarian, Swedish and Danish
- Rematch with one button, and an award for every player at the results (Kleptoghost, Butterfingers, Fish Food...)
- Four rival delivery companies ⚠️ (names pending legal review)

## Content survey notes

- Cartoon ghosts; death is played for laughs (you respawn from a gravestone after falling in water).
- Mild slapstick: pedestrians and props get bumped or flung, then pop back — no blood or gore.
- No chat (text or voice), no purchases and no user-generated content. Online, other players' Steam names and profile pictures are shown (lobby board, scoreboard, and over their scooters).

## System requirements

⚠️ Estimates (2026-11-03), not measurements: nobody has logged a `Perf:` line on real hardware yet (`docs/performance.md`). Minimum aims at 1–2 players on Low at 1080p, 30 fps; Recommended at 4-player split-screen on High at 1080p, 60 fps. Replace them once measured. The Windows build is 1.2 GB; 3 GB leaves room for Steam's updates.

Windows, macOS (Universal) and Linux builds all build cleanly (2026-11-03, Tools → Dead on Arrival → Build), but only Windows has been played. ⚠️ Play the Mac build on a Mac and the Linux build on Linux or a Deck before ticking those platforms: the Mac build is made on Windows, so it isn't code-signed. The Steam Deck runs the Windows build through Proton without a Linux build (`docs/steam/steam-deck.md`).

**Windows**

| | Minimum | Recommended |
| --- | --- | --- |
| OS | Windows 10 64-bit | Windows 10/11 64-bit |
| Processor | Intel Core i5-4460 / AMD Ryzen 3 1200 | Intel Core i5-8400 / AMD Ryzen 5 2600 |
| Memory | 8 GB RAM | 16 GB RAM |
| Graphics | NVIDIA GeForce GTX 1050 Ti / AMD Radeon RX 570, 4 GB | NVIDIA GeForce GTX 1660 / AMD Radeon RX 5600 XT, 6 GB |
| DirectX | Version 11 | Version 11 |
| Network | Broadband (online play) | Broadband (online play) |
| Storage | 3 GB | 3 GB |
| Notes | Controllers recommended; one player can use the keyboard | |

**macOS** (only if a Mac build ships)

| | Minimum | Recommended |
| --- | --- | --- |
| OS | macOS 12 Monterey | macOS 14 Sonoma or later |
| Processor | Apple M1 | Apple M1 Pro / M2 or better |
| Memory | 8 GB | 16 GB |
| Graphics | Apple M1 GPU (Metal) | Apple M1 Pro / M2 GPU (Metal) |
| Network | Broadband (online play) | Broadband (online play) |
| Storage | 3 GB | 3 GB |
| Notes | Intel Macs not supported | |

**Linux + SteamOS** (only if a Linux build ships; the Steam Deck plays the Windows build through Proton either way)

| | Minimum | Recommended |
| --- | --- | --- |
| OS | Ubuntu 22.04 64-bit or SteamOS 3 | Ubuntu 24.04 64-bit or SteamOS 3 |
| Processor | Intel Core i5-4460 / AMD Ryzen 3 1200 | Intel Core i5-8400 / AMD Ryzen 5 2600 |
| Memory | 8 GB RAM | 16 GB RAM |
| Graphics | Vulkan GPU: GTX 1050 Ti / RX 570, 4 GB, or Steam Deck | Vulkan GPU: GTX 1660 / RX 5600 XT, 6 GB |
| Network | Broadband (online play) | Broadband (online play) |
| Storage | 3 GB | 3 GB |
| Notes | Steam Deck starts on Medium | |

## Store graphics (Steamworks → Store Page → Graphical Assets)

Capsules show the game's logo/art only: no review quotes, award badges or "Out now" text (Valve rejects them).

| Asset | Size (px) | Notes |
| --- | --- | --- |
| Header capsule | 920 × 430 | Top of the store page; most-seen image |
| Small capsule | 462 × 174 | Search and lists; logo must read at this size |
| Main capsule | 1232 × 706 | Front-page carousel |
| Vertical capsule | 748 × 896 | Seasonal sales pages |
| Page background | 1438 × 810 | Optional |
| Library capsule | 600 × 900 | Steam library grid |
| Library header | 920 × 430 | Library |
| Library hero | 3840 × 1240 | Library banner; no text |
| Library logo | 1280 × 720 | Transparent PNG, sits over the hero |
| Screenshots | 1920 × 1080 | At least 5; game footage only |
| Community icon | 184 × 184 | JPG |
| Client icon | 32 × 32 | .ico (Steamworks → Installation) |

⚠️ Before shooting: the scooter decals show the parody company logos, and the HUD uses the Sobiscuit and jcandlestick fonts. Settle both in `LICENSES.md` first, or pick shots that hide them.

## Screenshot / trailer shot list

1. Four-way split-screen mid-race.
2. A Phase-Boost straight through a building.
3. A steal: boosting into a rival and taking their package.
4. A purple-flame stolen package with the scoreboard visible.
5. Drift sparks at tier 3.
6. The Golden Order finale.
7. Character customization (colors and hats).
8. The results podium with player names.
9. The Dispatch lobby: friends' tickets filling up before an online match.

## Achievements (paste into Steamworks → Stats & Achievements)

The API names, display names and descriptions are in `docs/steam/in-game-features.md` (Achievements). Each needs two 256×256 JPG icons (unlocked and locked), so 18 icons in all.

## Rich Presence

Upload `docs/steam/rich-presence-english.vdf` as it is (checked 2026-10-20: its 8 tokens match `PresenceRules`).

# Game modes: choices made without asking (2026-11-04)

Everything here was decided during the build so work could keep moving. Each is easy to change; the file to change is given. Numbers live in `GoldRushRules.cs` and `TimedModeRules.cs` (Assets/Scripts/Management).

## Menu and mutators

- **Row order in MATCH RULES:** Mode, then the existing rules (Wave Length, Waves, Round Timer, Steals, Golden Order, Bots), then the new mutators (Speed, Low Gravity).
- **Speed choices:** 50 / 75 / 100 / 125 / 150 / 200 %. **Waves:** 1–6. **Round Timer:** 2 / 3 / 5 min (default 3).
- **Low Gravity** is half gravity (−13 → −6.5) for everything, cans and civilians included.
- **Speed** scales driving force and the steering's top-speed scale; bots follow it too.
- **More or fewer waves** spread the scene's three authored quota rows over the waves played (easy → hard). One wave plays the hardest row.
- **The "wasn't designed" note** shows for any mode other than Standard, or Speed / Low Gravity / Waves changed. Wave Length, Steals, Golden Order, Bots and Round Timer alone don't trigger it (they were already normal rules).
- **"LENGTH" was renamed "WAVE LENGTH"** and shows seconds (45S / 60S / 90S).
- **Public lobbies** always play Standard with no mutators, as before.

## Gold Rush

- **3× the order quotas**, spawn cooldown 1 s.
- **Drain:** removed 2026-11-05 for the Mother Lode (below).
- **Mother Lode (picked 2026-11-05, docs/gold-rush-brainstorm.md idea 1):** one at a time, 30 to 40 s after the last has gone. A free order from the Hard pool (then Medium, then Easy) turns into it, so it uses that order's pickup and drop-off. Pays a flat $250 that never doubles, because doubling $250 would decide a match on one steal; a thief still gets the usual pay. Its carrier keeps 80% of their engine and spills a $10 nugget every 12 m for anyone but themselves, gone after 20 s. Left untouched for 60 s it goes, so one that lands out of reach can't block the rest. It counts as a Hard delivery for achievements and awards. Three times the packages and rush hour stay.
- **Rush hour = last 30 s of the waves:** each steal doubles the order twice and pays the thief twice (14 instead of 7). A hint announces it on every machine.
- **Keeps the tutorial** (it's still a delivery mode).

## Kings Crown, Infection, Hot Potato (the "timed" modes)

- **The round is one "wave" as long as the Round Timer.** This reuses the existing clock, time-up, highlight reel and results flow. No golden round; no orders spawn.
- **The delivery tutorial is skipped** in these modes.
- **The hit that counts** is the same as a steal: a boosting player hits one who isn't boosting. A clash (both boosting) does nothing special.
- **Water** never changes who holds the crown or potato (it's tied to the player, not a package).
- **A player who leaves** drops the crown back on the road / the potato relights on someone else.
- **All their state syncs online** as one snapshot whenever anything changes; each machine counts the fuse down on its own clock.

### Kings Crown
- **Start:** the crown lies on the road at a random respawn point; first to drive within 5 m takes it.
- **10 points/s** while you hold it. Most points wins.
- **2 s of safety** after taking it (no instant steal-back).
- **The crown is built from primitives** with the golden order's material, spinning above the holder, with a warm light.

### Infection
- **First hunter is random.** Hunters catch thieves by boosting into them.
- **Thieves earn 5 points/s while free; a catch pays the hunter 50.** Highest score wins, so survivors naturally rank high.
- **Ends early** when no thief is left (needs at least 2 players).
- **Infected ghosts take the first hunter's ghost material** (body and eyelids), restored on the podium.
- Steals are hidden for Infection, as the design table said.

### Hot Potato
- **The cursed package** uses the Hard order's mesh and material with a red pulsing light, ticking (menu "scroll" sound) faster as the fuse burns; it goes off with the respawn "death" sound.
- **Fuse:** random 15–25 s, never shown as a number. First potato after 3 s; the next 3 s after each explosion, on a random player still in.
- **Passing:** only the holder can pass, by boosting into someone. **No tag-backs for 1.5 s.**
- **10 points/s for every player still in**, so the order you go out in decides the podium. Last one standing wins and the round ends early.
- **Out players** are hidden, frozen and lose their colliders; their own camera follows a player still in, and **Boost switches who they watch**. Everything is restored on the podium.

## Bots

- **Kings Crown:** drive to the crown on the road; otherwise chase the holder; the holder runs from the nearest rival.
- **Infection:** hunters chase the nearest thief; thieves run from the nearest hunter.
- **Hot Potato:** the holder chases the nearest player; others run from the holder (or the nearest player before the potato appears).
- "Running" is a point 35 m straight away from the threat; the bots' usual boost-into-target rule does the hits.

## Highlight reel

- **New moments:** Crown Taken (7, +6 in the last 10 s), Infected (5–9, heavier as it spreads), Last Caught (12), Potato Passed (2–9, heavier the closer the fuse was), Potato Exploded (9), Last Standing (12).
- **Gold Rush:** a steal in rush hour weighs 12 instead of 8.
- **New captions,** in the reel's CCTV style: CROWN SEIZED, INFECTION SPREADING, PACKAGE DETONATED, LAST SURVIVOR.
- Everything else (combo window, reel length, picking every player) is unchanged.

## Translations

- All new text was translated into the 13 languages by Claude (Sonnet) and needs the same native-speaker check as the rest.

## Playtest round (2026-11-04): choices made without asking

- **"Modifiers reset between games":** every rule except the mode and Bots goes back to normal when players return to the lobby or menus. A rematch from the results and Restart from the pause menu keep them (the user's call, 2026-11-05). A public lobby plays normal rules and leaves the host's picks alone. Modifiers saved when the game was quit midway don't come back at launch.
- **The mode stays picked** between games; only the modifiers reset.
- **Mode selector:** only the Mode row loops. The other rows still stop at their ends.
- **Hot Potato has no round timer now:** the row is hidden and the round plays until one player is left (10 minute safety limit). The HUD clock shows the fuse and rests at 0:00 between fuses.
- **Fuse length** stays random, 15 to 25 s, but is now shown.
- **Kings Crown's money column shows time held** (0:42), since its points are time. Infection and Hot Potato show nothing there. Ranking still uses the hidden points.
- **"Icon red on the map":** the game has no minimap (the prefab has a minimap camera, but nothing uses it), so infected players show as a red ghost on the compass, and their leaderboard picture and frame turn red.
- **Infected colour:** ashen white with red rim and halftone. The picks are purple, red, green, yellow, blue and orange, and this also reads without colour vision.
- **Crown holder's boost** recharges at the "carrying a package" rate.
- **Spectating:** an out player's camera uses the watched player's own camera targets. Boost (A by default) moves to the next player. Text says BOOST, not A, because the button can be rebound and keyboards differ.
- **Controls from the main menu:** a new CONTROLS page in Options, opening the same panel as the pause menu. Mouse can open it but, as in pause, the panel itself is pad and keyboard.
- **Ultrawide:** any canvas fits the screen's height when the screen is wider than 16:9. Screens at 16:9 or narrower are unchanged.
- **Lobby card:** "TONIGHT'S SHIFT", the mode, and a gold stamp for each changed modifier, top centre, on every machine.
- **Bots:** they slow for sharp corners, watch for the road's edge 0.8 s ahead, only boost with clear road ahead, roam towards a rival when idle, and give up on a target after being stuck twice. Thief steal appetite 3 to 2, chase 8 s to 5 s; thief and hunter boost as often as every 1.5 s.
- **Gold Rush:** unchanged. Ideas are in `docs/gold-rush-brainstorm.md`, waiting for a pick.

## Known gaps (playtest)

- Rush hour's double steal is shown on clients from their own clock: right at the 30 s mark a client may show one doubling off (display only; the host's score is right).
- Unfreezing an out player when a match ends early depends on the results flow re-freezing scooters for the podium; check the podium.

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
- **Drain:** 2 points/s after 5 s without a delivery; the match's start counts as a delivery; never below 0; no drain in the golden round.
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

## Known gaps (playtest)

- No scoreboard icon for the crown holder or who's infected/out; the 3D crown, colours and hiding carry it.
- The ghost portraits on the scoreboard don't change colour when infected.
- Rush hour's double steal is shown on clients from their own clock: right at the 30 s mark a client may show one doubling off (display only; the host's score is right).
- Unfreezing an out player when a match ends early depends on the results flow re-freezing scooters for the podium; check the podium.

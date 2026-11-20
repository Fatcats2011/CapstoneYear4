# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-10-26 (finished steps removed).

## 1. Local Play and Online Play on the title screen (5 minutes, main editor)

Done: the MULTIPLAYER and LOCAL entries are in the scene (Selector Objects: Multiplayer, Local, Options, Credits, Quit), and the code follows that order. Left to do:
- **Selector Ghost Sprites** (MainMenu) still holds 4, so from Local down each entry shows the next one's ghost. Add one at the top (element 0) so it holds 5, one per entry in the same order: Multiplayer, Local, Options, Credits, Quit. With no Multiplayer ghost art yet, `Play.png` for both Multiplayer and Local is fine.
- **Check, with Steam running:**
  - Local Play: player select; Y does nothing, and no line along the top.
  - Online Play: Dispatch (no invite window until Y). (Until Dispatch, 1c, is built) the top line says "Y / Triangle: invite friends      B / Circle: leave"; Y reopens the window; B goes back to the title screen.
- **Check, with Steam closed:** Online Play shows "Start Steam to play online." and goes back to the title screen.

## 1b. Dispatch: X opens the customize pop-up (done: check only)

- **Check, on Dispatch:**
  - X (or Q) pops up CUSTOMIZE: your ghost with the colour and hat sliders, the room dimmed behind.
  - The d-pad scrolls them, and your ticket follows.
  - B (or A) closes it.
  - Local player select still shows the sliders all the time, and X does nothing there.

## 1e. Button prompts follow the device (2 minutes, main editor)

- **Art is made:** `Assets/Sprites/UI/Buttons` (PlayStation ✕ ○ □ △ L1 R1, keyboard Space Backspace Q Tab F R), drawn by `tools/button-art.py`.
- **Dispatch:** Build Board In Menu Scene → Replace gives its prompts the switching art.
- **Everywhere else:** open each scene with button prompts (the menu scene, the game scenes), run **Tools → Dead on Arrival → Add Button Glyphs To Open Scenes**, and save. Every Image showing an Xbox A, B, X, Y, LB or RB gets its PlayStation and keyboard art.
- **Check:** press a key: the prompts show keys. Press a DualShock / DualSense button: PlayStation buttons. An Xbox pad: Xbox buttons.
- **With Steam Input on** (Steam's per-game controller setting), Steam hands the game a virtual Xbox pad for every controller, so a PlayStation pad shows Xbox buttons. Turn Steam Input off for the game to see PlayStation buttons.

## 1d. Matchmaking: check only

The Left/Right Shoulder actions, the status line and the Fill prompt are in.
- **Check, online player select (the host):** R opens the lobby to anyone and the line and the R prompt follow; R again closes it. F searches (`LOOKING FOR A MATCH…`) and B cancels. A client's R does nothing, and it shows no R prompt. Two machines seated in an open lobby show the countdown from 60.

## 1c. Dispatch lobby: materials, then check

The room scene and the board are set up (`DispatchRoom`, `RoomClock`, the board and the build entry are in; the menu's spare lobby model is gone).
- **Materials:** set the model's materials to the menu's toon shader, or extract them (Materials → Extract Materials). The cork, wood, wall and floor textures (`Assets/Textures/Dispatch`) are already linked.
- **Optional:** a short celebration animation on the ghost when a player clocks in.
- **Check:**
  - Online Play opens Dispatch, with no invite window; Y opens it.
  - A friend's ticket and ghost appear.
  - X opens the sliders, and the ticket follows them.
  - A stamps the ticket.
  - Everyone ready: the banner, then 3-2-1, then the match.
  - After the results you're back on Dispatch, unstamped.
  - A second pad can't join online.
  - Local Play's player select is unchanged.

## 2. Phases 5A and 3J: check (40 minutes)

- **Joining:** in player select, press keys other than Space and Enter: no player appears, and the hint says to press Space. A controller still joins on any button, the keyboard on Space or Enter.
- **Fullscreen:** with exclusive fullscreen on (`settings.cfg`), finish a match and return to the menu: no flicker.
- **Bumps, in two editors:** drive into the other editor's scooter without boosting. It's pushed away in that editor too (a round trip later). If the push feels too weak or too strong, change `BumpRules.MAX_SPEED` (a bump that fast pushes as hard as a clash).
- **Two PCs over Steam, optional:** a kicked player sees "Disconnected: too many messages." and can't reconnect until the host hosts again. Kicking needs a modified game, so skip this unless you have one.
- **Steam's Rich Presence file** (`docs/steam/rich-presence-english.vdf`) is plain text now: upload it again when you upload it at all.
- **Two players on one machine online (Phase 3J), in two editors** (`docs/online.md`, Two editors on one computer):
  - Host in editor 1. In editor 2, have two controllers in player select (F1 adds a test pad), then Join: both sit down (the second a moment later), each with its own view; editor 1 shows three scooters.
  - In editor 1, press A on a second controller in player select: it sits in the fourth seat. A fifth controller anywhere is told "That match is full.".
  - B on editor 2's second player: its scooter goes in both editors. B on editor 2's first player leaves the match, as before.
  - Ready everyone up and play a wave: four views across the two editors, steals and orders as usual.
- **Two PCs over Steam, optional:** two players on each PC, playing together.
- **Phase 5B, in two editors:** start a match just as a client's second controller joins player select: its player never flashes up in the match on either editor.

## 4. Measure the frame rate, then the editor changes (30–60 minutes)

- **A first number:** play a 4-player match (F1 three times in player select), press F6, drive for a minute, and note the FPS and 1% low at High, then at Medium (F4). In a build, the same numbers are in `Player.log` as `Perf:` lines every 30 s.
- **Then the editor list in `docs/performance.md`, one change at a time,** measuring after each:
  - Player N - Main Renderer: Deferred → Forward, Depth Priming off; URP Asset MSAA 4×. Check the outlines, phase effect and decals.
  - SSAO: Downsample, Low samples; remove it from the Phase renderers.
  - URP Asset shadows: 150 m, 2 cascades, 2048, extra-light shadows off.
  - HDR off if the bloom still looks right; keep one of Bloom and BenDayBloom.
  - Player Settings: IL2CPP; Graphics Jobs on.
  - Prefabs: animator culling; the compass on its own Canvas; static scenery and an occlusion rebake per scene.
- **For 120 FPS** you need a 120 Hz+ monitor, or `vsync=0` in `settings.cfg`.

## 5. Online safety checks (10 minutes)

- **Two PCs over Steam** (`docs/online.md`, Two PCs over Steam):
  - A friend joins through the invite as before, and the match plays normally.
  - Steam's overlay network view (Shift+Tab, then the connection info) shows the connection as relayed, not direct.
  - Optional: a third Steam account that isn't in the lobby can't connect.

## 7. Keyboard and display settings (15 minutes)

- **Keyboard (editor):** click into the Game view first, since keys reach the game only while it has focus.
  - On the title screen, press F1: a test pad joins, and the keyboard doesn't. Press X: the hint says "Press Space to play with the keyboard…".
  - Remove the test pad (exit and re-enter Play Mode). Press **Space**: the keyboard joins as P1. Join a real pad as P2.
  - Play one match: drive with the arrows (some letters are dev keys in the editor), Space to boost, Left Shift to drift, Escape to pause. Keys are in `docs/controls.md`.
- **The Resolution row (a build only, the editor ignores sizes):** Options → down to Resolution (under Fullscreen). Left and right change the size and the row says "A: apply"; A applies it. Try it windowed (Fullscreen Off) too. Leave Options after picking a size without A: the old size stays. If the row sits awkwardly on the screen, tell me where it should go.
- **Display (a build only, the editor ignores these):** open `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\settings.cfg` and add lines, launching each time:
  - `vsync=0` and `framecap=60`: the Steam overlay's FPS counter stays near 60.
  - `fullscreen=0`, `width=1600`, `height=900`: a 1600×900 window.
  - `fullscreen=1`, `exclusive=1`: exclusive fullscreen. Alt-Tab is slower than borderless.
  - Delete the lines afterwards to go back to the defaults.

## 8. From Phase 4A, optional: with Steam running (10 minutes)

- Steam running, one offline match in the editor. A friend's Steam friends list shows you in Spacewar, with the raw token (`#DeliveringSolo`) or nothing: the texts arrive with the `.vdf` upload (section 15).
- The first delivery logs `Steam: this app has no achievement FIRST_DELIVERY` once: normal on app 480.
- A build launched with `-sessionlog`: open `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\logs\`. There's a `session-….log` with a header, and the `Online:` lines of any online match.

## 9. From Phase 3I, if not done yet: disconnects in two editors (15 minutes)

- Play in both editors. Editor 1: **Tools → Dead on Arrival → Online → Host**. The clone: **Join This Computer** (`docs/online.md`, Two editors).
- Each editor's Console shows an `Online: the Game scene was ready here after …` line once the match is up.
- Mid-match, stop Play in editor 1: the clone goes back to the title screen and says "The host left the match.".
- Again, three more times, stopping Play in editor 1:
  - during the opening cutscene, with the clone paused;
  - on the loading screen, before the game appears;
  - on the loading screen, just before the game appears (the clone's load is done, and it waits for editor 1).
  - Each time the clone ends on the title screen with the message, and its match never starts.
- In the golden round, pick up the golden order in the clone, then stop Play in the clone: in editor 1 the golden order is back at its start, worth its starting value, and the round goes on. Pick it up in editor 1: its value climbs.
- Anything odd? Note it, plus each editor's Console lines starting `Online:`.

## 10. From Phase 3I, if not done yet: two PCs over Steam, time a load (10 minutes)

- On the slowest PC, start an online match in a build (`docs/online.md`, Two PCs over Steam).
- Open `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\Player.log` and find `Online: the Game scene was ready here after … s; its longest frame took … s`.
- Note both numbers, and whether the other PC stayed in the match.

## 11. From Phase 3H, if not done yet: one-shots, cutscenes and pause in two editors (20 minutes)

- Host in editor 1, join in the clone, and ready up in both. During the opening cutscene, press **L** in the clone: nothing happens. Press **L** in editor 1: both skip to the tutorial.
- Drive out of the tutorial into the first wave in both, then:
  - Boost in the clone beside editor 1's scooter: editor 1 hears it. Drive apart: it fades, and it's silent from about 60 m.
  - After a boost in the clone, wait for its horn to fill: editor 1 sees the clone's horns flash.
  - Steal from each other: the robbed editor hears the thief's whoosh.
  - Drive the clone's scooter into the water: editor 1 sees its wisp, then its gravestone, then it rises from it with a sparkle.
  - Drive the clone's scooter through pedestrians and cans in editor 1's view: they react. Tail it closely: editor 1's scooter gets the slipstream.
  - At each new wave, the bells ring in both. At the whistle, both scooters stop.
- Pause in the clone: editor 1 drives on, and the clone's hint says Main Menu leaves the match. Pause in editor 1: its hint says Main Menu ends it for everyone.
- Then again with **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)** ticked in the clone before **Join**.

## 12. From Phases 3F and 3G, if not done yet: the tutorial, orders, steals and respawns in two editors (20 minutes)

- Host in editor 1, join in the clone, and ready up in both. After the opening cutscene, each player is at the start of their own tutorial lane.
- In the clone: pick up the tutorial order, then boost into the cardboard cutout. Its barrier drops at once, and the order rides on the clone's scooter in both editors.
- Finish the tutorial in editor 1 only: the first wave waits. Drive out in the clone: the waves start in both.
- Deliver an order in the clone: the same score in both HUDs. The golden round's value climbs in both, and the results match.
- In the clone, pick up an order. In editor 1, boost into the clone's scooter: the order jumps to editor 1's scooter in both, and the clone's scooter spins out.
- Boost into each other at the same time: both bounce, and no order moves.
- In the golden round, steal the golden order: the robbed player isn't slower afterwards.
- Drive both scooters into the water at one spot: they rise on different respawn points.

## 13. Optional: tidy the game scene (2 minutes, main editor only)

- Not in Play Mode: open `Design Scene(Main)`, delete **Spawning Manager → Normal Positions** (the four city `Spawn 1-4` objects), and save the scene.
- Nothing refers to them any more: players start in their tutorial lanes.

## 14. From Phase 3D, if not done yet: Steam (20 minutes)

- One PC, Steam running: **Play**, A, **Play** → player select. **Y** hosts (the line along the top changes). **B** goes back offline.
- Two PCs, two Steam accounts that are friends: follow `docs/online.md`, Two PCs over Steam.

## 15. At Phase 4B, when the App ID exists: the Steamworks dashboard

- First run `python tools/set-app-id.py <APP_ID>` (it sets `SteamStartup.cs`, `steam_appid.txt` and `app_build.vdf`; these go on `main` too).
- Stats & Achievements: create all 9 achievements in the table in `docs/steam/in-game-features.md`, with the exact API names. Each needs two 256×256 JPG icons (unlocked and locked).
- Community → Rich Presence: upload `docs/steam/rich-presence-english.vdf`.
- Store page and tags: `docs/steam/store-page-draft.md`. Steam Deck: `docs/steam/steam-deck.md`.

## 15b. Steamworks "Your Game Build" checklist (checked 2026-11-17)

Done on the dashboard: Developer Comp and Beta Testing packages now hold the Mac and Linux depots (match the store package); "no redistributables needed" ticked (the build imports no VC++ runtime); Auto-Cloud syncs `settings.cfg` and `bindings.cfg`, and the Linux path is fixed to `unity3d/`.

Published 2026-11-17 (with the 16 Nov OS list and icons): every DEPOTS item is ticked. Left:

- **Files Saved to Steam Cloud (5 minutes):** launch the game from Steam, change one option, quit. The cloud icon on the game's library page should say it's synced.
- **Pricing (needs your decision):** store package 1915201 → Propose Pricing. Valve reviews it; "Published Pricing" ticks once approved.
- **Gameplay trailer on the store:** cut it from `../TrailerFootage` (`tools/trailer`), upload under Edit Store Page → Trailers.
- **Submit the build for review (3–5 business days):** the three ticks ("tested on a different PC", "review team can access all content", "all store-page features are in") are honest only after the playtests in 16–16m and a fresh build on `default`.

## 16. Highlight reel: playtest (30 minutes)

- Watch a full local round to time up: the reel plays, skips with everyone holding A, and the golden round (or results, if it's off) loads after.
- Online, two PCs: both see the same reel, and skip works.
- Check the reel on the Steam Deck and in 4-player split-screen with `PerfOverlay` (F6).
- Tune the scoring weights and the "good enough" bar (`HighlightRules`).
- Check the camera choices look good in the real town.
- After the playtest fix, check again:
  - each player shows as their own ghost on the scooter, in their colour and hat, not just a box;
  - the camera feels steady, with no shake or motion sickness. Only the fixed cameras are used now, and their aim eases after the action.
- Check that no HUD (the match timer, the time-up banner) draws over the reel, and that the colours look right (post-processing is off on the reel camera).

## 16b. Bot personalities: playtest (20 minutes)

- Host an online match alone, Bots on: three CPU drivers join.
- Pick out the three by how they drive:
  - the thief hounds whoever is carrying, the leader most of all;
  - the hunter drives far for Hard and Golden orders;
  - the courier takes nearby orders, boosts a lot, and swerves from boosting rivals.
- Pull ahead: the bots behind should get bolder.
- After the round, read the console: one `Bot seat N <Kind>: ...` line per bot. The steals and average values should differ clearly.
- Steals off in MATCH RULES: nobody chases.
- Tune in `Assets/Scripts/Player/BotPersonality.cs` (weights) and `BotTactics.cs` (desperation gap, detour distance).

## 16d. Mutators: playtest (20 minutes)

Plan: `docs/superpowers/plans/2026-11-04-modes-framework-and-mutators.md`. Start opens MATCH RULES in player select.

- New rows: WAVES (1–6), SPEED (50–200%), LOW GRAVITY. LENGTH is now called WAVE LENGTH (45s / 60s / 90s).
- Changing any of them shows the "wasn't designed to be played this way" note.
- Speed 50% and 200%, splitscreen: driving, drifting and boosting still feel controllable; bots keep up.
- Low Gravity: scooters, cans and civilians float more. Back to the menu, then a normal match: everything falls normally again.
- Waves 1 and 6: the clock starts at one and six wave lengths; the match ends into the golden round (or the results with Golden Order off).
- Online (two editors): host with Speed 150% + Low Gravity; the joining machine drives at the same speed and gravity.
- The MODE row appears once the first new mode is built.

## 16e. Gold Rush: playtest (15 minutes)

Numbers to tune: `Assets/Scripts/Management/GoldRushRules.cs`.

- MATCH RULES now has a MODE row: pick GOLD RUSH. STEALS shows ON (LOCKED).
- Many more packages appear, and faster.
- Every 30 to 40 s after the last one has gone: "THE MOTHER LODE IS OUT! FOLLOW THE LIGHT". A huge gold package under a pillar of light, on every compass, the pillar following whoever carries it.
- It slows its carrier, pays $250 to deliver, and doesn't double when stolen.
- The carrier leaves a trail of small gold nuggets, one every 12 m. Anyone else driving through one gets $10; they go after 20 s.
- Fall in the water or get robbed carrying it: it stays a Mother Lode.
- Last 30 s of the waves: "RUSH HOUR! STEALS COUNT DOUBLE" shows; a stolen package grows twice as much.
- The golden round plays as usual, with no Mother Lode.
- 4 players or bots: packages never run out; bots go for the Mother Lode.
- Online (two editors): the joining machine sees the extra packages, the Mother Lode, its pillar and the nuggets (taken once, paid once), and the rush hour hint.

## 16f. Kings Crown, Infection, Hot Potato: playtest (45 minutes)

Every choice made without asking is in `docs/game-modes-decisions.md`. Numbers: `Assets/Scripts/Management/TimedModeRules.cs`.

- All three: no tutorial, no orders, an info box says what to do and goes after 5 s, the highlight reel plays, then the podium (no golden round). Kings Crown and Infection run on the ROUND TIMER (2 / 3 / 5 min).
- **Kings Crown:** a gold crown under a tall beacon, on the compass; drive through it to take it; it floats over the holder; boost into the holder to take it (not within 2 s of them taking it). The holder's boost recharges slower, the board shows time held and a crown on their row.
- **Infection:** one random player starts infected: ashen white ghost, red name in bold, red picture on the board, red ghost on the compass. Boosting into someone infects them. No money on the board. Catching the last one ends the round at once.
- **Hot Potato:** a large purple package in pink flame appears over someone after 3 s, with its seconds counting down on it and on the HUD clock; boost into someone to pass it (they can't pass straight back). At zero that player vanishes, gets a red X on the board, and watches someone else (named on screen; Boost switches who). Falling in water doesn't put you out. Last one in ends the round; everyone is back on the podium.
- **Lobby:** the card at the top shows the mode and a stamp per changed modifier, on the joining machine too, updating as the host changes them. Modifiers go back to normal in the lobby and menus; a rematch keeps them; the mode stays. MATCH RULES' Mode row loops.
- **Options:** CONTROLS page works from the title screen, with pad and keyboard. Title screen and menus fit on an ultrawide monitor.
- **Bots** (online, Bots on): they chase the crown / thieves / the potato holder's targets and run away when they should.
- **Highlight reel:** the crown changing hands, catches and explosions show, with CROWN SEIZED / INFECTION SPREADING / PACKAGE DETONATED / LAST SURVIVOR.
- **Online (two editors):** the joining machine sees the crown, colours, potato, ticking and players out; an out player there spectates too.
- Check the podium after Hot Potato: nobody stuck invisible or frozen; then a Standard match plays normally.

## 16g. Record your matches, so the bots can learn from them (10 minutes of play, more is better)

- Playing in the editor records by itself. In a Steam build, add `-recordmatches` to the game's launch options (other players' builds never record, and nothing leaves your PC).
- Play at least 3 Standard matches on your own pad, as you normally would. The more of your own driving, the better.
- See how you compare with the bots:

  ```bash
  python tools/analyse_matches.py
  ```

- Then tell Claude they're there: `%USERPROFILE%\AppData\LocalLow\<company>\<game>\matches`.

## 16h. Steal feedback: playtest (20 minutes)

Play 4 players (bots are fine) in split screen, then a match in two editors online.

- **The line** (bottom middle of each player's own view, just over the horn charge bars):
  - Steal an order: you read `STOLE FROM <name> +$7`, in gold. Steal the same order again: no `+$7`.
  - Get robbed: you read `<name> TOOK YOUR ORDER`, in red.
  - Boost into a rival while holding two orders: `SLOTS FULL`. Into one holding nothing: `NOTHING TO STEAL`.
  - Turn the Steals rule off and bump someone: `STEALS ARE OFF`.
  - Can you read each line in a quarter-screen view? Does it stay clear of the compass, the scoreboard and the horn bars?
  - Nobody but the two players in the bump gets a line.
- **The stamp:**
  - A stolen package shows `×2` over it, then `×4`; you can read it on a rival's scooter from each view.
  - The scoreboard shows the same stamp on that package. The golden order and the Mother Lode never show one.
  - Tell Claude if it sits too high, too low or too big (one number each).
- **Feel:**
  - A steal shakes both pads and kicks both cameras, harder for the victim.
  - Rumble off: no shake. Reduce motion on: no kick.
- **Sounds:** the victim hears their own "robbed" sound (a low falling sweep), and a bump that takes nothing gives a dull thud. The steal keeps its whoosh.
  - Can you hear both over the music without them drowning it? Tell Claude "louder" or "quieter" for either (one number each). Both follow the SFX volume.
- **Online:** both machines show the same lines and stamps for the same bump.
- **Modes:** in Kings Crown, Infection and Hot Potato, a hit that moves the crown, tags or passes the package shows no steal line.

## 16i. Score gains: playtest (15 minutes)

Play 4 players (bots are fine) in split screen, then a match in two editors online.

- **Your line** (the same small line as 16h, in gold):
  - Deliver an order: `DELIVERED +$65` (or its price).
  - Deliver a stolen order: `DELIVERED ×2 +$130`.
  - Deliver the golden order: `GOLDEN ORDER +$...`, the whole amount it paid.
  - Gold Rush: deliver the Mother Lode: `MOTHER LODE +$250`. Nuggets give no line.
  - Kings Crown and Infection: no line each second.
- **The scoreboard:**
  - A small `+$65` shows beside that player's money for about a second, and the number counts up.
  - Nuggets picked up quickly read as one growing `+$30`.
  - The `+$` stays inside the row and can be read. Tell Claude if it is too small or badly placed.
  - A new match starts with no `+$` and no lead call.
- **Lead change:**
  - Pass the leader: you read `YOU TOOK THE LEAD`; they read `<name> TOOK THE LEAD`, in red. Nobody else gets a line.
  - The first money of a match calls nothing. A tie calls nothing.
  - Deliver and take the lead at once: the `DELIVERED` line shows first, then the lead line.
- **Online:** both machines show the same lines and the same `+$`.

## 16j. Mode goals and event calls: playtest (30 minutes)

Play Kings Crown, Infection and Hot Potato with 4 players (bots are fine) in split screen, then one of them in two editors online.

- **The goal tag** (two or three small words under the compass, in your own view):
  - It comes up once the mode's info box has gone, and goes after 25 seconds.
  - Kings Crown: `GET THE CROWN`, then `KEEP THE CROWN` when you hold it. Each change brings it back for 25 seconds.
  - Infection: `DON'T GET CAUGHT`, then `CATCH THEM` once you are infected.
  - Hot Potato: `STAY AWAY`, then `PASS IT!` when the package is yours. Out: no tag.
  - Standard and Gold Rush: no tag.
  - Can you read it in a quarter-screen view? Is it clear of the compass and the info box? Tell Claude "lower", "smaller" and so on.
- **Your line** (the same small line as 16h):
  - Kings Crown: `YOU HAVE THE CROWN`; lose it and read `<name> TOOK YOUR CROWN`.
  - Infection: `YOU'RE INFECTED` when caught (and for the first hunter); the only hunter reads `CAUGHT <name>`; the last free player reads `YOU'RE THE LAST ONE FREE`.
  - Hot Potato: `YOU HAVE THE CURSED PACKAGE` with its sound; the passer reads `PASSED TO <name>`; everyone else reads `<name> IS OUT`.
- **Round ending:**
  - Kings Crown and Infection: at 30 seconds everyone reads `30 SECONDS LEFT`, hears the warning once, and the timer digits pulse.
  - The last-10-seconds countdown reads `Time's Up In...` in Kings Crown and Infection, and still `Golden Order In...` in Standard.
- **Sounds:** the cursed-package and 30-second sounds can be heard over the music without drowning it. Tell Claude "louder" or "quieter" for either.
- **Online:** both machines show the same tags and lines for the same event.

## 16k. Orders and where to go: playtest (20 minutes)

Play Standard with 4 players (bots are fine) in split screen, then in two editors online.

- **Rival warning** (a small red chevron at the edge of your own view, only while you carry an order):
  - A rival within about 30 m who could steal: a dim chevron on their side (left, right, or bottom middle for behind). A rival you can see in front: no mark.
  - A rival boosting at you: the chevron pulses bright and your pad shakes once.
  - A rival holding two orders, or steals switched off in the rules: no mark.
  - Is the side right? Does it show too often in a crowd, or too late to react? Tell Claude "further", "nearer", "smaller" and so on.
  - Reduce motion on: the chevron doesn't pulse.
- **Beacon prices:**
  - Every pickup beacon shows a small gold price under its floating icon: `$55`, `$65`, `$80`. The golden order shows none; the Mother Lode (Gold Rush) shows `$250`.
  - Steal an order and drop it: its price has doubled.
  - Can you read it in a quarter-screen view, near and far?
- **Drop-off marks:** carry two orders: each drop-off's compass mark shows its own package icon and price.
- **Online:** both machines show the same prices; each player's warning is their own.

## 16l. Results breakdown and first-time hints: playtest (20 minutes)

- **Results cards** (play a full match with 4 players, bots are fine; then one online in two editors):
  - Under each score: small lines such as `Deliveries $520`, `Steals $21`, two to a row. Only sources that paid show.
  - Add them up: they equal the score on the card.
  - Steal an order and deliver it: Steals gains the extra it paid, not only the $7.
  - Kings Crown or Gold Rush: a `Mode` line appears. A golden round: a `Golden` line.
  - The winner's card with all four lines: is it clear of the ghosts and the title? Tell Claude if it overlaps.
  - Online: the client's cards show the same amounts as the host's.
  - Can you read the lines at 16 px on your screen and on the Steam Deck?
- **Hints** (delete the `hints=` line in `settings.cfg`, or set it to 0, to see them again):
  - Bump a rival without boosting: `BOOST INTO RIVALS TO STEAL`, once.
  - Get robbed: `BOOST TO BLOCK A THIEF`, after the robbed line, once.
  - Steal an order: `STOLEN ORDERS PAY DOUBLE`, after the steal line, once.
  - Pick up the golden order: `HOLD GOLD LONGER, EARN MORE`, once.
  - Each stays about 4 seconds, never shows in the tutorial, and never comes back after a restart.
  - Split screen: only the player it happened to sees it. Online: as a client, the bump hint still shows.
  - Change the language: the longest hints still fit the line.

## 16m. Boost pads and respawn line: playtest (10 minutes)

- **Boost pads:**
  - Cross one: the speed lines flash on for half a second, your pad shakes lightly once, and you hear a short rising sweep.
  - Cross a row of pads, or sit on one: still one flash, one shake, one sound.
  - Faster crossings sound slightly higher.
  - Is the sound right for a boost pad? It is an interface-style sweep, not a whoosh. Too loud or too often? Tell Claude "quieter", "louder", or "find a whoosh" (that needs your OK for a new CC0 download).
  - Reduce motion on: no speed lines. Rumble off: no shake.
  - Split screen: only the player on the pad hears and feels it. Bots make no sound.
- **Respawn:**
  - Drive into the water: your line reads `FELL IN THE RIVER`, and it is gone by the time you drive again.
  - No countdown shows: the respawn takes about 2 seconds, too short to count.
  - Online as a client: the same line.

## 16n. Split-screen scoreboard: playtest (10 minutes)

- **1 player** (bots on, or online alone): the board is middle-left with names, as before.
- **2 players, Boxed:** the board sits in the empty bar left of the two views, with names, and covers neither view.
- **2 players, Side-by-side:** a compact column (picture, money, packages, no name) sits on the line between the views. It takes the same slice of both and doesn't block the road.
- **2 players, Stacked:** a compact strip, ranked left to right, sits on the line between top and bottom.
- **3 players:** the board fills the empty bottom-left corner, with names.
- **4 players:** a 2 by 2 compact grid sits where the four views meet, first place top-left. Each view gets one cell.
- In the centre layouts check nothing collides with the steal callout, the mode info box or reconnect prompts.
- **Stacked:** the strip splits round a gap in the middle. The bottom player's compass stays fully visible.
- **Text size 150%:**
  - Boxed and 3 players: the board stays inside the black space and fully on screen.
  - Centre boards (Side-by-side, Stacked, 4 players) don't grow.
  - Side-by-side: the column stays clear of the mode info box.
  - 4 players in Hot Potato: the grid stays clear of the spectating label.
- **A pad joining mid-match:** the board changes layout at once, with no stray or doubled rows. Unplug it and the board changes back.
- **Options > Display > Split-screen mid-match** (with 2 players): the board follows each choice at once.
- Money, +$, packages and crowns still update in every layout.

## 16o. Boost ring: setup and playtest (15 minutes)

- **Setup:** run **DoA → Setup → Boost Ring** once. It adds the ring and prompt to `PlayerAvatar.prefab`. Running it twice is safe.
- **Couch, 1 to 4 players:**
  - Each scooter has a ring on the ground that fills as boost recharges, in the horn colours.
  - When full it glows, pulses and flashes with the horns. The boost button shows in front of it.
  - Each player sees only their own button. Everyone sees every ring.
  - The button matches the pad: A on Xbox, Cross on PlayStation, Space on keyboard.
  - Boost: the ring bursts outward, then fills from empty.
  - Size, height and button position look right? They are tunable on the Boost Ring object.
- **Carrying a package:** the ring fills dashed and slower. Full, it turns solid.
- **Leave boost full for 15 seconds:** the boost hint shows once (not in the tutorial).
- **With bots (host online, Bots on):** CPU drivers have no ring. Yours works as normal.
  - Solo with bots plays like local: your button shows on every full ring, and the match doesn't count toward the 3 online ones.
- **Online, host and guest:** your own ring and button work. Other players' scooters have no ring.
  - First 3 online matches: the button shows as soon as the ring is full. After that, only after about 3 seconds full.
- **Tutorial:** the ring works, no hint.
- **On a slope:** the ring tilts with the ground and doesn't sink in or flicker.
- **Jumping:** the ring fades out in the air and back on landing.
- **Phasing:** the ring still looks right.
- **Lobby:** the ring shows under scooters there too. Keep it, or hide it outside matches?
- **Mac and Linux builds:** the ring draws.
- **Fresh player:** one match, no explanation. Then ask "what's the ring?"

## 16p. Ghost emotes: playtest (20 minutes)

Nothing to set up: the emotes are added when players join. Play a couch match with bots, then an online one.

- **In a match, each fires and reads from the game camera:**
  - Steal: the thief cackles, the victim shakes a fist.
  - Delivery: a flourish. The golden order too.
  - Boost: a hold-your-hat. Only about one boost in a few (20 s apart).
  - Airtime: "Wheee" after a real jump off a ramp or bump. Not on a drop into the river.
  - Hard bump: stars around the head.
  - Golden order: a treasure hold on pickup, then every 15 s.
  - A rival closing in behind while you carry: a jump scare. Further behind: a side eye.
  - Not carrying, closing on a carrier: a scheming look.
  - Standing still for 12 s: bored.
- **Not spammy:** the same emote never twice running, and a big one (steal, bump) cuts a small one. Does a ghost ever feel like it is always doing something?
- **Lobby:** a new player materialises; readying up warms up or cracks knuckles; a new hat or colour shows off; ready while someone isn't, after 5 s, impatient.
- **Countdown:** every ghost revs at 3, 2, 1.
- **Results:** the winner (victory or crown polish) and the last place (tantrum or fade away) emote every few seconds. The podium acts wait for them.
- **Props:** the parcel is in the ghost's hands during the cackle, treasure hold and side eye, then back on the rack. The winner holds a crown. Does anything flicker or stay behind?
- **Online:** other players' ghosts emote too (steal, delivery, boost, lobby, results). A hard hit shows stars on the victim's ghost on every machine, yours too.
- **Decided (2026-11-20):** the d-pad emotes stay off in matches. They work in code but nothing is bound to them, by choice.
- **Known:** Bored is mostly hidden behind the theft replay.

## 16c. Accessibility: to build, then playtest

For Steam's store-page accessibility section. None of these exist yet. Today Options → ACCESSIBILITY has only Reduce motion.

- **Colorblind mode:**
  - Players are told apart only by ghost colour, and a stolen package only by its purple flame.
  - Add a player number or symbol over each ghost (and on the scoreboard), and give the stolen-package flame a shape too.
  - Playtest: with a colour-blindness filter (protanopia, deuteranopia, tritanopia), check you can still tell all four players apart and spot a stolen package.
- **Turn off rumble:**
  - Add a RUMBLE on/off row next to Reduce motion, saved in `settings.cfg`. `Rumbler` should do nothing while it's off.
  - Playtest: off means no pad shakes on boosts, hits or water; check each player's pad.
- **Adjustable text size:**
  - Add a TEXT SIZE row (for example 100% / 125% / 150%) for the HUD, scoreboard, hints and menus.
  - Playtest: at the largest size, in 4-way split and on the Steam Deck (1280×800), check nothing overlaps or gets cut off.

## 17. Reference

- Frame rate: `docs/performance.md`.
- Online safety: `docs/online-safety.md`.
- How the keyboard and display settings work: `docs/controls.md`. Steam features: `docs/steam/in-game-features.md`.
- What's left before launch: `docs/launch-checklist.md`.
- What's next: `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`.
- Bots path on the **Scooter** NavMesh (agent type Scooter; a NavMesh Surface for it in `Design Scene(Main)` and `FinalAreaScene`). After moving roads, walls or buildings, select that surface and **Bake** again, then save the scene. Its NavMesh Modifiers keep rocks, walls, buildings and the pedestrians' road blockers out; kickables are left out of the bake.

## Localization: Set Up (2 minutes, main editor)

The strings and translations are in `Assets/Resources/Localization/strings.csv`; the game reads it directly until Unity Localization's tables exist. To make them (Unity's Localization tools and the CSV export then work):
- Open the project so Unity downloads the Localization package (`Packages/manifest.json` has `com.unity.localization` 1.4.5; it brings Addressables). Commit `Packages/packages-lock.json` with it.
- **Tools → Dead on Arrival → Localization → Set Up.** It makes `Assets/Localization` (the settings, made active; the 16 locales; the `Game` string table) and fills it from the CSV. Save, and commit `Assets/Localization` and `ProjectSettings` (it registers the settings there).
- After editing the CSV (`python tools/strings.py`): **Tools → Dead on Arrival → Localization → Reimport Strings.**
- **Look by eye, in French and Czech say:** the title screen's words and flag, the Options rows and the LANGUAGE row's flag, the pause menu, and a Czech word with accents (ČEŠTINA on the LANGUAGE row) to see the drawn marks sit well on their letters.
- **Before release:** a native speaker checks each language's column (add the code to `reviewed`).

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

## 17. Reference

- Frame rate: `docs/performance.md`.
- Online safety: `docs/online-safety.md`.
- How the keyboard and display settings work: `docs/controls.md`. Steam features: `docs/steam/in-game-features.md`.
- What's next: `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`.
- Bots path on the **Scooter** NavMesh (agent type Scooter; a NavMesh Surface for it in `Design Scene(Main)` and `FinalAreaScene`). After moving roads, walls or buildings, select that surface and **Bake** again, then save the scene. Its NavMesh Modifiers keep rocks, walls, buildings and the pedestrians' road blockers out; kickables are left out of the bake.

## Localization: Set Up (2 minutes, main editor)

The strings and translations are in `Assets/Resources/Localization/strings.csv`; the game reads it directly until Unity Localization's tables exist. To make them (Unity's Localization tools and the CSV export then work):
- Open the project so Unity downloads the Localization package (`Packages/manifest.json` has `com.unity.localization` 1.4.5; it brings Addressables). Commit `Packages/packages-lock.json` with it.
- **Tools → Dead on Arrival → Localization → Set Up.** It makes `Assets/Localization` (the settings, made active; the 16 locales; the `Game` string table) and fills it from the CSV. Save, and commit `Assets/Localization` and `ProjectSettings` (it registers the settings there).
- After editing the CSV (`python tools/strings.py`): **Tools → Dead on Arrival → Localization → Reimport Strings.**
- **Look by eye, in French and Czech say:** the title screen's words and flag, the Options rows and the LANGUAGE row's flag, the pause menu, and a Czech word with accents (ČEŠTINA on the LANGUAGE row) to see the drawn marks sit well on their letters.
- **Before release:** a native speaker checks each language's column (add the code to `reviewed`).

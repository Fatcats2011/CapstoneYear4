# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-10-13 (the Dispatch lobby; Local Play and Online Play on the title screen; Phase 5A: the deferred fixes and bumps online; Phase 3J: several players on one machine online).

## 1. Local Play and Online Play on the title screen (5 minutes, main editor)

Done: the MULTIPLAYER and LOCAL entries are in the scene (Selector Objects: Multiplayer, Local, Options, Credits, Quit), and the code follows that order. Left to do:
- **Selector Ghost Sprites** (MainMenu) still holds 4, so from Local down each entry shows the next one's ghost. Add one at the top (element 0) so it holds 5, one per entry in the same order: Multiplayer, Local, Options, Credits, Quit. With no Multiplayer ghost art yet, `Play.png` for both Multiplayer and Local is fine.
- **Check, with Steam running:**
  - Local Play: player select; Y does nothing, and no line along the top.
  - Online Play: player select, and Steam's invite window opens. (Until Dispatch, 1c, is built) the top line says "Y / Triangle: invite friends      B / Circle: leave"; Y reopens the window; B goes back to the title screen.
- **Check, with Steam closed:** Online Play shows "Start Steam to play online." and goes back to the title screen.

## 1b. Dispatch: X opens the customization sliders (2 minutes, main editor)

- **Input asset** (`Assets/Resources/CapstoneYear4.inputactions`): in the UI map add a Button action **West Face** (like North Face), bound to `<Gamepad>/buttonWest` (group Gamepad), gamepad binding only; the keyboard key (Q) comes from `KeyboardControls`. The code wires it by name (`UI/West Face`), so nothing else is needed. Until it exists, X does nothing in online player select, so the sliders can't be opened there.
- **Check, online player select:** the sliders are hidden; X shows them (d-pad scrolls them); B or A hides them; local player select still shows them all the time, and X does nothing there.

## 1d. Matchmaking buttons and status line (5 minutes, main editor)

Code is done (`docs/online.md`, Matchmaking). Neither shoulder nor F/R was used in the UI map, so these are free:
- **Input asset** (`Assets/Resources/CapstoneYear4.inputactions`): in the UI map add two Button actions, like North Face:
  - **Left Shoulder**, bound to `<Gamepad>/leftShoulder` (group Gamepad); the keyboard key (F) comes from `KeyboardControls`.
  - **Right Shoulder**, bound to `<Gamepad>/rightShoulder` (group Gamepad); the keyboard key (R) comes from `KeyboardControls`.
  - The code wires them by name (`UI/Left Shoulder`, `UI/Right Shoulder`). Until they exist, the buttons do nothing.
- **The Dispatch board:** add a text for `matchmakingLine` (below the ready counter, say) and wire it. It shows `LOOKING FOR A MATCH…`, `OPEN TO ANYONE`, `OPEN TO ANYONE · STARTS IN 42` or `FRIENDS ONLY`. Optional: with none wired, the line is just not shown.
- **Two prompts** with the shoulder button art, in the bottom prompts: `FIND MATCH` (left shoulder) and the right shoulder's Fill prompt. For the Fill prompt, add a text and wire it to the board's `fillPrompt`, with its right-shoulder art as a child of that text: the code sets it to what pressing does (`OPEN TO ANYONE` or `FRIENDS ONLY`) and hides it (art included) on a client. Nothing to swap by hand.
- **Check, online player select (the host):** R opens the lobby to anyone and the line and the R prompt follow; R again closes it. F searches (`LOOKING FOR A MATCH…`) and B cancels. A client's R does nothing, and it shows no R prompt. Two machines seated in an open lobby show the countdown from 60.

## 1c. Dispatch lobby: online player select (2-3 hours, main editor, mostly art)

Code is done (`docs/online.md`, Phase 3B). Needs step 1b first (the West Face action), then, in the menu scene:
- **The depot back room, behind the podiums:**
  - A wall and a floor.
  - A full-width corkboard above the podiums, lit at about 75% of the podium spotlights.
  - A spotlight on each of the 4 `menuSpawnPositions` podiums, each rim glowing in that seat's company colour.
- **A Screen Space canvas with a `DispatchBoard`:**
  - `root`: the object the board shows and hides. Put the `DispatchBoard` component on a different object (the canvas, say). If it sits on `root`, hiding `root` stops its `Update` and the board never comes back.
  - `tickets`: 4 `TicketView`s in seat order, laid out over the board, one above each podium. Wire each one's fields (header, company, playerName, colourName, hatName, colourSwatch, hatIcon, filled, hiring, stamp, gettingReady, mineMarker).
  - The `stamp`, `gettingReady` and `hiring` objects each need a TMP_Text child holding their copy: `DISPATCHED`, `GETTING READY…`, `HIRING…`.
  - `highlight` (an Animator) is optional: with none, a flash does nothing.
  - `readyLine`: top right, shows `{ready} / {seated} READY`.
  - `banner`: `ALL ORDERS DISPATCHED`.
  - A "DISPATCH" wordmark top left (new art, in the style of `Local.png` and `Multiplayer.png`).
  - Bottom prompts with the green button art: A CLOCK IN · X CUSTOMIZE · Y INVITE · B LEAVE.
- **Ticket art:** the paper, a pin, a company header strip, the DISPATCHED stamp and the HIRING… blank. A `Flash` animator trigger on `highlight` (brighten, then fade back over about 0.6 s).
- **`displayName`:** fill it in on the 6 colour assets and 7 hat assets (`PlayerColorInformationSO`, `PlayerHatInformationSO`).
- **The camera:** with one local player online, the menu camera frames the board and all 4 podiums.
- **Optional:** a short celebration animation on the ghost when a player clocks in.
- **Check:**
  - Online Play opens Dispatch, with Steam's invite window.
  - A friend's ticket and ghost appear.
  - X opens the sliders, and the ticket follows them.
  - A stamps the ticket.
  - Everyone ready: the banner, then 3-2-1, then the match.
  - After the results you're back on Dispatch, unstamped.
  - A second pad can't join online.
  - Local Play's player select is unchanged.

## 2. Phases 5A and 3J: import, then check (40 minutes)

- **Import:** click into the main editor, then the ParrelSync clone. The patched Steam transport changed again (`Packages/.../CHANGES-DOA.md` items 6 and 7). No scene or prefab changes. The Console should end with no red errors.
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

## 3. Let both editors import Phases 4C and 4D (2 minutes)

- Click into the **main editor**, then into the **ParrelSync clone**. The Package Manager re-resolves once: the Steam transport is now a copy inside the project (`Packages/com.community.netcode.transport.steamnetworkingsockets`, patched; see `docs/online-safety.md`). No scene or prefab changes.
- The Console should end with no red errors.

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

## 5. Online safety checks (15 minutes)

- **Unity Hub:** confirm the editor is 2022.3.62f3 or later (Unity's October 2025 runtime advisory, CVE-2025-59489, is fixed from 62f2). Rebuild any build made before.
- **Two PCs over Steam** (`docs/online.md`, Two PCs over Steam):
  - A friend joins through the invite as before, and the match plays normally.
  - Steam's overlay network view (Shift+Tab, then the connection info) shows the connection as relayed, not direct.
  - Optional: a third Steam account that isn't in the lobby can't connect.

## 6. Let both editors import Phase 1C (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New scripts: `DisplayRules`, `DisplaySettings`, `KeyboardControls`.
  - Changed: `GameSettings`, `OptionsMenu`, `PlayerInstantiate`, `ControllerPrompts`.
  - New tests: `DisplayRulesTests`, `KeyboardControlsTests`, `KeyboardPlayerTests`.
- The Console should end with no red errors.

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

## 16. When there's art

- Options rows for the display settings (screen size, borderless or exclusive, VSync, frame cap), and for Quality. The code reads and saves them already: wire each row's selector like the existing rows (`OptionsMenu`).
- Keyboard button prompts (Space, Backspace, Escape), and PlayStation/Nintendo button prompts. The code that swaps them is ready (`ButtonGlyph`):
  - Add a **Button Glyph** component to each prompt Image. Today `MenUI_Buttons.png` is used 5 times: 3 in `Alex Player Testing` (one may be a prefab override), and 1 each in `Menu Canvas.prefab` and `Loading Screen World Canvas.prefab`. New Dispatch prompts get one too.
  - Fill in **PlayStation**, **Nintendo** and **Keyboard** with the new art. Leave **Xbox** empty: it takes the Image's current sprite.
  - Empty slots fall back to Xbox, so adding the component before the art exists changes nothing.
  - Inside a player's hierarchy, a prompt follows that player's controller. Elsewhere it follows whichever device was used last.
  - With Steam Input on, Steam turns every pad into an Xbox pad, so the prompts stay Xbox (fine on the Steam Deck).

## 17. Reference

- Committed on `steam-phase1a` up to Phase 1C (`8ce31e85`). Not committed: Phase 4C (online safety) and Phase 4D (frame rate). Suggested titles: "Phase 4C: online safety", "Phase 4D: frame rate" (or one commit for both).
- Frame rate: `docs/performance.md`.
- Online safety: `docs/online-safety.md`.
- How the keyboard and display settings work: `docs/controls.md`. Steam features: `docs/steam/in-game-features.md`.
- What's next: `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`.

# Dispatch: the online lobby screen: design

**Date:** 2026-10-13  
**Branch:** `steam-phase1a`  
**Status:** approved in chat, waiting for the user to review this written spec  
**Mockups:** `.superpowers/brainstorm/631-1791924428/content/w3-brighter.html` (the chosen look). `lobby-concepts.html`, `dispatch-3d.html` and `wall-subtle.html` hold the options that weren't picked.

## Why

Online play works, but its lobby is just player select plus a thin black hint bar. The user wants it to feel like Mario Kart's online room: **you see everyone's character, their colour and hat, and their ready state, changing live as they change them.** It should look like the rest of the game: purple and slime green, chunky slanted white lettering, the green controller-button art from the loading screen.

## Goals

- A separate lobby screen, **Dispatch**. Picking Online Play on the title screen lands you there, not in player select.
- It keeps the existing 3D ghosts on scooters standing on podiums, the same models and look as player select.
- A delivery-ticket board shows each seat's company, name, ghost colour, hat and ready state, and updates live.
- Players customize, ready up, invite, start and come back after the match, all from this screen.

## Non-goals

- Couch players online. **One player per machine** on this screen for now. A second controller on the same PC can't take a seat. Phase 3J's several-seats-per-machine support stays in the code, but this screen doesn't use it.
- Public lobbies, a lobby browser or matchmaking. It stays friends only.
- Changing Local Play's player select.
- New 3D models or hats.

## Anti-goals (a failure even if it works)

- **The board taking over.** The user specifically turned down a bright, prominent board. The 3D models are the star, and the board is lit but secondary.
- Not looking like the game. No generic UI kit look and no flat grey panels.
- Changes that don't show up for everyone at once. A colour, hat or ready change has to reach every machine quickly enough that it feels live.

## The screen (decided with the user)

Reference: W3 in `w3-brighter.html`.

- **The room:**
  - A depot back room in 3D, lit dimmer than the podiums but clearly visible. The mockup uses about 75% brightness, raised from 50% when the user asked for it brighter.
  - A full-width corkboard on the back wall, above the podiums.
- **Podiums:**
  - Four of them in a row in front of the board, spotlit, each with that seat's 3D ghost on a scooter. Same models and dressing as player select (`ScooterLook`, colour and hat).
  - The podium rim glows in the seat's company colour.
  - An empty seat is an empty podium with a faint, slow spotlight.
- **Tickets:**
  - Four delivery tickets are pinned to the board, one above each podium.
  - Each shows:
    - the company (header strip in the company colour)
    - the player's Steam name
    - the ghost colour (swatch and name)
    - the hat name
  - This machine's own ticket is marked (★ in the mockup).
  - **A ticket lights up briefly whenever it changes**, for example a new colour, a new hat, a stamp or a new player. Then it fades back to the board's dim light.
- **Ready ("Clock In"):** the ticket is stamped **DISPATCHED**. Not ready yet shows "Getting ready…".
- **Empty seat:** a blank "Hiring…" ticket with the Y (invite) prompt.
- **Top of the screen:**
  - a "DISPATCH" wordmark top-left, in the same hand-drawn style as the Local and Multiplayer wordmarks;
  - an "X / Y READY" counter top-right.
- **Bottom prompts:** A Clock In · X Customize · Y Invite · B Leave, using the green button art.

## Behaviour (decided with the user)

- **Entering:**
  - Online Play on the title screen hosts a friends-only lobby automatically and opens Steam's invite dialog. `OnlinePlay.StartOnline` already does this today.
  - Then the game shows the Dispatch screen instead of player select. The host's ticket is already pinned.
- **Joining:**
  - Friends accept the invite or use "Join Game", the same as today, and take the next free seat.
  - Their ticket pins up and their ghost appears on the podium.
- **Customize:**
  - X opens the existing colour and hat sliders (`CustomizationSelector` / `CustomizationSlider`) **on this screen**.
  - This machine's ghost and ticket update while the player scrolls, and so does everyone else's view of them. That already syncs through `OnlinePlayer`'s NetworkVariables.
- **After the match:** results, then everyone goes back to the Dispatch board together with tickets unstamped (not ready).
  - The session stays up across the return, as it does today: `docs/online.md` already has the online menu reopening on the lobby with everyone ready to go again.
  - So each player keeps their seat, company, colour and hat. Only ready is reset.

## Behaviour (my calls, marked so the user can check them)

- **Start:**
  - When every seated player has clocked in, an "ALL ORDERS DISPATCHED" banner hits the board, then a 3-2-1 countdown runs, like local play's.
  - The host starts the match when the countdown ends, through the existing scene flow (`OnlineSceneFlow.LoadGameScene`).
  - Anyone un-readying, or a new player joining, cancels the countdown.
  - The 3-second countdown already runs online (`PlayerInstantiate.CheckReadyUpCount`). What's new is the banner. (Corrected while planning; the first draft said online had no countdown.)
- **Starting alone:** a host by themselves can clock in and start. That matches today's `REQUIRE_MAX_PLAYERS = false`.
- **B:**
  - A guest leaves the lobby, their seat goes back to "Hiring…", and they return to the title screen.
  - The host leaving closes the lobby for everyone, the same as today.
  - While the customize sliders are open, B closes them first.
- **Ghost reaction:** when a player clocks in, their 3D ghost does a short celebration, like a scooter rev or a hat tip, if an existing animation fits. Otherwise skip it. This is nice to have.
- **No joining mid-match:** unchanged (`JoinRules`: the host must be in the menus).
- **Hosting fails** (no Steam, lobby fails): unchanged. It goes back to the title screen with the reason, as `OnlinePlay.BackToTitle` does now.
- **The old hint bar (`LobbyPrompt`)** isn't shown on Dispatch. The bottom prompts replace it.
- **Seats:** the existing seat rules (`SeatTable`) stay as they are.
  - A guest who leaves frees their seat, which shows as a gap ("Hiring…") until someone takes it.
  - A new player takes the lowest free seat, and a seat's company is fixed.
  - Seats aren't reshuffled to close gaps.
- **Who counts toward "everyone ready":** players whose `OnlinePlayer` has spawned.
  - A player who disconnects drops out of the count. That doesn't cancel the countdown by itself; only a new spawn or an un-ready does.
  - The host dropping ends the session, countdown or not, the same as today.
- **Names:** the Steam persona name. Without Steam (editor and direct test sessions), "Player N", where N is the seat number. The builder picks how the name travels: a synced value, or a lookup from each player's Steam ID.
- **Input:** the one player on each machine uses whatever device drives the menu today (`MenuInteractions`), with no new keyboard mapping.
- **Reuse versus Local Play:** sharing the podium, ghost-spawning and customization prefabs/components with player select is fine, as long as Local Play's player select behaves exactly as before. Its tests should keep passing, and the user checks it in the editor.

## Constraints

- Unity 2022.3, URP, Netcode for GameObjects 1.15.1, Steam lobbies through `ILobbyService` / `SteamLobbyService`.
- Max 4 players (`Constants.MAX_PLAYERS`). 6 colours, 7 hats (including No Hat), 4 companies, each assigned by seat.
- Everything the tickets show already exists on `OnlinePlayer` (seat, colour, hat, ready). A new synced value should only be added if a real gap turns up, and the Steam name is the obvious candidate.
- The project's habits:
  - rules in plain C# classes with Edit Mode tests, using fakes for the lobby (see `OnlineLobby`, `JoinRules`, `TitleEntries`);
  - CRLF files are edited with the Edit tool only.
- Scene, prefab and art work is the user's, in the editor. The code side should come with a section in `EDITOR-TODO.md` that says exactly what to build and wire.
- Fonts: Bangers and jcandlestick are what the menu uses now.

## Left to the builder

- Whether Dispatch is a new `GameState` in the menu scene (likely, next to `PlayerSelect`) or its own scene. Either way, the online flow that goes back to the menu after results has to land on Dispatch, not player select or the title. Today `MainMenu.Start` opens player select when online.
- How to reuse the player select podiums, scooter/ghost spawning and the customization UI without a second copy.
- How ticket highlighting and stamping are triggered: a change in an `OnlinePlayer` NetworkVariable is the natural hook.
- Where the countdown runs (host-driven and synced, or local on each machine from everyone's ready state), as long as everyone sees the same start.
- Which tests: rules such as "everyone ready → start", "cancel on un-ready/join" and "which seat is empty" should be plain C# and tested in Edit Mode.
- Updates to `docs/online.md` and `docs/testing.md`.

## Open for the user (not blocking)

- The board, ticket, stamp, banner and DISPATCH wordmark art. The mockups only use placeholders.
- Whether "Clock In" or "Ready" is the better label for A.

# Modern features: handoff and mocks

**Date:** 2026-10-23
**Branch:** `steam-phase1a`
**Status:** a handoff, not an approved design. The user agreed to Tiers 1 and 2 of the feature review (everything but free roam). Each feature still goes through its own design, spec and plan before any code.

> **For the next session:**
> 1. Read this, then `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md` (how to work in this repo).
> 2. Take one pack at a time (the build order below). Brainstorm it with the user (superpowers:brainstorming), starting from the "Open questions" for that feature, then write its spec and plan.
> 3. The user commits. No scene, prefab, `.inputactions` or ProjectSettings edits: UI is built by code at runtime (like `ResolutionRow`), anything else is an `EDITOR-TODO.md` chore.
> 4. Rules go in small pure classes with edit-mode tests, as in the rest of the code (`ResolutionRules`, `JoinRules`, `StealRules`).

Lines marked **(my call)** are suggestions nobody has agreed to yet. The mocks are rough layouts, not art.

## The features

| # | Feature | Pack | Size |
|---|---|---|---|
| 1 | Rematch / Play Again | C: between matches | medium |
| 2 | CPU bots | G: bots | large (several phases) |
| 3 | Match rules the host sets | D: rules | medium |
| 4 | Every setting on the Options screen | A: settings | medium |
| 5 | Options, Restart and Controls in the pause menu | B: pause | small–medium |
| 6 | Mouse in the menus | F: mouse | medium |
| 7 | Accessibility | A (global parts), B (per-player parts) | medium |
| 8 | End-of-match awards | C: between matches | medium |
| 9 | Cutscene skip for players | C: between matches | small |
| 10 | Audio options | A: settings | small |
| 11 | Split-screen layouts | A: settings | small |
| 12 | Connection info and region online | E: online | medium |

Dropped: free roam (the user). Replaying the tutorial is possible already: the Tutorial toggle in player select is on by default for every match.

## Build order (my call)

1. **C: rematch, then cutscene skip, then awards.** These change the post-match loop, which is where party games keep or lose players. Rematch also adds the "new match" reset that Restart (pack B) reuses.
2. **A: settings.** This builds the paged Options screen that packs B, D and the accessibility work all add rows to.
3. **B: pause.** It extends the control-rebinding spec (`2026-10-23-control-rebinding-design.md`), so it comes after or alongside that work.
4. **D: rules.**
5. **F: mouse.** It touches every menu, so it goes after the menus stop changing.
6. **E: online.**
7. **G: bots.** This ranks #2 for value but is the most work. Prototype one bot early (phase G1) if there's time, so its driving can be tuned in parallel.

---

## 1. Rematch / Play Again

**Why:** the results screen only goes back to player select (or Dispatch online). Each new round costs a trip through the lobby and the loading screen's "press A". Party games run on "one more round".

**Mock: the results screen, once the scores are up**

```
            ┌─────────────────────────────────────────┐
            │   1st  PURPLE GHOST         $1,240      │
            │   2nd  RED GHOST              $980      │
            │   3rd  GREEN GHOST            $610      │
            │                                         │
            │      ▶ REMATCH        BACK TO LOBBY     │
            └─────────────────────────────────────────┘
   Online, on a client:   WAITING FOR THE HOST…      Ⓑ LEAVE
```

**What happens:**
- **Rematch:** the same players, seats, colours and hats go straight into the match scene, with the tutorial skipped and every score, order and boost reset.
- **Back to lobby:** what happens today (`SceneFlow.Current.ReturnToMenu()`): player select locally, Dispatch online.
- **Who picks (my call):** locally, any player. Online, the host, while clients see "WAITING FOR THE HOST…" and can press B to leave the session.

**How it fits the code:**
- `ResultsMenu.ConfirmMenu` is the only exit today. It's authority-only already, so the host decides online.
- Loading the match from the golden scene works: `SceneManager.StartGameLoad` checks only that the match scene isn't the active one. Online, `OnlineSceneFlow.LoadGameScene` → `Begin(MatchScene.Game)` already loads it on every machine and shows it once all of them have it.
- **The catch: the per-match resets only happen on the way to the menu.** A rematch needs them without the menu:
  - `ScoreManager.ResetScore`, `BallDriving.ResetBoost`, `Compass.ResetCompass` and `SoundPool.TurnOffPlayerSounds` listen to `GameManager.OnSwapMenu`;
  - `OrderHandler.ResetHandler` listens to `SceneFlow.OnReturnToMenu`;
  - `MatchFeats.NewMatch` runs "at the menus" (`SteamFeatures`).
  - Suggested (my call): add one "new match" event that the menu path raises too, and move those listeners onto it. Then Restart (pack B) reuses it.
- **Tutorial:** `OrderManager.ResetForNextGame` sets `ShouldTutorialize = true`. A rematch must set it false after that call.
- **Loading screen:** a rematch should keep the loading screen (it hides the scene swap). Locally it waits for every player to press A. Skipping that wait on a rematch is my call.
- **Online:** `JoinRules` already lets nobody in once a match has started, so the players can't change between rounds. A player who left during the results is simply gone. If only the host is left, Rematch still works (a solo match).
- **Music:** `SoundManager` plays the main theme on `OnSwapStartingCutscene`, so a rematch gets it without changes.

**Open questions (the user):**
- Should a rematch need everyone to agree (each player presses Rematch, Mario Kart style), or is the host's (or any couch player's) press enough?
- Is a third button, "Title screen", worth adding, or is going back through the lobby enough?

**Tests:** a new-match reset test (scores, orders and boost are zero after a rematch). Online: a Play Mode test where the host rematches and every machine loads the match with the tutorial off.

---

## 2. CPU bots

**Why:** a solo player has nobody to beat, and online lobbies with few players sit half empty. Bots fill empty seats.

**Mock: player select, an empty seat**

```
 ┌──────────────┬──────────────┬──────────────┬──────────────┐
 │   PLAYER 1   │   PLAYER 2   │     CPU      │   (empty)    │
 │   [ghost]    │   [ghost]    │   [ghost]    │              │
 │   READY      │   READY      │  ◀ NORMAL ▶  │  Ⓨ ADD CPU   │
 └──────────────┴──────────────┴──────────────┴──────────────┘
   Ⓨ add a CPU     ◀ ▶ its difficulty     Ⓑ on it: remove
```

**What happens (my calls):**
- Bots take empty seats, with a random colour and hat that no player is using, and the name "CPU".
- There are three difficulties: Easy, Normal and Hard.
- Bots never earn achievements, and they don't count toward "Delivering solo" in Rich Presence.

**How it fits the code:**
- **The seam exists already:** everything that drives a scooter reads `IDriveInput` (steer, throttle, brake, camera, boost and drift events). A `BotDriver : IDriveInput` drives a scooter exactly as a pad does, and online, remote avatars already plug in through the same interface.
- **Navigation:**
  - `CivilianAgent` already uses Unity's NavMesh, but that mesh is likely only for sidewalks.
  - A bot needs a road mesh: a second NavMesh agent type for the scooter (an editor chore: bake it).
  - The bot asks for a path to its target (`NavMesh.CalculatePath`) and steers toward the next corner.
  - Phase-boosting goes through buildings, so a Hard bot can cut straight lines when the corner is inside a building.
- **What it chases:** the nearest order it can carry (`OrderManager`, `Order`, `OrderBeacon`), then that order's customer. A Hard bot also chases the Golden Order and steals from players.
- **Difficulty:** a top-speed cap, a reaction delay, how often it boosts and drifts, and a small chance of mistakes.
- **Seats:** `PlayerRoster` / `PlayerSlot` hold seats by device today. A bot seat needs a slot with no device. That's the riskiest change: `PlayerInstantiate`, `LoadingScreenManager`'s confirm buttons, `SplitScreenLayout` and the camera count all assume one view per seat. **Bots get no camera or split-screen view.**
- **Online:** the host runs the bots, so they reach clients like any host-driven player. Matchmaking with bots in the lobby is a later phase.

**Phases (my call):**
- G1: one bot drives the town and delivers orders (a test scene, no seat).
- G2: bot seats in local player select, with no views.
- G3: difficulty tuning, stealing and the Golden Order.
- G4: bots online (the host's seats).

**Open questions (the user):**
- Should bots fill online seats too (for example, when matchmaking times out), or stay local only?
- Should a bot's skill catch up when players lead (rubber-banding), or stay fixed?

---

## 3. Match rules the host sets

**Why:** the match length is fixed (`OrderManager.waveLengthInSeconds` × the number of waves in `maxEasy`/`maxMedium`/`maxHard`), and there's no way to turn steals or the Golden Order off.

**Mock: a Rules panel (player select, or Dispatch for the host)**

```
   ┌──────────── MATCH RULES ────────────┐
   │  Length         ◀  NORMAL  ▶        │   SHORT · NORMAL · LONG
   │  Steals         ◀   ON     ▶        │
   │  Golden Order   ◀   ON     ▶        │
   │  Catch-up       ◀   OFF    ▶        │
   │                                     │
   │  Ⓐ done        Ⓧ defaults           │
   └─────────────────────────────────────┘
```

**What happens (my calls):**
- **Length:** Short, Normal or Long. Normal is today's match. Short and Long change the wave length; the number of waves stays the same.
- **Steals off:** boosting into a rival still bumps them, but takes nothing (`StealRules`).
- **Golden Order off:** the match ends after the last wave and goes to the results, with no golden scene.
- **Catch-up:** whoever is last refills boost faster. The user needs to decide whether to include this at all.
- The rules are remembered in `settings.cfg`.
- The rules line shows on the Dispatch board, so clients see it.
- Matchmaking (public) lobbies always use the default rules.

**How it fits the code:**
- **Online:** the host owns the rules. Put them in the Steam lobby data (`LobbyRules` already sets keys there) so clients read them before the match starts, and so matchmaking can filter on "default rules". The host's `OrderManager` and `StealRules` read them, and clients only follow states, so nothing else needs syncing.
- **Golden Order off:** the switch from the last wave to `LoadFinalOrderScene` (`OrderManager`, `OnMainGameFinishes`) needs another branch. The results UI is the `Results Canvas` prefab (`ResultsMenu`), placed in `FinalAreaScene` but not in the match scene (`Design Scene(Main)`). So with the Golden Order off, either the prefab goes into the match scene too (an editor chore), or the golden scene loads straight into Results. That's the biggest unknown here.
- **Achievements:** `GOLDEN_WIN`, `GOLDEN_STEAL` and `LAST_TO_FIRST` can't be earned with the Golden Order off. That's fine, but say so in the rules panel.

**Open questions (the user):**
- Which rules matter most to you? The four above are my list.
- Should the rules panel live in player select, on the Dispatch board, or both?

---

## 4. Every setting on the Options screen

**Why:** VSync, the frame cap and borderless vs exclusive fullscreen are only in `settings.cfg`. The Quality row waits for art. Players and Valve's Steam Deck review expect all of it in-game.

**Mock: Options, split into pages**

```
   DISPLAY   AUDIO   ACCESSIBILITY            ◀ LB      RB ▶
   ─────────────────────────────────────────────────────────
   Window mode    ◀  BORDERLESS  ▶     BORDERLESS · FULLSCREEN · WINDOWED
   Resolution     ◀  1920×1080   ▶     Ⓐ apply
   VSync          ◀  ON          ▶
   Frame cap      ◀  OFF         ▶     30 · 60 · 120 · 144 · 165 · 240 · OFF
   Quality        ◀  HIGH        ▶
   Split-screen   ◀  BOXED       ▶     (feature 11)
```

**What happens (my calls):**
- **Window mode** merges today's Fullscreen row and the `exclusive` key into one row with three choices.
- **Frame cap** is shown greyed out while VSync is on, since the cap only applies with VSync off (`docs/controls.md`).
- The pages switch with the shoulders, which are free in the UI map. Note that the Dispatch shoulder actions need adding to the input asset first (`EDITOR-TODO.md` 1d).

**How it fits the code:**
- `GameSettings` already has every field (`exclusiveFullscreen`, `vsync`, `frameCap`, `qualityLevel`), and `DisplayRules` already applies them. Only the rows are missing.
- Build the rows by code, like `ResolutionRow`, in the screen's font, until the hand-lettered art exists. When art comes, it can replace a row's label.
- `OptionsMenu` scrolls fixed `selectorObjects` today. Pages need its scroll and confirm to cover the code-built rows too. The Resolution row already does this for one row.

**Open questions (the user):**
- Pages (my call) or one long scrolling list?
- Are code-built rows in the existing font fine for launch, or must every row be hand-lettered?

---

## 5. Options, Restart and Controls in the pause menu

**Why:** the pause menu has Resume and Main Menu only (`PauseMenu.ConfirmMenu`, cases 0 and 1). The rebinding spec already adds Controls.

**Mock**

```
   Host's pause (local)            Another player's pause (local)
   ┌───────────────┐               ┌───────────────┐
   │ ▶ RESUME      │               │   PAUSED      │
   │   RESTART     │               │ ▶ CONTROLS    │
   │   OPTIONS     │               └───────────────┘
   │   CONTROLS    │
   │   MAIN MENU   │               Online: no Restart
   └───────────────┘
```

**What happens (my calls):**
- **Restart** (local only) asks "RESTART THE MATCH? Ⓐ yes Ⓑ no", then does what Rematch does (feature 1): the same reset, with the tutorial skipped.
- **Options** opens the same pages as the title screen's Options, over the paused game. Only the host's pause has it, since those settings are shared.
- **Controls** follows the rebinding spec. Per-player settings from feature 7 (camera sensitivity, invert, auto-accelerate) go there too, saved per seat like the bindings.

**How it fits the code:**
- `PauseMenu` uses fixed `selectorObjects` for its entries, so code-built entries need the same treatment as the Options rows.
- Online, Restart is hidden: the online pause doesn't stop the game, and restarting mid-match would need every client's agreement.

**Open questions (the user):**
- Should Restart need confirming?
- Should Restart replay the tutorial if it was on?

---

## 6. Mouse in the menus

**Why:** the mouse isn't used anywhere (`docs/controls.md`). PC players will try to click, and Steam reviews mention it.

**Mock**

```
   ┌──────────────┐
   │   MULTIPLAYER │ ← hover moves the selector here
   │ ▶ LOCAL   ↖   │ ← click picks it
   │   OPTIONS     │
   │   CREDITS     │      ◀ ▶ arrows on a row are clickable;
   │   QUIT        │      right-click or Esc = back
   └──────────────┘
```

**What happens (my calls):**
- The mouse works in the menus only, not for driving.
- It acts as the keyboard player's menu input.
- In player select, a click joins the keyboard player (same as Space).
- `CursorHider` already shows the cursor when the mouse moves and hides it after 2 s.

**How it fits the code:**
- The menus are custom selectors driven by input actions (`ScrollMenu`, `Confirm`), not Unity UI selection. The menu scene has an EventSystem, but the selectors don't use it.
- Suggested: one `MouseMenus` component per menu. It finds which `selectorObjects` entry the pointer is over (`RectTransformUtility`), moves the selector there, and calls the menu's own Confirm on a click. That way the menus keep a single path for input.
- The menus to cover: title, Options, Credits, player select, Dispatch (tickets and pop-up), pause, results and the loading screen's ready.

**Open questions (the user):**
- Should the mouse also turn the camera while driving for the keyboard player, or stay menus only?

---

## 7. Accessibility

**Why:** players are told apart by colour alone (Purple, Red, Green, Yellow, Blue, Orange). Red and green are the most common colour-blind confusion. There's also no way to turn down motion effects, change camera feel, or drive with one hand.

**Mock: other players, seen in your view**

```
           ▲ P2                 ■ P3
          [scooter]           [scooter]
   Compass / minimap icons use the same shapes: ● P1  ▲ P2  ■ P3  ◆ P4
```

**Options → Accessibility (global, my calls):**
- **Player markers:** On (default) or Off. Shows a shape and P1–P4 (or the Steam name online) over each rival, and the same shapes on `Compass` and `Minimap` icons and the results.
- **Reduce motion:** Off by default. It turns off the boost's FOV kick (`OrbitalCamera` `boostFOV`, `FovHold`) and the speed lines (`Assets/Rendering/Player N Speed Lines.mat`), plus any shake.
- **HUD size:** 90%, 100% or 125%, for the Steam Deck. It sets the HUD canvases' `CanvasScaler`.

**Controls (per seat, saved like the bindings, pack B):**
- **Camera sensitivity:** 50%–200%. It scales `OrbitalCamera`'s axis input.
- **Invert camera:** X and Y separately.
- **Auto-accelerate:** throttle stays on unless the player brakes. Implemented where `IDriveInput.Accelerate` is read for a local pad (`InputManager`). Online it's the driving machine's own setting, so nothing is sent.

**How it fits the code:**
- **Shapes are tied to seats, not colours (my call),** so a hat or colour change never changes them. `PlayerColorInformationSO` stays as it is.
- Check the six colours with a colour-blind simulator. Even with markers, the Dispatch tickets and podium rims (`DispatchRoom`) use the company colour, so add the shape there too.

**Open questions (the user):**
- Are name tags over rivals wanted all the time, or only with the setting on?
- Should hold-to-drift also get a toggle option?

---

## 8. End-of-match awards

**Why:** a cheap way to make every player feel seen at the results, even the one who came last. Most of the counting exists already, for achievements.

**Mock: after the scores, one award card per player (my call)**

```
   ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
   │ KLEPTOGHOST  │ │ FISH FOOD    │ │ BUSY BEE     │ │ BUTTERFINGERS│
   │ most steals  │ │ 4 swims      │ │ 9 deliveries │ │ robbed 5×    │
   │   ● P1  (3)  │ │   ▲ P2       │ │   ■ P3       │ │   ◆ P4       │
   └──────────────┘ └──────────────┘ └──────────────┘ └──────────────┘
```

**Award list, phase 1 (all of these are known where the rules run):**
- Busy Bee: the most deliveries.
- Kleptoghost: the most steals.
- Butterfingers: stolen from the most.
- Fish Food: the most falls in the water. Respawns go through the host already (`OnlineRespawns`, `SendRespawn`).
- Golden Grip: held the Golden Order the longest.
- High Roller: the most from hard orders.

Phase 2, counted on the driver's machine and sent at the results: Drift King (the most tier-3 sparks) and Wall Walker (the most building phases).

**How it fits the code:**
- **A `MatchStats` tally, next to `MatchFeats`,** fed by the same events: `FeatSync` deliveries and steals, and respawns.
- `AwardRules` picks one award per player where it can: each player's best award that nobody else took, then the best of what's left.
- **Online:** the host sends the awards with the results, one message (`OnlineMatch`, like `SendAchievement`). Clients only show them.
- `ResultsMenu.UpdateResults` shows the cards after `timeBeforeUI`, before the Rematch buttons.

**Open questions (the user):**
- One award per player (my call), or only the two or three biggest?
- Joke names like these, or plain ones ("Most steals")?

---

## 9. Cutscene skip for players

**Why:** the starting and golden cutscenes play every match. The only skip is the developer key (L, `CutsceneManager.Skip`).

**Mock**

```
                                              ● P1 ✓  ▲ P2 ✓  ■ P3 …
                                              (hold Ⓐ) SKIP ◔
```

**What happens (my calls):**
- Each player holds A for half a second to vote, and gets a tick.
- Once everyone has voted, the cutscene ends.
- The prompt shows only after the first second, so nobody skips by accident while still pressing A from the loading screen.

**How it fits the code:**
- `CutsceneManager.Skip()` already works only where states are decided, and online the host's next state ends the cutscene on every client. Only the vote is new.
- **Locally:** the per-player ticks work like the loading screen's confirm buttons (`LoadingScreenManager.ConfirmButton`).
- **Online:** each client sends its players' votes to the host (a new `OnlineMatch` message). Add the message to the rate limits (`docs/online-safety.md`). The host skips once every seated player has voted.
- **The golden cutscene** shows the final area. On a first play it's worth watching, so its skip could need everyone's vote while the starting one needs only the host's. That's for the user to decide.

**Open questions (the user):**
- Does everyone vote (my call), or can the host (or Player 1) skip alone?

---

## 10. Audio options

**Why:** there's only Music and SFX, in 11 steps. There's no master volume, and the game keeps playing sound when it's in the background.

**Options → Audio (my calls):**
- **Master:** a new row (mixer group).
- **Music** and **Effects:** the current rows.
- **In the background:** Mute (default) or Keep playing. On `OnApplicationFocus(false)`, set `AudioListener.volume` to 0. Don't pause: `runInBackground` is on, and online needs the game to keep running.

**How it fits the code:**
- `SoundManager` sets the mixer's `SFX` and `Music` exposed parameters (`mainMixer.SetFloat`). Master needs a `Master` exposed parameter on the mixer's Master group: an editor chore.
- `GameSettings` gains `masterPosition` and `backgroundMute`.
- **Finer steps:** the art has 11 selector positions (`MAX_VOLUME_POSITION`). Keep 11 unless code-built bars replace the art. That's the user's call.

---

## 11. Split-screen layouts

**Why:** with two players, each view is a quarter of the screen, centred and stacked (`SplitScreenLayout.CalculateRects`: width 0.5, height 0.5). It keeps 16:9 but leaves half the screen empty.

**Mock: Options → Display → Split-screen (two players)**

```
   BOXED (today)        STACKED              SIDE BY SIDE
   ┌──┬──────┬──┐       ┌────────────┐       ┌──────┬──────┐
   │  │  P1  │  │       │     P1     │       │      │      │
   │  ├──────┤  │       ├────────────┤       │  P1  │  P2  │
   │  │  P2  │  │       │     P2     │       │      │      │
   └──┴──────┴──┘       └────────────┘       └──────┴──────┘
```

**How it fits the code:**
- `CalculateRects(playerCount)` gains a layout argument, saved in `GameSettings`.
- Stacked and side by side change each view's aspect ratio. Cinemachine follows the viewport, but check that the HUD (`PlayerCameraResizer`, the compass, minimap and indicators) fits a 32:9 or 8:9 view.
- Three and four players stay as they are (my call).

**Open questions (the user):**
- Should the empty quarter with three players show something (a shared map, the scores)?

---

## 12. Connection info and region online

**Why:** nobody can see whose connection is bad. Matchmaking takes the fullest lobby (`Matchmaker`) without knowing how far away it is.

**Mock: a Dispatch ticket, and the online pause**

```
   ┌─────────────────┐          ┌──────── PAUSED ────────┐
   │ ● FATCATS       │          │ ● FATCATS     ▮▮▮ 24ms  │
   │   READY  ▮▮▮    │          │ ▲ ALEX        ▮▮▯ 96ms  │
   └─────────────────┘          │ ■ SAM         ▮▯▯ 180ms │
                                └─────────────────────────┘
   ▮▮▮ under 80 ms · ▮▮▯ 80–150 ms · ▮▯▯ over 150 ms (my call)
```

**What happens:**
- **Pings:** the host measures each machine's round trip and sends them every 2 s (one small message, added to the rate limits). Everyone sees them in Dispatch and in the pause menu, not on the driving HUD (my call).
- **Region:** matchmaking prefers the closest lobby.
  - The host puts its Steam ping location in the lobby data (`SteamNetworkingUtils.GetLocalPingLocation`, as a string).
  - A searcher estimates each lobby's ping (`EstimatePingTimeBetweenTwoLocations`) and drops lobbies over a limit (say 200 ms), then picks the fullest of the rest.
  - `SteamLobbyService`'s list request sets no distance filter today. Steam's default, nearby regions, applies.

**How it fits the code:**
- Ping per client: `NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(clientId)` on the host. The patched Steam transport implements it (`SteamNetworkingSocketsTransport.GetCurrentRtt`). It logs an error for a client that isn't connected, so only ask for seated machines.
- Matchmaking keeps its rules in `MatchRules` and `Matchmaker`. Ping filtering belongs in `MatchRules`, with tests.

**Open questions (the user):**
- Should a "Worldwide" search be offered for quiet hours, or does matchmaking widen the search on its own after a while?

---

## Shared notes

- **New online messages** (rematch votes if the user wants them, skip votes, awards, pings) go through `OnlineMatch`, with rate limits and checks on what a client may send (`docs/online-safety.md`, `RateGate`).
- **New settings** go in `GameSettings` (`ToText`/`Parse`, so `settings.cfg` and Steam Cloud carry them). Per-seat settings go with the per-seat bindings from the rebinding spec.
- **Docs to update as each pack lands:** `docs/controls.md`, `docs/online.md`, `docs/steam/store-page-draft.md` (features and screenshots), and `EDITOR-TODO.md` for the chores.

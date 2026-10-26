# Controls and display settings

## Controllers

Up to 4 players, one controller each (Xbox, PlayStation and Switch Pro controllers). Press any button to join.

| What | Controller |
|---|---|
| Steer | Left stick |
| Accelerate / brake | Right trigger / left trigger |
| Boost | A (cross) |
| Drift | X (square) |
| Turn the camera | Right stick |
| Swap the camera | Right stick press |
| Pause / resume | Start |
| Menus: move / pick / back | D-pad or left stick / A / B |
| Menus: host an online lobby (player select) | Y (triangle) |
| Menus: open the customization sliders (online player select) | X (square) |
| Menus: find a match (online player select, host) | Left shoulder (L1 / LB) |
| Menus: open the lobby to anyone, or back to friends only (online player select, host) | Right shoulder (R1 / RB) |
| Cutscenes: vote to skip (everyone must) | Hold A (cross) for half a second |
| Results: vote for a rematch | A (cross) |
| Results: take your vote back, or back to the lobby | B (circle) |

## Languages

- The game speaks 16 languages: English, French, German, Italian, Dutch, Spanish (Spain and Latin America), Portuguese (Portugal and Brazil), Polish, Turkish, Czech, Romanian, Hungarian, Swedish and Danish.
- **First launch:** the player's Steam language, else the PC's, else English.
- **Options → GENERAL → LANGUAGE** (under Resolution): left and right change it at once, everywhere, saved in `settings.cfg` (`language=fr`). A little flag beside the row and in the title screen's top-right corner shows the language spoken.
- **The hand-lettered words** (OPTIONS, RESUME, PAUSED…) stay the drawn art in English; in another language the word is spelled in Jack's Candlestick, the font the art was drawn with, shrinking to fit a longer word.
- **Accents the fonts lack** (Polish, Czech, Turkish, Romanian and Hungarian letters such as Č, Ł, Ő, Ş): the base letter with a mark drawn from the font's own accents placed on it. Capital ẞ is written SS.
- **Stays English:** the two loading screens (instructions painted into the pictures), keyboard key names on the Controls panel (W, LEFT SHIFT), the Dispatch wordmark and ticket art, and player names.
- **The strings** are in `Assets/Resources/Localization/strings.csv` (key, a note for translators, one column per language, and `reviewed`: the languages a native speaker has checked). Edit `tools/strings_en.py` (keys and English) and `tools/strings_tr.py` (translations), then run `python tools/strings.py`. The translations were written by Claude: each needs a native speaker's check before release.

## Match rules

- In player select (local) or Dispatch (the online host), **Start** opens **MATCH RULES**; only the player who works the menus can. Up / down pick a rule, left / right change it, **A** or **B** close it (Start too).
- **LENGTH:** SHORT, NORMAL or LONG: each wave lasts 0.75×, 1× or 1.5× as long.
- **STEALS:** OFF still bumps a rival, but takes and drops nothing. The tutorial's steal lesson still works.
- **GOLDEN ORDER:** OFF ends the match after the last wave, straight to the results. Golden Win, Golden Steal and Last to First can't be earned then (the panel says so).
- The rules are kept in `settings.cfg` (`length`, `steals`, `golden`). Online the host's rules run the match; a public lobby (matchmaking, or an open lobby) always plays the normal rules. Clients aren't shown the host's rules yet.

## The pause menu

- **Whoever paused** gets the list: **RESUME · RESTART · CONTROLS · MAIN MENU**. Online there's no Restart: the online pause doesn't stop the match, and a restart would need everyone.
- **Restart** asks "RESTART THE MATCH?": A restarts it as a rematch does (the same players, scores back to 0, no tutorial), B goes back to the list.
- **The other players' pause** shows PAUSED and **CONTROLS**: A opens their own Controls panel.
- **Online**, every pause lists each player and their connection: bars and the round trip in ms, as the host measures it every 2 s.

### Controls (rebinding)

- The panel shows the set for the device that player is using when it opens: **CONTROLS — KEYBOARD** or **CONTROLS — CONTROLLER**, a row per control and **RESET TO DEFAULTS** last.
- **A** on a row: "PRESS A KEY..." / "PRESS A BUTTON...", then press the new one on your own keyboard or pad. One another control has swaps with it. **B** (circle) or **Backspace** cancels the wait.
- **Can't be bound:** Pause (Start / Escape), B / circle and Backspace, the sticks' directions, Select, and the keyboard's second keys (the arrows, which keep working as they are).
- **Controller:** Accelerate, Brake, Boost, Drift and Swap Camera can go on the face buttons (not B), shoulders, triggers, stick presses or the d-pad. The sticks stay steering and camera.
- **Keyboard:** the first key of each control: Accelerate, Brake, Steer left / right, Boost, Drift, Swap Camera and the four camera keys.
- **B** closes the panel; **Start** closes it too (not while it's waiting). Closing saves.
- **Saved by seat** in `bindings.cfg`, next to `settings.cfg`: whoever plays as Player N next time gets Player N's controls, keyboard and controller each. A damaged line only loses that set.
- The loading screen's controls picture is drawn art of the default controls: it doesn't change with a rebind.

## Skipping a cutscene

- A second into the opening or golden-round cutscene, **HOLD A TO SKIP n/m** shows bottom right. Each player holds A (Space) for half a second to vote; a tap doesn't count, and neither does an A still held from the loading screen (let go and press again).
- Once every player has voted, the cutscene ends. A vote can't be taken back. Online the host counts the votes and every machine shows the same count.

## The results screen: rematch

- Once the scores are up, every player votes for themselves: A (Space) is **REMATCH**, B (Backspace) takes the vote back, or, with no vote, is **BACK TO LOBBY**.
- READY shows under each player who voted. The countdown is 15 s (`RematchVote.SECONDS`).
- Once everyone still in has voted, the rematch starts at once. When time runs out, whoever voted plays again and the rest are taken out. Nobody voted: everyone goes back to the lobby.
- A rematch keeps the players, seats, colours and hats, resets scores, orders and boost, skips the tutorial (everyone starts in the city) and doesn't wait for A on the loading screen.
- On the couch, a player taken out loses their seat and the others get the screen. They can join again from player select.
- Online, see `docs/online.md` (Rematch).

## Keyboard (one player)

One player can use the keyboard, alongside up to 3 controllers. **Press any key to join** (not F1–F12: those are the dev keys). With one player on this PC, using the keyboard or another controller switches that player over to it, so they play on whatever they used last (outside offline player select, where a new device joins as a new player).

The game joins players itself (`PlayerJoiner`, Phase 5A): the scene's `PlayerInputManager` is switched to manual joins, so another key makes no player at all (Unity used to make one for any key, which the game then destroyed).

| What | Keys |
|---|---|
| Steer | A / D, or ← / → |
| Accelerate | W or ↑ |
| Brake | S or ↓ |
| Boost | Space |
| Drift | Left Shift |
| Turn the camera | Q / E (sideways), Z / C (up and down) |
| Swap the camera | Tab |
| Pause / resume | Escape |
| Menus: move | W A S D or the arrows |
| Menus: pick | Space or Enter |
| Menus: back | Backspace |
| Menus: host an online lobby (player select) | Tab |
| Menus: open the customization sliders (online player select) | Q |
| Menus: find a match (online player select, host) | F |
| Menus: open the lobby to anyone, or back to friends only (online player select, host) | R |
| Loading screen: ready | Space or Enter |

- **Button prompts still show controller buttons:** read A as Space, B as Backspace and Start as Escape. Keyboard prompt art is still to come.
- **The mouse works the menus, not the driving:** on the title screen, Options (every page), Credits and the pause menu of whoever paused, moving over a row selects it, a left click does what A does there (on an Options row it steps the value towards the click; on the Resolution size it applies it), and a right click does what B does. A click only counts while the cursor shows (it moved in the last 2 s), so a click that brings the window back never quits or leaves a match. On a drawn Options row a click goes to the choice nearest it; on the Resolution size it applies it, either side steps. Join first (any key, or any pad button): with nobody in, the mouse does nothing. Player select, Dispatch, the Controls panel, Restart's question and the results still need a pad or the keyboard.
- **A lost controller:** if a player's controller is unplugged mid-match, only another controller can take their place, not the keyboard.
- **Dev hotkeys:** in the editor and in development builds, some letters double as dev hotkeys (`docs/dev-hotkeys.md`): S skips the tutorial, B refills boost, R respawns everyone, and T drops everyone's orders. Drive with the arrows when testing there. Release builds have no dev hotkeys.
- **How it works:**
  - The keys are added in code, in memory, when the game starts (`KeyboardControls`); the input actions asset on disk is unchanged.
  - The asset's unused Keyboard&Mouse scheme is swapped for a keyboard-only one.
  - In the editor, the asset is put back when Play Mode ends.

## The Options pages

Options has four pages, turned with **LB / RB** (L1 / R1; **F / R** on the keyboard). The page names run along the top, the current one bright. Leaving Options saves and goes back to the first page. Every row below the drawn ones is built by code in the screen's font (`OptionRow`, `OptionsBook`), and applies the moment it changes. See `docs/superpowers/specs/2026-10-24-options-pages-design.md`.

| Page | Rows |
|---|---|
| GENERAL | The drawn screen: Music, Effects, Fullscreen, Resolution (A applies it) |
| DISPLAY | Fullscreen style (BORDERLESS / EXCLUSIVE; dimmed while windowed), VSync, Frame cap (dimmed while VSync is on), Quality, Split-screen (two players: BOXED / STACKED / SIDE BY SIDE) |
| AUDIO | Master (0–100%), In the background (MUTE / KEEP PLAYING: muting never pauses the game) |
| ACCESSIBILITY | Reduce motion (no boost FOV kick, no speed-up FOV, no speed lines) |

## Display settings in settings.cfg

Every setting below has a row on the Options pages. They're kept in `settings.cfg` too.

**The Resolution row** (`ResolutionRow`, `ResolutionRules`) is built by code under the Fullscreen row, in the screen's own font, since its sizes depend on the monitor:
- Left and right step through the monitor's sizes, smallest to largest, stopping at both ends. It starts on the saved size, or the current one.
- **A** applies the shown size and saves it (`width`, `height` below). Until then the row says "A: apply".
- Leaving Options without A keeps the size that was applied.
- In borderless fullscreen (the default) Unity draws at the chosen size and stretches it to the screen; a window or exclusive fullscreen changes the real size.
- The editor ignores display sizes (as for every display setting below): the row works there, but the size only changes in a build.

The file:

- **Where:** `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\settings.cfg`.
- **When:** the game reads it at launch.
- **What:** one `key=value` per line. Leave a key out to keep its default.

| Key | Values | Default | What it does |
|---|---|---|---|
| `fullscreen` | 0, 1 | 1 | 0 windowed, 1 fullscreen (the Options row) |
| `exclusive` | 0, 1 | 0 | Fullscreen only: 0 borderless, 1 exclusive |
| `width`, `height` | 0, or a size | 0 | 0 means the desktop's size (the Resolution row) |
| `vsync` | 0, 1 | 1 | VSync off or on |
| `framecap` | 0, or 30–500 | 0 | The most frames per second; 0 means no cap. It only applies with VSync off. A number the row doesn't list shows as it is. |
| `quality` | 0–2 | 2 (1 on a Steam Deck) | LOW, MEDIUM, HIGH |
| `split` | 0–2 | 0 | Two players: 0 boxed, 1 stacked, 2 side by side |
| `master` | 0–10 | 10 | Overall volume in tenths |
| `bgmute` | 0, 1 | 0 | 1: silent while the window isn't focused |
| `reducemotion` | 0, 1 | 0 | 1: Reduce motion |
| `length` | 0–2 | 1 | Match rules: 0 short, 1 normal, 2 long |
| `steals` | 0, 1 | 1 | Match rules: 0 no steals |
| `golden` | 0, 1 | 1 | Match rules: 0 no golden round |

How a saved size is used (`DisplayRules`):
- **Fullscreen:** a size the monitor doesn't list falls back to the desktop's.
- **Windowed:** the size is shrunk to fit inside the desktop. With no size saved, the window is 1280×720, or the desktop's size if that's smaller.

The defaults match how the game ran before these settings existed: borderless at the desktop's size, VSync on, no cap. A Steam Deck gets the same, at its 1280×800 screen.

In the editor these settings aren't applied (the Game view ignores them), so check them in a build.

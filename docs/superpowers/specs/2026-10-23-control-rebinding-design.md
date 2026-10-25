# Control rebinding in the pause menu: design

**Date:** 2026-10-23
**Branch:** `steam-phase1a`
**Status:** approved in chat (scope, seats, approach); built 2026-10-24 while the user was away ("I trust your judgement"), before their review of this written spec

## Why

Players want to change which keys and buttons drive their scooter, on the keyboard and on a controller. They do it from the pause menu, mid-session, and each split-screen player sets their own.

## Goals (decided with the user)

- **Rebinding on both keyboard and controller**, from the **pause menu**.
- **Each player has their own bindings** (split-screen: every seat independent).
- **Remembered by seat**: whoever plays as Player N next time gets Player N's bindings, across launches.
- **Rebindable: the driving controls.**
  - Accelerate, Brake, Boost, Drift and Swap Camera, on keyboard and controller.
  - On keyboard only, also: Steer left / right, and the four camera keys (camera left / right, Q / E; camera up / down, Z / C).
- **Fixed (not rebindable):**
  - Pause: Start / Escape, so a player can always get back to undo a mistake.
  - Every menu button (the UI and Load maps: confirm, back, the face buttons and shoulders on Dispatch). The button prompts (ButtonGlyph) rely on them.
  - The controller's sticks: steering on the left stick, the camera on the right stick.
- **Two sets per seat:** a keyboard set and a controller set, both saved. The screen edits the set of the device that player is using right now, so a seat that swaps between keyboard and pad keeps both.
- **Reset to defaults** for the seat (the set being edited).
- **A key or button already in use swaps:** binding Boost to the button Drift had gives Drift Boost's old one. Nothing is left unbound.

## Approach (picked by the user: option 1 of 3)

**Built by code at runtime**, like the Options screen's Resolution row (`ResolutionRow`):
- The pause menu gets a **Controls** entry.
- It opens a panel the code builds under that player's own pause menu, in the pause menu's font with the Dispatch art.
- No prefab or scene edits for the user.

Options not taken:
- (2) A Tools item building the panel into the player prefab, which would be hand-stylable but edits the most shared asset.
- (3) Unity's Rebinding UI sample: generic look, no per-seat saving.

## The screen

- **Pause menu:**
  - The host's pause list becomes **Resume · Controls · Main Menu**. The order is my call; Main Menu stays last.
  - The other players' pause views (Sub pause), which today show only the paused state with no selectable entries, get a single selectable **Controls** entry. Only the host resumes. My call: every player must be able to reach their own controls.
  - Online (one player per machine, always the host type), it's the same list.
- **The Controls panel** (per player, inside their own split-screen view):
  - **Title:** "CONTROLS — KEYBOARD" or "CONTROLS — CONTROLLER", whichever device that player is using.
  - **One row per rebindable action** for that device, showing the action's name and its current key or button. Keyboard keys show by name ("W", "LEFT SHIFT"). Controller buttons show with the button art where it exists (`Assets/Sprites/UI/Buttons`, MenUI_Buttons), else the control's display name ("RT", "R3").
  - **Last row: "RESET TO DEFAULTS".**
- **Using it:**
  - Up / down moves between rows.
  - Confirm on a row shows "PRESS A KEY…" or "PRESS A BUTTON…" on that row and waits for the next press on that player's device.
  - Confirm on Reset resets that player's set for the current device.
  - Back closes the panel and returns to the pause list.
  - While waiting for a press:
    - **Back cancels, and can't itself be bound.** On the controller that's B / circle (`<Gamepad>/buttonEast`); on the keyboard, Backspace.
    - **Pause and the sticks can't be bound either.** Pressing one keeps waiting.
    - Only the player's own device is listened to, on the matching kind of control: keys for the keyboard set; gamepad buttons, triggers, shoulders and stick presses for the controller set.
  - A press on a key or button another rebindable action already uses **swaps** the two (see Goals).
  - Changes apply at once, and are saved when the panel closes.

## How it fits the game (findings)

- **Players:**
  - Each player is a `PlayerInput` with its own copy of the actions (PlayerInputManager joins), so overrides on `PlayerInput.actions` are per player.
  - A player's seat is their slot (`PlayerSlot.Index`, 0–3). Online it's the seat the host gave, which `PlayerInstantiate` keeps in step.
- **Bindings as they are:**
  - Player map, controller: one binding per action, `<Gamepad>/…`, with an **empty** group (except Pause, `Gamepad`). Pick controller bindings by their path's device (`<Gamepad>`), not by group.
  - Keyboard: `KeyboardControls` adds `Keyboard`-group bindings at launch. Most actions have two keys, the WASD set and the arrows. Steer and the camera axes are 1DAxis composites (negative / positive parts).
  - **Rebinding a keyboard action changes its first key** (W, S, Space, Left Shift, Tab; A / D for Steer's parts; Q / E and Z / C for the camera parts). The second keys (the arrows) stay as fixed alternates. My call: one key per action keeps the screen simple. A swap only looks at the rebindable first keys.
- **Saving:**
  - A new file next to `settings.cfg`, `bindings.cfg` (my call on the name).
  - It holds per seat (1–4) two override strings, keyboard and controller, from the Input System's own override JSON (`SaveBindingOverridesAsJson` limited to that set's bindings, or one equivalent override list per set).
  - A damaged or unreadable entry loses only that seat's set, as `GameSettings` does for its lines. Loading must never throw.
- **When a player's bindings apply:** once they take a seat (`PlayerInstantiate.AddPlayerReference`), and again whenever their seat changes (online seat moves: `MoveToSeat`, the host giving a seat).
- **Pause behaviour stays:**
  - Offline the host's pause stops the game.
  - Online the match goes on behind the pause (existing). The panel works the same; it's the player's choice to rebind mid-race.
- **Button prompts (ButtonGlyph):**
  - They show menu buttons, which are fixed, so they stay right.
  - The loading screen's controls picture (`DOA_LoadingScreenNEW`) is baked art of the default layout and won't reflect a rebind. Known limit, noted in `docs/controls.md`.

## Details settled after review (my calls)

- **Start / Escape while the panel is up:**
  - While waiting for a press, they're ignored: the wait goes on, so a key can't resume the game mid-rebind.
  - With the panel open but not waiting, they close the panel back to the pause list, and do nothing else.
  - Only from the pause list do they resume, as now.
- **Which presses can be bound:**
  - **Keyboard:** any key except Escape and Backspace (Pause and Back), and except a key another driving action already holds as its fixed second key (the arrows). Such a press is refused and the wait goes on. Keys the menus use (Space, Enter, Tab) can be bound: the menus and driving are separate maps, as Space already is both Boost and menu confirm.
  - **Controller:**
    - Allowed: the face buttons, the shoulders, the triggers, the stick presses and the d-pad.
    - Refused: Start / Select (Pause), B / circle (Back while waiting), and the sticks' directions.
    - Accelerate and Brake can go on any of these; a button gives full press.
- **One controller set for every pad type:** the bindings are generic `<Gamepad>/…` paths, so Xbox, PlayStation and Switch pads share a seat's controller set.
- **Which set the panel edits:** it's decided when the panel opens, from the player's current control scheme (`PlayerInput.currentControlScheme`: Keyboard, else the controller set). It doesn't change while open. Both sets always apply to the player, so a seat moving between keyboard and pad needs no re-apply.
- **`bindings.cfg`:**
  - Format: plain `key=value` lines, one per seat and set (`seat1.keyboard=…`, `seat1.controller=…`), as `settings.cfg` is.
  - Each value is the Input System's override JSON for that set.
  - Saved when the panel closes, including when the pause menu goes to the main menu with the panel open. A quit during a wait loses only that unfinished press.
  - An override whose binding no longer exists (after a future change to the defaults) is skipped silently.
- **Reset** clears the set's overrides. That restores the asset's and `KeyboardControls`' defaults; the alternates are never overridden.
- **Timing:**
  - A rebind completes on the press. The Player map is off while paused, so nothing drives from it then.
  - On resume, the map comes back with the new bindings; a key still held from the rebind doesn't fire until it's pressed again (the Input System's normal behaviour on enable). The builder should check that in the tests.

## Non-goals

- Rebinding menu buttons, Pause, or the sticks.
- More than one key per action in the screen (the alternates aren't shown or edited).
- A title-screen copy of the panel (only the pause menu, as asked).
- Rebinding for the keyboard's second player (there's only one keyboard player).
- Changing the loading screen's controls art.

## Anti-goals

- A player locked out: unable to drive, unpause, or leave the panel. Back always works; Pause is fixed; swaps never leave an action unbound.
- One player's change reaching another player's seat.
- Bindings lost or the game failing to start because `bindings.cfg` is damaged.

## Constraints

- Unity 2022.3, Input System 1.14 (`PerformInteractiveRebinding`, binding overrides).
- Code in plain, tested rules where possible: which bindings a set covers, swap, load and save parsing. The project's habit is Edit Mode tests with no Play Mode unless needed (see `docs/testing.md`).
- CRLF files are edited with the Edit tool only. New `Assets/` files get a `.meta` (`tools/newmeta.sh`).
- Short, plain user-facing text in the game's style (Bangers, capitals for headers).

## Left to the builder

- The panel's exact layout and sizes (follow the pause menu's font and the Dispatch art), and how it's parented in each player's pause canvas.
- How the panel reaches the player's `PlayerInput` and seat.
- How the wait for a press is wired: `InputActionRebindingExtensions.PerformInteractiveRebinding` with control-path filters is expected.
- How `MenuInteractions` routes input while the panel is open (a new pause sub-state, like `DispatchMenu`'s rules), as long as Pause and Back behave as above.
- Docs: `docs/controls.md` (the screen, the file, the loading-screen limit) and `docs/testing.md`.

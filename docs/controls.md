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

## Keyboard (one player)

One player can use the keyboard, alongside up to 3 controllers. **Press Space or Enter to join.** Any other key shows "Press Space to play with the keyboard, or connect a controller". This stops the dev keys (F1 and others) from joining the keyboard by accident.

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
| Loading screen: ready | Space or Enter |

- **Button prompts still show controller buttons:** read A as Space, B as Backspace and Start as Escape. Keyboard prompt art is still to come.
- **The mouse isn't used.**
- **A lost controller:** if a player's controller is unplugged mid-match, only another controller can take their place, not the keyboard.
- **Dev hotkeys:** in the editor and in development builds, some letters double as dev hotkeys (`docs/dev-hotkeys.md`): S skips the tutorial, B refills boost, R respawns everyone, and T drops everyone's orders. Drive with the arrows when testing there. Release builds have no dev hotkeys.
- **How it works:**
  - The keys are added in code, in memory, when the game starts (`KeyboardControls`); the input actions asset on disk is unchanged.
  - The asset's unused Keyboard&Mouse scheme is swapped for a keyboard-only one.
  - In the editor, the asset is put back when Play Mode ends.

## Display settings in settings.cfg

The Options screen has rows for music, sound effects, fullscreen and (once its art exists) quality. The display settings below have no rows yet: their hand-lettered art is still to come. Until then they're set in `settings.cfg`:

- **Where:** `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\settings.cfg`.
- **When:** the game reads it at launch.
- **What:** one `key=value` per line. Leave a key out to keep its default.

| Key | Values | Default | What it does |
|---|---|---|---|
| `fullscreen` | 0, 1 | 1 | 0 windowed, 1 fullscreen (the Options row) |
| `exclusive` | 0, 1 | 0 | Fullscreen only: 0 borderless, 1 exclusive |
| `width`, `height` | 0, or a size | 0 | 0 means the desktop's size |
| `vsync` | 0, 1 | 1 | VSync off or on |
| `framecap` | 0, or 30–500 | 0 | The most frames per second; 0 means no cap. It only applies with VSync off. |

How a saved size is used (`DisplayRules`):
- **Fullscreen:** a size the monitor doesn't list falls back to the desktop's.
- **Windowed:** the size is shrunk to fit inside the desktop. With no size saved, the window is 1280×720, or the desktop's size if that's smaller.

The defaults match how the game ran before these settings existed: borderless at the desktop's size, VSync on, no cap. A Steam Deck gets the same, at its 1280×800 screen.

In the editor these settings aren't applied (the Game view ignores them), so check them in a build.

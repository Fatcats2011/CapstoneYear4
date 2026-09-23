# Developer hotkeys

Every key below goes through `DevTools.GetKeyDown`: it works in the Unity editor and in **Development Build** players, and does nothing in release builds (`DevTools.Enabled` is false there).

## Testing without enough controllers

| Key | Where it works | What it does | Code |
|---|---|---|---|
| F1 | Player select (the title screen only lets player 1 in) | Plugs in a virtual controller ("DoA Test Pad") and presses A on it, so it joins as the next player — or, while a player's controller is missing mid-match, takes over that player | `TestPlayers.Add` via `HotKeys.cs` |
| F2 | Anywhere | Presses A on every test controller (ready up, confirm the loading screen) | `TestPlayers.PressSouthOnAll` |
| F3 | Anywhere | Unplugs the newest test controller; press again to plug it back in (tests the disconnect pause and "reconnect" message) | `TestPlayers.ToggleNewest` |
| F4 | Anywhere | Cycles graphics quality Low → Medium → High (not saved; logged to the Console) | `GraphicsQuality.Apply` via `HotKeys.cs` |

Test controllers are removed when Play Mode ends. They don't move: steer with your real controller as P1.

The title screen only lets player 1 join: join with your controller (or F1), pick Play, then press F1 in player select for each extra player.

Any key press, F-keys included, also counts as a keyboard trying to join, so on the title screen and in the lobby the "Connect a controller to play" hint appears for a few seconds. That's expected.

## Match and flow shortcuts (from the original team)

| Key | Where it works | What it does | Code |
|---|---|---|---|
| 1 / 2 | During a match | Previous / next wave | `HotKeys.cs` → `OrderManager` |
| 4 | Anywhere | Loads the game scene | `HotKeys.cs` |
| 5 | During a match | Jumps to wave 3 | `OrderManager.cs` |
| Y | During a match | Deletes every active order | `OrderManager.cs` |
| P | During a match | Pauses / resumes the wave timer | `OrderManager.cs` |
| S | Tutorial | Skips the tutorial (every player) | `TutorialManager.cs`, `TutorialHandler.cs` |
| L | Cutscenes | Skips the cutscene | `CutsceneManager.cs` |
| L | Main menu | Removes every joined player | `MainMenu.cs` |
| B | Driving | Refills every player's boost | `BallDriving.cs` |
| T | Driving | Every player drops the orders they carry | `OrderHandler.cs` |
| R | Driving | Respawns every player | `Respawn.cs` |

## Adding a hotkey

- Read keys with `DevTools.GetKeyDown(KeyCode.X)`, never `Input.GetKeyDown` — the EditMode test `DevToolsTests.GameplayScripts_ReadDebugKeysOnlyThroughDevTools` fails otherwise.
- Add the key to this list.

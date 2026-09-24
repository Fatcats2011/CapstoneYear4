# The player prefab

A player is two prefabs in `Assets/Prefabs/Player Prefabs/` (since Phase 2C, 2026-09-23):

- **`PlayerAvatar.prefab`** is the scooter every machine sees.
  - `Ball Of Fun`: the rolling sphere (`Rigidbody`, `Respawn`, `CanKicker`).
  - `Control`: `BallDriving`, `OrderHandler`, `SoundPool`, `PhaseIndicator`, `DrivingIndicators`, `CompassMarker`, trails, headlight, the scooter and ghost models, particles.
  - Online (Phase 3), another machine's player will be this prefab alone.
- **`Player - Cinemachine.prefab`** is a player on this machine. The menu scene's Player Input Manager spawns it when a controller joins.
  - Its root holds the controller (`PlayerInput`, `InputManager`, `PlayerUIHandler`) and `PlayerCameraResizer`.
  - Its first child is a `PlayerAvatar`. It adds its own parts to that avatar's `Control` as prefab overrides:
    - children: `Camera Holder` (cameras, Cinemachine rigs), `Driving Canvas`, `Menu Canvas`, `Compass Calc`;
    - components: `OrbitalCamera`, `Compass`, `DynamicNumberUI`, `TutorialHandler`, `Rumbler`.
  - They sit on the scooter's `Control`, so the scooter's scripts find them as before.

At runtime both top objects are named `P1`–`P4`: the golden round's leaderboard and the results screen show the scooter's top object's name.

## Editing

- Scooter look, physics, sounds and effects: open `PlayerAvatar.prefab`.
- Cameras, HUD, menus and controls: open `Player - Cinemachine.prefab`. The view's parts show as overrides (a green +) on the nested `PlayerAvatar`.
- Never apply the view's added parts to `PlayerAvatar.prefab` (Overrides → Apply All, or Apply to Prefab 'PlayerAvatar'): every avatar would get cameras and menus.
- Never revert the nested `PlayerAvatar` (Overrides → Revert All): the local player would lose its cameras and menus.
- `PlayerPrefabTests` (Test Runner → EditMode) fails if either happens.
- Online, another machine's player is `PlayerAvatar.prefab` alone (`RemoteAvatar`), dressed by path (`ScooterLook`).
  - The paths lead to the scooter body, logos, ghost, eyelids and hat.
  - Renaming or moving one of those means updating `ScooterLook`'s paths; `ScooterLookTests` fails until then.

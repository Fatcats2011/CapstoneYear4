# Third-Party Licenses & Asset Provenance

Inventory for the Steam release (2026-09-22). "Used" = referenced by a build scene, prefab, material or asset.

**Status:** ✅ license found and allows commercial use · ⚠️ confirm before release · ❌ remove before release

## Code, packages and tools

| Component | Location | License | Status | Notes |
|---|---|---|---|---|
| Unity engine + Unity packages (URP, Input System, TextMesh Pro, Timeline, VFX Graph, Shader Graph, AI Navigation, Post Processing, Visual Scripting) | `Packages/manifest.json` | Unity Terms of Service / Unity Companion License | ✅ | Unity Personal has a revenue/funding cap ($200K when last checked) — confirm current terms for whoever ships the build. |
| Cinemachine 2.1 (embedded source) | `Assets/Cinemachine` | Unity Companion Cinemachine Code License 1.0 (`Assets/Cinemachine/LICENSE`) | ✅ | Unity-only use. |
| DOTween (free) | `Assets/Plugins/Demigiant` | DOTween license — http://dotween.demigiant.com/license.php | ⚠️ | Author allows commercial use of the free version; keep the copyright notice. |
| Udar SceneField | `Assets/Plugins/Udar` | "© 2022 Udar Ltd." — no license text in repo | ⚠️ | Likely an Asset Store package → Asset Store EULA; confirm. |
| Noisy Nodes | `Packages` (git: JimmyCushnie/Noisy-Nodes) | WTFPL v2 | ✅ | |
| Steamworks.NET 2025.164.1 | `Packages` (git: rlabrecque/Steamworks.NET) | MIT | ✅ | Wrapper code. |
| Netcode for GameObjects 1.15.1 | `Packages` (Unity registry) | MIT (© Unity Technologies) | ✅ | Online play (Phase 3). |
| Unity Transport 1.5.0, Collections 1.2.4, Mono Cecil 1.11.6 | `Packages` (installed with Netcode) | Unity Companion License | ✅ | |
| SteamNetworkingSockets transport (community) | `Packages` (git: Unity-Technologies/multiplayer-community-contributions, pinned `d862504b`) | MIT | ✅ | Keep the copyright notice. |
| ParrelSync 1.5.3 | `Packages` (git: VeriorPies/ParrelSync) | MIT | ✅ | Editor only; not in builds. |
| Steamworks SDK runtime (`steam_api64.dll` etc., shipped inside Steamworks.NET) | build `Plugins` folder | Steamworks SDK Access Agreement | ✅ | Redistributable with a Steam game; covered by your Steamworks partner agreement. |
| OToon – URP Toon Shading | `Assets/OToon- URP Toon Shading` | Unity Asset Store EULA | ⚠️ | Confirm who bought it; Asset Store tool licenses can be per seat. |
| OToon demo content (Unity-chan) | `Assets/OToon- URP Toon Shading/Demo(Can be delete)` | Unity-Chan License | ❌ | Not used by the build scenes — delete before release. |
| TextMesh Pro examples | `Assets/Plugins/TextMesh Pro/Examples & Extras` | Unity Companion License | ✅ | Not needed at runtime; safe to delete. |

## Fonts

| Font | Used by | License in repo | Status |
|---|---|---|---|
| Sobiscuit (`Assets/Fonts/Sobiscuit.otf`) | Driving Canvas, player prefab, Results Canvas | none | ⚠️ **High** — confirm a commercial license |
| jcandlestick (`Assets/Fonts/jcandlestick.ttf`) | Driving Canvas, Loading Screen, player prefab | none | ⚠️ **High** — confirm a commercial license |
| Distress, Lazy Sans, Yagitudeh | unused | none | ❌ delete (or license if you start using them) |
| Liberation Sans (TMP default) | TMP fallback | SIL Open Font License | ✅ |

## Audio

- `Assets/Audio/Music`, `SFX`, `Closed Playtest` — credits list composers Nicole Mellor and Shaun Mellor. ⚠️ Confirm which files are original and which came from sound libraries, and get written permission for commercial use.
- `Assets/Audio/TEST/Car Tire Screech Sound Effect  No Copyright Sound effects.mp3` — unknown source, **unused**. ❌ delete.

## Art, models, textures

- ⚠️ Team-made (confirm): `Models`, `Textures`, `Sprites`, `Animations`, `Terrain`, `Materials`, `DoA-Mats-Ryan-DesignerProjFiles`.
- ⚠️ Unknown origin, no license files: `Assets/FlowerPot`, `Assets/Podium`, `Assets/Grass`.

## Brand names

- ⚠️ The four delivery companies parody real apps: Door Death (DoorDash), Ghostmates (Postmates), Skip The Scare (SkipTheDishes), Uber Creeps (Uber Eats), with logos on the scooters (`scooterDecalMaterial`). Get a legal opinion or rename/redraw before a commercial release.

## People

Written agreement (ownership, revenue split, credits) needed from everyone in the in-game credits: Alex Fischer, Max Moverley, Ryan Carolan, Dominic Momich, Logan Moss, Taria Tuuri, Will Doran, Marcus Werner, Naym Salam, Nicole Mellor, Shaun Mellor. The credits also name an instructor (Michael Ferguson) — check the school's student-IP policy.

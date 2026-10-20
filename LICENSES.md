# Third-Party Licenses & Asset Provenance

Inventory for the Steam release (2026-09-22). "Used" = referenced by a build scene, prefab, material or asset.

**Status:** ✅ license found and allows commercial use · ⚠️ confirm before release · ❌ remove before release

## Code, packages and tools

| Component | Location | License | Status | Notes |
|---|---|---|---|---|
| Unity engine + Unity packages (URP, Input System, TextMesh Pro, Timeline, VFX Graph, Shader Graph, AI Navigation, Post Processing, Visual Scripting) | `Packages/manifest.json` | Unity Terms of Service / Unity Companion License | ✅ | Unity Personal has a revenue/funding cap ($200K when last checked) — confirm current terms for whoever ships the build. |
| Cinemachine 2.1 (embedded source) | `Assets/Cinemachine` | Unity Companion Cinemachine Code License 1.0 (`Assets/Cinemachine/LICENSE`) | ✅ | Unity-only use. |
| DOTween (free) | `Assets/Plugins/Demigiant` | DOTween license — http://dotween.demigiant.com/license.php | ✅ | Checked 2026-10-20: free for commercial use, no attribution needed in the game. Don't modify and redistribute it; keep `readme.txt` with it in the repo. |
| Udar SceneField | `Assets/Plugins/Udar` | "© 2022 Udar Ltd." — no license text in repo | ⚠️ low | Used by `SceneManager` and `SplashScreen`. Matches the free "Scene Field" Asset Store package (v1.1.0), which falls under the Standard Asset Store EULA (fine for games). Confirm the publisher on its listing, or swap it for our own small scene-name field (needs editor re-wiring). |
| Noisy Nodes | `Packages` (git: JimmyCushnie/Noisy-Nodes) | WTFPL v2 | ✅ | |
| Steamworks.NET 2025.164.1 | `Packages` (git: rlabrecque/Steamworks.NET) | MIT | ✅ | Wrapper code. |
| Netcode for GameObjects 1.15.1 | `Packages` (Unity registry) | MIT (© Unity Technologies) | ✅ | Online play (Phase 3). |
| Unity Transport 1.5.0, Collections 1.2.4, Mono Cecil 1.11.6 | `Packages` (installed with Netcode) | Unity Companion License | ✅ | |
| SteamNetworkingSockets transport (community) | `Packages/com.community.netcode.transport.steamnetworkingsockets` (embedded copy of Unity-Technologies/multiplayer-community-contributions `d862504b`, patched: `CHANGES-DOA.md`) | MIT | ✅ | Keep the copyright notice (`LICENSE.md` in the folder). |
| ParrelSync 1.5.3 | `Packages` (git: VeriorPies/ParrelSync) | MIT | ✅ | Editor only; not in builds. |
| Steamworks SDK runtime (`steam_api64.dll` etc., shipped inside Steamworks.NET) | build `Plugins` folder | Steamworks SDK Access Agreement | ✅ | Redistributable with a Steam game; covered by your Steamworks partner agreement. |
| OToon – URP Toon Shading | `Assets/OToon- URP Toon Shading` | Unity Asset Store EULA | ⚠️ | Confirm who bought it (it shows under their Asset Store "My Assets"). The listing is deprecated (2026-10-20 check): it can't be bought any more, but owners keep their rights. If no team member owns it, the toon look must be replaced. |
| OToon demo content (Unity-chan) | `Assets/OToon- URP Toon Shading/Demo(Can be delete)` | Unity-Chan License | ❌ | **Don't delete the whole folder** (corrected 2026-10-20): the game's own materials (scooters, packages, ghosts, trees, hats) use 7 OToon textures from it: `line.png`, `noise_c.png` and 5 `*_RampTex.png`. Those are OToon's own textures, not Unity-chan. Move them to a folder of our own in the editor (references survive), then delete the rest. |
| TextMesh Pro examples | `Assets/Plugins/TextMesh Pro/Examples & Extras` | Unity Companion License | ✅ | Not needed at runtime; safe to delete. |

## Fonts

| Font | Used by | License in repo | Status |
|---|---|---|---|
| Sobiscuit (`Assets/Fonts/Sobiscuit.otf`) | Driving Canvas, player prefab, Results Canvas | none | ⚠️ **High** — buy the **App / Game** license (one game) from Khurasan: https://khurasanstudio.com/license/ (contact khurasantype@gmail.com). The font file says "© 2023 Khurasan. All rights reserved." |
| jcandlestick (`Assets/Fonts/jcandlestick.ttf`) | Driving Canvas, Loading Screen, player prefab | none | ⚠️ **High** — "Jack's Candlestick" by Iconian Fonts (Daniel Zadorozny). Commercial use: email iconian@aol.com for consent, then pay $20 per seat: https://www.iconian.com/commercial.html. Embedding in a game is allowed; selling the font itself isn't. |
| Distress, Lazy Sans, Yagitudeh | unused (no scene, prefab or material refers to their SDF assets; checked 2026-10-20) | none | ❌ delete (or license if you start using them) |
| Liberation Sans (TMP default) | TMP fallback | SIL Open Font License | ✅ |

## Audio

- `Assets/Audio/Music`, `SFX`, `Closed Playtest` — credits list composers Nicole Mellor and Shaun Mellor. ⚠️ Confirm which files are original and which came from sound libraries, and get written permission for commercial use.
- `Assets/Audio/TEST/Car Tire Screech Sound Effect  No Copyright Sound effects.mp3` — unknown source, **unused**. ❌ delete.
- ⚠️ **High: 14 placeholder sounds from `Assets/Audio/TEST/` play in the shipped game** (found 2026-10-20). Each one's Audio Object is listed in brackets.
  - `659618__zlgreenwood__volcanic-perk-o-lator1.wav` (Phase): Freesound, **CC0** ✅.
  - `495334__kirkpatricklive__coach_whistle_02_hard_stop.wav` (TimeOver): Freesound, but its page is gone (404), so the license can't be checked any more. ❌ replace it.
  - `Game Object Pick Up Sound Effect.mp3` (Pickup), `Level Up Sound Effect.mp3` (Boost Charged), `Whoosh sound effect.mp3` (Steal): titles like YouTube "free sound effect" uploads, origin unknown. ❌ replace unless someone finds the source and license.
  - `MenuUI/Enter_1`, `Pause`, `Return_1`, `Scroll_1` (UI sounds), `angry.wav`, `joy.wav`, `laugh.mp3`, `sigh.mp3` (emotes), `placeholder_miniboost.wav` (Mini Boost): origin unknown. ⚠️ Ask the Mellors whether they made them; replace any they didn't.
  - To swap one, point its Audio Object (`Assets/Audio/Audio Objects/...`) at the new clip.

## Art, models, textures

- ⚠️ Team-made (confirm): `Models`, `Textures`, `Sprites`, `Animations`, `Terrain`, `Materials`, `DoA-Mats-Ryan-DesignerProjFiles`.
- ⚠️ Unknown origin, no license files: `Assets/FlowerPot`, `Assets/Podium`, `Assets/Grass`.

## Brand names

- ⚠️ The four delivery companies parody real apps: Door Death (DoorDash), Ghostmates (Postmates), Skip The Scare (SkipTheDishes), Uber Creeps (Uber Eats), with logos on the scooters (`scooterDecalMaterial`). Get a legal opinion or rename/redraw before a commercial release.

## People

Written agreement (ownership, revenue split, credits) needed from everyone in the in-game credits: Alex Fischer, Max Moverley, Ryan Carolan, Dominic Momich, Logan Moss, Taria Tuuri, Will Doran, Marcus Werner, Naym Salam, Nicole Mellor, Shaun Mellor. The credits also name an instructor (Michael Ferguson) — check the school's student-IP policy.

## What to do before release (checked 2026-10-20)

- **Buy:** the Sobiscuit App / Game license (Khurasan) and the Jack's Candlestick commercial license (Iconian, email first, then $20 per seat).
- **Confirm:**
  - Who owns OToon on the Asset Store.
  - The SceneField publisher (low risk).
  - Which audio is original (the Mellors), plus their written OK.
  - The origin of `FlowerPot`, `Podium` and `Grass`.
- **Replace:** the TEST placeholder sounds that play in the game (Audio, above): at least the whistle and the three "Sound Effect" mp3s.
- **Delete:** the OToon demo (after moving its 7 used textures out), the three unused fonts and the unused TEST audio files. `docs/unused-assets.md` (Approval checklist) lists these with the rest of the unused art.
- **Decide:** the parody brand names and logos (legal opinion, or rename them).
- **Sign:** every person in the People list.

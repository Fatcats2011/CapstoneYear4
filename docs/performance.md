# Frame rate (Phase 4D)

The goal is 120 FPS, or as high as the game can go, with 1 to 4 split-screen players.
- **Code:** changes that cost nothing visible are done.
- **Editor:** the biggest remaining gains are asset and settings changes in the Unity editor. They're listed below in the order to try them, each measured on its own.
- **Art:** after those, what's left is art.

## Measuring

- **The overlay:** F6 shows the frame rate, frame time, the 1% low, the player count and the number of cameras rendering, in a box at the top left. It works in the editor and development builds (`PerfOverlay`).
- **The log line:** every build logs a line every 30 s, for example `Perf: 4 players, 118 fps average, 92 fps 1% low, 13 cameras`.
  - It goes to `Player.log`: `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\`.
  - With the `-sessionlog` launch option it also goes to the session log.
- **A fair test:** a 4-player match.
  - In the editor or a development build, press F1 three times in player select.
  - Drive for a minute in the first wave, then read the last `Perf:` line.
  - Do it at High and at Medium (F4 cycles the quality level).
- **VSync:** VSync is on by default, so the frame rate stops at the monitor's refresh rate. 120 FPS needs a 120 Hz or faster monitor, or `vsync=0` in `settings.cfg` (`docs/controls.md`). The `Perf:` line shows the real frame rate either way.
- **Editor vs build:** the editor itself costs frames, so measure a build for the real number.

## What the code does now

- **Cameras:**
  - Each player's view stops rendering while a full-screen camera covers it: the opening and golden cutscenes, and the results. Before, all four views rendered under the cutscene.
  - The player-select preview camera renders only in player select. Before, it also ran during each match's first seconds and the results.
  - Rule and code: `CameraBudget`, applied in `PlayerCameraResizer.LateUpdate`.
- **Quality levels** (`GraphicsQuality`, picked in Options or with F4) scale with the number of players on this machine:

  | | High | Medium | Low |
  |---|---|---|---|
  | Shadow distance | 150 m | 100 m | 60 m |
  | Shadow cascades | as authored (4) | 2 | 1 |
  | Main shadow map | as authored (4096); 2048 with 3–4 players | 2048 | 2048 |
  | Extra-light shadows | off (those lights are baked) | off | off |
  | SSAO | on | on with 1–2 players, off with 3–4 | off |
  | MSAA | as authored | up to 4× | off |
  | Render scale | 1 | 1 | 0.8 |

  Nothing goes above what the URP asset was authored with. In the editor, the asset and its renderers are put back when Play Mode ends.
- **Per-frame scripts:**
  - **Boosting** no longer starts a coroutine every frame (`FovHold`).
  - **The compass** sorts in place, moves an icon only when its order changes, and rewrites a distance only when it changes.
  - **Boost pads** stop allocating a materials array per pad per player per frame.
  - **Physics triggers:**
    - cutouts no longer throw and catch exceptions every physics step;
    - beacons no longer read object names every step.
  - **HUD writes:**
    - the timer and horn sliders are written only when they change;
    - the scooter's drag and speed lines likewise.
  - **Pedestrians'** animators stop animating off-screen.
  - **Online:** other players' scooters are looked up once, not every frame.

## Editor changes, in order

Make one change, then measure again (a 4-player match, then the `Perf:` line). Each change is an asset or Player Settings edit. Check the look after each.

1. **Forward rendering for the players' views.**
   - In each of `Assets/Rendering/Player 1 - Main Renderer` to `Player 4 - Main Renderer`:
     - Rendering Path: Deferred → Forward;
     - Depth Priming Mode: Disabled.
   - In `Assets/Rendering/URP Asset`: Anti-aliasing (MSAA) 8× → 4×. Deferred never used MSAA; Forward does.
   - **Expected gain:** the largest single one, since each view now skips a G-buffer pass and a forced depth pass.
   - **Check:** the toon outlines, the phase effect and decals.
2. **SSAO's own cost.**
   - In each Main renderer's Screen Space Ambient Occlusion feature: Downsample on, Sample Count Low, Blur Quality Medium.
   - Remove the feature from the four `Player N - Phase Renderer` assets, where it only shows for a moment.
3. **Shadows in the URP asset:**
   - Max Distance 150;
   - Cascade Count 2;
   - Main Light Shadow Resolution 2048;
   - Additional Lights → Cast Shadows off.

   The code already does this at runtime; changing the asset makes it the authored look too.
4. **HDR.** In the URP asset, HDR off, if the bloom still looks right.
5. **One bloom.** The `Main Volume` profile has both Bloom and BenDayBloom. Keep the one the art wants, and turn the other off.
6. **Player Settings → Other Settings:**
   - **Scripting Backend:** IL2CPP. Scripts run roughly 1.5–3× faster. It needs the Windows Build Support (IL2CPP) module and Visual Studio's C++ tools.
   - **Graphics Jobs:** on. Test both settings; with 4 split-screen views it usually helps.
7. **Animator culling on the prefabs.** `Civilian` prefabs and `PlayerAvatar`: Culling Mode → Cull Update Transforms. The code sets this at runtime for pedestrians already.
8. **The `Driving Canvas` prefab:**
   - put the compass under its own nested Canvas, so its moving icons don't rebuild the whole HUD;
   - turn off Raycast Target on images and text nobody clicks.
9. **Static scenery.** Mark buildings and props that never move as Static, then rebake occlusion culling in each match scene. All three match scenes share one occlusion data asset today.

## Art, after that

- LOD groups on heavy props: there are none today.
- The toon shader's options (halftone, hatching, rim): each adds per-pixel work on every view.
- `Models/Modular Kit/Market01.fbx` (15 MB): check its polygon count.

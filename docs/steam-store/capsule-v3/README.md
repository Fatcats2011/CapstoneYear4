# Main capsule v3 (2026-11-16)

New Steam main-capsule concepts (1232x706), built in Blender from the game's own models and prefabs. The user picked **The Steal** (2026-11-16); the hats, glow and yellow ghost were then fixed.

| File | What it is |
|---|---|
| `main_capsule_hero_1232x706.png` | **The Steal** (recommended): a tug-of-war mid-race: orange snatches the game's yellow package (MediumPackage) by its handle while purple holds on from underneath; purple is bare-headed, yellow wears the top hat, two rivals chasing, clock tower + moon, street motion blur |
| `main_capsule_golden_1232x706.png` | **Golden Order showdown** (alternate): four couriers drift in on the glowing golden parcel |
| `*_render_2x.png` | Clean renders without the logo or post effects (2464x1412) |
| `iterations/` | Progress sheets and a small-size readability check |

## Photoshop

`main_capsule_hero_1232x706.psd` is the hand-off for tweaks in Photoshop: the same image as the PNG, with every post step on its own layer (bottom to top): **Render (Blender)**, **Bloom** (Screen), **Vignette** (Multiply), **Ground Shade** (Multiply, darkens the near pavement), **Contrast** and **Vibrance** adjustment layers, **Logo Shadow**, **Logo**. The document is already Steam's 1232x706, so export it as PNG at that size. Rebuild it after a new render with `python -P tools/keyart/to_photoshop.py` (writes the layer PNGs to `photoshop/`) and then run `tools/keyart/to_photoshop.jsx` in Photoshop (File > Scripts > Browse).

### Polish pass

`tools/keyart/polish.jsx` (after `python -P tools/keyart/polish_layers.py`) adds, on the open PSD: **Render Sharpen** (unsharp copy of the render), **Focus Light** (Soft Light 28%, lifts the riders and package), **Curves** (gentle S), **Color Balance** (violet shadows, warm highlights) and **Grain** (Overlay 11%). It saves the PSD and exports `main_capsule_hero_1232x706_polished.png`, the version to upload. Backups: `_prepolish.psd`, `_your_edits_backup*.psd`.

## Rebuild

- Scene: `blender -b --factory-startup --python tools/keyart/build_capsule_v3.py -- <hero|golden> <out.png> 200 96 <save.blend>`
- Logo + bloom: `python -P tools/keyart/composite_v3.py <render.png> <out.png> 0.25 0.30 0.42 0.6 - 0.35` (the last number darkens the near pavement; golden: `0.25 0.27 0.40`)
- Shot numbers live in `HERO` / `GOLD` at the top of each concept in `build_capsule_v3.py`. Saved scenes: `tools/keyart/capsule_v3_hero.blend`, `capsule_v3_golden.blend`.
- Run Python with `-P`: `tools/keyart/inspect.py` shadows the standard library's `inspect`.

## Notes

- Hats are the game's own (mesh, material, and the per-hat offset/rotation/scale from `Scriptable Ojects/Player Hats`), seated on the rigged `Official_Anim_4.fbx` Head bone and carried onto the capsule ghosts by lining up horns and eye whites (`capsule_kit.game_hat`). The Bucket Hat can't be placed this way (its model carries a large built-in offset); the Golden alternate still uses the old hand-placed hats.
- Hats are the game meshes unchanged, with their own shading normals (an earlier bevel/subdivision pass was dropped: it changed their shapes). A small neutral light on each hat (`hat_light`) keeps the night's purple from staining their colours. Ghost bodies are 90% opaque (purple 95%) (`GHOST_ALPHA`, blended, back faces culled); anything bright behind a ghost shows through, which is why the nearest right-hand street lamp is left out.
- The package's colour is converted from Unity's sRGB tint (the importer passes .mat tints through as linear, which fades the yellow to cream), and it casts no shadow (its cut-out shadow stippled everything near the flame light).
- Review fixes (2026-11-16): bigger bag in open space, purple's right hand off its grip onto the bag's handle (on the grip it sat in front of its face; laid on the bag's side it sank into it), orange turned so its handlebar sits under its eyes, camera pulled back and nudged so orange clears the right edge, slower street blur (cleaner top-left), wheels spin across the shutter (`spin_wheels`), soft contact shadows under grounded scooters (`ground_shadow`), small headlight glows (`headlight_glow`), tighter green halo, brighter clock hands.
- Each ghost glows softly in its own colour (`GHOST_GLOW` 0.3 on the body): a soft halo behind it and a light of that colour (`ghost_glow`).
- The logo is `Assets/Sprites/DoA-Logo_02-08-24.png`, added in post, never rendered.
- Scooters use the four companies' colours but no company logos (the names are still under legal review).
- The clock tower reads 3:48, a faint cool light washes its face (`HERO["clock_light"]`), and its middle windows are unlit (`window_glow=False`: the tower texture's glow map lit them green and they showed through purple) (`tower(..., time=(3, 48))` swings the camera-side hands round the tick ring's hub). The package is turned 180 degrees about the upright.
- Orange holds the bag from underneath: its hand is laid flat on the bag's bottom (`capsule_kit.grab` ray-casts along the tilted bag's own down axis and turns the hand to the surface normal). The purple-flame sprites (`flame_sprite.py`, `flame_cards`) are no longer used in this shot.
- If one is picked, the GDD's Fig 1 ("current key art, also used as the Steam main capsule") should be swapped for it.

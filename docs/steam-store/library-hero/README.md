# Steam Library Hero (2026-11-16)

The Steal, re-shot for the Library Hero: **`library_hero_3840x1240.png`** is the file to upload (Steam makes the 1920x620 half-size itself).

| File | What it is |
|---|---|
| `library_hero_3840x1240.png` | Upload this. No text or logo (Steam lays the Library Logo on separately). |
| `library_hero_1920x620_preview.png` | Half-size preview, as Steam will generate |
| `library_hero_safe_area_check.png` | Preview with the 860x380 safe area outlined |
| `library_hero_render_3840x1240.png` | Clean render before the post pass |

## Notes

- Same street, riders, hats, package, clock (3:48) and moon as the main capsule, seen from further back on a longer lens (`LIBRARY` in `tools/keyart/build_capsule_v3.py`), so purple's and orange's faces and the package sit inside the centre safe area and the street runs out to both edges.
- Yellow rides on the road on the right (mid-air, as in the capsule, it looked stranded in this wide shot) and looks at the package (`look_at_bag`); green rides on the left, as before.
- Wall lanterns on the houses in front of the riders are removed (`strip_near_lamps_y`): in this wider shot they showed as big gold frames in the top corners.
- Post pass: `composite_v3.py` with no logo (bloom, vignette, ground shade, contrast, colour), no Photoshop polish layers yet.
- `library_hero_3840x1240.psd`: the same as Photoshop layers (Render, Bloom, Vignette, Ground Shade, Contrast, Vibrance), built by `python -P tools/keyart/to_photoshop.py <render> docs/steam-store/library-hero/photoshop 3840x1240 nologo` then `tools/keyart/to_photoshop_library.jsx`.
- Rebuild: `blender -b --factory-startup --python tools/keyart/build_capsule_v3.py -- library <render.png> 100 96 tools/keyart/library_hero.blend`, then `python -P tools/keyart/composite_v3.py <render.png> library_hero_3840x1240.png 0 0 0 0.6 - 0.35 3840x1240`.

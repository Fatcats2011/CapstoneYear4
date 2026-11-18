# App icon (2026-11-16)

Enhancement of the user's own 256x256 icon (designed in Photoshop as "Untitled-2").

| File | What it is |
|---|---|
| `app_icon_enhanced_256.png` | Enhanced icon, transparent corners |
| `app_icon_enhanced.psd` | Layered: the user's layers plus Depth, Light, Sky, Logo Shadow and Rim |
| `app_icon_original.png`, `app_icon_original_backup.psd` | The icon as it was before |
| `app_icon_before_after.png` | Before/after, also at 64 and 32 px |

## Changes

- White Background hidden, so the rounded corners are transparent.
- Logo smart object scaled to 92% of the width and centred: it used to run past both edges (clipping the D and the L).
- Added layers, all cut to the rounded square: **Sky** (moon and soft stars in the empty top band, Screen), **Light** (soft light from the top left, Soft Light 25%), **Depth** (darker toward the edges and bottom, Multiply 70%), **Logo Shadow** (soft drop shadow) and **Rim** (thin bevel: light top edge, dark bottom edge).
- Rebuild: `tools/keyart/icon_masks.jsx` (in Photoshop), `python -P tools/keyart/icon_layers.py`, then `tools/keyart/icon_enhance.jsx`.

At 32 px the wordmark can't be read in either version; a simpler mark (the ghost on its scooter) would suit the smallest sizes.

# App icon, 184x184 (2026-11-16)

The user's 184x184 icon ("Untitled-4" in Photoshop; Steam's community icon size), given the same enhancement as the 256 icon in `../app-icon/`.

| File | What it is |
|---|---|
| `app_icon_enhanced_184.png` | Enhanced icon |
| `app_icon_enhanced.psd` | Layered: the user's layers plus Depth, Light, Sky, Logo Shadow and Rim |
| `app_icon_184_original.png`, `app_icon_184_original_backup.psd` | The icon as it was before (with the 256 icon's layers pasted in) |
| `app_icon_184_before_after.png` | Before/after, also at 64 and 32 px |

- The 256 icon's enhancement layers that had been pasted in didn't fit this canvas (they drew a dark rounded frame inside it), so they were replaced with ones built for 184x184.
- This icon's gradient fills the whole square (no rounded corners), so the layers fill it too.
- The logo was left at the user's size (it runs slightly past the edges), as the user had scaled the 256 icon's logo back up.
- Rebuild in Photoshop, with `var ICON_DOC='Untitled-4'; var ICON_OUT='<this folder>/'; var ICON_FIT=0;` put in front of `tools/keyart/icon_masks.jsx`, then `python -P tools/keyart/icon_layers.py <this folder>/work`, then `icon_enhance.jsx` with the same prefix.

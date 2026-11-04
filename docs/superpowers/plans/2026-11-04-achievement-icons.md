# Achievement Icons Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Draw the 9 Steam achievement icons as flat 2D badges, each with an unlocked and a locked JPG.

**Architecture:** One Pillow script draws each badge at 1024 px from shared parts (badge, parcel, ghost, hand), scales it to 256, and saves a colour and a grey copy. A pytest file checks the outputs.

**Tech Stack:** Python 3, Pillow 12, pytest.

**Spec:** docs/superpowers/specs/2026-11-04-achievement-icons-design.md

## Global Constraints
- 18 files in `docs/steam-store/achievements/`: `<API_NAME>.jpg` + `<API_NAME>_locked.jpg`, 256×256, RGB JPG.
- API names exactly: FIRST_DELIVERY, GOLDEN_WIN, TUTORIAL_DONE, STEAL, GOLDEN_STEAL, HARD_DELIVERY, WIN, LAST_TO_FIRST, FELL_IN_WATER (same as `Assets/Scripts/Steam/Achievement.cs`).
- Night-purple badge, logo-green glowing rim, white symbols with dark outline, gold parcel on GOLDEN_WIN / GOLDEN_STEAL.
- Fonts from `Assets/Plugins/TextMesh Pro/Examples & Extras/Fonts/` (Bangers / Anton).
- Python file is new: LF line endings are fine.

## Review Focus
- Readability at 64 px: every symbol must still be recognisable when downscaled. Check: a 64 px contact sheet, looked at by eye.
- Locked icons must be grey (no colour left), but still show the symbol. Test: locked image's channel spread ≤ 8 per pixel, and its contrast isn't flat.
- JPG corners: Steam shows the square; outside the round badge must be a solid dark colour, not white. Test: corner pixels are dark.
- Running from any folder: paths come from the script's own location.
- Re-runs overwrite cleanly with no leftover files from older names.

---

### Task 1: Icon script and outputs

**Files:**
- Create: `tools/achievement-icons.py`
- Create: `tools/test_achievement_icons.py`
- Output: `docs/steam-store/achievements/*.jpg`

**Interfaces:**
- Produces: `ICONS: dict[str, Callable[[ImageDraw, Image], None]]` keyed by API name; `render(name) -> Image` (1024 px RGB colour); `locked(img) -> Image`; `main()` writes all 18 files plus `docs/steam-store/achievements/_sheet.png` (64 px contact sheet, both versions).

- [ ] **Step 1: Write the failing tests** in `tools/test_achievement_icons.py` (load the script with importlib, since its name has a hyphen):
  - `test_all_nine_names` — `set(mod.ICONS) == {the 9 API names}`.
  - `test_files_written` — after `mod.main()`, each of the 18 files exists, opens as JPEG, mode RGB, size (256, 256).
  - `test_locked_is_grey` — for each `_locked.jpg`, max over pixels of `max(r,g,b)-min(r,g,b)` ≤ 8, and the stddev of luminance > 20.
  - `test_unlocked_has_colour` — each unlocked icon has some pixel with channel spread > 60.
  - `test_corners_dark` — the 4 corner pixels of every file have luminance < 60.
- [ ] **Step 2:** Run `python -m pytest tools/test_achievement_icons.py -q`. Expected: FAIL (module missing).
- [ ] **Step 3: Implement the script.** Draw at 1024, then `resize(256, LANCZOS)`; locked = `ImageOps.grayscale` darkened to about 60 % with the contrast kept, back to RGB. Glow rim = a blurred green ring under a solid one. Symbols per the spec.
- [ ] **Step 4:** Run the tests. Expected: PASS. Open `_sheet.png` and check that each icon reads at 64 px; fix any that don't.
- [ ] **Step 5: Commit** the script, the test and the 18 JPGs (not `_sheet.png`). Commit on steam-phase1a.

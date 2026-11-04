# Achievement icons (short design)

## Intent
Steam needs two 256×256 JPG icons for each of the game's 9 achievements (unlocked + locked) before they can go live in Steamworks. The user picked flat 2D badges over 3D renders: fast, and clear at the 64 px size Steam shows in pop-ups and friends lists.

## Approaches
- **A. 3D renders from game models** (Blender, like the capsule): matches the store art, but slowest, and posed scenes read badly at 64 px.
- **B. Flat 2D badges drawn in code** — **picked by the user.** Fast, crisp at small sizes; doesn't match the 3D capsule.
- **C. Mixed 3D/2D**: quicker than A, but inconsistent.

## Design
- One Python script, `tools/achievement-icons.py` (Pillow), draws all 18 files, so tweaks are a re-run, not hand edits. Drawn at 1024 px and scaled down for smooth edges.
- Output: `docs/steam-store/achievements/<API_NAME>.jpg` and `<API_NAME>_locked.jpg`.
- **Shared badge** (my call, from the logo's colours): deep night-purple round badge, a thick logo-green glowing rim, white symbols with a dark outline. Golden achievements use gold for the parcel.
- **Symbols** (my call; one bold shape each):
  - `FIRST_DELIVERY`: a parcel with a "1" tag
  - `GOLDEN_WIN`: a gold parcel with a crown
  - `TUTORIAL_DONE`: the horned ghost with an L-plate
  - `STEAL`: a parcel with a grabbing hand
  - `GOLDEN_STEAL`: a gold parcel with a grabbing hand
  - `HARD_DELIVERY`: a parcel with an "80" tag and a skull
  - `WIN`: a tombstone marked "1" (the undertaker)
  - `LAST_TO_FIRST`: a rising arrow from 4 to 1
  - `FELL_IN_WATER`: a fish with "Zzz" over waves
- **Locked versions:** the same icons in grey and darker, so players can still tell what each one is.
- Numbers use a bold font from the repo (Bangers / Anton, TextMesh Pro extras).

## Not in scope
Uploading to Steamworks (the user does that), new achievements, and stats.

## Revision (2026-11-04): simplified
The first set (round badge, neon rim, gradient, outlines, 3D boxes) looked AI-made. Redrawn like most shipped Steam icons: full-bleed square, one flat night-purple background, one flat shape per icon, three colours across the set (white, gold, green), no rings, glows, gradients or outlines. Symbols: parcel; gold parcel + crown; ghost (no horns, user's call); parcel + green arrow over it (steal); same in gold; skull; tombstone "1"; green up arrow; fish over waves.

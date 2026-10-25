# Options pages (pack A: settings): design

**Date:** 2026-10-24
**Branch:** `steam-phase1a`
**Status:** the user is away and said "I trust your judgement for how these are to be done", so every choice here is **my call**, not agreed. It covers features 4 (every setting), 10 (audio) and 11 (split-screen), plus the global part of 7 (accessibility) from `2026-10-23-modern-features-handoff.md`.

## Intent

Everything in `settings.cfg` can be changed in the game, as players and the Steam Deck review expect, and the Options screen gets room for pack B's and D's rows later. The drawn screen stays as it is: new rows are built by code in its font, like the Resolution row.

## Goals

- Options gets pages, switched with LB/RB (F/R on the keyboard): **GENERAL · DISPLAY · AUDIO · ACCESSIBILITY**.
- **GENERAL** is today's drawn screen, unchanged: Music, Effects (SFX), Fullscreen, Resolution.
- **DISPLAY:** Fullscreen style (BORDERLESS / EXCLUSIVE), VSync (ON / OFF), Frame cap (OFF, 30, 60, 120, 144, 165, 240; dimmed while VSync is on, since it only applies with VSync off), Quality (LOW / MEDIUM / HIGH), Split-screen (BOXED / STACKED / SIDE BY SIDE).
- **AUDIO:** Master (0–100% in steps of 10), In the background (MUTE / KEEP PLAYING).
- **ACCESSIBILITY:** Reduce motion (OFF / ON): no boost FOV kick and no speed lines.
- Every new row applies the moment it changes. Leaving Options saves, as today. Resolution still applies with A.
- A page header at the top shows the four pages, the current one bright and the rest dimmed, with the LB/RB glyphs.

## Non-goals (later, ledgered)

- Player markers and HUD size (feature 7's other global parts): markers touch the compass, minimap, results and lobby; HUD size touches every HUD canvas inside the player prefab. Each needs its own design. The ACCESSIBILITY page is where they'll go.
- Per-seat settings (camera sensitivity, invert, auto-accelerate): pack B, with the rebinding work.
- Finer volume steps for Music/Effects: they're drawn art with 11 positions.
- Hand-lettered art for the new rows.

## Decisions

- **Pages (my call), not one long list:** the drawn rows fill the screen down to the Resolution row; nine more rows would need scrolling art that doesn't exist.
- **Master volume is `AudioListener.volume`, not a mixer group (my call).** The mixer has no exposed Master parameter (an editor chore), and the listener's volume scales everything the same way. Muting in the background multiplies it by 0 while the game window isn't focused (`OnApplicationFocus`). The game never pauses: online needs it running (`runInBackground`).
- **Split-screen only changes two players (my call).** Boxed is today's (two 16:9 views stacked in the middle). Stacked is full width, top and bottom. Side by side is full height, left and right. Three and four players stay as they are. Changing it in the menus re-lays out the views at once.
- **Frame cap shows the saved number** even if it isn't one of the listed values (someone edited `settings.cfg`): it's added to the list in order, so nothing changes until they pick another.
- **Saved keys** (new, in `GameSettings.ToText`/`Parse`, with the defaults that match today's game): `master=10` (0–10), `bgmute=0` (0/1; the game has always kept playing in the background, so keep playing is the default), `split=0` (0 boxed, 1 stacked, 2 side by side), `reducemotion=0` (0/1). Steam Cloud carries `settings.cfg` already.
- **Only the host player** works the Options screen, as today (`MenuInteractions` checks `hostPlayer`).

## Shape

- Pure, edit-mode tested:
  - `OptionSetting`: a row's label, its choices (labels and values), the index, `Step(right)` (stops at the ends, like the drawn rows), and whether it's dimmed.
  - `OptionsBook`: the pages (names and settings), the current page and row, `TurnPage(right)` (wraps), `Move(down)` (wraps within the page), `Step(right)` on the current row (does nothing while it's dimmed), and an event when a setting changes.
  - `SplitScreenLayout.CalculateRects(count, layout)`.
  - `AudioVolume`: master, mute-in-background and focus → the listener's volume.
  - `Comfort.ReduceMotion`, read by `OrbitalCamera` (target FOV) and `BallDriving` (speed lines).
- Views, built by code: `OptionRow` (label and value, as the Resolution row, dimmed in grey) and `OptionsPageHeader`.
- `OptionsMenu` keeps the GENERAL page as it is and hands the other pages to the book: on those pages the drawn rows and the Resolution row hide and the code rows show; the ghost selector moves to the code row. Leaving Options goes back to GENERAL.
- `MenuInteractions` sends the shoulders to `OptionsMenu.TurnPage`.
- `BackgroundAudio`: a small object made on launch that tells `AudioVolume` when the window gains or loses focus.

## Approaches

- **A. Pages with code rows beside the drawn page (chosen).** Leaves the art alone and keeps every rule testable.
- B. Rebuild the whole Options screen by code. One system for every row, but it throws away the drawn art the user made.
- C. Put everything below the drawn rows and scroll. It needs scrolling that the drawn layout doesn't allow for.

## Left to check by eye (the user)

- Where the page header sits against the logo and backgrounds.
- Stacked and side-by-side views with the HUD (compass, minimap, indicators): 32:9 and 8:9 views.

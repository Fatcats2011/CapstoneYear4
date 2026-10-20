# Steam Deck readiness

Checked against Valve's Verified criteria (https://partner.steamgames.com/doc/steamdeck/compat) on 2026-10-20.

## Passes in code

- **Controller only:** every screen works with a controller. No script reads the keyboard or mouse directly, except the developer keys (`DevTools`, off in release). The game has no text entry, so it needs no on-screen keyboard.
- **Glyphs:** the prompts show Xbox buttons. Valve accepts Xbox glyphs when they match the active input, and on the Deck Steam Input hands the game an Xbox pad. Keyboard glyphs never show (no art exists for them yet). `ButtonGlyph` picks PlayStation and Nintendo art on a PC once that art exists, and on the Deck it stays on Xbox.
- **Text size:** Valve's minimum is 9 px at 1280×800 (12 px recommended).
  - The smallest scene text is about 23 px full screen, and about 12 px in a 4-way split.
  - The text built in code (hints, the lobby line, the Resolution row) is 20 px or more.
- **Resolution:** viewports and the phase texture follow the screen's shape, so 16:10 works (tested at 1280×800). The Resolution row lists the Deck's own size.
- **Quality:** the Deck starts on Medium (`GraphicsQuality.DefaultLevel`, `SteamManager.IsSteamDeck`).
- **No launcher** and no "unsupported device" warnings.
- **Mouse cursor** (new 2026-10-20): `CursorHider` hides it once the mouse rests for 2 s (builds only), so it no longer sits in the middle of the screen.

## Still to check on a Deck (or the Steam Deck review)

- **Frame rate:** 30 fps at 1280×800 on Medium (F6 shows the overlay; `docs/performance.md`). Check 1 player and a 2-player split.
- **HUD at 16:10:** check it by eye (also on the roadmap's Phase 1 list).
- **Steam Input default:** set the default config to Gamepad in Steamworks (Phase 4B). The game's own bindings already cover the Deck's buttons, and the shoulders open Find/Fill online.
- **Suspend and resume:** put the Deck to sleep mid-match and wake it. Online, a long sleep drops the session after Unity Transport's 30 s timeout, like any disconnect.
- **The review:** request it in Steamworks once a build is up (Phase 4B).

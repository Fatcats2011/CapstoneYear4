# Localization: design

**Date:** 2026-10-24
**Branch:** `steam-phase1a`
**Status:** written for the user's review. Decided in chat: what's below marked **(the user)**. Everything else is **my call**.

## Intent

Every bit of text in the game reads in the player's language, for 15 languages beyond English, without losing the hand-lettered look. Steam shows the game in these languages' stores, and players expect the game itself to follow.

## Goals

- **16 languages (the user: Latin-alphabet ones only):** English, French, German, Italian, Dutch, Spanish (Spain), Spanish (Latin America), Portuguese, Portuguese (Brazil), Polish, Turkish, Czech, Romanian, Hungarian, Swedish, Danish. Locale codes: `en`, `fr`, `de`, `it`, `nl`, `es`, `es-419`, `pt`, `pt-BR`, `pl`, `tr`, `cs`, `ro`, `hu`, `sv`, `da`.
- **Unity Localization (the user: approach A),** `com.unity.localization` 1.4.5, already in `Packages/manifest.json`. One string table collection, `Game`, holds every string.
- **The drawn words keep their look (the user):** every hand-lettered word image becomes real text in **Jack's Candlestick** (`Assets/Fonts/jcandlestick.ttf`, the font the art was made with; its TMP assets `jcandlestick norm` / `edged`), white, in the same place and size, so each language spells its own word.
- **Code-built text** (everything the scripts write: prompts, notices, awards, the Controls panel, match rules, the Options pages, the results vote, the connection list) comes from the table, in the font it uses now (Bangers).
- **Missing accents, drawn by me, placed over the letters (the user: approach B):** the fonts are left untouched. Where a font lacks a character, the base letter is shown and a drawn mark is placed over (or under) it.
  - Jack's Candlestick lacks 24: `Ă Ą Ć Č Ď Ę Ě Ğ İ Ł Ń Ň Ő Ř Ś Ş Ť Ů Ű Ź Ż Ș Ț ẞ`. Bangers lacks most of the same (it has `Ł`).
  - Eight marks cover them: caron (ˇ), breve (˘), dot above (˙), double acute (˝), ring (˚), ogonek (˛), comma below (,) and acute (´, for `Ć Ń Ś Ź` where the font's own acute can't be used on that letter). `Ł` uses a drawn stroke. Capital `ẞ` is written `SS`, as German does in capitals.
  - The marks match the fonts' style: chunky, slightly tilted, white. They're drawn by a script (`tools/accent-art.py`, as `tools/button-art.py` draws the button art) into PNGs under `Assets/Sprites/UI/Accents`.
- **Choosing a language:**
  - First launch: the player's Steam language (`SteamApps.GetCurrentGameLanguage`: `french`, `german`, `latam`, `brazilian`… mapped to the locales above), else the PC's, else English.
  - **Options → GENERAL → LANGUAGE**, a new row under Resolution: left and right change it at once, everywhere, including text already on screen.
  - Saved in `settings.cfg` (`language=fr`), which Steam Cloud carries.
- **A little flag (the user):** the current language's flag in a corner of the title screen, and beside the LANGUAGE row. Flags are simple drawn images (`tools/flag-art.py` → `Assets/Sprites/UI/Flags`): Spain for `es`, Mexico for `es-419`, Portugal for `pt`, Brazil for `pt-BR`, the UK for `en`, each country's own for the rest.
- **Translations:** I write all 15, keeping the jokes playful (KLEPTOGHOST, BUTTERFINGERS, TOURIST…) rather than literal, and keeping every `{0}` placeholder. Each is marked for a native speaker's check before release (a `reviewed` column).

## Non-goals

- **The loading and golden-round loading screens** (`DOA_LoadingScreenNEW.png`, `DOA_GoldenLoadingScreen_.png`): whole pictures with the instructions painted in. They stay English until there are versions without the words (the user didn't have them to hand).
- Non-Latin scripts (Russian, Greek, Japanese, Korean, Chinese).
- The Steam store page, achievement names and Rich Presence strings: set on the Steamworks site, a separate job (`docs/steam/in-game-features.md` lists them).
- Player names: never translated.
- Voice, audio.

## Anti-goals

- A word that overflows its spot or its button. German and French run long: a localized drawn word may shrink to fit its rect (TMP auto-size, down to 40%), never spill.
- A missing translation showing a key (`menu.options`). A missing entry falls back to English.
- Changing the font files (the user, B), or any scene, prefab, `.inputactions` or ProjectSettings file by hand (the standing rule).

## Constraints

- No scene or prefab edits: the drawn word images are swapped for text **at runtime**. A `DrawnWords` table maps each word sprite (by sprite name) to its key; at scene start, every `Image` showing one of those sprites gets a sibling TMP text in the same rect and is hidden. New drawn words later only need a row in the table.
- The Localization settings asset and the string table collection are editor assets, registered in project settings. That's one editor step for the user: **Tools → Dead on Arrival → Localization → Set Up** (a builder script, like Build Board In Menu Scene) creates the settings, the 16 locales and the `Game` collection, and imports the strings. It's an `EDITOR-TODO.md` chore.
- **The strings' source of truth is a CSV in the repo,** `Assets/Localization/strings.csv` (key, a note for translators, then one column per locale, then `reviewed`). The Set Up tool (and a Reimport menu item) loads it into the collection. Translators work on the CSV; tests read it.
- Localization initialises asynchronously; the game waits for it once at startup (`LocalizationSettings.InitializationOperation`) before the first menu shows, so no frame shows English first.
- Tests: Edit Mode where possible, as the rest of the code (`docs/testing.md`).

## Approaches

- **A. Unity Localization with tables built from a CSV by an editor tool (chosen by the user).** Standard, translator-friendly (CSV / Google Sheets), switches live.
- B. Our own text files per language and a small lookup: no editor step and fully testable, but no translator tooling, and the package the user installed would sit unused.

## What else changes

- **Online:** each machine shows its own language. Text a host sends to clients (a kick reason, a notice) goes as a key and its arguments where it can, so the client shows it in its own language. Any text that can't be keyed (a Steam error) shows as sent.
- **Steam Input / button glyphs:** unaffected (pictures).
- **docs:** `docs/controls.md` (the LANGUAGE row, the flag), `docs/testing.md`, `EDITOR-TODO.md` (Set Up), `docs/steam/store-page-draft.md` (the language list), `LICENSES.md` (no change: the fonts aren't modified).

## Tests the builder adds (at least)

- **The CSV:** every key has all 16 columns filled; every translation keeps the English entry's placeholders; no duplicate keys; every key the code asks for exists (a scan of `Loc.Get("…")` calls).
- **Characters:** every character in every translation is either in the font it'll be shown in, or one the accent overlay knows (a test per font).
- **The accent overlay:** each of the 24 characters maps to a base letter and mark(s); text without them is untouched.
- **The language rules:** Steam language names to locales; the saved key round-trip; an unknown saved value falls back to Steam's, then English.
- **The drawn-word swap:** an Image with a known word sprite becomes text with that word's key, in the same rect, the image hidden.
- **A play-mode check:** switching to German in the real menu scene changes the title entries' text, and the flag.

## Settled after the builder check (my calls)

- **The drawn-word inventory comes first.** The builder's first step is an editor scan listing every `Image` and `SpriteRenderer` (scenes, prefabs) whose sprite has letters in it, and every sprite the code assigns at runtime. Known so far: the word sheet `MenuText_UI-sprites2.png` (12 words: RESULTS, FULLSCREEN, PAUSED, SELECT, MENU, ON, OFF, RETURN, RESUME, PRESS, TO BOOST, TO CONFIRM, TO CONTINUE), `Local.png`, `Multiplayer.png`, used in the menu scene, `Menu Canvas.prefab` and `Loading Screen World Canvas.prefab`. The swap covers Images and SpriteRenderers, at scene load and whenever a scene loads later; a sprite the code swaps in afterwards (none known with letters) gets the same treatment by calling the swap.
- **Baked text that stays English** gets listed in `docs/controls.md` from that scan: the two loading screens for sure, plus anything else whose letters are painted into a larger picture (the Dispatch wordmark and ticket art, if they have words). Names in the credits aren't translated.
- **The accent overlay attaches at runtime, never in scenes:** it listens to TextMesh Pro's text-changed event for every text, and, only where a text holds one of the 24 characters, places small child images from the character positions TMP worked out (so wrapping, auto-size and live language changes are already laid out). Placement good enough to read naturally beats pixel-perfect; per-mark offsets are tuned by eye. It works the same for Jack's Candlestick and Bangers.
- **The look of swapped words:** `jcandlestick norm`, the drawn words' off-white (`#F2F2F2`, from the sheet), no outline, the word's rect, auto-size down to 40%. `edged` stays as it's used now (the scores).
- **Fallbacks:** `es-419` → `es`, `pt-BR` → `pt`, everything → `en`. The CSV test still requires every column filled (translations are written up front); the fallback is the runtime's safety net, not a licence to leave gaps.
- **Code text:** the builder finds it by scanning the scripts for player-facing string literals; touching the uncommitted files on this branch is fine (it's all one branch, committed by the user).
- **Online messages:** changing a message to carry a key and arguments is fine: machines of different builds can't play together anyway (the session's version check).
- **`reviewed`:** data only. It gates nothing; it's how the user tracks native checks.

## Left to the builder

- The exact key naming (`menu.title.local`, `pause.restart`…), the CSV's note wording, how `Loc.Get` wraps the package (smart strings or plain `string.Format`), and the overlay's per-mark offsets.
- Where the title screen's flag sits (a corner clear of the logo and the bottom prompts).
- Whether code-built text that's set once (the panels) re-reads its strings on a language change by rebuilding, or by a `LocalizedText` component that listens.

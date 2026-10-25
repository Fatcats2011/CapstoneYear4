# Localization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every piece of game text shows in the player's language (16 Latin-alphabet languages), the hand-lettered words become Jack's Candlestick text, missing accents are drawn over the letters, and a LANGUAGE row and flag show the choice.

**Architecture:** One CSV in the repo (`Assets/Resources/Localization/strings.csv`) is the strings' source of truth. `Loc` serves text from Unity Localization's `Game` string table once its settings exist (an editor tool builds them from the CSV), and from the CSV itself until then (so tests and an un-set-up project still work). Drawn word images are swapped for TMP text at runtime from a sprite-name table; an overlay places drawn accent marks over characters a font lacks.

**Tech Stack:** Unity 2022.3, `com.unity.localization` 1.4.5 (already in the manifest), TextMesh Pro, NUnit Edit Mode / Play Mode tests (`bash tools/run-tests.sh`), Python + Pillow for art scripts (`tools/button-art.py` is the pattern).

**Spec:** `docs/superpowers/specs/2026-10-24-localization-design.md`

## Global Constraints

- Locales, in this order: `en`, `fr`, `de`, `it`, `nl`, `es`, `es-419`, `pt`, `pt-BR`, `pl`, `tr`, `cs`, `ro`, `hu`, `sv`, `da`.
- Fallbacks at runtime: `es-419` → `es`, `pt-BR` → `pt`, all → `en`. Never show a key.
- No scene, prefab, `.inputactions` or ProjectSettings edits by hand; editor setup is a Tools menu item the user runs (`EDITOR-TODO.md`).
- Fonts untouched (`jcandlestick.ttf`, Bangers). Missing characters: base letter + drawn mark; capital `ẞ` → `SS`.
- Swapped drawn words: `jcandlestick norm`, colour `#F2F2F2`, no outline, the word's rect, TMP auto-size min 70% of the size that fits the English word.
- CSV: UTF-8, RFC 4180 quoting, header `key,note,en,fr,de,it,nl,es,es-419,pt,pt-BR,pl,tr,cs,ro,hu,sv,da,reviewed`. Every cell filled; placeholders `{0}`…`{n}` identical to English's.
- New `Assets` files get a `.meta` (`bash tools/newmeta.sh <file>`). CRLF files keep CRLF. The user commits: no `git commit` steps; each task ends with its tests passing.
- One test run at a time (`bash tools/run-tests.sh '<filter>'`).

## Review Focus

- A German/French word longer than the English art's rect: must shrink (auto-size, ≥70%) and never spill over its neighbours. → Task 6 test `ALongWord_ShrinksToFitItsRect`.
- Switching language while a code-built panel is open (Options pages, Controls, MATCH RULES, the pause list): its text must update, not keep the old language until rebuilt. → Task 4 test `ChangingLanguage_UpdatesTextAlreadyShown`.
- A character with two needs at once (`Ő` in a wrapped, auto-sized line): the mark must follow the laid-out character, including after a re-layout. → Task 5 test `Overlay_FollowsTheCharacterAfterRelayout`.
- `settings.cfg` from before this feature, or with a language code this build doesn't know: falls back to Steam's language, then English, never crashes. → Task 2 test `SavedLanguage_Unknown_FallsBackToSteamThenEnglish`.
- A client receiving a host notice: shows it in the client's language with the host's arguments, and an old free-text notice still shows. → Task 9 test `Notice_ByKey_ShowsInTheClientsLanguage`.

---

### Task 1: The strings CSV and its reader

**Files:**
- Create: `Assets/Scripts/Localization/StringsCsv.cs`, `Assets/Resources/Localization/strings.csv`
- Test: `Assets/Tests/Editor/StringsCsvTests.cs`

**Interfaces:**
- Produces: `public static class LocaleCodes { public static readonly string[] All; public const string ENGLISH = "en"; public static string FallbackOf(string code); }` (`es-419`→`es`, `pt-BR`→`pt`, `en`→null, else `en`).
- Produces: `public class StringsCsv { public static StringsCsv Parse(string text); public static StringsCsv Load(); /* Resources "Localization/strings" */ public IReadOnlyList<string> Keys; public string Get(string key, string locale); /* null when absent or empty */ public string Note(string key); public bool Reviewed(string key, string locale); }`
- `reviewed` cell: space-separated locale codes checked by a native speaker.

- [ ] **Step 1: Write the failing tests** in `StringsCsvTests`:
  - `Parse_QuotedCellsWithCommasAndQuotes` (`"a, ""b"""` → `a, "b"`), `Parse_NewlineInsideQuotes`.
  - `Get_AbsentKeyOrEmptyCell_IsNull`.
  - `FallbackOf_RegionalToBase_ElseEnglish` (`es-419`→`es`, `pt-BR`→`pt`, `de`→`en`, `en`→null).
  - On the real file (`StringsCsv.Load()`): `TheFile_HasEveryLocaleColumn_InOrder`, `TheFile_EveryCellFilled`, `TheFile_PlaceholdersMatchEnglish` (same set of `{n}` in each cell), `TheFile_NoDuplicateKeys`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'StringsCsvTests'` → FAIL (types missing).
- [ ] **Step 3: Implement** `StringsCsv` and `LocaleCodes`; create `strings.csv` with the header and the English column only for now, translation cells filled with the English text and `reviewed` empty (Task 8 replaces them). Seed rows: the keys Task 4 lists. Add the `.meta` files.
- [ ] **Step 4: Run** `bash tools/run-tests.sh 'StringsCsvTests'` → PASS.

### Task 2: Language rules and the saved choice

**Files:**
- Create: `Assets/Scripts/Localization/LanguageRules.cs`
- Modify: `Assets/Scripts/Menu/GameSettings.cs` (key `language`, default `""` = not chosen)
- Test: `Assets/Tests/Editor/LanguageRulesTests.cs`, additions in `GameSettingsTests.cs`

**Interfaces:**
- Produces: `public static class LanguageRules { public static string FromSteam(string steamLanguage); /* "english"→"en","french"→"fr","german"→"de","italian"→"it","dutch"→"nl","spanish"→"es","latam"→"es-419","portuguese"→"pt","brazilian"→"pt-BR","polish"→"pl","turkish"→"tr","czech"→"cs","romanian"→"ro","hungarian"→"hu","swedish"→"sv","danish"→"da"; else null */ public static string FromSystem(SystemLanguage language); /* the same set; Spanish→"es", Portuguese→"pt"; else null */ public static string Pick(string saved, string steam, string system); /* the first known of saved, steam, system, else "en" */ public static string Next(string code, bool right); /* LocaleCodes.All order, stops at the ends */ public static string Name(string code); /* each language's name in itself, in capitals: "ENGLISH","FRANÇAIS","DEUTSCH","ITALIANO","NEDERLANDS","ESPAÑOL","ESPAÑOL (LATAM)","PORTUGUÊS","PORTUGUÊS (BRASIL)","POLSKI","TÜRKÇE","ČEŠTINA","ROMÂNĂ","MAGYAR","SVENSKA","DANSK" */ }`
- Produces: `GameSettings.language` (string; `ToText`/`Parse`/`Copy` carry it; `Parse` keeps any value, `Pick` validates).

- [ ] **Step 1: Write the failing tests:** `FromSteam_EveryLanguage` (TestCase per pair), `FromSteam_Unknown_IsNull`, `Pick_SavedFirst_ThenSteam_ThenSystem_ThenEnglish`, `SavedLanguage_Unknown_FallsBackToSteamThenEnglish` (`Pick("xx","german",…)`→`de`; `Pick("xx",null,SystemLanguage.Japanese)`→`en`), `Next_StopsAtTheEnds`, `Name_IsInItsOwnLanguage`; in `GameSettingsTests`: `Language_LoadsBack`, `SettingsFromBeforeLanguage_HaveNone` (`""`).
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'LanguageRulesTests|GameSettingsTests'` → FAIL.
- [ ] **Step 3: Implement** `LanguageRules`; add `language` to `GameSettings`.
- [ ] **Step 4: Run** the same → PASS.

### Task 3: `Loc`, the runtime lookup

**Files:**
- Create: `Assets/Scripts/Localization/Loc.cs`
- Modify: `Assets/Scripts/Menu/OptionsMenu.cs` (`LoadOptions` sets the language: `Loc.SetLanguage(LanguageRules.Pick(settings.language, steam, Application.systemLanguage))`, Steam's from `SteamApps.GetCurrentGameLanguage()` when `SteamManager.Initialized`)
- Test: `Assets/Tests/Editor/LocTests.cs`

**Interfaces:**
- Produces: `public static class Loc { public static string Language { get; } public static event Action Changed; public static void SetLanguage(string code); public static string Get(string key, params object[] args); internal static StringsCsv Csv; /* tests swap */ }`
  - `Get`: the current language's text, else `LocaleCodes.FallbackOf` chain, else the key's English, else the key itself logged once as a warning (never shown to players in a build: the CSV tests guarantee it exists). `args` → `string.Format(CultureInfo.InvariantCulture, …)`.
  - Source: when `LocalizationSettings.HasSettings` and the `Game` table has the entry, the package (`LocalizationSettings.StringDatabase.GetLocalizedString("Game", key, …)`), with `SelectedLocale` set by `SetLanguage`; otherwise the CSV. Startup waits on `LocalizationSettings.InitializationOperation` once (in `OptionsMenu.Start` before `LoadOptions`, or a `[RuntimeInitializeOnLoadMethod]` that calls `WaitForCompletion()`).
  - `SetLanguage` with the current code does nothing; otherwise raises `Changed`.

- [ ] **Step 1: Write the failing tests** (CSV-backed, swapping `Loc.Csv` for a parsed test CSV with `en`, `fr`, `es`, `es-419` columns): `Get_TheCurrentLanguage`, `Get_RegionalFallsBackToBase` (an empty `es-419` cell gives `es`'s), `Get_MissingTranslation_IsEnglish`, `Get_FormatsArguments` (`"{0} steals"`, 3 → `3 steals`), `Get_UnknownKey_IsTheKey`, `SetLanguage_RaisesChanged_OnlyOnAChange`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'LocTests'` → FAIL.
- [ ] **Step 3: Implement** `Loc`; wire `LoadOptions`.
- [ ] **Step 4: Run** `bash tools/run-tests.sh 'LocTests|OptionsPagesMenuTests'` → PASS.

### Task 4: Code-built text from the table

**Files:**
- Modify: every script that sets player-facing text. At least: `AwardRules.cs` (names, lines), `RulesPanel.cs`, `HouseRules.cs` (`Summary`), `OptionsMenu.cs` (page names, rows, values), `OptionRow.cs`/`OptionsPageHeader.cs`, `ResolutionRow.cs`/`ResolutionRules.cs` (`RESOLUTION`, `A: apply`), `PauseMenu.cs` (RESTART, CONTROLS, question, PAUSED), `ControlsPanel.cs` + `RebindRules.cs` (row names, titles, PRESS A KEY…, RESET), `CutsceneSkip.cs`, `ResultsVoteView.cs`, `ResultsAwardsView.cs`, `PingBoard.cs` (`MS`), `MatchRules.cs` (status lines, notices), `ControllerPrompts.cs` hints, `LobbyPrompt.cs`, `DispatchBoard.cs`/`TicketView.cs` texts, `OnlinePlay.cs` notices, `PausePolicy.cs` hints, `PlayerNames.Fallback`. The builder finds the rest: `grep -rnE '"[A-Z][A-Z0-9 !?.,:'"'"'·…/-]{2,}"' Assets/Scripts` plus any `.text = ` with a literal.
- Modify: `Assets/Resources/Localization/strings.csv` (a row per string; keys `area.thing`, e.g. `award.kleptoghost.name`, `award.kleptoghost.line`, `pause.restart`, `rules.length.short`).
- Create: `Assets/Scripts/Localization/LocalizedText.cs`
- Test: `Assets/Tests/Editor/LocalizedTextTests.cs`; update existing tests that assert English copy only where the call now needs `Loc` (they keep their English expectations: English is the default).

**Interfaces:**
- Produces: `public class LocalizedText : MonoBehaviour { public static LocalizedText On(TMP_Text text, string key, params object[] args); public void Set(string key, params object[] args); }` — sets `text.text = Loc.Get(…)` now and again on `Loc.Changed`.
- Code that composes text from rules (e.g. `AwardRules.Line(award, count)`) returns `Loc.Get` results, so views refresh by re-calling on `Loc.Changed`.

- [ ] **Step 1: Write the failing tests:** `LocalizedText_ShowsTheKeysText`, `ChangingLanguage_UpdatesTextAlreadyShown` (an open `RulesPanel` and a `LocalizedText` both show French after `Loc.SetLanguage("fr")` with a test CSV), `CodeKeys_AllExistInTheCsv` (scan `Assets/Scripts` for `Loc.Get("…"` / `LocalizedText.On(…, "…"` literals; each key is in `StringsCsv.Load()`).
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'LocalizedTextTests'` → FAIL.
- [ ] **Step 3: Move every string to the CSV and `Loc`;** views that build text once subscribe to `Loc.Changed` (or use `LocalizedText`).
- [ ] **Step 4: Run** the full suite `bash tools/run-tests.sh` → all pass (English unchanged).

### Task 5: Drawn accents over missing characters

**Files:**
- Create: `tools/accent-art.py` (Pillow; draws `caron`, `breve`, `dot`, `doubleacute`, `ring`, `ogonek`, `commabelow`, `acute`, `lstroke` as white `#F2F2F2` shapes, chunky, ~8° tilt, 128×128 PNG with transparent background), `Assets/Sprites/UI/Accents/*.png`, `Assets/Resources/AccentMarks.asset` (a `AccentMarkSet` ScriptableObject, hand-written YAML like `ResultsButtons.asset`, listing the sprites by mark name)
- Create: `Assets/Scripts/Localization/AccentRules.cs`, `Assets/Scripts/Localization/AccentOverlay.cs`, `Assets/Scripts/Localization/AccentMarkSet.cs`
- Test: `Assets/Tests/Editor/AccentRulesTests.cs`, `Assets/Tests/Editor/AccentOverlayTests.cs`

**Interfaces:**
- Produces: `public enum AccentMark { Caron, Breve, Dot, DoubleAcute, Ring, Ogonek, CommaBelow, Acute, LStroke }`; `public readonly struct Accented { public readonly char Base; public readonly AccentMark Mark; }`; `public static class AccentRules { public static bool TryGet(char c, out Accented accented); /* the 24 from the spec; 'ẞ' handled by Replace */ public static string Replace(string text, Func<char,bool> fontHas); /* each char the font lacks and TryGet knows → its base; 'ẞ'→"SS"; others untouched */ }`
- Produces: `AccentOverlay` — `[RuntimeInitializeOnLoadMethod]` subscribes to `TMPro_EventManager.TEXT_CHANGED_EVENT`; for a changed `TMP_Text` whose `text` holds a known character its font lacks: rewrites to the bases (once, guarded against its own change) and places a child `Image` per mark at the character's laid-out position (`textInfo.characterInfo`: top-centre for above marks, bottom-right for ogonek, bottom-centre for comma below, across the stem for `LStroke`), sized to the character's height × 0.35, coloured as the text. Children are pooled per text and hidden when unused.

- [ ] **Step 1: Write the failing tests:** `TryGet_TheTwentyFour` (TestCase per character → base + mark), `Replace_OnlyWhatTheFontLacks` (`"ÉČ"` with a font having `É` → `"ÉC"`), `Replace_SharpS_IsSS`, `Replace_PlainText_Untouched`; overlay (edit mode, a `TextMeshProUGUI` with the Bangers asset, `ForceMeshUpdate`): `Overlay_PlacesAMarkOverTheBase` (`"ČAS"`: one active mark image whose x is within the `C` character's bounds), `Overlay_FollowsTheCharacterAfterRelayout` (set width smaller to wrap; the mark moves with `C`), `Overlay_TextWithoutThemHasNoMarks`. Per-font coverage: `EveryTranslation_IsShowableInEachFont` (every character in every CSV cell is in jcandlestick or Bangers, or `AccentRules` knows it, or is `ẞ`).
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'AccentRulesTests|AccentOverlayTests'` → FAIL.
- [ ] **Step 3: Draw the marks** (`python tools/accent-art.py`), add metas, write `AccentMarks.asset`; implement `AccentRules`, `AccentOverlay`.
- [ ] **Step 4: Run** the same → PASS. Record in the ledger that mark placement needs a look by eye.

### Task 6: Drawn words become text

**Files:**
- Create: `Assets/Scripts/Editor/DrawnWordScan.cs` (Tools → Dead on Arrival → Localization → List Drawn Words: logs every `Image`/`SpriteRenderer` in build scenes and prefabs using a sprite from a texture listed in `DrawnWords`, or any sprite whose name contains "Text"), `Assets/Scripts/Localization/DrawnWords.cs`
- Modify: `Assets/Resources/Localization/strings.csv` (keys `word.results`, `word.fullscreen`, `word.paused`, `word.select`, `word.menu`, `word.on`, `word.off`, `word.return`, `word.resume`, `word.press`, `word.toboost`, `word.toconfirm`, `word.tocontinue`, `word.local`, `word.multiplayer`, plus any the scan finds)
- Test: `Assets/Tests/Editor/DrawnWordsTests.cs`

**Interfaces:**
- Produces: `public static class DrawnWords { public static readonly IReadOnlyDictionary<string,string> KeyOfSprite; /* "MenuText_UI-sprites2_0" → "word.results", … by sprite name; "Local" → "word.local"; "Multiplayer" → "word.multiplayer" */ public static TMP_Text Swap(Image image); /* builds the text, hides the image, returns null for an unknown sprite */ public static void SwapAll(Scene scene); }` — runs on `SceneManager.sceneLoaded` (and for the first scene), covering inactive objects; idempotent.
- The text: sibling of the image, same anchors/pivot/size/position/rotation/scale, `jcandlestick norm`, `#F2F2F2`, centred, auto-size max = the size that fits the English word in that rect, min 70% of it; `LocalizedText` keeps it current. If the image is a `selectorObjects` entry, the menus keep using the image's GameObject for positions (the text is a sibling, so selectors still move the same).

- [ ] **Step 0: Map the sprites:** open `MenuText_UI-sprites2.png.meta` and the sheet; record sprite index → word in `DrawnWords` (the 13 words from the spec); run the scan tool in the test mirror via a test (`DrawnWordScanTests.EveryWordSpriteUseIsKnown`) that loads `Alex Player Testing.unity`, `Menu Canvas.prefab`, `Loading Screen World Canvas.prefab` and asserts every Image using the sheet or `Local.png`/`Multiplayer.png` has a key.
- [ ] **Step 1: Write the failing tests:** `Swap_AKnownWord_BecomesItsText_InTheSameRect`, `Swap_UnknownSprite_DoesNothing`, `Swap_Twice_MakesOneText`, `ALongWord_ShrinksToFitItsRect` (German `VOLLBILD`-length text in a rect sized for `FULLSCREEN`: `textInfo` bounds within the rect, font size ≥ 70% of the English fit), `EveryWordSpriteUseIsKnown`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'DrawnWordsTests|DrawnWordScanTests'` → FAIL.
- [ ] **Step 3: Implement** `DrawnWords` and the scan tool; add the word rows (English = the drawn word).
- [ ] **Step 4: Run** the same, then the menu Play Mode tests (`OptionsPagesMenuTests|ResolutionRowMenuTests|PauseRestartPlayTests`) → PASS.

### Task 7: LANGUAGE row and the flags

**Files:**
- Create: `tools/flag-art.py` (16 flags, 96×64 PNG, simple shapes; Mexico's and Brazil's emblems simplified to a centred circle/diamond), `Assets/Sprites/UI/Flags/<code>.png`, `Assets/Resources/Flags.asset` (`FlagSet` ScriptableObject: code → sprite), `Assets/Scripts/Localization/FlagSet.cs`, `Assets/Scripts/Localization/LanguageFlag.cs`
- Modify: `OptionsMenu.cs` (GENERAL gets a code-built `LANGUAGE` row under Resolution: `OptionSelected.LANGUAGE`, `RowsFor` adds it after RESOLUTION; left/right `LanguageRules.Next` → `Loc.SetLanguage`, saved at once in `settings.language`; value shows `LanguageRules.Name` and the flag), `MainMenu.cs` (a flag in the title screen's top-right, built in `Start`)
- Test: `Assets/Tests/Editor/LanguageFlagTests.cs`, additions to `OptionsPagesMenuTests.cs`

**Interfaces:**
- Produces: `public class LanguageFlag : MonoBehaviour { public static LanguageFlag Create(RectTransform parent, Vector2 anchor, Vector2 position, float height); /* shows Loc.Language's flag, follows Loc.Changed */ }`

- [ ] **Step 1: Write the failing tests:** `Flag_ShowsTheCurrentLanguage_AndFollowsAChange`, `FlagSet_HasEveryLocale`; Play Mode in `OptionsPagesMenuTests`: `Language_RightOnTheRow_SwitchesTheGame` (on GENERAL, select LANGUAGE, right → `Loc.Language == "fr"`, the title entries' swapped text reads French, `settings.cfg` holds `language=fr`).
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'LanguageFlagTests|OptionsPagesMenuTests'` → FAIL.
- [ ] **Step 3: Draw the flags,** add metas and `Flags.asset`; implement the row and the flags.
- [ ] **Step 4: Run** the same → PASS.

### Task 8: The translations, and Unity's tables

**Files:**
- Modify: `Assets/Resources/Localization/strings.csv` (all 15 columns written; `reviewed` left empty)
- Create: `Assets/Scripts/Editor/LocalizationSetup.cs` (Tools → Dead on Arrival → Localization → Set Up: creates `Assets/Localization/LocalizationSettings.asset` and makes it active, the 16 `Locale` assets in `Assets/Localization/Locales`, the `Game` `StringTableCollection` in `Assets/Localization/Tables`, then Reimport; Tools → … → Reimport Strings: fills every table entry from the CSV, removing keys the CSV no longer has)
- Modify: `EDITOR-TODO.md` (a "Localization: Set Up" chore: run the menu item once, save, then commit `Assets/Localization`), `docs/controls.md`, `docs/testing.md`, `docs/steam/store-page-draft.md` (languages)
- Test: `Assets/Tests/Editor/LocalizationSetupTests.cs`

**Interfaces:**
- Consumes: `StringsCsv`, `LocaleCodes`.
- Produces: `internal static class LocalizationSetup { internal static void FillTables(StringTableCollection collection, StringsCsv csv); }`

- [ ] **Step 1: Write the failing tests:** `FillTables_EveryKeyAndLocale` (a collection created in a temp folder under `Assets/Tests/Temp` in the test, filled from a 2-key test CSV; each locale's table has both entries with the CSV's text; the folder deleted in TearDown), `FillTables_RemovesKeysNoLongerInTheCsv`; the CSV tests from Task 1 now pass with real translations (`TheFile_EveryCellFilled` stays green; add `TheFile_TranslationsDifferFromEnglish` for at least 90% of cells per locale, excluding keys whose note says "same in every language").
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'LocalizationSetupTests|StringsCsvTests'` → FAIL.
- [ ] **Step 3: Write the translations** (playful, placeholders kept, capitals where the English is in capitals); implement the setup tool; write the docs.
- [ ] **Step 4: Run** the same, then `EveryTranslation_IsShowableInEachFont` → PASS.

### Task 9: Host messages by key

**Files:**
- Modify: the host → client text messages (`OnlineMatch` notices/kick reasons via `OnlineSession.SendAway(clientId, reason)` and any `ShowHint` text a host sends): send `key` + `string[] args` (a `FixedString` list, each ≤ 64 bytes); clients show `Loc.Get(key, args)`; a message without a key shows its text as sent (Steam errors).
- Test: `Assets/Tests/Editor/HostNoticeTests.cs`

**Interfaces:**
- Produces: `public struct HostNotice : INetworkSerializable { public FixedString64Bytes Key; public FixedString128Bytes Text; public FixedString64Bytes Arg0, Arg1; public string Shown(); }` (`Shown`: `Loc.Get(Key, args)` when `Key` is set and known, else `Text`); `NetChecks.Sane(HostNotice)` (key matches `^[a-z0-9.]+$`, ≤ 64 bytes).

- [ ] **Step 1: Write the failing tests:** `Notice_ByKey_ShowsInTheClientsLanguage` (`Loc.SetLanguage("fr")`, a notice with a known key and an arg → the French text with the arg), `Notice_WithoutKey_ShowsTheText`, `Sane_ABadKey_IsRefused`.
- [ ] **Step 2: Run** `bash tools/run-tests.sh 'HostNoticeTests'` → FAIL.
- [ ] **Step 3: Implement** and switch the senders.
- [ ] **Step 4: Run** `bash tools/run-tests.sh 'HostNoticeTests|Online'` → PASS, then the full suite `bash tools/run-tests.sh` → all pass.

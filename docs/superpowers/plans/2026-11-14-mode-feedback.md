# Mode Goals and Event Calls (Pack C) Implementation Plan

> **For agentic workers:** each task is handed to one Sonnet agent, one at a time (the user asked for Sonnet agents on the smaller tasks; there is one test mirror). The main session reviews each diff, sources the sounds with the user, runs the full suite and updates the documents. Steps use checkbox (`- [ ]`) syntax.

**Goal:** In Kings Crown, Infection and Hot Potato a player reads their current goal in a small tag that goes after 25 seconds, is told in their callout line when a mode event happens to them, and everyone is warned 30 seconds before a timed round ends.

**Architecture:** Other machines receive only the mode's state (`ModeSnapshot`), never its events, so every machine works events out the same way: a pure rules class (`ModeFeedbackRules`) compares the state before and after each `ModeMatch.Changed` and returns the lines; a runtime watcher (`ModeWatcher`) announces them through a static hub (`ModeFeedback`). Pack A's `StealCallout` shows the lines; a new per-player `GoalTag` shows the goal. No network messages are added.

**Tech Stack:** Unity 2022.3, C#, TextMeshPro, DOTween, NUnit edit-mode tests.

**Spec:** `docs/superpowers/specs/2026-11-14-mode-feedback-design.md`

## Global Constraints

- Read `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`, "How to work in this repo". In short:
  - **Never commit, push, stash or switch branch.** Work in place on `steam-phase1a`. The tree has other uncommitted work: touch only the files your task names.
  - Never edit a scene, prefab, `.inputactions` or ProjectSettings file.
  - Check line endings with `git ls-files --eol <file>` and keep them; change existing files with the Edit tool only.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
  - Tests: `bash tools/run-tests.sh "<filter>"` (a regex of class names; a few minutes; a compile error prints `NO RESULTS`). One run at a time. Never run the full suite.
  - TDD: write the test, run it and see it fail (or `NO RESULTS`), then implement, then see it pass. Tests first, honestly.
  - Tests are edit mode: `Awake`, `OnEnable` and `Update` don't run. Build objects explicitly and call internal methods directly. No lambda may capture a test's local; use fields and recorder methods subscribed as method groups, unsubscribed in `TearDown`. Follow `Assets/Tests/Editor/GainCalloutTests.cs` and `ScoreFeedbackTests.cs`.
  - Comments: every class and public/internal method gets a plain-English `/// <summary>` in the style of `Assets/Scripts/World/GainFeedbackRules.cs`.
- UI stays small (the user's rule): the goal tag is text only (no box), font size 26, at most 320 wide, one line. No banners, no screen flash.
- The goal tag goes 25 seconds after it appears (the user's number). Event lines keep the callout's 1.5 s.
- A seat is 0–3; "no seat" is `ModeMatch.NONE` (-1). Player sets are bit masks (bit n = seat n).
- Sounds are never made by Claude: this plan only adds the names and volumes; the main session adds approved CC0 files. A missing file must play nothing and throw nothing.

## Review Focus

1. **Two changes in one update** (online, a pass and an explosion arrive together): both are reported from the before and after. Test in Task 1.
2. **A player leaves mid-round** (their bit drops out of `Players`): no line is addressed to them, no "took your crown" for a crown that fell because its holder left. Test in Task 1.
3. **A new match, or the mode changing between matches:** nothing from the last match's state leaks: the first state of a match is compared against nothing. Test in Task 1 and Task 2.
4. **The same state announced twice** (clients raise `Changed` on every snapshot, even unchanged): no line repeats. Test in Task 1 (equal before and after gives no lines) and Task 2.
5. **Pause and match end:** the tag and lines hide while paused and clear at match end. Test in Task 3 (`GoalTag` alpha rules).

---

### Task 1: The rules and the strings

**Files:**
- Create: `Assets/Scripts/Management/ModeFeedbackRules.cs` (+ `.meta`)
- Modify: `Assets/Resources/Localization/strings.csv` (append rows; LF; keep the trailing empty `reviewed` cell)
- Test: `Assets/Tests/Editor/ModeFeedbackRulesTests.cs` (+ `.meta`)

**Interfaces — Produces:**

```csharp
/// What a machine knows of a mode's state at one moment. Crown and Potato are seats or ModeMatch.NONE; the rest are seat masks
public struct ModeView
{
    public GameMode Mode;
    public int Players, Crown, Potato, Infected, Out;
    public static ModeView Nothing { get; }   // Mode Standard, Players 0, Crown and Potato NONE, Infected 0, Out 0
}

public enum ModeLineKind : byte { CrownYours, CrownTaken, Infected, Caught, LastFree, PotatoYours, PotatoPassed, PlayerOut }
public enum ModeRole : byte { None, KeepCrown, GetCrown, Catch, DontGetCaught, PassIt, StayAway }

/// One line for the callout. Seat: who reads it, or EVERYONE_ELSE (-2) for every player but Other. Other: the player it names, or ModeMatch.NONE
public readonly struct ModeLine
{
    public const int EVERYONE_ELSE = -2;
    public readonly int Seat; public readonly ModeLineKind Kind; public readonly int Other;
    public ModeLine(int seat, ModeLineKind kind, int other);
}

public static class ModeFeedbackRules
{
    public const float GOAL_SECONDS = 25f;
    public const float GOAL_FADE = 0.3f;
    public const float WARN_SECONDS = 30f;

    public static System.Collections.Generic.List<ModeLine> Lines(ModeView before, ModeView after);
    public static string Key(ModeLineKind kind);            // strings.csv key, below
    public static CalloutTone Tone(ModeLineKind kind);      // CrownYours, Caught, PotatoPassed: Thief (gold); CrownTaken, Infected, PotatoYours: Victim (red); LastFree, PlayerOut: Plain
    public static ModeRole GoalFor(ModeView view, int seat);
    public static string GoalKey(ModeRole goal);            // null for None
    public static float GoalAlpha(float age);               // 0 outside [0, GOAL_SECONDS); fades over GOAL_FADE at each end; 1 between
    public static bool WarnsIn(GameMode mode);              // KingsCrown, Infection
    public static bool CrossedWarning(float before, float after);   // before > WARN_SECONDS && after <= WARN_SECONDS && after > 0
    public static string CountdownKey(bool goldenRound);    // "hud.goldenin" with a golden round, else "hud.timesupin"
}
```

**`Lines` rules.** If `before.Mode != after.Mode` or `before.Players == 0`, use `ModeView.Nothing` (with `after.Mode`) as `before`. A seat "is playing" if its bit is in `after.Players`. Never address or name a seat that is not playing in `after`, except `Other` of `PlayerOut` (an out player is still in `Players`).
- **Kings Crown** (`after.Mode == KingsCrown`): if `after.Crown != before.Crown` and `after.Crown` is a seat: `(after.Crown, CrownYours, NONE)`; and if `before.Crown` is a seat still playing: `(before.Crown, CrownTaken, after.Crown)`.
- **Infection:** `newly = after.Infected & ~before.Infected & after.Players`. For each seat in `newly`: `(seat, Infected, NONE)`. If `before.Infected & after.Players` has exactly one seat (the lone hunter) and it is not in `newly`: for each seat in `newly`: `(hunter, Caught, seat)`. `free(v) = v.Players & ~v.Infected`: if `free(after)` has exactly one seat and `free(before)` has more than one: `(that seat, LastFree, NONE)`.
- **Hot Potato:** `newlyOut = after.Out & ~before.Out`. For each seat in `newlyOut`: `(EVERYONE_ELSE, PlayerOut, seat)`. If `after.Potato != before.Potato` and `after.Potato` is a seat: `(after.Potato, PotatoYours, NONE)`; and if `before.Potato` is a seat, still playing, and not in `newlyOut`: `(before.Potato, PotatoPassed, after.Potato)`.
- Any other mode: no lines. Equal `before` and `after`: no lines.
- Order of the list: out lines, then crown/potato "yours", then taken/passed, then infected, caught, last free.

**`GoalFor`:** not playing, or out → `None`. Kings Crown: `KeepCrown` if `view.Crown == seat`, else `GetCrown`. Infection: `Catch` if infected, else `DontGetCaught`. Hot Potato: `PassIt` if `view.Potato == seat`, else `StayAway`. Other modes → `None`.

**Strings** (key, note, 16 locales in the header's order, empty `reviewed` cell). Upper case except the last row, which matches the case of the existing `hud.goldenin` row. Reuse the file's existing wording for the crown, infected and the cursed package in each language (see rows `rules.mode.*`, `mode.start.*`, `hotpotato.out`).

| key | en |
|---|---|
| `mode.goal.keepcrown` | `KEEP THE CROWN` |
| `mode.goal.getcrown` | `GET THE CROWN` |
| `mode.goal.catch` | `CATCH THEM` |
| `mode.goal.dontgetcaught` | `DON'T GET CAUGHT` |
| `mode.goal.passit` | `PASS IT!` |
| `mode.goal.stayaway` | `STAY AWAY` |
| `mode.line.crown.yours` | `YOU HAVE THE CROWN` |
| `mode.line.crown.taken` | `{0} TOOK YOUR CROWN` |
| `mode.line.infected` | `YOU'RE INFECTED` |
| `mode.line.caught` | `CAUGHT {0}` |
| `mode.line.lastfree` | `YOU'RE THE LAST ONE FREE` |
| `mode.line.potato.yours` | `YOU HAVE THE CURSED PACKAGE` |
| `mode.line.potato.passed` | `PASSED TO {0}` |
| `mode.line.out` | `{0} IS OUT` |
| `mode.line.thirty` | `30 SECONDS LEFT` |
| `hud.timesupin` | `Time's Up In...` |

`Key(kind)` returns the `mode.line.*` key in the table's order of `ModeLineKind`; `GoalKey` the `mode.goal.*` key.

- [ ] **Step 1: Write the failing tests** (`ModeFeedbackRulesTests`; build views with a small helper `View(mode, players, crown, potato, infected, outMask)`; compare lines as `(Seat, Kind, Other)` tuples):
  - Crown: nobody → seat 2 picks it up: exactly `(2, CrownYours, NONE)`. Seat 2 → seat 0: `(0, CrownYours, NONE)` and `(2, CrownTaken, 0)`. Seat 2 holds it and leaves (players 0b1011, crown NONE): no lines. Unchanged: no lines.
  - Infection, players 0b1111: start (before `Nothing`, after infected 0b0001): exactly `(0, Infected, NONE)`. Lone hunter 0 catches 2: `(2, Infected, NONE)`, `(0, Caught, 2)`. Two hunters (0b0101) and 1 is caught: `(1, Infected, NONE)`, `(3, LastFree, NONE)`, and no `Caught`. Hunter leaves and a new one is picked in one update (before infected 0b0001, after players 0b1110 infected 0b0010): `(1, Infected, NONE)` only.
  - Hot Potato, players 0b1111: lit on seat 1 from nothing: `(1, PotatoYours, NONE)`. Seat 1 passes to 3: `(3, PotatoYours, NONE)`, `(1, PotatoPassed, 3)`. Seat 3 explodes (out 0b1000, potato NONE): exactly `(EVERYONE_ELSE, PlayerOut, 3)`. Explosion and relight in one update (before potato 3; after out 0b1000, potato 0): `(EVERYONE_ELSE, PlayerOut, 3)`, `(0, PotatoYours, NONE)`, and no `PotatoPassed`.
  - Mode changes between matches (before Kings Crown with crown 1; after Infection infected 0b0100): only `(2, Infected, NONE)`.
  - Standard and Gold Rush views: no lines.
  - `GoalFor`: each row of the spec's goal table; an out player → `None`; a seat not in `Players` → `None`; Standard → `None`.
  - `Key`/`GoalKey` for every enum value (exact strings above); `GoalKey(None)` is null. `Tone` as listed.
  - `GoalAlpha(-1) == 0`, `(0.15f)` is 0.5 ±0.01, `(10) == 1`, `(24.85f)` is 0.5 ±0.01, `(25) == 0`.
  - `WarnsIn` true for KingsCrown and Infection only. `CrossedWarning(31, 29.9f)` true; `(29, 28)` false; `(31, 31)` false; `(31, 0)` false; `(31, -1)` false.
  - `CountdownKey(true) == "hud.goldenin"`, `(false) == "hud.timesupin"`.
  - Strings: all 16 keys are non-null in every `LocaleCodes.All` locale of `StringsCsv.Load()`; `string.Format(en of mode.line.crown.taken, "Ryan") == "Ryan TOOK YOUR CROWN"`.
- [ ] **Step 2:** Run `bash tools/run-tests.sh ModeFeedbackRulesTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement the rules and add the 16 rows.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "ModeFeedbackRulesTests|StringsCsvTests"`. Expected: all pass.

### Task 2: The watcher, the hub, the callout's lines, the warning

**Files:**
- Create: `Assets/Scripts/Management/ModeWatcher.cs` (holds `ModeWatcher` and the static `ModeFeedback`) (+ `.meta`)
- Modify: `Assets/Scripts/UI/StealCallout.cs`, `Assets/Scripts/Player/FeedbackSounds.cs`, `Assets/Scripts/UI/NumberHandler.cs`
- Test: `Assets/Tests/Editor/ModeWatcherTests.cs`, `Assets/Tests/Editor/ModeCalloutTests.cs` (+ `.meta`); extend `FeedbackSoundsTests.cs`

**Interfaces:**
- Consumes: Task 1; `ModeMatch.Current`, `ModeMatch.Changed`, `ModeMatch`'s `Mode`, `Players`, `CrownSeat`, `PotatoSeat`, `Infected`, `Out` (read `Assets/Scripts/Management/ModeMatch.cs`); `OrderManager.Instance.GameTimer` and `RoundRunning`; `HouseRules.Playing`; `Scoreboard.EndsMatch`.
- Produces:

```csharp
public static class ModeFeedback
{
    public static event System.Action<ModeLine> Said;
    public static event System.Action Warned;          // 30 seconds left
    public static void Say(ModeLine line);
    public static void Warn();
    // both cleared on SubsystemRegistration
}

public class ModeWatcher : MonoBehaviour   // made at launch like ModeInfoBox ([RuntimeInitializeOnLoadMethod(AfterSceneLoad)], DontDestroyOnLoad)
{
    internal static ModeView ViewOf(ModeMatch match);          // null → ModeView.Nothing
    internal void See(ModeView now);                           // says ModeFeedbackRules.Lines(last, now), then last = now
    internal void SeeClock(GameMode mode, float timeLeft, bool running);   // Warn() once when CrossedWarning(lastTime, timeLeft) in a WarnsIn mode while running; not running → forgets lastTime
    internal void Forget();                                    // last = Nothing, clock forgotten: at match end
    // OnEnable: ModeMatch.Changed += OnChanged (See(ViewOf(ModeMatch.Current))); Update: SeeClock from OrderManager, and Forget() when Scoreboard.EndsMatch(state)
}

// StealCallout
internal void HearMode(ModeLine line);     // this player's line: Seat == their seat, or EVERYONE_ELSE and they are not Other. Loc.Get(Key, Short(ShownName(Other))) in Tone. PotatoYours also plays FeedbackSounds.CURSED
internal void HearWarning();               // Loc.Get("mode.line.thirty") in Plain, FeedbackSounds.WARNING, and the HUD's timer pulse
// subscribes to ModeFeedback.Said and Warned beside its other events

// FeedbackSounds
public const string CURSED = "cursed", WARNING = "warning";
// Volume(name): CURSED 0.5, WARNING 0.45 (the others unchanged)

// NumberHandler
internal void PulseTimer();   // a short grow-and-shrink of the timer's digits (DOTween DOPunchScale on the timer digits' shared parent, 0.25 larger, 0.4 s). Null-safe; does nothing outside play mode
```

- `StealCallout.AddTo` already receives the player's `NumberHandler`: keep it in a field for `PulseTimer`.
- An out player (Hot Potato) still reads `<name> IS OUT` for others; that is fine.
- A lead line (Pack B) waits for a mode line like any other line; mode lines show at once.

- [ ] **Step 1: Write the failing tests.** `ModeWatcherTests` (a watcher added to a test object; a recorder on `ModeFeedback.Said` and `Warned`): `See` of a Kings Crown view with the crown on seat 2 says `(2, CrownYours, NONE)`; the same view again says nothing; `Forget()` then the same view says it again; `SeeClock(KingsCrown, 31, true)` then `(…, 29.5f, true)` warns once; another `(…, 29, true)` doesn't; `SeeClock(Standard, 31, true)` then `29` doesn't; not running then running at 29 doesn't (no crossing was seen); `ViewOf(null)` equals `ModeView.Nothing` field by field. `ModeCalloutTests` (players built as in `GainCalloutTests`, clock set): `HearMode((0, CrownYours, NONE))` shows `Loc.Get("mode.line.crown.yours")` on seat 0 and nothing on seat 1; `HearMode((1, CrownTaken, 0))` shows seat 0's short name on seat 1's line, in red (`ModeIcons.RED`); `HearMode((EVERYONE_ELSE, PlayerOut, 2))` shows on seats 0 and 1 and not on seat 2; `HearWarning()` shows `Loc.Get("mode.line.thirty")` on every callout. `FeedbackSoundsTests`: `Volume(CURSED) == 0.5f`, `Volume(WARNING) == 0.45f`, and the existing volume test still passes; do not assert that the two new clips exist (their files come later).
- [ ] **Step 2:** Run `bash tools/run-tests.sh "ModeWatcherTests|ModeCalloutTests"`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "ModeWatcherTests|ModeCalloutTests|FeedbackSoundsTests|GainCalloutTests|StealCalloutTests"`. Expected: all pass.

### Task 3: The goal tag and the countdown's words

**Files:**
- Create: `Assets/Scripts/UI/GoalTag.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`Start`, beside `StealCallout.AddTo`; CRLF: Edit tool only), `Assets/Scripts/UI/NumberHandler.cs` (`SetFinalCountdown`)
- Test: `Assets/Tests/Editor/GoalTagTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: Task 1; `ModeWatcher.ViewOf`; `ModeInfoBox.SECONDS`; `PlayerInstantiate.DrivingFrom`; `GameCard.Words`; `OrderSync.SeatOf`.
- Produces:

```csharp
public class GoalTag : MonoBehaviour
{
    internal const float SIZE = 26f, WIDTH = 320f, HEIGHT = 36f;
    internal const float Y = 310f;   // from the middle of the HUD (1920 x 1080): just under the compass
    internal System.Func<float> Clock;                       // defaults to unscaled time
    internal static GoalTag AddTo(OrderHandler player, NumberHandler hud);   // null hud → null. Builds one TextMeshProUGUI on the HUD's root canvas: text only, white, black outline, rich text off, no wrapping
    internal string ShownWords { get; }                      // "" when nothing is up
    internal ModeRole Shown { get; }
    // The player's goal now. A different goal shows its words and starts the 25 s again from max(now, notBefore); None clears the tag
    internal void See(ModeRole goal, float notBefore);
    internal float Alpha(bool paused);                       // 0 while paused; else ModeFeedbackRules.GoalAlpha(Clock() - shownAt)
    // Update: goal = match ended or not driving ? None : GoalFor(ViewOf(ModeMatch.Current), seat); See(goal, driving start + ModeInfoBox.SECONDS); line alpha = Alpha(paused)
}
```

- `OrderHandler.Start`: `GoalTag.AddTo(this, numberHandler)` in the same `if (!RemoteAvatar.IsRemote(this))` block as `StealCallout.AddTo`.
- "Driving" is the test `ModeInfoBox` uses (read `Assets/Scripts/UI/ModeInfoBox.cs`); `notBefore` is `PlayerInstantiate.DrivingFrom + ModeInfoBox.SECONDS` converted to the tag's clock if the two clocks differ (say how you handled it in your report).
- **Countdown words:** `NumberHandler.SetFinalCountdown(bool enabled)` shows a text object that reads "Golden Order In...". When it is enabled, set that object's text to `Loc.Get(ModeFeedbackRules.CountdownKey(HouseRules.Playing.GoldenOn))`. First find out how that text is localised today (search for `hud.goldenin` and `SceneTexts`) and make sure your text is not overwritten by it; report what you found.

- [ ] **Step 1: Write the failing tests** (`GoalTagTests`, players built as in `GainCalloutTests`, `Clock` set to a field): `AddTo(player, null)` is null; a new tag shows nothing (`ShownWords == ""`, `Alpha(false) == 0`); `See(GetCrown, 0)` at now 10 → `ShownWords == Loc.Get("mode.goal.getcrown")`, `Alpha(false)` is 1 at now 11, 0 at now 35.1; `See(GetCrown, 0)` again at now 20 doesn't restart it (still 0 at 35.1); `See(KeepCrown, 0)` at now 30 shows the new words and is up until 55; `See(None, 0)` clears the words; `See(PassIt, 50)` at now 40 → `Alpha(false) == 0` at now 45, 1 at now 51; `Alpha(true) == 0` while it would otherwise be 1; the text has `richText` false, no wrapping, `fontSize <= 26` and width `<= 320`.
- [ ] **Step 2:** Run `bash tools/run-tests.sh GoalTagTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "GoalTagTests|ModeFeedbackRulesTests|StealCalloutTests|ScoreboardTests"`. Expected: all pass.

### Task 4: Sounds, documents, playtest note, full run (main session)

- [ ] Send the user candidates for the two sounds from the Kenney CC0 packs; add the approved files as `Assets/Resources/Audio/Feedback/cursed.ogg` and `warning.ogg` (+ `.meta`), record them in `LICENSES.md`, add clip-exists tests.
- [ ] GDD and TDD (minor versions up, Revision History); `EDITOR-TODO.md` 16j; mark Pack C built in the handoff.
- [ ] Review each agent's diff against the spec; run the full suite in the background; report.

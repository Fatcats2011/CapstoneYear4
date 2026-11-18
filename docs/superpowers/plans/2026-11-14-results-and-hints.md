# Pack G: Results Breakdown and First-Time Hints — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Each results card shows where its player's money came from, and four rules are explained once each, the first time they matter.

**Architecture:** As Packs A to D: pure rules classes with edit-mode tests, small static hubs, views built in code. The host counts money by source (`MatchAwards` keeps a `MoneyTally`) and the amounts ride to clients inside the existing `AwardBoard` message. Hints are local to each machine and saved with the settings.

**Tech Stack:** Unity 2022.3, C#, TextMeshPro, Netcode for GameObjects, NUnit edit-mode tests.

**Spec:** `docs/superpowers/specs/2026-11-14-results-and-hints-design.md`

## Global Constraints

- Never commit, push or merge: the user commits. Work in place on `steam-phase1a`.
- Never edit scenes, prefabs, `.inputactions` or ProjectSettings. UI is built in code.
- Every new file under `Assets/` needs a `.meta`: `bash tools/newmeta.sh <path>`.
- Check a file's line endings (`file <path>`) before editing; CRLF files (`OrderHandler.cs`, `Order.cs`, `NumberHandler.cs` and others) are changed with the Edit tool only.
- `Assets/Resources/Localization/strings.csv` is LF, UTF-8: `key,note,en,fr,de,it,nl,es,es-419,pt,pt-BR,pl,tr,cs,ro,hu,sv,da,reviewed` (the last cell empty). Every new key gets all 16 languages. Quote a cell that contains a comma.
- Tests: `bash tools/run-tests.sh "<filter>"`, one run at a time. Edit-mode only: `Awake`, `OnEnable`, `Update` don't run. No lambdas that capture test locals.
- The match plays in `GameState.Begin`, not `MainLoop`.
- Added UI stays small. No new sounds.
- TDD: write each test, watch it fail, then write the code.

## Review Focus

- The four sources of a seat always add up to that seat's score, the tutorial's money included; nothing takes money away.
- A stolen Hard order delivered for $160: $80 Deliveries, $80 Steals. The golden order: all Golden. The Mother Lode: all Deliveries.
- An amount over 65,535 is clamped in the board, never wrapped.
- A client counts nothing: its cards show the host's amounts when the awards arrive, and old `AwardBoard` tests still pass.
- A hint is marked seen only when a local player's callout takes it; another machine's player earning it changes nothing here. No hints in the tutorial.

---

### Task 1: Count money by source and carry it in the awards

**Files:**
- Create: `Assets/Scripts/Menu/MoneyTally.cs` (with `MoneySource` and `MoneyRules`), `Assets/Scripts/Steam/MoneySync.cs`
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (CRLF: the two delivery branches, `AwardGoldenBonus`, `PayForSteal`, `AddModePoints`), `Assets/Scripts/Menu/MatchAwards.cs`, `Assets/Scripts/Online/AwardBoard.cs`
- Test: `Assets/Tests/Editor/MoneyTallyTests.cs`; extend `AwardBoardTests.cs`, `MatchAwardsTests.cs`

**Interfaces — Produces:**

```csharp
public enum MoneySource : byte { Deliveries, Steals, Golden, Mode }   // also the order the cards list them

public static class MoneyRules
{
    public const int SOURCES = 4;
    // What a delivered order's pay counts as. Golden: all Golden. Lode: all Deliveries.
    // Otherwise Deliveries = StealValueRules.Price(tier), Steals = worth - that (never negative)
    public static void SplitDelivery(Constants.OrderValue tier, bool isLode, int worth, out int deliveries, out int steals, out int golden);
}

public class MoneyTally   // per seat, per source
{
    public void Add(int seat, MoneySource source, int amount);   // ignores seat < 0 or >= Constants.MAX_PLAYERS and amount <= 0
    public int Of(int seat, MoneySource source);
    public int Total(int seat);
    public void NewMatch();
}

public static class MoneySync   // as FeatSync: the game says, MatchAwards counts
{
    public static event System.Action<int, MoneySource, int> Earned;
    public static void Earn(int seat, MoneySource source, int amount);   // nothing for amount <= 0
    public static void Reset();   // on SubsystemRegistration, as FeatSync
}

// AwardBoard: sixteen more ushorts (four per seat), serialised after the counts
public const ushort MAX_MONEY = ushort.MaxValue;
public static AwardBoard From(MatchStats stats, IEnumerable<int> seats, MoneyTally money = null);
public int MoneyOf(int seat, MoneySource source);
public void SetMoney(int seat, MoneySource source, int amount);   // clamped 0..MAX_MONEY
```

- `OrderHandler`, each inside its existing authority-only path, beside the `score +=` line: a delivery calls `MoneyRules.SplitDelivery` and `MoneySync.Earn` for each non-zero part; `AwardGoldenBonus` earns Golden; `PayForSteal` earns Steals for `paid`; `AddModePoints` earns Mode. Unlike `MatchStats`, the tutorial counts.
- `MatchAwards`: owns a `MoneyTally`, listens to `MoneySync.Earned` in `Begin`/`End` (authority only), clears it in `NewMatch`, passes it to `AwardBoard.From` in `Results`. Expose `internal MoneyTally Money` for tests.
- `AwardBoard.Equals`/`GetHashCode` include the money.

- [ ] **Step 1:** `MoneyTallyTests`: `SplitDelivery` for Easy unstolen (55/0/0), Hard stolen once (80/80/0), Golden (0/0/worth), Lode 250 (250/0/0); tally adds, ignores bad seats and non-positive amounts, `Total` is the four summed, `NewMatch` clears; `MoneySync.Earn` raises once with its values and not for 0.
- [ ] **Step 2:** `AwardBoardTests`: `SetMoney`/`MoneyOf` per seat and source; 70,000 clamps to 65,535; a board with money survives the same serialise round trip the existing tests use; `From` with a tally copies its amounts; `From` without one has zeros. `MatchAwardsTests`: an `Earn` reaches `Money`; `NewMatch` clears it.
- [ ] **Step 3:** Run `bash tools/run-tests.sh "MoneyTally|AwardBoard|MatchAwards"`. Expected: compile failure.
- [ ] **Step 4:** Write the code. Run `bash tools/run-tests.sh "MoneyTally|AwardBoard|MatchAwards|OnlineAwards|AwardRules|ResultsAwardsView|ScoreFeedback|GainCallout"`. Expected: all pass.

### Task 2: The breakdown on the results cards

**Files:**
- Create: `Assets/Scripts/Menu/BreakdownRules.cs`
- Modify: `Assets/Scripts/Menu/ResultsAwardsView.cs`, `Assets/Resources/Localization/strings.csv`
- Test: `Assets/Tests/Editor/BreakdownRulesTests.cs`; extend `ResultsAwardsViewTests.cs`

**Interfaces — Consumes:** `AwardBoard.MoneyOf`, `MoneySource`. **Produces:**

```csharp
public static class BreakdownRules
{
    public const float ROW = 22f;          // one row's height on the card, in the canvas's 1080
    public const float TEXT = 16f;
    public static string Key(MoneySource source);          // "results.deliveries", "results.steals", "results.golden", "results.mode"
    public static int Rows(int shown);                     // 0 -> 0, 1-2 -> 1, 3-4 -> 2
    public static float ExtraHeight(int shown);            // Rows * ROW
    // The sources that paid something, in MoneySource order
    public static void Shown(AwardBoard board, int seat, System.Collections.Generic.List<MoneySource> into);
}
// ResultsAwardsView
internal string BreakdownText(int score);   // the card's lines joined with "\n", e.g. "Deliveries $520 | Steals $21\nGolden $80"; "" with none
internal float CardHeight(int score);
```

- In `ResultsAwardsView.Show(board, seatOfScore)`: each card lists its seat's sources that paid, two per row, label left and amount right in each half (`Loc.Get(key)` then `$amount`), under the header's rule and above the award. The card's height becomes `HEIGHT + ExtraHeight` and the award block moves down by the same amount (the card's pivot is its bottom edge, so it grows upward; keep the vote tag on the top edge). Text `BreakdownRules.TEXT`, the card's `MONEY` colour, built with the file's own `Text` helper. With nothing to show the card is exactly as today.
- Strings (16 languages): `results.deliveries` "Deliveries", `results.steals` "Steals", `results.golden` "Golden", `results.mode` "Mode". Keep each under about 12 characters in every language (a half card is about 130 px wide at 16 px text).

- [ ] **Step 1:** `BreakdownRulesTests`: keys; `Rows` and `ExtraHeight` for 0 to 4; `Shown` skips zeros and keeps the order; all four keys exist in all 16 languages (model on how `StealCalloutTests` checks its keys in every language).
- [ ] **Step 2:** `ResultsAwardsViewTests`: a board with no money leaves `CardHeight` at `HEIGHT` and `BreakdownText` ""; two sources give one row and `HEIGHT + 22`; four give two rows and `HEIGHT + 44`, with the amounts in the text; awards arriving later (`Follow` / `OnArrived` path the existing tests use) fill it too.
- [ ] **Step 3:** Run `bash tools/run-tests.sh "BreakdownRules|ResultsAwardsView"`. Expected: compile failure.
- [ ] **Step 4:** Write the code and strings. Run `bash tools/run-tests.sh "BreakdownRules|ResultsAwardsView|ResultsMenu|Localization|Loc"`. Expected: all pass.

### Task 3: First-time hints

**Files:**
- Create: `Assets/Scripts/World/HintRules.cs` (with `HintKind`), `Assets/Scripts/World/Hints.cs`
- Modify: `Assets/Scripts/UI/StealCallout.cs`, `Assets/Scripts/Menu/GameSettings.cs` and `Assets/Scripts/Menu/OptionsMenu.cs` (load and save one int), `Assets/Scripts/World/Order.cs` (CRLF, where `playerHolding.HasGoldenOrder = true`), `Assets/Resources/Localization/strings.csv`
- Test: `Assets/Tests/Editor/HintRulesTests.cs`, `Assets/Tests/Editor/HintCalloutTests.cs`

**Interfaces — Produces:**

```csharp
public enum HintKind : byte { BoostToSteal, BoostToBlock, StolenPaysDouble, HoldGold }

public static class HintRules
{
    public const float HINT_SECONDS = 4f;
    public static string Key(HintKind kind);              // "hint.boost_to_steal", "hint.boost_to_block", "hint.stolen_double", "hint.hold_gold"
    public static bool ShowsIn(GameState state);          // not Tutorial
    public static bool Seen(int mask, HintKind kind);
    public static int Mark(int mask, HintKind kind);
}

public static class Hints
{
    public static int Seen { get; set; }                               // the saved mask; setting it raises nothing
    public static event System.Action<int> SeenChanged;                // to save it
    public static event System.Action<int, HintKind> Offered;          // seat, hint: a callout for that seat may take it
    public static void Offer(HintKind kind, int seat);                 // nothing when seen, seat < 0, or the state doesn't show hints
    public static bool Take(HintKind kind);                            // false when already seen; else marks, raises SeenChanged, true
    public static void Reset();                                        // SubsystemRegistration: events cleared (Seen is reloaded by the settings)
}
// StealCallout
internal void HearHint(int seat, HintKind kind);   // its own seat only: Take, then show when the line is free
```

- Offers (each on every machine, for the seat it happened to):
  - `BoostToSteal`: `FeatSync.Bumped` (bumper seat).
  - `BoostToBlock`: `StealFeedback.Happened` with outcome `Stolen` (victim seat).
  - `StolenPaysDouble`: the same notice (attacker seat).
  - `HoldGold`: where `Order` sets `playerHolding.HasGoldenOrder = true` (`OrderSync.SeatOf(playerHolding)`).
  Put the first three subscriptions in one small place (a runtime-made listener as `ModeWatcher`, or inside `Hints` with a `RuntimeInitializeOnLoadMethod`), not in each callout.
- `StealCallout.HearHint`: for its own seat, `Hints.Take(kind)`; if true, hold the hint as the lead line is held and show it (plain tone, `Loc.Get(HintRules.Key(kind))`) once no line is up or the line up has finished; it stays `HINT_SECONDS` (the other lines keep `CALLOUT_SECONDS`); any newer event line replaces it. Read how `leadHeld` / `Tick` / `LineIsFresh` work and reuse them.
- Saving: `GameSettings` gains `public int hintsSeen;`. Where the settings load and apply (`OptionsMenu`, near `Comfort.ReduceMotion = settings.reduceMotion == 1`), set `Hints.Seen`; on `Hints.SeenChanged`, store the mask and save the file the way an option change does. If the options object isn't alive during a match, give `Hints` its own tiny load/save of that one field through the same file helper `OptionsMenu` uses, and say which you did.
- Strings (16 languages, upper case, each at most 30 characters): `hint.boost_to_steal` "BOOST INTO RIVALS TO STEAL", `hint.boost_to_block` "BOOST TO BLOCK A THIEF", `hint.stolen_double` "STOLEN ORDERS PAY DOUBLE", `hint.hold_gold` "HOLD GOLD LONGER, EARN MORE" (quote cells with commas).

- [ ] **Step 1:** `HintRulesTests`: keys; `ShowsIn` false only for Tutorial; `Mark` then `Seen`; marking one leaves the others unseen; `Take` true once then false and raises `SeenChanged` once; `Offer` raises `Offered` for an unseen hint and not for a seen one or seat -1; the four keys in all 16 languages, none longer than 30 characters.
- [ ] **Step 2:** `HintCalloutTests` (setup as `StealCalloutTests`): a hint for another seat shows nothing and is not marked seen; a hint for this seat shows its words and is marked seen; the same hint again shows nothing; a hint heard while an event line is fresh waits, then shows after it; an event line replaces a hint; a hint stays up past `CALLOUT_SECONDS` and is gone after `HINT_SECONDS`.
- [ ] **Step 3:** Run `bash tools/run-tests.sh "HintRules|HintCallout"`. Expected: compile failure.
- [ ] **Step 4:** Write the code and strings. Run `bash tools/run-tests.sh "Hint|StealCallout|GainCallout|ModeCallout|GoalTag|OptionsMenu|GameSettings|Localization"`. Expected: all pass.

### Task 4: Documents and the full suite (done by the lead)

- [ ] `EDITOR-TODO.md` 16l, handoff doc, spec status; GDD and TDD with revision rows; `docs/online.md` (the awards message carries money).
- [ ] Full suite in the background. Expected: 0 failed.

# Score Gains with Reasons (Pack B) Implementation Plan

> **For agentic workers:** each task is handed to one Sonnet agent, one at a time (the user asked for Sonnet agents on the smaller tasks, 2026-11-14; there is one test mirror). The main session reviews each diff, runs the full suite and updates the documents. Steps use checkbox (`- [ ]`) syntax.

**Goal:** A player who earns money reads how much and why in their own callout line; the scoreboard shows a `+$` and counts up; a change of first place is called out.

**Architecture:** A pure rules class (`GainFeedbackRules`) and a static hub (`ScoreFeedback`). Every machine announces a gain as it replays a delivery (`OrderHandler.DeliverOrder`), and a lead change from the scoreboard's own ranking, so all machines agree without new network messages. Pack A's `StealCallout` shows gain and lead lines; `Scoreboard.Row` shows the `+$` and counts up from the change in each total.

**Tech Stack:** Unity 2022.3, C#, TextMeshPro, NUnit edit-mode tests.

**Spec:** `docs/superpowers/specs/2026-11-14-score-gain-feedback-design.md`

## Global Constraints

- Read `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`, "How to work in this repo". In short:
  - **Never commit, push, stash or switch branch.** Work in place on `steam-phase1a`. The tree has other uncommitted work: touch only the files your task names.
  - Never edit a scene, prefab, `.inputactions` or ProjectSettings file.
  - Check line endings with `git ls-files --eol <file>` and keep them. `Order.cs` is CRLF: Edit tool only, never `sed -i`.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
  - Tests: `bash tools/run-tests.sh "<filter>"` (a regex of class names; takes a few minutes; a compile error prints `NO RESULTS`). One run at a time. Never run the full suite.
  - TDD: write the test, run it and see it fail (or `NO RESULTS`), implement, run it and see it pass.
  - Tests are edit mode: `Awake`, `OnEnable` and `Update` don't run. Build objects explicitly and call internal methods directly. No lambda may capture a test's local; use recorder methods subscribed as method groups. Follow `Assets/Tests/Editor/StealCalloutTests.cs` and `StealFeedbackTests.cs`.
  - Comments: every class and public/internal method gets a plain-English `/// <summary>` in the style of `Assets/Scripts/World/StealFeedbackRules.cs`.
- No new sounds. No new screen area: the callout line is Pack A's; the `+$` sits inside a scoreboard row.
- No line for per-second mode points or Gold Rush nuggets (they never go through `DeliverOrder`).
- Timings: merge window 0.5 s; scoreboard `+$` 1.2 s; count-up 0.4 s.

## Review Focus

1. **A gain of 0 or less** (a hostile or buggy value): no line, no `+$`. Test in Task 1 (`AnnounceGain`) and Task 3.
2. **A score reset between matches** (totals drop to 0): no `+$`, no lead line, and the next match's first leader is not announced. Test in Task 3.
3. **A tie for first:** the lead doesn't change until someone is strictly ahead. Test in Task 1.
4. **A lead change in the same instant as a delivery:** the gain line shows first, the lead line after it. Test in Task 2.
5. **A player leaves mid-match (their seat vanishes from the ranking):** no exception; if they led, the next strict leader is announced. Test in Task 1 (`NewLeader` with a missing seat).

---

### Task 1: Rules, hub and strings

**Files:**
- Create: `Assets/Scripts/World/GainFeedbackRules.cs`, `Assets/Scripts/World/ScoreFeedback.cs` (+ `.meta`)
- Modify: `Assets/Resources/Localization/strings.csv` (append rows; LF endings; keep the trailing empty `reviewed` column)
- Test: `Assets/Tests/Editor/GainFeedbackRulesTests.cs`, `Assets/Tests/Editor/ScoreFeedbackTests.cs` (+ `.meta`)

**Interfaces — Produces:**

```csharp
public enum GainReason : byte { Delivered, Golden, Lode }

public static class GainFeedbackRules
{
    public const float MERGE_SECONDS = 0.5f;
    public const float BOARD_POP_SECONDS = 1.2f;
    public const float BOARD_POP_FADE = 0.3f;
    public const float COUNT_UP_SECONDS = 0.4f;
    public const float GAIN_HOLD = 0.8f;   // how long a gain line is up before a lead line may replace it

    public static GainReason ReasonFor(Constants.OrderValue tier, bool isLode);   // lode → Lode; Golden → Golden; else Delivered
    // strings.csv key: "gain.delivered_stolen" for Delivered with a stamp (stamp != ""), else "gain.delivered" / "gain.golden" / "gain.lode"
    public static string Key(GainReason reason, string stamp);
    public static bool BoardShowsGains(GameMode mode);   // Standard, GoldRush
    public static bool LeadCalled(GameMode mode);        // Standard, GoldRush, KingsCrown
    public static int CountUp(int from, int to, float age);   // from at age <= 0, to at age >= COUNT_UP_SECONDS, linear between, rounded
    public static float PopAlpha(float age);   // 0 outside [0, BOARD_POP_SECONDS); 1 until the last BOARD_POP_FADE, then down to 0
    // Who leads: the first seat of the ranking if it is strictly ahead of the second (or alone with more than 0); otherwise lastLeader if it is still in the ranking, else -1
    public static int NewLeader(int lastLeader, System.Collections.Generic.IReadOnlyList<int> ranking, System.Func<int, int> points);
    public static bool LeadChanged(int lastLeader, int newLeader);   // lastLeader >= 0 && newLeader >= 0 && newLeader != lastLeader
}

public static class ScoreFeedback
{
    public static event System.Action<int, GainReason, int, string> Gained;   // seat, reason, amount, stamp ("" for none)
    public static event System.Action<int, int> LeadTaken;                    // new leader's seat, old leader's seat
    public static void AnnounceGain(int seat, GainReason reason, int amount, string stamp);   // amount <= 0 or seat < 0: dropped; null stamp → ""
    public static void AnnounceLead(int newLeader, int oldLeader);
    // both events cleared on RuntimeInitializeLoadType.SubsystemRegistration, like StealFeedback
}
```

**Strings** (key, note, then 16 locales in the header's order, then an empty cell). English:

| key | en |
|---|---|
| `gain.delivered` | `DELIVERED +${0}` |
| `gain.delivered_stolen` | `DELIVERED {1} +${0}` ({1} is the stamp, e.g. ×2) |
| `gain.golden` | `GOLDEN ORDER +${0}` |
| `gain.lode` | `MOTHER LODE +${0}` |
| `lead.you` | `YOU TOOK THE LEAD` |
| `lead.other` | `{0} TOOK THE LEAD` |

Translate in upper case, short. Reuse the file's existing wording for "golden order" (`rules.golden`) and the Mother Lode (search the file for the mode's existing lode wording) in each language. Every locale keeps exactly the English row's placeholders.

- [ ] **Step 1: Write the failing tests.** `GainFeedbackRulesTests`: `ReasonFor(Easy,false)==Delivered`, `(Golden,false)==Golden`, `(Hard,true)==Lode`, `(Golden,true)==Lode`; `Key(Delivered,"")=="gain.delivered"`, `Key(Delivered,"×2")=="gain.delivered_stolen"`, `Key(Golden,"×2")=="gain.golden"`, `Key(Lode,"")=="gain.lode"`; `BoardShowsGains` true for Standard and GoldRush, false for KingsCrown, Infection, HotPotato; `LeadCalled` true for Standard, GoldRush, KingsCrown, false for Infection, HotPotato; `CountUp(100,200,-1)==100`, `(100,200,0.2f)==150`, `(100,200,0.4f)==200`, `(100,200,9)==200`; `PopAlpha(-0.1f)==0`, `PopAlpha(0.5f)==1`, `PopAlpha(1.05f)` is 0.5 ±0.01, `PopAlpha(1.2f)==0`. `NewLeader` with a points dictionary read through a helper method (no lambda capturing locals: make the dictionary a field): ranking [1,0] points {1:50,0:0}, last -1 → 1; ranking [1,0] points equal 50/50, last 0 → 0 (a tie keeps the leader); same tie, last -1 → -1; ranking [2,1] strictly ahead, last 0 (seat 0 gone) → 2; tie between 2 and 1, last 0 gone → -1; ranking [3] points 0 → last; ranking [3] points 10 → 3; empty ranking → -1. `LeadChanged(-1,1)` false, `(0,1)` true, `(1,1)` false, `(0,-1)` false. `ScoreFeedbackTests`: a gain is heard once with its four values; amount 0, amount -5 and seat -1 are never heard; a null stamp arrives as ""; a lead is heard once with both seats. A strings test in `GainFeedbackRulesTests`: each of the six keys is non-null in every `LocaleCodes.All` locale of `StringsCsv.Load()`, and `string.Format(en of gain.delivered_stolen, 130, "×2") == "DELIVERED ×2 +$130"`.
- [ ] **Step 2:** Run `bash tools/run-tests.sh "GainFeedbackRulesTests|ScoreFeedbackTests"`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement the two classes and add the six rows.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "GainFeedbackRulesTests|ScoreFeedbackTests|StringsCsvTests"`. Expected: all pass.

### Task 2: Announce deliveries; the callout shows gains and lead changes

**Files:**
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`DeliverOrder`, about lines 165–200), `Assets/Scripts/UI/StealCallout.cs`
- Test: `Assets/Tests/Editor/GainCalloutTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: Task 1; `StealFeedbackRules.StampText(tier, isLode, steals)`, `StealFeedbackRules.Short`, `Scoreboard.ShownName(int)`, `OrderSync.SeatOf`, `Loc.Get`, `CalloutTone`.
- Produces:

```csharp
// OrderHandler
// What a delivery of this order pays on this machine: the Mother Lode's worth, the golden order's full value
// (OrderManager.Instance.FinalOrderValue when there is an order manager, else its Worth), otherwise its Worth
internal static int DeliveryPay(Order order);

// StealCallout
internal System.Func<float> Clock;   // defaults to () => Time.unscaledTime; tests set it
internal void HearGain(int seat, GainReason reason, int amount, string stamp);
internal void HearLead(int newLeader, int oldLeader);
internal void Tick();   // shows a held lead line once the gain line has had GAIN_HOLD; Update calls it
```

- `DeliverOrder`: in both branches (order1 and order2), before the order's own `DeliverOrder()` is called and on every machine (outside the `GameAuthority.IsAuthority` block), call `ScoreFeedback.AnnounceGain(OrderSync.SeatOf(this), GainFeedbackRules.ReasonFor(order.Value, order.IsLode), DeliveryPay(order), StealFeedbackRules.StampText(order.Value, order.IsLode, order.Steals))`. Put the shared lines in one private helper rather than writing them twice.
- `StealCallout` subscribes to `ScoreFeedback.Gained` and `LeadTaken` beside `StealFeedback.Happened` (and unsubscribes).
- `HearGain`: not this player's seat → nothing. If the last line shown was a gain line less than `MERGE_SECONDS` ago, the new line's amount is the sum of both; it keeps the reason and stamp when both are the same, otherwise it is a plain `gain.delivered` with no stamp. Shows `Loc.Get(Key(reason, stamp), amount, stamp)` in `CalloutTone.Thief` (gold).
- `HearLead`: this player is the new leader → `Loc.Get("lead.you")`, gold. This player is the old leader → `Loc.Get("lead.other", Short(ShownName(newLeader)))`, `CalloutTone.Victim` (red). Anyone else → nothing. If a gain line is up and younger than `GAIN_HOLD`, the lead line is held (one held line; a newer one replaces it) and `Tick` shows it once the gain line reaches `GAIN_HOLD`.
- A steal line (Pack A) always shows at once, as now, and clears any held lead line's wait only by replacing the gain line (a held lead line then shows on the next `Tick`).
- Update the class's summary comment: it is now the player's callout for steals, gains and lead changes.

- [ ] **Step 1: Write the failing tests** in `GainCalloutTests`, building players as `StealCalloutTests.NewPlayer` does and setting `callout.Clock` to a method that returns a field `now`: a gain for this seat shows `Loc.Get("gain.delivered", 65, "")`; a stolen one shows the `×2` key's text; a gain for another seat shows nothing; two gains 0.3 s apart show one line with the sum (65 + 80 → 145, plain delivered); two gains 0.6 s apart show the second alone; `HearLead(0, 1)` shows `lead.you` on seat 0's callout and `lead.other` with seat 0's short name on seat 1's, nothing on seat 2's; a lead heard 0.1 s after a gain leaves the gain line up, and after `now += 0.8f; Tick()` the lead line is up; a lead heard with no line up shows at once. `OrderHandler.DeliveryPay`: an Easy order stolen once → 110; an Easy order never stolen → 55; a Golden order with no `OrderManager` → 50.
- [ ] **Step 2:** Run `bash tools/run-tests.sh GainCalloutTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "GainCalloutTests|StealCalloutTests|StealFeedbackTests|OrderStealValueTests|HighlightEventsTests"`. Expected: all pass.

### Task 3: The scoreboard's `+$`, count-up and lead watch

**Files:**
- Modify: `Assets/Scripts/UI/Scoreboard.cs`
- Test: `Assets/Tests/Editor/ScoreboardGainTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: Task 1.
- Produces:

```csharp
// Scoreboard: a small pure helper the row and the tests share
internal struct MoneyShow
{
    public int Total;        // the last total seen: -1 before any
    public int From;         // what the number counts up from
    public int Gain;         // the "+$" showing: 0 for none
    public float GainedAt;   // when the last gain landed
    // A new total at a time: a rise adds to Gain (or starts it, if the last pop has ended), sets From to what was
    // showing and GainedAt to now; a fall or the first total clears Gain and From. showGains false: never a Gain
    public void See(int total, float now, bool showGains);
    public int Shown(float now);          // GainFeedbackRules.CountUp(From, Total, now - GainedAt) while a Gain shows, else Total
    public string GainText(float now);    // "+$" + Gain while PopAlpha(now - GainedAt) > 0, else ""
}

internal int Leader { get; }   // the seat the board last saw leading: -1 for none
// Each frame's ranking: announces a lead change. Reset to -1 when the match ends or the mode doesn't call leads
internal void WatchLead(System.Collections.Generic.IReadOnlyList<int> ranking, System.Func<int, int> points, GameMode mode);
```

- `Row`: holds a `MoneyShow`. Where it sets the money text today, it calls `See` with `GainFeedbackRules.BoardShowsGains(mode)` and writes `ScoreText(mode, Shown(now))` each frame the shown value changes. A new small label (`Label(...)`, size 16, bold, `GameCard.GOLD` with an `INK` outline of 0.3, non-wrapping) sits at the top of the money column, over the number, and shows `GainText`, with its alpha from `GainFeedbackRules.PopAlpha`. It stays inside the row.
- Use the same clock the board already uses for its slide and flame (read the file); pass it to the row.
- `WatchLead`: `if (!LeadCalled(mode)) { leader = -1; return; }` then `int now = NewLeader(leader, ranking, points); if (LeadChanged(leader, now)) ScoreFeedback.AnnounceLead(now, leader); leader = now;`. Call it from the board's update where it already has the ranking, only in the states where the board is up; set `leader = -1` where the board handles `EndsMatch`.

- [ ] **Step 1: Write the failing tests** in `ScoreboardGainTests`. `MoneyShow`: first `See(0, 1f, true)` → `Gain == 0`, `GainText(1f) == ""`; then `See(65, 2f, true)` → `GainText(2f) == "+$65"`, `Shown(2f) == 0`, `Shown(2.2f)` is 32 or 33, `Shown(2.4f) == 65`; then `See(145, 2.5f, true)` → `GainText(2.5f) == "+$145"` and `Shown(2.5f) == 65`; after the pop ends (`GainText(4f) == ""`), `See(155, 5f, true)` → `"+$10"`; `See(0, 6f, true)` (a reset) → `Gain == 0`, `Shown(6f) == 0`, `GainText(6f) == ""`; with `showGains` false a rise gives `GainText == ""` and `Shown` equal to the total at once. `WatchLead` on a `Scoreboard` added to a test object, with a recorder on `ScoreFeedback.LeadTaken` and points read from a field dictionary through a method: first strict leader (seat 1) → `Leader == 1`, nothing announced; seat 0 passes → one announcement `(0, 1)`; a tie → nothing, `Leader` stays; mode `Infection` → `Leader == -1`, nothing announced; back in Standard with the same points → nothing announced (it starts over).
- [ ] **Step 2:** Run `bash tools/run-tests.sh ScoreboardGainTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `bash tools/run-tests.sh "ScoreboardGainTests|ScoreboardTests|GainFeedbackRulesTests"`. Expected: all pass.

### Task 4: Documents, playtest note, full run (main session)

- [ ] GDD (minor version up, Revision History): the gain lines, the scoreboard `+$` and count-up, the lead-change lines; values checked against the code.
- [ ] TDD (minor version up, Revision History): `GainFeedbackRules`, `ScoreFeedback`, `Scoreboard.MoneyShow` and `WatchLead`, the callout's wider job, the new test files and counts.
- [ ] `EDITOR-TODO.md`: a section 16i playtest list. Handoff: mark Pack B built.
- [ ] Review each agent's diff against the spec; run the full suite in the background; report.

# Steal and Bump Feedback (Pack A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task, inline (this repo: no subagents unless the user asks). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The thief and victim of a steal each get a small HUD line, rumble, a camera kick and a sound; a bump that takes nothing says why; a stolen package shows a `×2` stamp.

**Architecture:** A pure rules class (`StealFeedbackRules`) decides the outcome of a bump, which line each player reads and the stamp's text. `OrderHandler.StealFrom` returns that outcome; a static hub (`StealFeedback`) announces it on every machine (offline at once; online it rides the existing `PlayerHit` message). Small runtime-built views listen: `StealCallout` (HUD line, rumble, kick, sound) on each local player, `WorthStamp` on each order, and a label on the scoreboard's flame.

**Tech Stack:** Unity 2022.3, C#, TextMeshPro, Netcode for GameObjects, Cinemachine, NUnit edit-mode tests.

**Spec:** `docs/superpowers/specs/2026-11-13-steal-feedback-design.md`

## Global Constraints

- Read `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md` "How to work in this repo" first. In short:
  - **No commits:** the user commits. No task has a commit step.
  - Never edit a scene, prefab, `.inputactions` or ProjectSettings file. UI is built by code at runtime.
  - `OrderHandler.cs` neighbours `Order.cs`, `BallDriving.cs`, `GameManager.cs` are CRLF: Edit tool only, never `sed -i`. Check others with `git ls-files --eol <file>`.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
  - Tests: `bash tools/run-tests.sh <filter>`; a compile error prints `NO RESULTS`. One test run at a time (check `ListAgents`).
  - Play Mode / network tests: no lambda captures a test local; next free network port is 7819.
- **UI stays small** (the user's rule): the callout is one line, font size 24 in the player HUD's units, never wider than 300 units; long names are cut to 12 characters plus "…".
- **Sounds are never made by Claude:** CC0 files from online sources only, each approved by the user before download, each recorded in `LICENSES.md`.
- Callout life: 1.5 s including 0.15 s fades. Colours: thief `GameCard.GOLD`, victim `ModeIcons.RED`, nothing-taken white.
- No hit-stop: `Time.timeScale` is never touched.
- Rumble goes through `Rumbler` (so `Comfort.Rumble` gates it); the camera kick is off with `Comfort.ReduceMotion`.
- Effects for a player happen only on the machine that drives them (`!RemoteAvatar.IsRemote(handler)`); bots get none.

## Review Focus

1. **A Steam name with rich-text tags or 30+ characters** (`<size=900>x`): the line shows it as plain text, cut to 12 characters. Test in Task 1 (`Short`) and Task 4 (`richText` off).
2. **A host sending an outcome byte this build doesn't know:** nothing shows, nothing throws. Test in Task 1 and Task 3.
3. **Two steals within 1.5 s on one player:** the second line replaces the first; never two lines. Test in Task 4.
4. **Gold Rush rush hour** (a steal counts twice): the line shows what was really paid (`+$14`) and the stamp jumps `×4`. Tests in Task 1 and Task 2.
5. **A sound file missing from Resources:** the steal falls back to the old "whoosh"; the others stay silent; no exception. Test in Task 6.

---

### Task 1: The rules

**Files:**
- Create: `Assets/Scripts/World/StealFeedbackRules.cs` (+ `.meta`)
- Test: `Assets/Tests/Editor/StealFeedbackRulesTests.cs` (+ `.meta`)

**Interfaces — Produces:**

```csharp
public enum BumpOutcome : byte { Stolen, SlotsFull, NothingToSteal, StealsOff, ModeHit }
public enum CalloutTone { None, Thief, Victim, Plain }

public static class StealFeedbackRules
{
    public const int NAME_LENGTH = 12;
    public const int MAX_STAMPED_STEALS = 6;       // ×64, then "×64+"
    public const float CALLOUT_SECONDS = 1.5f;
    public const float CALLOUT_FADE = 0.15f;

    // What a bump did. modeTookIt: ModeReferee.Strike returned true
    public static BumpOutcome Outcome(bool modeTookIt, bool stealsOn, bool victimHolds, bool thiefHasRoom);
    public static bool Known(byte outcome);                      // a value this build defines
    // Loc key and tone for the attacker / the victim; key null = no line
    public static string AttackerKey(BumpOutcome outcome, int paid, out CalloutTone tone);
    public static string VictimKey(BumpOutcome outcome, out CalloutTone tone);
    public static string Short(string name);                     // 12 chars + "…"; null/empty stays ""
    public static string StampText(Constants.OrderValue tier, bool isLode, int steals);  // "" = no stamp
    public static float CalloutAlpha(float age);                 // 0 outside [0, CALLOUT_SECONDS)
}
```

Loc keys: `steal.stole_paid` (args: name, paid), `steal.stole` (name), `steal.robbed` (name), `steal.full`, `steal.nothing`, `steal.off`.

- [ ] **Step 1: Write the failing tests** in `StealFeedbackRulesTests` (namespace and attributes as `StealValueRulesTests`):

```csharp
// Outcome: order of checks is mode, steals off, victim empty, slots full
Assert.AreEqual(BumpOutcome.ModeHit,        StealFeedbackRules.Outcome(true,  true,  true,  true));
Assert.AreEqual(BumpOutcome.StealsOff,      StealFeedbackRules.Outcome(false, false, true,  true));
Assert.AreEqual(BumpOutcome.NothingToSteal, StealFeedbackRules.Outcome(false, true,  false, false));
Assert.AreEqual(BumpOutcome.SlotsFull,      StealFeedbackRules.Outcome(false, true,  true,  false));
Assert.AreEqual(BumpOutcome.Stolen,         StealFeedbackRules.Outcome(false, true,  true,  true));

// Keys and tones
Assert.AreEqual("steal.stole_paid", StealFeedbackRules.AttackerKey(BumpOutcome.Stolen, 14, out tone)); // tone Thief
Assert.AreEqual("steal.stole",      StealFeedbackRules.AttackerKey(BumpOutcome.Stolen, 0, out tone));  // tone Thief
Assert.AreEqual("steal.full",       StealFeedbackRules.AttackerKey(BumpOutcome.SlotsFull, 0, out tone));      // Plain
Assert.AreEqual("steal.nothing",    StealFeedbackRules.AttackerKey(BumpOutcome.NothingToSteal, 0, out tone)); // Plain
Assert.AreEqual("steal.off",        StealFeedbackRules.AttackerKey(BumpOutcome.StealsOff, 0, out tone));      // Plain
Assert.IsNull(StealFeedbackRules.AttackerKey(BumpOutcome.ModeHit, 0, out tone));                              // None
Assert.AreEqual("steal.robbed",     StealFeedbackRules.VictimKey(BumpOutcome.Stolen, out tone));              // Victim
// VictimKey is null (tone None) for every other outcome
// Unknown byte: Known((byte)200) false; AttackerKey((BumpOutcome)200, 7, out tone) null, tone None; VictimKey too

// Short
Assert.AreEqual("Ryan", StealFeedbackRules.Short("Ryan"));
Assert.AreEqual("ABCDEFGHIJKL…", StealFeedbackRules.Short("ABCDEFGHIJKLMNOP"));
Assert.AreEqual("", StealFeedbackRules.Short(null));

// Stamp
Assert.AreEqual("",     StealFeedbackRules.StampText(Constants.OrderValue.Hard, false, 0));
Assert.AreEqual("×2",   StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 1));
Assert.AreEqual("×4",   StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 2));
Assert.AreEqual("×64",  StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 6));
Assert.AreEqual("×64+", StealFeedbackRules.StampText(Constants.OrderValue.Easy, false, 7));
Assert.AreEqual("",     StealFeedbackRules.StampText(Constants.OrderValue.Golden, false, 3));
Assert.AreEqual("",     StealFeedbackRules.StampText(Constants.OrderValue.Hard, true, 3));
Assert.AreEqual("",     StealFeedbackRules.StampText(Constants.OrderValue.Hard, false, -1));

// Alpha: 0 at -0.1 and at 1.5; 1 at 0.75; 0.5 at 0.075 and at 1.425 (±0.01)
```

- [ ] **Step 2:** Run `bash tools/run-tests.sh StealFeedbackRulesTests`. Expected: `NO RESULTS` (does not compile).
- [ ] **Step 3:** Implement `StealFeedbackRules` with a summary comment in the style of `StealValueRules`.
- [ ] **Step 4:** Run the same filter. Expected: all pass.

### Task 2: The steal reports its outcome

**Files:**
- Create: `Assets/Scripts/World/StealFeedback.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`StealOrder`, `StealFrom`, ~lines 396–436)
- Test: `Assets/Tests/Editor/StealFeedbackTests.cs` (+ `.meta`); extend the existing edit-mode test that drives `StealFrom` for steal value (find it with `Grep "StealFrom(" Assets/Tests`)

**Interfaces:**
- Consumes: Task 1.
- Produces:

```csharp
public readonly struct BumpNotice
{
    public readonly int Attacker, Victim;   // seats
    public readonly BumpOutcome Outcome;
    public readonly int Paid;               // what the thief was paid, 0 if nothing
    public BumpNotice(int attacker, int victim, BumpOutcome outcome, int paid);
}

public static class StealFeedback
{
    public static event System.Action<BumpNotice> Happened;
    public static void Announce(BumpNotice notice);   // raises Happened; an unknown outcome is dropped
    // cleared on SubsystemRegistration, like TextSize.ResetForPlay
}

// OrderHandler
public BumpNotice StealFrom(OrderHandler victim);   // was void; not authority: returns outcome ModeHit, paid 0 (says nothing)
```

- `StealFrom` computes the outcome with `StealFeedbackRules.Outcome` from: `ModeReferee.Strike`'s result, the existing Steals-rule check (line 418, tutorial exception kept), `victim.GetBestOrder() != null`, and `order1 == null || order2 == null`. `Paid` is `PayForSteal`'s return when `StealsCount`, else 0. Behaviour is otherwise unchanged (the victim still drops everything in the same cases as today).
- `StealOrder` (offline) calls `StealFeedback.Announce` with the notice. Online announcing is Task 3.

- [ ] **Step 1: Write the failing tests.** In `StealFeedbackTests`: a recorder class subscribed as a method group collects notices; `Announce` of each known outcome arrives once with the same fields; `Announce(new BumpNotice(0, 1, (BumpOutcome)200, 0))` arrives never. In the existing `StealFrom` test fixture add: first steal of an order returns `Outcome == Stolen, Paid == 7`; the same thief stealing it again returns `Paid == 0`; thief with two orders returns `SlotsFull`; victim with none returns `NothingToSteal`; Steals rule off returns `StealsOff`; with `GoldRushRules.StealCounts()` at 2 (use the fixture's existing way of setting rush hour) `Paid == 14` and `order.Steals == 2`.
- [ ] **Step 2:** Run `bash tools/run-tests.sh StealFeedback` then the existing fixture's filter. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement `BumpNotice`, `StealFeedback`, and the `OrderHandler` changes.
- [ ] **Step 4:** Run both filters. Expected: pass, and the existing steal-value tests still pass.

### Task 3: Online: the hit carries the outcome

**Files:**
- Modify: `Assets/Scripts/Online/PlayerHit.cs`, `Assets/Scripts/Online/OnlineSteals.cs` (`Judge`, `Show`)
- Modify tests: `Assets/Tests/Editor/PlayerHitTests.cs`, `OnlineStealsNetworkTests.cs` (lines ~389, ~434 compare whole hits), `StealMessagesNetworkTests.cs`
- Docs: `docs/online.md` (the steals section: the hit's two new fields)

**Interfaces:**
- Consumes: `BumpNotice`, `StealFeedback.Announce`, `StealFeedbackRules.Known`.
- Produces:

```csharp
// PlayerHit: two new serialized fields, after Victim
public byte Outcome;   // BumpOutcome
public byte Paid;      // 0..255, clamped when built
public static PlayerHit Steal(int thief, int victim, BumpOutcome outcome = BumpOutcome.Stolen, int paid = 0);
```

- `Judge`: `BumpNotice notice = thief.StealFrom(target); hit = PlayerHit.Steal(attacker, victim, notice.Outcome, notice.Paid);`
- `Show`, for `HitKind.Steal`: after the existing `TooSoon` guard, `StealFeedback.Announce(new BumpNotice(hit.Attacker, hit.Victim, (BumpOutcome)hit.Outcome, hit.Paid))` on every machine (host included, since `Judge` calls `Show`). The victim's bounce stays as it is. The client-side `TooSoon` guard already limits a hostile host to one callout per pair per second.

- [ ] **Step 1: Write the failing tests.** `PlayerHitTests.EveryKind` adds `PlayerHit.Steal(2, 3, BumpOutcome.SlotsFull, 0)` and `PlayerHit.Steal(1, 0, BumpOutcome.Stolen, 14)` (round trip keeps both fields); `PlayerHit.Steal(0, 1, BumpOutcome.Stolen, 999).Paid == 255`. In `OnlineStealsNetworkTests`: a recorder on `StealFeedback.Happened` (method group) on the steal test at ~389 hears exactly one notice with the steal's seats, `Stolen`, `Paid == 7`; update the whole-hit comparisons at ~389 and ~434 to the new fields. New test (port 7819): `Show(new PlayerHit { Kind = HitKind.Steal, Attacker = 0, Victim = 1, Outcome = 200 })` announces nothing and doesn't throw.
- [ ] **Step 2:** Run `bash tools/run-tests.sh PlayerHitTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement. Update `docs/online.md`.
- [ ] **Step 4:** Run `PlayerHitTests`, then `OnlineStealsNetworkTests`, then `StealMessagesNetworkTests` (one at a time, in the background). Expected: pass.

### Task 4: The callout line, rumble and kick

**Files:**
- Create: `Assets/Scripts/UI/StealCallout.cs` (+ `.meta`)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`Start`), `Assets/Scripts/UI/NumberHandler.cs`, `Assets/Scripts/Player/OrbitalCamera.cs`, `Assets/Scripts/Player/Comfort.cs`, `Assets/Scripts/Player/BallDriving.cs`
- Modify: `Assets/Resources/Localization/strings.csv`
- Test: `Assets/Tests/Editor/StealCalloutTests.cs` (+ `.meta`); extend the `Comfort` and strings tests that exist (`Grep "Comfort\." Assets/Tests`, `Grep "StringsCsv" Assets/Tests`)

**Interfaces:**
- Consumes: `StealFeedback.Happened`, `StealFeedbackRules`, `Scoreboard.ShownName(int)`, `OrderSync.SeatOf`, `RemoteAvatar.IsRemote`, `Loc.Get`.
- Produces:

```csharp
// NumberHandler
internal RectTransform ScoreBlock { get; }   // tipHolder's RectTransform's parent: where the callout hangs

// Comfort
public static float Kick(float wanted);      // 0 with ReduceMotion

// OrbitalCamera
public void Kick(float degrees);             // widens the view by degrees now, easing back over KICK_SECONDS = 0.25f; added on top of fovValue where the lens FOV is set (lines 70–71)

// BallDriving
public void StealJolt(bool robbed);          // rumble through its own rumble/pad (as line 663) + its camera's Kick
//   thief:  RumblePulse(pad, 0.2f, 0.4f, 0.15f), Kick(Comfort.Kick(3f))
//   robbed: RumblePulse(pad, 0.6f, 1.0f, 0.35f), Kick(Comfort.Kick(6f))

// StealCallout : MonoBehaviour
internal static StealCallout AddTo(OrderHandler player, NumberHandler hud);  // null hud (bot, remote avatar) returns null
internal void Show(string words, CalloutTone tone);
internal string ShownWords { get; }          // "" when nothing is up
internal const float SIZE = 24f, WIDTH = 300f, BELOW_SCORE = 70f;
```

- `StealCallout` builds one `TextMeshProUGUI` with `GameCard.Words` under `hud.ScoreBlock`: `richText = false`, no wrapping, overflow ellipsis, right-aligned to the score's right edge, a black outline for contrast, `BELOW_SCORE` under the score block. It subscribes to `StealFeedback.Happened` in `OnEnable`, unsubscribes in `OnDisable`.
- On a notice where this player is the attacker: `AttackerKey` → `Loc.Get(key, Short(ShownName(victim)), paid)`; where it is the victim: `VictimKey` → `Loc.Get(key, Short(ShownName(attacker)))`. A null key shows nothing. Then `ball.StealJolt(robbed)` for `Stolen` only, and the sound (Task 6 hooks in here).
- Alpha each frame from `StealFeedbackRules.CalloutAlpha(Time.unscaledTime - shownAt)`; hidden while `GameState.Paused`, and cleared when `Scoreboard.EndsMatch(state)`.
- `OrderHandler.Start` calls `StealCallout.AddTo(this, numberHandler)` when `!RemoteAvatar.IsRemote(this)`.
- **strings.csv:** add the six keys of Task 1 after the `rules.*` rows, with a description column and every language column the header has. English: `STOLE from {0} +${1}` / `STOLE from {0}` / `{0} took your order` / `Slots full` / `Nothing to steal` / `Steals are off`. Translate the rest in the register of the neighbouring rows (short, upper-case where they are).

- [ ] **Step 1: Write the failing tests.** `StealCalloutTests` (edit mode, objects built in code): after `Show("A", CalloutTone.Thief)` then `Show("B", CalloutTone.Victim)`, `ShownWords == "B"` and the HUD holds exactly one callout text; its colour is `ModeIcons.RED`; its `richText` is false; `AddTo(player, null)` returns null. `Comfort.Kick(6f)` is 6 with `ReduceMotion` false and 0 with it true (restore the flag in teardown). Strings: each of the six keys resolves in every language `StringsCsv` knows, and `Loc.Get("steal.stole_paid", "Ryan", 7)` in English is `STOLE from Ryan +$7`.
- [ ] **Step 2:** Run `bash tools/run-tests.sh StealCalloutTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `StealCalloutTests`, the Comfort filter and the strings/localization filter. Expected: pass.

### Task 5: The worth stamp

**Files:**
- Create: `Assets/Scripts/World/WorthStamp.cs` (+ `.meta`)
- Modify: `Assets/Scripts/World/Order.cs` (`ApplyStealLook`, and where `isLode` changes), `Assets/Scripts/UI/Scoreboard.cs` (`Row`: constructor loop ~465–486, `ShowPackage` ~585)
- Test: `Assets/Tests/Editor/WorthStampTests.cs` (+ `.meta`); extend `ScoreboardTests` if one builds a `Row`

**Interfaces:**
- Consumes: `StealFeedbackRules.StampText`, `Order.Value`, `Order.IsLode`, `Order.Steals`.
- Produces:

```csharp
// WorthStamp : MonoBehaviour, one per Order, made the first time it has text to show
internal static void Refresh(Order order, Transform mesh);   // Order.ApplyStealLook calls it, before its golden early-return
internal string Text { get; }
internal const float HEIGHT_OVER_MESH = 1.6f, WORLD_SIZE = 6f;
```

- A world-space `TextMeshPro` (Bangers, gold with a black outline) parented to the order (not the swelling mesh), `HEIGHT_OVER_MESH` above it. It turns to face each camera as that camera renders: check which pipeline the project uses (`GraphicsSettings.currentRenderPipeline`) and hook `RenderPipelineManager.beginCameraRendering` or `Camera.onPreCull` accordingly; unhook in `OnDisable`. Disabled (not destroyed) when the text is "".
- Scoreboard: each package slot gets a small `Label` (size 16, bold, white on the flame, bottom-right of the package icon) showing `StampText` for the order in that slot; hidden when "". It sits inside the row, so the board's size doesn't change.

- [ ] **Step 1: Write the failing tests.** `WorthStampTests`: an `Order` built in code at tier Easy with a mesh object; `CountSteal()` once → its stamp's `Text == "×2"` and the stamp is active; twice → `"×4"`; `ResetSteals()` → stamp inactive; a Golden order after `CountSteal()` has no active stamp.
- [ ] **Step 2:** Run `bash tools/run-tests.sh WorthStampTests`. Expected: `NO RESULTS`.
- [ ] **Step 3:** Implement.
- [ ] **Step 4:** Run `WorthStampTests`, then `Scoreboard`. Expected: pass.

### Task 6: Sounds

**Files:**
- Create: `Assets/Scripts/Player/FeedbackSounds.cs` (+ `.meta`); `Assets/Resources/Audio/Feedback/steal.*`, `robbed.*`, `thud.*` (+ `.meta` each)
- Modify: `Assets/Scripts/Player/SoundPool.cs` (`PlayOrderTheft`, new `PlayFeedback`), `Assets/Scripts/UI/StealCallout.cs`, `LICENSES.md`
- Test: `Assets/Tests/Editor/FeedbackSoundsTests.cs` (+ `.meta`)

**Interfaces:**
- Produces:

```csharp
public static class FeedbackSounds
{
    public const string STEAL = "steal", ROBBED = "robbed", THUD = "thud";
    public const float VOLUME = 0.8f;
    public static AudioClip Clip(string name);   // Resources.Load("Audio/Feedback/" + name), cached; null if missing
}

// SoundPool
public void PlayFeedback(string name);   // a pooled one-shot on the SFX group at VOLUME; a missing clip plays nothing
// PlayOrderTheft: plays FeedbackSounds.STEAL when its clip exists (local and at-distance paths), else "whoosh" as now
```

- `StealCallout`: the victim's machine plays `ROBBED`; the attacker's plays `THUD` for `SlotsFull`, `NothingToSteal` and `StealsOff`. The thief's steal sound already plays from `TakeOrderFrom`.

- [ ] **Step 1: Source the files (needs the user).** Search CC0 sources (Kenney audio packs, OpenGameArt filtered to CC0, Freesound filtered to CC0) for: a short snatch/swipe (steal), a short low "loss" sting (robbed), a soft dull thud. Give the user, for each: the page URL, the file name and size, and the licence line as the page states it. Download only what the user approves, each into its own new folder under the scratchpad, then copy the three clips in. Freesound needs a sign-in: those the user downloads themselves.
- [ ] **Step 2:** Add each file to `LICENSES.md` (Audio): path, source URL, author, "CC0", date fetched. Mark the old whoosh as no longer played once `steal` exists.
- [ ] **Step 3: Write the failing tests.** `FeedbackSoundsTests`: `Clip("no-such-sound")` is null and doesn't throw; `Clip(STEAL)`, `Clip(ROBBED)`, `Clip(THUD)` are not null, each under 2 s long.
- [ ] **Step 4:** Run `bash tools/run-tests.sh FeedbackSoundsTests`. Expected: `NO RESULTS`.
- [ ] **Step 5:** Implement.
- [ ] **Step 6:** Run `FeedbackSoundsTests`, then `StealCalloutTests`. Expected: pass.

### Task 7: Documents and the playtest note

**Files:**
- Modify: `docs/design/DoA GDD.docx`, `docs/design/DoA TDD.docx` (docx skill; keep styles; Revision History line and the cover's minor version bumped in each)
- Modify: `EDITOR-TODO.md`, `docs/superpowers/specs/2026-11-13-player-feedback-handoff.md`

- [ ] **Step 1: GDD.** Where steals and the HUD are described: the six callout lines and who sees them, rumble and the camera kick (off with Reduce motion / Rumble), the `×2` stamp on the package and scoreboard (`×64+` cap, never on golden or the Mother Lode), the three sounds. Check every value against the code, not this plan.
- [ ] **Step 2: TDD.** `StealFeedbackRules`, `StealFeedback`/`BumpNotice`, `StealCallout`, `WorthStamp`, `FeedbackSounds`; `PlayerHit`'s `Outcome` and `Paid`; the new test fixtures.
- [ ] **Step 3: EDITOR-TODO.md.** A playtest section, short bullets: 4 players (bots fine), split screen and online; each of the six lines readable in a quarter-screen view and clear of the score, compass and scoreboard; the stamp readable over a carried package; kick off with Reduce motion; rumble off with Rumble off; all three sounds heard, at the SFX volume.
- [ ] **Step 4: Handoff.** Mark Pack A built in the packs table, and correct "no steal sound found".
- [ ] **Step 5:** Run the full suite in the background (`bash tools/run-tests.sh`, about 40 minutes). Expected: no new failures. Then self-review the diff against the spec.

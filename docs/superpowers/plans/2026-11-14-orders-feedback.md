# Pack D: Orders and Where to Go — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A carrier is warned of a rival who could steal, beacons show an order's worth, and each drop-off's compass mark shows its package and price.

**Architecture:** As Packs A to C: a pure static rules class with edit-mode tests, and small views built in code at runtime. Every machine works the warning out from what it already has (positions, `DriveFlags` boost, held orders); no new network messages.

**Tech Stack:** Unity 2022.3, C#, TextMeshPro, NUnit edit-mode tests.

**Spec:** `docs/superpowers/specs/2026-11-14-orders-feedback-design.md`

## Global Constraints

- Never commit, push or merge: the user commits. Work in place on `steam-phase1a`.
- Never edit scenes, prefabs, `.inputactions` or ProjectSettings. UI is built in code.
- Every new file under `Assets/` needs a `.meta`: `bash tools/newmeta.sh <path>`.
- `Order.cs`, `OrderHandler.cs`, `NumberHandler.cs`, `Comfort.cs`, `OrbitalCamera.cs` are CRLF: change them with the Edit tool only.
- Tests: `bash tools/run-tests.sh "<filter>"`, one run at a time. Edit-mode only: `Awake`, `OnEnable`, `Update` don't run, so views expose internal methods the tests call. No lambdas that capture test locals.
- TMP `outlineWidth` on world-space text only when `Application.isPlaying`.
- The match plays in `GameState.Begin`, not `MainLoop`.
- Added UI stays small. No new sounds. No new strings (prices are numbers).
- A player holds at most two orders (`order1`, `order2`; `HeldCount`).
- TDD: write each test, watch it fail, then write the code.

## Review Focus

- A rival exactly on the carrier (distance 0) or a zero camera vector: no exception, no mark.
- A remote avatar (another machine's scooter) gets no `RivalWarning`, but counts as a rival for local players.
- The golden order and the Mother Lode: golden shows no price; the lode shows `$250`.
- An order stolen while on the ground or dropped again: its beacon price follows `Order.Worth`.
- Split screen: each view's price label sits on that view's own icon (`beconRotationObjects[i]`) and layer.

---

### Task 1: The rules (`RivalWarningRules`, `OrderFeedbackRules`)

**Files:**
- Create: `Assets/Scripts/World/RivalWarningRules.cs`, `Assets/Scripts/World/OrderFeedbackRules.cs`
- Test: `Assets/Tests/Editor/RivalWarningRulesTests.cs`, `Assets/Tests/Editor/OrderFeedbackRulesTests.cs`

**Interfaces — Produces:**

```csharp
public enum ThreatLevel : byte { None, Near, Charging }
public enum ThreatSide : byte { None, Left, Right, Behind }

public readonly struct Threat   // the one rival a carrier is warned about
{
    public readonly ThreatLevel Level; public readonly ThreatSide Side; public readonly float Distance;
    public Threat(ThreatLevel level, ThreatSide side, float distance);
    public static readonly Threat Nothing;   // None, None, float.MaxValue
}

public static class RivalWarningRules
{
    public const float NEAR_METRES = 30f;
    public const float FRONT_DEGREES = 50f;    // within this of the camera's forward: on screen, no mark
    public const float BEHIND_DEGREES = 130f;  // beyond this: behind
    public const float PULSE_SECONDS = 0.5f;
    public const float NEAR_ALPHA = 0.45f;

    // Whether a rival can be a threat at all
    public static bool Counts(bool playing, bool stealsOn, bool carrierHolds, bool rivalHasRoom, bool eitherRespawning);
    public static ThreatLevel Level(float distance, bool rivalBoosting, bool closing);   // > NEAR_METRES: None; boosting && closing: Charging; else Near
    public static ThreatSide Side(Vector3 toRival, Vector3 cameraForward);               // flat (y ignored); zero vectors: None
    public static Threat Worse(Threat a, Threat b);                                      // higher level, then nearer; Side None never beats a side
    public static float Alpha(ThreatLevel level, float age, bool reduceMotion);          // None 0; Near NEAR_ALPHA; Charging pulses NEAR_ALPHA..1 over PULSE_SECONDS, or 1 with reduceMotion
}

public static class OrderFeedbackRules
{
    // "" for the golden order (not the lode); otherwise "$" + worth
    public static string PriceText(Constants.OrderValue tier, bool isLode, int worth);
}
```

- [ ] **Step 1:** Write `RivalWarningRulesTests`: `Counts` false when any one condition fails, true when all hold; `Level` at 29 m near, 31 m none, boosting+closing charging, boosting but not closing near; `Side` for a rival dead ahead (None), 49° (None), 90° right (Right), 90° left (Left), 131° and 180° (Behind), zero `toRival` (None), a rival straight above (None); `Worse` picks charging over a nearer near, nearer of two equal levels, and anything with a side over `Nothing`; `Alpha` values at None, Near, Charging at age 0 and `PULSE_SECONDS / 2`, and 1 with reduce motion.
- [ ] **Step 2:** Write `OrderFeedbackRulesTests`: Easy 55 → `$55`; Medium stolen once 130 → `$130`; Golden, not lode → `""`; lode 250 → `$250`.
- [ ] **Step 3:** Run `bash tools/run-tests.sh "RivalWarningRulesTests|OrderFeedbackRulesTests"`. Expected: compile failure (types missing).
- [ ] **Step 4:** Write both classes with metas. Run again. Expected: all pass.

### Task 2: The edge mark (`RivalWarning`)

**Files:**
- Create: `Assets/Scripts/UI/RivalWarning.cs`
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`Start`, beside `StealCallout.AddTo`), `Assets/Scripts/Player/BallDriving.cs` (beside `StealJolt`)
- Test: `Assets/Tests/Editor/RivalWarningTests.cs` (model the setup on `StealCalloutTests.NewPlayer`)

**Interfaces — Consumes:** Task 1's rules. **Produces:**

```csharp
public class RivalWarning : MonoBehaviour
{
    internal const float SIZE = 40f;
    internal static readonly Vector2 LEFT = new Vector2(-900f, 0f), RIGHT = new Vector2(900f, 0f), BEHIND = new Vector2(0f, -340f); // from the HUD's middle, 1920 x 1080
    internal System.Func<float> Clock;                       // as StealCallout.Clock
    internal static RivalWarning AddTo(OrderHandler player, NumberHandler hud);  // null without a HUD canvas
    internal void See(Threat threat);                        // shows, moves and turns the mark; rumbles once on entering Charging
    internal ThreatLevel ShownLevel { get; }
    internal Vector2 ShownAt { get; }                        // the mark's anchored position
    internal float ShownAlpha { get; }
    internal int Rumbles { get; }                            // how many times it has asked for the rumble (tests)
    internal static Threat Look(OrderHandler carrier, Transform camera, System.Collections.Generic.IEnumerable<OrderHandler> everyone, System.Collections.Generic.Dictionary<OrderHandler, float> lastDistances); // the worst threat now
}
// BallDriving
public void WarnJolt();   // rumble.RumblePulse(pad, 0.3f, 0.5f, 0.15f); nothing without a rumbler
```

- The mark is one `Image` on the HUD's root canvas with a chevron sprite drawn in code (a texture filled from a distance function, as `Colourblind.Draw` does), red `new Color(1f, 0.25f, 0.2f)`, `raycastTarget = false`. It points outward: left edge points left, right edge right, behind points down.
- `Update` (play only): `See(Look(...))` over `PlayerInstantiate.Instance.Roster.Players`, with `playing` = `GameManager.Instance.MainState` is `Begin` or `Tutorial` and `stealsOn` as `OrderHandler.JudgeBump` reads it. `closing` = this frame's distance is less than the last one stored for that rival. `rivalHasRoom` = `HeldCount < 2`. For respawning, use what `OnlineSteals.Respawning` uses.
- `OrderHandler.Start`: `RivalWarning.AddTo(this, numberHandler)` inside the same `if (!RemoteAvatar.IsRemote(this))` block.

- [ ] **Step 1:** Write `RivalWarningTests`: `AddTo` returns null without a canvas; `See(Nothing)` shows nothing (alpha 0, level None); `See` Near/Left puts the mark at `LEFT` at `NEAR_ALPHA`; Right and Behind positions; entering Charging rumbles once and staying Charging doesn't again; back to Near then Charging rumbles again; `Look` with a carrier holding nothing returns `Nothing`; `Look` ignores the carrier itself and a rival with no room (give the rival two orders, or whatever `HeldCount` needs in a test: if a held order can't be made in edit mode, test `Counts` wiring through a small internal static `Threat Judge(...)` taking plain values instead, and say so in the report).
- [ ] **Step 2:** Run `bash tools/run-tests.sh "RivalWarningTests"`. Expected: compile failure.
- [ ] **Step 3:** Write `RivalWarning`, `WarnJolt`, the `Start` line. Run `bash tools/run-tests.sh "RivalWarning|StealCalloutTests|GoalTagTests"`. Expected: all pass.

### Task 3: Prices (`PriceTag`, compass price)

**Files:**
- Create: `Assets/Scripts/World/PriceTag.cs`
- Modify: `Assets/Scripts/UI/BeconIndicator.cs`, `Assets/Scripts/World/Order.cs` (CRLF), `Assets/Scripts/World/OrderBeacon.cs` (`SetDropoff`), `Assets/Scripts/UI/CompassMarker.cs`, `Assets/Scripts/UI/CompassIconUI.cs`, `Assets/Scripts/UI/Compass.cs` (`AddCompassMarker`)
- Test: `Assets/Tests/Editor/PriceTagTests.cs`

**Interfaces — Consumes:** `OrderFeedbackRules.PriceText`. **Produces:**

```csharp
// BeconIndicator
internal GameObject[] Icons { get; }            // beconRotationObjects
internal SpriteRenderer[] IconSprites { get; }  // rotationSprites

public class PriceTag : MonoBehaviour   // one per order, on the BeconIndicator's object
{
    internal const float WORLD_SIZE = 4f;
    internal const float BELOW_ICON = -0.9f;      // local y under each view's icon
    internal static void Refresh(Order order, BeconIndicator indicator);  // makes it the first time; sets every label to PriceText
    internal string Text { get; }                 // "" when hidden
    internal int Labels { get; }                  // one per icon
}

// CompassMarker
public string price = "";
// CompassIconUI
internal const float PRICE_SIZE = 18f;
public void SetPrice(string price);   // "" hides it; otherwise a small gold line above the main icon and both child icons, made the first time
internal string ShownPrice { get; }
```

- `PriceTag`: one `TextMeshPro` child under each of `indicator.Icons[i]` (so it turns, scales and is hidden with that view's icon), on that icon's layer, Bangers font and gold as `WorthStamp`, outline only in play. In `LateUpdate` each label's alpha follows `IconSprites[i].color.a`.
- `Order`: call `PriceTag.Refresh(this, beconIndicator)` after both `beconIndicator.InitalizeBeconIndicator(Look)` calls and at the end of `ApplyStealLook` (guard a null `beconIndicator`).
- `OrderBeacon.SetDropoff`, before `compass.AddCompassMarker(compassMarker)`: `compassMarker.icon = order.Icon ?? compassMarker.icon; compassMarker.price = OrderFeedbackRules.PriceText(order.Value, order.IsLode, order.Worth);`
- `Compass.AddCompassMarker`: after `SetCompassIconSprite`, `compassIconUI.SetPrice(marker.price)`.

- [ ] **Step 1:** Write `PriceTagTests`: an indicator with two icons gets two labels reading the price; a second `Refresh` with a new worth changes the text without adding labels; a golden order's labels read `""`; `CompassIconUI.SetPrice("$80")` then `ShownPrice` is `$80`, `SetPrice("")` hides it. If an `Order` can't be built in edit mode, give `Refresh` an internal overload `Refresh(BeconIndicator indicator, string price)` that the `Order` one calls, and test that.
- [ ] **Step 2:** Run `bash tools/run-tests.sh "PriceTagTests"`. Expected: compile failure.
- [ ] **Step 3:** Write the code. Run `bash tools/run-tests.sh "PriceTag|WorthStamp|Compass"`. Expected: all pass.

### Task 4: Documents and the full suite (done by the lead, not an agent)

- [ ] `EDITOR-TODO.md` section 16k (playtest), handoff doc marks Pack D built, spec status.
- [ ] GDD: the warning, beacon prices, drop-off marks; TDD: new classes and tests; versions bumped with revision rows.
- [ ] Full suite in the background: `bash tools/run-tests.sh`. Expected: 0 failed.

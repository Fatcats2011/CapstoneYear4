# Pack F: Driving and Respawn — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Crossing a boost pad gives a short burst of speed lines, a rumble and a rising sound; a player in the water reads how long until they drive again.

**Architecture:** As Packs A to G: one pure rules class with edit-mode tests, a small static hub, and the existing views. Each machine does this for the players it drives; nothing is sent.

**Tech Stack:** Unity 2022.3, C#, TextMeshPro, NUnit edit-mode tests.

**Spec:** `docs/superpowers/specs/2026-11-16-driving-feedback-design.md`

## Global Constraints

- Never commit, push or merge: the user commits. Work in place on `steam-phase1a`.
- Never edit scenes, prefabs, `.inputactions` or ProjectSettings.
- Every new file under `Assets/` needs a `.meta`: `bash tools/newmeta.sh <path>`.
- Check a file's line endings (`file <path>`) before editing; CRLF files are changed with the Edit tool only.
- `Assets/Resources/Localization/strings.csv` is LF, UTF-8: `key,note,en,fr,de,it,nl,es,es-419,pt,pt-BR,pl,tr,cs,ro,hu,sv,da,reviewed` (last cell empty). Every new key gets all 16 languages.
- Tests: `bash tools/run-tests.sh "<filter>"`, one run at a time. Edit-mode only: `Awake`, `OnEnable`, `Update`, `FixedUpdate` and coroutines don't run. No lambdas that capture test locals.
- The sound file is already in: `Assets/Resources/Audio/Feedback/pad.ogg`. No other new sounds. Added UI stays small.
- TDD: write each test, watch it fail, then write the code.

## Review Focus

- A scooter resting on a pad, or crossing pads in a row: one moment, not one per physics step.
- A bot or another machine's scooter on a pad: no sound, no rumble, no error (no camera, no pad, maybe no sound pool).
- Reduce motion on: no speed-line burst. Rumble off: no shake (the rumbler handles it).
- The countdown never shows 0 or a negative number, holds while the game is paused, and ends when the respawn is stopped early (a cutscene, the results).
- An event line during a respawn is not cut short; the count resumes at the right number.

---

### Task 1: The rules and the boost pad moment

**Files:**
- Create: `Assets/Scripts/World/DrivingFeedbackRules.cs`
- Modify: `Assets/Scripts/Player/BallDriving.cs` (the `case "Speed":` ground check, `ControlSpeedLines`, beside `StealJolt`), `Assets/Scripts/Player/SoundPool.cs` (`PlayFeedback`, `PlayClip`), `Assets/Scripts/Player/FeedbackSounds.cs`
- Test: `Assets/Tests/Editor/DrivingFeedbackRulesTests.cs`; extend `FeedbackSoundsTests.cs`

**Interfaces — Produces:**

```csharp
public static class DrivingFeedbackRules
{
    public const float PAD_GAP = 0.75f;        // seconds off every pad before a touch is a new crossing
    public const float BURST_SECONDS = 0.5f;   // how long the speed lines stay at boost strength
    public const float MIN_PITCH = 1f, MAX_PITCH = 1.25f;
    public const float SLOW = 15f, FAST = 40f; // metres a second: the speeds the pitch runs between

    // Whether a touch at `now` starts a new crossing, given the last touch (negative: never touched)
    public static bool NewCrossing(float lastTouch, float now);
    public static bool Bursting(float crossedAt, float now);       // crossedAt negative: never
    public static float Pitch(float speed);                        // clamped MIN_PITCH..MAX_PITCH

    // The number a respawn countdown shows with this long left: the whole seconds, rounded up, at least 1; 0 when none is left
    public static int Countdown(float secondsLeft);
}
// FeedbackSounds
public const string PAD = "pad";      // Volume(PAD) = 0.35f: pads come often, so it sits under the other feedback sounds
// SoundPool
public void PlayFeedback(string name, float pitch = 1f);
// BallDriving
internal bool TouchPad(float now);    // records the touch; true when it began a new crossing (then the burst, rumble and sound happen)
internal bool PadBursting(float now);
public void PadJolt();                // rumble.RumblePulse(pad, 0.2f, 0.4f, 0.15f); nothing without a rumbler
```

- `BallDriving`, in `case "Speed":` call `TouchPad(Time.time)`. On a new crossing: `PadJolt()`, and `soundPool.PlayFeedback(FeedbackSounds.PAD, DrivingFeedbackRules.Pitch(currentVelocity))` — only for a scooter this machine's player drives (not a bot, not another machine's: use the checks `StealJolt` / the camera kick / `SwapRendering` rely on, e.g. a null `orbitalCamera` or `RemoteAvatar.IsRemote`; null-safe sound pool).
- `ControlSpeedLines`: the boosting branch's speed-line value also applies while `PadBursting(Time.time)` (lines only: leave the FOV to the speed, as today).
- `SoundPool.PlayClip` takes the pitch and sets `source.pitch` (`ResetSource` already puts it back to 1).

- [ ] **Step 1:** `DrivingFeedbackRulesTests`: `NewCrossing` true for a first touch (last -1), false 0.1 s after the last, false at just under `PAD_GAP`, true at `PAD_GAP`; `Bursting` false when never crossed, true just after, false at `BURST_SECONDS`; `Pitch` at 0, `SLOW`, midway, `FAST`, 100; `Countdown` for 7 → 7, 6.01 → 7, 6 → 6, 0.2 → 1, 0 → 0, -1 → 0.
- [ ] **Step 2:** `FeedbackSoundsTests`: `PAD` is "pad", its volume 0.35, and its clip is in the game and under 2 s (add it to the existing clip TestCases). A `BallDriving` test (in `DrivingFeedbackRulesTests` or a new small class, built as `StealCalloutTests.NewPlayer` builds one): `TouchPad(0)` true, `TouchPad(0.02)` false, touches every 0.02 s for a second all false, then after a `PAD_GAP` gap true again; `PadBursting` true just after a crossing and false after `BURST_SECONDS`.
- [ ] **Step 3:** Run `bash tools/run-tests.sh "DrivingFeedbackRules|FeedbackSounds"`. Expected: compile failure.
- [ ] **Step 4:** Write the code. Run `bash tools/run-tests.sh "DrivingFeedback|FeedbackSounds|StealCallout|BallDriving|Comfort"`. Expected: all pass.

### Task 2: The respawn countdown

**Files:**
- Create: `Assets/Scripts/World/RespawnFeedback.cs`
- Modify: `Assets/Scripts/Player/Respawn.cs`, `Assets/Scripts/UI/StealCallout.cs`, `Assets/Resources/Localization/strings.csv`
- Test: `Assets/Tests/Editor/RespawnCalloutTests.cs`

**Interfaces — Consumes:** `DrivingFeedbackRules.Countdown`. **Produces:**

```csharp
public static class RespawnFeedback   // as ScoreFeedback: the game says, the callouts show
{
    public static event System.Action<int> Fell;                 // seat: went in the water
    public static event System.Action<int, float> Rising;        // seat, seconds until they drive again
    public static event System.Action<int> Back;                 // seat: driving again, or the respawn was stopped
    public static void AnnounceFell(int seat);
    public static void AnnounceRising(int seat, float secondsLeft);
    public static void AnnounceBack(int seat);
    // reset on SubsystemRegistration
}
// StealCallout
internal void HearFell(int seat);                  // own seat: shows "respawn.fell"
internal void HearRising(int seat, float secondsLeft);
internal void HearBack(int seat);                  // own seat: the countdown ends; a countdown line up is cleared
internal void TickRespawn(float deltaTime);        // Update calls it with Time.deltaTime (the game's clock: a pause holds the count)
```

- `Respawn`: `AnnounceFell` in `StartRespawnCoroutine` when a new respawn begins (where `respawnCoroutine` is created); `AnnounceRising(seat, Duration)` in `RespawnPlayer` just before the wisp starts to rise (after the respawn point is known); `AnnounceBack` in `StopRespawnCoroutine` when a respawn was running. Seat: `OrderSync.SeatOf(this)` as the file already uses. Only this machine's own players run this script (`ShowRemote` covers the others), so no extra check.
- `StealCallout`: while a count is running, `TickRespawn` lowers the time left and, when no line is fresh (`LineIsFresh`) and no lead line or hint is waiting, shows `Loc.Get("respawn.back", n)` each time `Countdown(left)` gives a new number (plain tone). It never shows 0; when the time runs out the count just ends. Any event line shows as usual; the count resumes after it with the current number. `HearFell` shows its line at once (it may replace a line: the player just fell).
- Strings (16 languages, upper case, at most 24 characters with a one-digit number): `respawn.fell` "FELL IN THE RIVER", `respawn.back` "BACK IN {0}". Look at how other rows with `{0}` are written.

- [ ] **Step 1:** `RespawnCalloutTests` (setup as `StealCalloutTests`): another seat's fall shows nothing; `HearFell` shows the fell line; after `HearRising(seat, 7)` and the fell line's hold, `TickRespawn` shows `BACK IN 7`, then with a second gone `BACK IN 6`; the same number is not shown twice (count the line's changes or check it isn't restarted); an event line heard mid-count is not replaced while fresh, then the count resumes with the right number; with 0.2 s left it shows 1, then nothing after it runs out; `HearBack` ends the count and no further number shows; `TickRespawn(0)` (paused) changes nothing; both keys in all 16 languages.
- [ ] **Step 2:** Run `bash tools/run-tests.sh "RespawnCallout"`. Expected: compile failure.
- [ ] **Step 3:** Write the code and strings. Run `bash tools/run-tests.sh "RespawnCallout|StealCallout|GainCallout|ModeCallout|HintCallout|GoalTag|Respawn|Localization|Loc"`. Expected: all pass.

### Task 3: Documents and the full suite (done by the lead)

- [ ] `EDITOR-TODO.md` 16m, handoff doc, spec status, GDD and TDD with revision rows.
- [ ] Full suite in the background. Expected: 0 failed.

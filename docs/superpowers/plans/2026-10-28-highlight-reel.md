# Highlight Reel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** at the end of the main round, play a 10–20 s full-screen "hidden-camera" reel of the round's best moments, weighted towards player interaction, filmed by invisible cameras the director places per clip. It's the same reel on every machine online, and it can be skipped by everyone holding A.

**Architecture:**
- **Recording:** every machine records each seat's pose and drive flags into a buffer.
- **Choosing:** the rules machine collects scored moments from game events. It turns them into clips (pure `HighlightRules`), then places shots per clip (pure `ShotDirector`, with an injected line-of-sight check), giving a `ReelList`.
- **Playing:** each machine plays that list against its own recording, using stand-in copies of the scooter models, a full-screen reel camera and a code-built style-C overlay.
- **Online:** the list and a server-time start go out through a `HighlightSync` seam and an `OnlineHighlights` relay, like the awards.

**Tech Stack:** Unity 2022.3.62f3, C#, URP, Netcode for GameObjects 1.15.1, TextMesh Pro, NUnit Edit Mode tests (Play Mode only inside network/scene tests, as the project does).

**Spec:** `docs/superpowers/specs/2026-10-28-highlight-reel-design.md`

## Global Constraints

**What changes, and how files are edited**
- No scene, prefab or ProjectSettings edits. The reel's camera, overlay and stand-ins are built by code at runtime.
- New `Assets/` files get a `.meta` from `bash tools/newmeta.sh <file>`.
- CRLF files are edited with the Edit tool only, never git-bash `sed -i`. Check line endings with `git ls-files --eol <file>`.
- New scripts go in `Assets/Scripts/Highlights/`, except the online relay (`Assets/Scripts/Online/`). Tests go in `Assets/Tests/Editor/`.

**Branch and testing**
- Branch `steam-phase1a`. Never check out `main`.
- **Commit each task** on `steam-phase1a` (as recent commits do, ending with the `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>` line). Stage only the task's own files: the user's unrelated changes (font assets, `FinalAreaScene.unity`) stay unstaged. Wherever a task says "Leave uncommitted", commit instead.
- Tests:
  - `bash tools/run-tests.sh <filter>`, **one run at a time**. Check `ListAgents` for another session using the mirror first.
  - Results are in `Logs/test-results.xml`.
  - Quick compile check: `python tools/compile-check.py`, which ends with `COMPILES`.

**Values pinned by the spec**
- Reel length 10–20 s, with no padding. Clips are 3–6 s, starting a beat before the action.
- Interaction weighs most. Start weights:
  - golden steal 10, steal 8, clash 6
  - bump 1–4, by strength
  - hard delivery 5, other delivery 2, +3 for a delivery with ≤ 10 s left
  - lone fall 3
- Clips play in match order.
- Skip: `SkipVote` / `CutsceneSkip`. Seats are every roster player.
- Overlay copy, through `Loc`, keys in `tools/strings_en.py`, then `python tools/strings.py`:
  - `reel.rec` = `REC`
  - `reel.cam` = `CAM {0}`
  - `reel.caption.theft` = `THEFT DETECTED`
  - `reel.caption.collision` = `COLLISION DETECTED`
  - `reel.caption.water` = `SUBJECT ENTERED WATER`
  - `reel.caption.delivery` = `DELIVERY CONFIRMED`
- Code comments: match the surrounding files (summary docs on public members, short "why" comments, plain English).

## Review Focus

1. **A skip, a host load or a disconnect in the middle of the reel.** Real players' renderers and HUDs come back, nothing throws, and the golden round loads once. Tests in Task 7.
2. **A round with no moments, or only tiny ones**, like a lone low-strength bump. No reel, and the load happens straight after the linger, as today. Tests in Tasks 2 and 7.
3. **A seat that left mid-round.** Its samples stop, and its clips still play (the stand-in vanishes after its last sample). The skip vote doesn't wait on it. Tests in Tasks 1 and 7.
4. **A client whose reel list arrives late (or never).** A late list starts at the right offset, and no list means no reel. A list with nonsense (NaN, a bad seat, too many clips) is ignored. Tests in Tasks 6 and 8.
5. **Every candidate shot blocked**, for example action in a tunnel or under a bridge. The clip falls back to the least-blocked candidate rather than dropping the reel or throwing. Test in Task 5.

---

### Task 1: The recording buffer

**Files:**
- Create: `Assets/Scripts/Highlights/ReelRecording.cs`
- Test: `Assets/Tests/Editor/ReelRecordingTests.cs`

**Interfaces:**
- Consumes: `ScooterPose` (Ball, Heading, ModelPosition, ModelRotation), `DriveFlags`.
- Produces:
  - `public struct ReelSample { public double Time; public ScooterPose Pose; public DriveFlags Flags; public byte Carrying; }`. `Carrying` is the best carried `Constants.OrderValue` + 1, or 0 for none.
  - `public class ReelRecording`, with:
    - `ReelRecording(int capacityPerSeat)`
    - `void Add(int seat, ReelSample sample)`
    - `bool TryGet(int seat, double time, out ReelSample sample)`, interpolated
    - `double First(int seat)`, `double Last(int seat)`
    - `void Clear()`
  - `public const float SAMPLE_RATE = 20f`.

- [ ] **Step 1: Write the failing tests**
  - `TryGet_BetweenSamples_Interpolates`: samples at t=0 (ball 0,0,0, heading 350) and t=1 (ball 10,0,0, heading 10). At 0.5 it gives ball (5,0,0) and heading 0 (`Mathf.LerpAngle`). `ModelRotation` is slerped.
  - `TryGet_FlagsAndCarrying_TakeTheEarlierSample`: Hidden is true at t=0 and false at t=1. At 0.9 it's still hidden.
  - `TryGet_OutsideTheSeatsSamples_ReturnsFalse`: before `First`, after `Last`, or a seat with no samples.
  - `Add_PastCapacity_DropsTheOldest`: capacity 3, add 5. `First` is the third sample's time.
  - `Add_OutOfOrderTime_IsIgnored`: a time ≤ the seat's last is dropped.
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh ReelRecordingTests`. Expected: compile failure, because `ReelRecording` isn't defined.
- [ ] **Step 3: Implement `ReelRecording`.** Use a fixed ring buffer per seat (`Constants.MAX_PLAYERS` seats), allocated in the constructor, with nothing allocated in `Add`. Look samples up by binary search.
- [ ] **Step 4: Run them and check they pass.** Run `bash tools/run-tests.sh ReelRecordingTests`. Expected: all PASS.
- [ ] **Step 5: Leave uncommitted.**

### Task 2: Moments, clips and picking the reel

**Files:**
- Create: `Assets/Scripts/Highlights/HighlightRules.cs` (with the `Moment`, `MomentKind`, `Clip` and `Caption` types)
- Test: `Assets/Tests/Editor/HighlightRulesTests.cs`

**Interfaces:**
- Produces:
  - `public enum MomentKind : byte { GoldenSteal, Steal, Clash, Bump, HardDelivery, Delivery, Fall }`
  - `public enum Caption : byte { Theft, Collision, Water, Delivery }`
  - `public struct Moment { public MomentKind Kind; public double Time; public int Seat; public int Other; public float Strength; public float TimeLeft; public Vector3 Place; }`
    - `Other` is -1 when no second player is involved.
    - `Strength` (0–1) is used only by bumps.
    - `TimeLeft` is the round clock at the moment.
  - `public class Clip { public double Start, End, KeyTime; public float Score; public Caption Caption; public int SeatsMask; public Vector3 KeyPlace; public float TimeLeftAtStart; public double Length => End - Start; }`
  - `public static class HighlightRules`, with:
    - `static float Weight(Moment m)`
    - `static List<Clip> Clips(IReadOnlyList<Moment> moments)`
    - `static List<Clip> Pick(IReadOnlyList<Clip> clips, IEnumerable<int> seats)`
  - Constants (starting values, to be tuned in playtests):
    - `COMBO_WINDOW = 4f`, `COMBO_MULTIPLIER = 1.5f`
    - `KNOCKED_IN_WINDOW = 3f`, `KNOCKED_IN_BONUS = 4f`
    - `LATE_SECONDS = 10f`, `LATE_BONUS = 3f`
    - `LEAD = 1.5f`, `TAIL = 1.5f`, `MIN_CLIP = 3f`, `MAX_CLIP = 6f`
    - `GOOD_ENOUGH = 5f`, `PLAYER_MINIMUM = 3f`, `MAX_REEL = 20f`

**The rules:**
- **Weights:**
  - `GoldenSteal` 10, `Steal` 8, `Clash` 6
  - `Bump` = 1 + 3 × `Strength`
  - `HardDelivery` 5, `Delivery` 2, each +`LATE_BONUS` when `TimeLeft ≤ LATE_SECONDS`
  - `Fall` 3, +`KNOCKED_IN_BONUS` when the same seat was the victim (`Other`) of a steal, clash or bump in the `KNOCKED_IN_WINDOW` seconds before it
- **Clips:** walk the moments in time order. A moment joins the open clip when it's within `COMBO_WINDOW` of that clip's last moment **and** shares a seat with it. Otherwise it starts a new clip.
  - Score = sum of weights × `COMBO_MULTIPLIER^(count−1)`.
  - Start = first − `LEAD`; End = last + `TAIL`. Clamp the length into [`MIN_CLIP`, `MAX_CLIP`]: too long trims the start, too short extends the end.
  - KeyTime and KeyPlace come from the heaviest moment, and so does the caption: steals → Theft, clash/bump → Collision, fall → Water, deliveries → Delivery.
- **Pick:**
  1. **Fairness:** for each seat in `seats`, take its best clip (`SeatsMask` contains it) scoring ≥ `PLAYER_MINIMUM`. Go seats in order of that clip's score, high first, skipping clips already taken or that would pass `MAX_REEL`.
  2. **Fill:** add the remaining clips scoring ≥ `GOOD_ENOUGH`, best first, while the total stays ≤ `MAX_REEL`.
  3. Return them sorted by `Start`. Overlapping chosen clips are merged.

- [ ] **Step 1: Write the failing tests**
  - `Weight_StartValues`: one case per kind, with the exact numbers above, including bump strength 0 → 1 and 1 → 4, and a hard delivery at 8 s left → 8.
  - `Fall_AfterBeingBumped_GetsTheKnockedInBonus`: a bump (victim seat 1) at t=10, then seat 1 falls at t=12 → the fall weighs 7. At t=14 it weighs 3.
  - `Clips_StealThenBumpThenFall_SharingASeat_MergeIntoOne`: score = (8 + 1 + 7) × 1.5² = 36.
  - `Clips_SameTimeDifferentSeats_StaySeparate`.
  - `Clips_Length_ClampedToThreeToSix`.
  - `Pick_EveryoneWithAGoodEnoughClip_GetsOne`: seat 0 has 4 clips of 20, seat 1 has one clip of 4 → seat 1's clip is picked.
  - `Pick_NeverPassesTwentySeconds`.
  - `Pick_OnlyTinyMoments_PicksNothing`: a lone bump at strength 0.1 → empty list.
  - `Pick_ReturnsMatchOrder`.
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh HighlightRulesTests`. Expected: compile failure.
- [ ] **Step 3: Implement `HighlightRules`** as a pure static class, with no `UnityEngine` calls beyond `Vector3`/`Mathf`.
- [ ] **Step 4: Run them and check they pass.** Run `bash tools/run-tests.sh HighlightRulesTests`. Expected: all PASS.
- [ ] **Step 5: Leave uncommitted.**

### Task 3: The game reports hits and bumps with both players

**Files:**
- Modify: `Assets/Scripts/Steam/FeatSync.cs` (two events and raisers, cleared in its `Reset`)
- Modify: `Assets/Scripts/Player/OrderHandler.cs` (`StealFrom` ~line 400, the offline clash branch of `Hit` ~line 592)
- Modify: `Assets/Scripts/Online/OnlineSteals.cs` (the clash branch ~line 99)
- Modify: `Assets/Scripts/Online/OnlineBumps.cs` (`Judge`, after the bump is accepted ~line 124)
- Create: `Assets/Scripts/Highlights/BumpWatcher.cs` (offline bumps, on each ball)
- Test: `Assets/Tests/Editor/HighlightEventsTests.cs`

**Interfaces:**
- Produces:
  - `FeatSync.Struck`, an `event Action<int, int, bool, bool>` of (attacker, victim, steal, golden).
    - Raised by `FeatSync.Strike(int attacker, int victim, bool steal, bool golden)`.
    - Raised only by the rules machine: in `StealFrom` after the order changes hands (steal=true; not when steals are off by the match rules), in `Hit`'s offline clash branch (once, by the attacker), and in `OnlineSteals`' clash branch.
  - `FeatSync.Bumped`, an `event Action<int, int, float>` of (bumper, victim, strength 0–1), raised by `FeatSync.Bump(...)`.
    - Online: in `OnlineBumps.Judge` once accepted, with strength = `push / toBall.ClashForce`, clamped to 0–1.
    - Offline: by `BumpWatcher`.
  - `public class BumpWatcher : MonoBehaviour`, with `void Begin(BallDriving driving, int seat)`.
    - Offline only. On a ball's `OnCollisionEnter` with another player's ball while neither is boosting, it raises `FeatSync.Bump(seat, otherSeat, Mathf.Clamp01(closing / BumpRules.MAX_SPEED))`.
    - It dedupes with a `BumpRules` pair cooldown (`TooSoon`), shared statically across watchers, so the two balls of one bump raise it once.

- [ ] **Step 1: Write the failing tests**
  - `StealFrom_RaisesStruckWithBothSeats`: two `OrderHandler`s set up as `OrderHandlerResetTests` does. Thief seat 0 steals from seat 1 → `Struck(0, 1, true, false)` exactly once.
  - `StealFrom_WithStealsOff_RaisesNoStruck`.
  - `OfflineClash_RaisesStruckOnce`.
  - `BumpWatcher_BothBallsReport_RaisesBumpedOnce`: call the watcher's collision handler from both sides within `BumpRules.PAIR_COOLDOWN` → one `Bumped`.
  - `BumpWatcher_Online_RaisesNothing` (`GameAuthority.Role` = Host).
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh HighlightEventsTests`. Expected: compile failure.
- [ ] **Step 3: Implement it.** Add the events and raisers, the raise sites listed above, and `BumpWatcher` (its collision handler `internal` so tests can call it, as `BallCollision.OnTriggerEnter` is).
- [ ] **Step 4: Run them and check they pass, with no regressions in neighbours.** Run `bash tools/run-tests.sh "HighlightEventsTests|AchievementsTests|BumpRulesTests|OrderHandlerResetTests"`. Expected: all PASS.
- [ ] **Step 5: Leave uncommitted.**

### Task 4: Recording a round and collecting its moments

**Files:**
- Create: `Assets/Scripts/Highlights/HighlightDirector.cs` (the per-launch singleton: recording, moments, and later playback)
- Test: `Assets/Tests/Editor/HighlightDirectorTests.cs`

**Interfaces:**
- Consumes:
  - `ReelRecording`, `ReelSample` (Task 1); `Moment`, `MomentKind` (Task 2); `FeatSync.Struck` / `Bumped` (Task 3), `FeatSync.Delivered` / `Sank`
  - `GameManager.StateApplied` / `OnNewMatch`, `OrderManager.OnMainGameFinishes`, `OrderManager.GameTimer`
  - `PlayerInstantiate.Instance.Roster`, `ScooterPose.Read(BallDriving)`, `BallDriving.Flags`, `OrderHandler.GetBestOrder()`
- Produces:
  - `public class HighlightDirector : MonoBehaviour`, created at launch like `MatchAwards` (`RuntimeInitializeOnLoadMethod`, `DontDestroyOnLoad`), with `static HighlightDirector Instance`.
  - `internal ReelRecording Recording`; `internal List<Moment> Moments` (the rules machine only).
  - `internal static double Now()`: Netcode server time online (`NetworkManager.Singleton.ServerTime.Time`), else `Time.timeAsDouble`.
  - `internal void Sample(double now)`: one sample of every roster seat. Tests call it directly.
  - `internal Func<int, Vector3> PlaceOf`: a seat's ball position now. Tests swap it.

**Behaviour:**
- Recording starts on `MainLoop` and runs at `ReelRecording.SAMPLE_RATE` (accumulated in `Update`, sampled at most once per frame).
  - It stops at `OnMainGameFinishes`, then the director stops listening. That's a C# event on the scene's `OrderManager`, so re-hook it each game scene.
  - It ignores the tutorial and `FinalPackage`.
  - Capacity is (round length + 30 s) × `SAMPLE_RATE`, or 12000 if unknown.
- `Clear()` the recording and moments on `OnNewMatch` and on entering `MainLoop`.
- **The rules machine only** (`GameAuthority.IsAuthority`) turns events into `Moment`s, with `Time = Now()`, `TimeLeft = OrderManager.Instance.GameTimer` and `Place = PlaceOf(seat)`:
  - `Struck` with steal → `GoldenSteal` / `Steal` (Seat = attacker, Other = victim)
  - `Struck` without → `Clash`
  - `Bumped` → `Bump`
  - `Delivered` → `HardDelivery` / `Delivery`
  - `Sank` → `Fall`
  - Only during `MainLoop`.
- Add a `BumpWatcher` to every roster ball at `MainLoop` when offline.

- [ ] **Step 1: Write the failing tests**
  - `Sample_RecordsEverySeatsPoseAndFlags`: with a menu-scene roster as `CompassTests` / `ScooterLevelTests` set one up, or a fake roster through an `internal Func<IEnumerable<(int, BallDriving, OrderHandler)>> Seats` hook (add that hook if the roster can't be faked).
  - `Struck_DuringMainLoop_OnAuthority_AddsASteal` with seat, other, time and time left.
  - `Events_OnAClient_AddNoMoments`.
  - `Events_InTheTutorial_AddNoMoments`.
  - `MainGameFinishes_StopsSampling`.
  - `NewMatch_ClearsRecordingAndMoments`.
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh HighlightDirectorTests`. Expected: compile failure.
- [ ] **Step 3: Implement the recording half of `HighlightDirector`.**
- [ ] **Step 4: Run them and check they pass.** Run `bash tools/run-tests.sh HighlightDirectorTests`. Expected: all PASS.
- [ ] **Step 5: Check the cost.** In `tools/run-tests.sh LocalMatchSmokeTest`, confirm no GC allocations come from `HighlightDirector.Sample`: wrap it in `Profiler.BeginSample("Highlights.Sample")` and check `GC.GetAllocatedBytesForCurrentThread` before and after over 100 calls in a test, `Sample_AllocatesNothing`. Add that test to Step 1's file.
- [ ] **Step 6: Leave uncommitted.**

### Task 5: The shot director

**Files:**
- Create: `Assets/Scripts/Highlights/ShotDirector.cs` (with `ShotKind` and `ReelShot`)
- Test: `Assets/Tests/Editor/ShotDirectorTests.cs`

**Interfaces:**
- Consumes: `Clip` (Task 2), `ReelRecording` (Task 1).
- Produces:
  - `public enum ShotKind : byte { Low, HighWide, SideTrack, Behind, FixedPan }`
  - `public struct ReelShot { public ShotKind Kind; public double Cut; public Vector3 Position; public int Subject; public Vector3 Offset; public int FramedMask; public float Speed; }`
    - `Cut` is the round time this shot starts.
    - `Position` is used by static kinds (Low, HighWide, FixedPan).
    - `Subject` (a seat) and `Offset` are used by moving kinds (SideTrack, Behind). The offset is in the subject's heading frame.
    - `Speed` is the playback speed: 1, or `PEAK_SPEED`.
  - `public class ShotDirector`, with:
    - `ShotDirector(ReelRecording recording, Func<Vector3, Vector3, bool> clear)`. `clear` is true when nothing solid lies between two points.
    - `List<ReelShot> Shots(Clip clip, ShotKind? previous)`: the first shot, plus a peak shot for big clips.
  - Constants (starting values):
    - `SAMPLES = 5`, `FOV = 60f`
    - `PEAK_SCORE = 12f`, `PEAK_LEAD = 0.3f`, `PEAK_SPEED = 0.5f`, `PEAK_LENGTH = 1.5f`
    - Placements, from the action's centre and direction of travel at `clip.Start`:
      - Low: 6 m ahead and 4 m to the side, 0.8 m up
      - HighWide: 25 m back, 18 m up
      - SideTrack: offset (8 side, 2 up, 0 forward)
      - Behind: offset (0 side, 3 up, −7 forward)
      - FixedPan: 15 m ahead along the travel, 3 m up

**Rules:**
- **Framed seats:** the clip's `SeatsMask` seats with samples in the clip.
- **Testing a candidate:** at `SAMPLES` times spread across the clip, the camera looks at the framed seats' centroid. Every framed seat must be within `FOV/2` of that look direction and pass `clear(camera, seat ball + 1 m up)`.
- **Choosing:** of the candidates that pass every sample, pick the one whose mean distance to the centroid is closest to that kind's ideal (its placement distance). Never pick `previous`. If none passes, take the candidate with the most clear samples (ties to the kind order above), still not `previous`.
- **Peak shot:** when `clip.Score ≥ PEAK_SCORE`, add a second shot at `KeyTime − PEAK_LEAD`, with `Speed = PEAK_SPEED` for `PEAK_LENGTH` s of clip time, then back to 1. Its kind is Behind or Low, whichever passes and isn't the first shot's kind.

- [ ] **Step 1: Write the failing tests.** Use a recording of two seats driving straight along +X at 10 m/s and a `clear` delegate that blocks named rays.
  - `Shots_AllClear_PicksTheBestFramedKind`.
  - `Shots_NeverRepeatsThePreviousKind`: previous = the result of the case above → a different kind.
  - `Shots_BlockedCandidate_IsRejected`: block every ray from the HighWide spot → not HighWide.
  - `Shots_EverythingBlocked_StillReturnsAShot`, with the most-clear rule (Review Focus 5).
  - `Shots_BigClip_AddsASlowPeakShot`: score 20 → 2 shots, the second at KeyTime − 0.3 with Speed 0.5.
  - `Shots_SmallClip_OneShot`.
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh ShotDirectorTests`. Expected: compile failure.
- [ ] **Step 3: Implement `ShotDirector`.** In the game, `clear` is `!Physics.Linecast(a, b, mask, QueryTriggerInteraction.Ignore)`, with `mask` excluding the players' ball/scooter layers. Read those layers from `PlayerAvatar.prefab`'s `Ball Of Fun` and `Control` at runtime. Don't hard-code numbers.
- [ ] **Step 4: Run them and check they pass.** Run `bash tools/run-tests.sh ShotDirectorTests`. Expected: all PASS.
- [ ] **Step 5: Leave uncommitted.**

### Task 6: The reel list, as it travels

**Files:**
- Create: `Assets/Scripts/Highlights/ReelList.cs`
- Modify: `Assets/Scripts/Online/NetChecks.cs` (add `Sane(ReelList)`)
- Test: `Assets/Tests/Editor/ReelListTests.cs`

**Interfaces:**
- Consumes: `Clip` (Task 2), `ReelShot` / `ShotKind` (Task 5).
- Produces:
  - `public struct ReelClip : INetworkSerializable { public double Start, End, KeyTime; public byte SeatsMask; public Caption Caption; public float TimeLeftAtStart; public ReelShot[] Shots; }`
  - `public struct ReelList : INetworkSerializable, IEquatable<ReelList> { public ReelClip[] Clips; public double StartAt; public float Length; }`
    - `StartAt` is the round time to begin playing on every machine.
    - `Length` is the real seconds, counting slow shots.
  - `ReelShot` gains `NetworkSerialize`.
  - `public static ReelList ReelList.From(List<Clip> clips, ShotDirector director, double startAt)`. Shots chain `previous` across clips.
  - `public const int MAX_CLIPS = 8, MAX_SHOTS = 3`.
  - `NetChecks.Sane(ReelList)`: false for null arrays, too many clips/shots, a NaN/∞ anywhere, Start ≥ End, a `SeatsMask` or `Subject` outside `Constants.MAX_PLAYERS`, an undefined `ShotKind`/`Caption`, `Length` > 30, or `Speed` outside (0, 1].

- [ ] **Step 1: Write the failing tests**
  - `RoundTrip_SerializesEveryField`, using a `FastBufferWriter`/`FastBufferReader` the way `AwardBoardTests` does.
  - `Length_CountsSlowShots`: a 4 s clip with a 1.5 s peak at 0.5× → 5.5 s.
  - `Sane_Rejects…`: one case per rule above.
  - `Sane_AcceptsARealList`.
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh ReelListTests`. Expected: compile failure.
- [ ] **Step 3: Implement `ReelList` and `NetChecks.Sane`.**
- [ ] **Step 4: Run them and check they pass.** Run `bash tools/run-tests.sh "ReelListTests|NetChecksTests"`. Expected: all PASS.
- [ ] **Step 5: Leave uncommitted.**

### Task 7: Playing the reel (offline) and the end-of-round hook

**Files:**
- Create: `Assets/Scripts/Highlights/HighlightReel.cs` (playback: stand-ins, camera, timing, skip)
- Create: `Assets/Scripts/Highlights/ReelOverlay.cs` (style C, built by code; `ReelOverlay.Format…` helpers kept pure)
- Modify: `Assets/Scripts/Highlights/HighlightDirector.cs` (`IEnumerator RunReel()`: pick, direct, play)
- Modify: `Assets/Scripts/Management/OrderManager.cs` (`PostGameClarity(false)`: `yield return` the reel between the linger and `LoadFinalOrderScene`)
- Modify: `Assets/Scripts/Player/TrailHandler.cs` (add `internal Material BoostTrailMaterial`, the first boost trail's `sharedMaterial`, or null)
- Modify: `tools/strings_en.py`, then regenerate `Assets/Resources/Localization/strings.csv` with `python tools/strings.py`
- Test: `Assets/Tests/Editor/ReelOverlayTests.cs`, `Assets/Tests/Editor/HighlightReelTests.cs` (menu scene, Play Mode, as `LateCameraTests`)

**Interfaces:**
- Consumes: `ReelList` (Task 6), `ReelRecording` (Task 1), `CutsceneSkip` (`PromptParent`, `Skip`, `Open(seats, decides)`, `Close()`), `CameraBudget.COVER_DEPTH`, `ScooterLook.MODEL`, `TrailHandler.BoostTrailMaterial`.
- Produces:
  - `public class HighlightReel : MonoBehaviour`, with:
    - `static HighlightReel Play(ReelList list, ReelRecording recording, bool decides)`: built in the active scene, so a scene load destroys it.
    - `bool Done`
    - `void End()`: idempotent, and restores everything.
    - `internal double Clock`: round time now, in the reel.
  - `ReelOverlay`, with:
    - `static string Timestamp(float timeLeft)` → `mm:ss`
    - `static int CameraNumber(Vector3 position)` → 1–24, stable for a position: `(int)Mathf.Abs(Mathf.Floor(p.x) * 7 + Mathf.Floor(p.z) * 13) % 24 + 1`
    - `static string CaptionKey(Caption c)`
  - `HighlightDirector.RunReel()`: on the rules machine, `HighlightRules.Clips(Moments)` → `Pick` → `ReelList.From(..., startAt: Now() + START_DELAY)`, with `START_DELAY = 0.5f`. Then `HighlightSync.Share(list)` (Task 8, a no-op offline). Then it plays and waits until `Done`. With nothing picked, it yields nothing.

**Playback behaviour:**
- **Stand-ins:** for each seat in any clip, clone that player's model (`ScooterLook.MODEL` under the player's avatar, already dressed in colour and hat).
  - Strip every `MonoBehaviour`, `Collider`, `Rigidbody`, `Animator`, `AudioSource` and `VisualEffect`, and any child holding an `Order`.
  - Add one `TrailRenderer` using `TrailHandler.BoostTrailMaterial`; with no material, add no trail. It emits while the sample's `Flags.Boosting` is true.
  - Show a carried parcel while `Carrying > 0`. Clone the player's held `Order` visual if they hold one at reel start, else a 0.4 m cube tinted by value. This is the builder's choice.
  - Each frame: `ReelRecording.TryGet(seat, Clock)` → the clone's world pose = `ModelPosition` / `ModelRotation`. Hidden, or no sample → inactive.
- **Real players:** disable every enabled `Renderer` under each roster player's avatar, plus each local player's `Driving Canvas`, and remember them. `End()` re-enables exactly those. **Never deactivate `modelParent`**, because `Respawn.RiderHidden` reads it.
- **Camera:** one new `Camera`, full screen, depth `CameraBudget.COVER_DEPTH + 20`, clear Skybox, FOV `ShotDirector.FOV`, with no `AudioListener`. `CameraBudget` then turns the player cameras off by itself.
  - Static shots sit at `Position`. Moving shots sit at the subject stand-in's position + its heading frame × `Offset`.
  - Every shot looks at the framed stand-ins' centroid each frame.
- **Time:**
  - `Clock` starts at the first clip's `Start` when `HighlightDirector.Now() ≥ list.StartAt`. If it starts late, it begins at the matching offset into the reel.
  - It advances by unscaled delta × the current shot's `Speed`.
  - Between clips it jumps to the next clip's `Start`, a hard cut.
  - `Done` after the last clip's `End`.
- **Overlay (`ReelOverlay`):** a Screen Space Overlay canvas with sortingOrder 500.
  - A full-screen `Image` tint (0.5, 0.5, 0.5, 0.12), plus scanlines: a 1×4 `Texture2D` (two clear rows, two black at α 0.12, point filter, tiled every 4 px) on a `RawImage`.
  - A thin frame inset 6%, white at α 0.4.
  - Top left `● REC` (the dot red, blinking at 1 Hz). Top right `CAM nn`.
  - Bottom left: the timestamp, `TimeLeftAtStart − (Clock − clip.Start)`, clamped ≥ 0.
  - The caption, centred upper third, from `KeyTime − 0.2` to `KeyTime + 1.8`.
  - Text in TMP with the project's font rules for the current language, as `ResolutionRow` gets its font.
- **Skip:** add `CutsceneSkip` to the reel object, with `PromptParent` = the overlay canvas, `Skip` = `End`, and `Open(every roster seat, decides)`. `Close()` on `End`.
- **Hook:** in `PostGameClarity(false)`, after the linger and `SetTimeScale(1)`, `yield return HighlightDirector.Instance.RunReel()` (guarded for a null instance), then `LoadFinalOrderScene()` as today.

- [ ] **Step 1: Write the failing overlay tests**
  - `Timestamp_FormatsMinutesAndSeconds`: 161.4 → `02:41`, −3 → `00:00`.
  - `CameraNumber_StableAndInRange`.
  - `CaptionKey_EachCaption`.
- [ ] **Step 2: Write the failing reel tests.** Menu scene, a fake two-seat recording, and a two-clip list.
  - `Play_CoversThePlayersAndHidesTheirRenderers`: `CameraBudget.CoveredNow()` is true, and the roster renderers are off.
  - `Play_StandInsFollowTheRecording`: at a known `Clock`, the stand-in's position is within 0.01 of the interpolated sample.
  - `Play_EndsAfterTheLastClip_AndRestoresEverything`: renderers and Driving Canvas are back exactly as before, and the reel object is gone.
  - `Skip_AllSeatsVote_EndsEarly`, using `CutsceneSkip.Pressed` swapped as `CutsceneSkipTests` does.
  - `RunReel_NoMoments_YieldsAtOnce` (Review Focus 2).
  - `End_Twice_IsHarmless`, and `End_AfterARosterSeatLeft_DoesNotThrow` (Review Focus 1, 3).
- [ ] **Step 3: Run them and check they fail.** Run `bash tools/run-tests.sh "ReelOverlayTests|HighlightReelTests"`. Expected: compile failure.
- [ ] **Step 4: Implement `ReelOverlay`, `HighlightReel`, `RunReel`, the `PostGameClarity` hook, and the strings.**
- [ ] **Step 5: Run them and check they pass.** Run `bash tools/run-tests.sh "ReelOverlayTests|HighlightReelTests|StringsCsvTests"`. Expected: all PASS.
- [ ] **Step 6: Run the smoke test.** Run `bash tools/run-tests.sh LocalMatchSmokeTest`. Expected: PASS, and the log shows the reel starting and ending (add one `Debug.Log("Highlights: reel of {n} clips, {s} s")` in `RunReel`, matching the log style of `SessionLog`).
- [ ] **Step 7: Leave uncommitted.**

### Task 8: Online: the host's reel on every machine

**Files:**
- Create: `Assets/Scripts/Highlights/HighlightSync.cs` (static seam, like `AwardSync`)
- Create: `Assets/Scripts/Online/OnlineHighlights.cs` (relay, like `OnlineAwards`)
- Modify: `Assets/Scripts/Online/OnlineMatch.cs` (`ShareReel(ReelList)` → `ReelClientRpc`, checked with `NetChecks.Sane`; `event Action<ReelList> ReelReceived`)
- Modify: `Assets/Scripts/Online/OnlineGame.cs` (add `OnlineHighlights` next to `OnlineAwards`, ~line 198; list it in the class summary)
- Modify: `Assets/Scripts/Highlights/HighlightDirector.cs` (a client plays an arrived list)
- Test: `Assets/Tests/Editor/HighlightMessagesNetworkTests.cs` (port **7823**, empty scene, as `AchievementMessagesNetworkTests`)

**Interfaces:**
- Consumes: `ReelList` (Task 6), `HighlightReel.Play` (Task 7), `HighlightDirector.Now()` (Task 4).
- Produces:
  - `HighlightSync`, with:
    - `static event Action<ReelList> Shared`, `Arrived`
    - `static void Share(ReelList)`, `Arrive(ReelList)`
    - `static void Reset()`, called from `HighlightDirector`'s `SubsystemRegistration` reset
  - **Client:** on `Arrived`, while the game scene is up and `MainState` isn't past `MainLoop`, `HighlightReel.Play(list, Recording, decides: false)`.
    - A list arriving after its `StartAt` starts at the offset, and plays nothing if the whole reel is already over.
    - A client ends its reel when the host's load starts. The scene unload destroys it, and `OnDestroy` calls `End()`.
- **Host:** `RunReel` waits only on its own reel. It never waits for clients.

- [ ] **Step 1: Write the failing network tests**
  - `Host_SharesAReel_ClientReceivesTheSameList`, with `IEquatable` equality.
  - `Client_NonsenseReel_IsIgnored`: the host sends a list with a NaN → no `Arrived`.
  - `Client_NeverSends`: `ShareReel` on a client sends nothing.
  - `Client_LateList_StartsAtTheOffset`: `StartAt` 2 s in the past → `Clock` = first clip's `Start` + 2 (Review Focus 4). This one may be an Edit Mode unit test on `HighlightReel`'s start-offset helper instead, `internal static double StartClock(ReelList list, double now)`. Add the helper if so.
- [ ] **Step 2: Run them and check they fail.** Run `bash tools/run-tests.sh HighlightMessagesNetworkTests`. Expected: compile failure.
- [ ] **Step 3: Implement the seam, the relay, the RPC and the client playback.**
- [ ] **Step 4: Run them and check they pass, with the network neighbours.** Run `bash tools/run-tests.sh "HighlightMessagesNetworkTests|AchievementMessagesNetworkTests|NonsenseNetworkTests"`. Expected: all PASS.
- [ ] **Step 5: Run the online match flow.** Run `bash tools/run-tests.sh OnlineMatchFlowNetworkTests`. Expected: PASS (time up on a client still reaches the golden load).
- [ ] **Step 6: Leave uncommitted.**

### Task 9: Docs and the full run

**Files:**
- Modify: `docs/testing.md` (the new tests and port 7823; the next free port becomes 7824)
- Modify: `docs/online.md` (the reel list: the host picks, every machine plays its own recording, the server-time start, no waiting)
- Modify: `docs/performance.md` (the recorder's budget and how it was checked)
- Modify: `EDITOR-TODO.md` (playtest items for the user, as short bullets: tune the weights and the "good enough" bar; check the reel on the Steam Deck and in 4-player split-screen with `PerfOverlay`; check the camera choices look good in the real town)

- [ ] **Step 1: Write the docs above,** in the files' own style.
- [ ] **Step 2: Run the full suite.** Run `bash tools/run-tests.sh`. Expected: every test PASS, and the count in `Logs/test-results.xml` = the previous count + the new tests.
- [ ] **Step 3: Compile-check the player scripts.** Run `python tools/compile-check.py`. Expected: `COMPILES`.
- [ ] **Step 4: Leave uncommitted,** with a commit draft for the user in the session scratchpad.

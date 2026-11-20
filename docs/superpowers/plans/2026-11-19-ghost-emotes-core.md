# Ghost emotes, part 1: core and no-art moves

Spec: `docs/superpowers/specs/2026-11-19-ghost-animations-design.md`. Branch `steam-phase1a`. Nothing is committed.

Scope of this part:
- the shared `GhostEmotes` system: version picker, priorities, cooldowns, and applying moves to the bones
- the 26 versions that need no new art (Code and Bone only)
- a from-behind capture to review them

Not in this part:
- the hand clips and the upper-body Animator layer (part 2, authored in Blender)
- triggers in the game (part 3)
- online
- prefab setup

## Facts this plan relies on
- **The ghost.**
  - It is `Official_Anim_4.fbx` as a Generic rig (no Avatar). Its `Animator` sits on "Player Model" in `PlayerAvatar.prefab`.
  - Bone paths from the Animator: `Armature.001/Torso`, `Armature.001/Torso/Head`, `Armature.001/Torso/Hand.Left|Hand.Right`, `Armature.001/Tail1` (→`Tail2`, NOT under Torso), and `Armature.001/Torso/TailIK/Tail3`.
- **The Animator writes these bones every frame.** `Idle Float` keys Torso, the hands and the tails; only `Grab Left` keys Head. So moves are applied in `LateUpdate` as offsets on top of what the Animator wrote.
  - Head may not be rewritten every frame. That's why `GhostRig` detects a bone it wrote last frame that nothing has rewritten since, and restores the base pose, so offsets never pile up.
- **The hat** is "Hat Object", a child of Head. `ScooterLook.ShowHat` sets its local TRS, so capture the hat's rest TRS when a move begins and put it back when the move ends.
- **The body** is a SkinnedMeshRenderer "Player_Model" using `URP/OToonLit`, which is opaque.
  - A fade must use `_DitherThreshold` (0–1) through a `MaterialPropertyBlock`. Verify that the shader really discards pixels with it. If it doesn't, fade by shrinking instead (Tall and Wide towards 0.05).
  - The eyelids are separate renderers under the model.
- **Proven in Blender:** turning only Head 180° tears the mesh. "Turn round to the camera" = body Yaw about 100° plus HeadYaw about 60°.
- **From behind, the body hides the hands** in the driving pose.
- **Reuse:**
  - `TitlePlaylist(int count, System.Random)`: `Next()` never repeats twice running.
  - The curve helpers in `PodiumActs`: `Arc`, `Bounces`, `Wave`, `Hold`, `Smooth` are internal. `Land` is private, so make it internal.
- **Assemblies:** there are no project asmdefs. Runtime goes in Assembly-CSharp, tests in `Assets/Tests/Editor`, `namespace DoA.Tests`, NUnit.
- **Running tests:** `bash tools/run-tests.sh <Fixture>`.
  - It mirrors the project into `../_doa_test_mirror/CapstoneYear4` and runs Unity in batch mode.
  - Never run two at once. Check first that no Unity is running on the mirror.
- **Line endings:** keep files' existing line endings (some are CRLF).

## Files
All new runtime files go in `Assets/Scripts/Player/Emotes/`.

### `GhostAction.cs`
```csharp
public enum GhostAction {
    Cackle, FistShake, DeliveryFlourish, HoldYourHat, Wheee, SeeingStars, TreasureHold, JumpScare, Bored,
    Taunt, BigWave, HatShowOff, Materialise, WarmUp, RevBounce, Victory, Tantrum, FadeAway,
    Scheme, SideEye, Impatient, Raspberry, MirrorCheck, KnuckleCrack, CrownPolish
}
```

### `GhostPose.cs`
`GhostPose` is a struct of offsets from the Animator's pose. `GhostPose.Rest` is all zeros, with Tall and Wide at 1.

Units:
- **U** = the rest distance from Torso to Head in world space (the ghost's own size).
- Angles are in degrees.
- "Its right" and "forward" are the ghost's own directions.

| Field | Meaning |
|---|---|
| `Lift` | U, + up (whole body, moves Torso) |
| `Push` | U, + forward |
| `Shift` | U, + to its right |
| `Pitch` | + tips forward |
| `Yaw` | + turns to its right (seen from above) |
| `Roll` | + leans to its right |
| `Tall`, `Wide` | Torso scale along up / across (1 = rest) |
| `HeadYaw`, `HeadPitch` (+ looks down), `HeadRoll` | Head bone |
| `HeadPop` | U, Head moves up |
| `TailUp` (+ raises), `TailWag` (+ to its right) | Tail1 and TailIK |
| `HatLift` | U, + up |
| `HatSpin` | about up |
| `HatTip` | + flips forward |
| `Fade` | 0..1, body and eyelids (1 = gone) |
| `HatFade` | 0..1, the hat (1 = gone) |

It also gets `bool IsRest(float tolerance = 1e-3f)`.

### `GhostMove.cs`
```csharp
public enum GhostExtra { None, Zzz }
public sealed class GhostMove {
    public GhostMove(GhostAction action, string name, float length, Func<float, float, GhostPose> at,
                     bool entrance = false, GhostExtra extra = GhostExtra.None);
    public GhostAction Action; public string Name; public float Length; public bool Entrance; public GhostExtra Extra;
    public GhostPose At(float share, float side);   // share clamped 0..1; side is +1 or -1 (which way it turns)
}
```

### `GhostEmoteRules.cs` (pure, static)
- `int Priority(GhostAction)`:
  - FistShake 100, Cackle 90, SeeingStars 80, JumpScare 70, DeliveryFlourish 60, Materialise 60, Wheee 50, HoldYourHat 40, TreasureHold 30, Taunt/BigWave/Raspberry 25, SideEye 20, Scheme 15, Bored 10.
  - Every lobby, countdown and results action is 50 (HatShowOff, WarmUp, RevBounce, Victory, Tantrum, FadeAway, Impatient, MirrorCheck, KnuckleCrack, CrownPolish).
- `float Cooldown(GhostAction)` in seconds: SeeingStars 3, Wheee 3, HoldYourHat 4, JumpScare 5, TreasureHold 8, SideEye 8, Scheme 10, Bored 12, all others 0.
- `bool CanStart(GhostAction next, GhostAction? playing, float cooldownLeft)`: false while cooling down. With nothing playing, true. Otherwise true only if `Priority(next) > Priority(playing)`.

### `GhostMoves.cs` (the catalogue, static)
- `GhostMove[] For(GhostAction)`: an empty array for actions with no version yet.
- `IEnumerable<GhostMove> All`.
- `s` is `side`. Every move starts and ends at `Rest` (`At(0)` and `At(1)`), except where marked **entrance**, which starts hidden (Fade 1).

| Spec id | Action | Name | Length | Pose over t |
|---|---|---|---|---|
| 1a | Cackle | Shoulder Shake | 1.2 | Lift 0.2·Bounces(t,5); HeadPitch −35·Hold(t,.15,.2); Roll 10·Wave(t,6) |
| 1c | Cackle | Turn And Cackle | 1.6 | h=Hold(t,.2,.2): Yaw s·100h; HeadYaw s·60h; Lift 0.06·h·\|sin(10πt)\| |
| 5b | Wheee | Floating Hat | 1.0 | HatLift 1.5·Hold(t,.25,.2); HatSpin 360·Smooth(t) |
| 6a | SeeingStars | Head Rolls | 1.4 | e=Hold(t,.1,.3): HeadRoll 35·sin(4πt)·e; HeadPitch 25·(1−cos(4πt))/2·e; Roll 10·sin(4πt)·e. *write it so it is exactly 0 at t=0 and 1* |
| 6b | SeeingStars | Sway | 1.4 | Roll 28·Wave(t,2) |
| 6c | SeeingStars | Hat Spin | 1.2 | HatSpin 720·Smooth(t); HatLift 0.4·Arc(t); HatTip 20·sin(4πt)·Arc(t) |
| 6d | SeeingStars | Bobble Head | 1.3 | HeadPop 1.0·Hold(t,.08,.3); HeadRoll 35·Wave(t,4) |
| 7c | TreasureHold | Check Behind | 1.4 | h=Hold(t,.2,.25): Yaw s·100h; HeadYaw s·60h |
| 8a | JumpScare | Leap | 1.0 | Lift 2.5·Arc(t); Tall 1+0.15·Arc(t)−0.12·Land(t); Wide 1+0.12·Land(t); TailUp 60·Arc(t) |
| 8c | JumpScare | Hide | 1.8 | h=Hold(t,.12,.25): Lift −0.55h; Tall 1−0.35h |
| 9d | Bored | Nap | 3.0 | h=Hold(t,.15,.15): Pitch 30h; HeadPitch 40h; Tall 1+0.12·sin(4πt)·h. **Extra Zzz** |
| 10b | Taunt | Bum Wiggle | 1.2 | h=Hold(t,.15,.15): Pitch 30h; TailUp 20h; TailWag 60·Wave(t,4); Roll 18·Wave(t,4); Shift 0.15·Wave(t,4) |
| 12b | HatShowOff | Spin Round | 1.2 | Yaw 360·Smooth(t); Lift 0.25·Arc(t) |
| 12c | HatShowOff | Hat Flip | 1.1 | HatLift 0.9·Arc(t); HatTip 360·Smooth(t) |
| 13a | Materialise | Wisp | 1.6 | **entrance**. a=Smooth(min(1,t/.6)): Fade 1−a; Tall 0.4+0.6a; then the wet-dog shake: Roll 10·Wave((t−.6)/.4,4) for t>.6 |
| 13c | Materialise | Drop In | 1.2 | **entrance** (Fade 1 at t=0 only, 0 from t>0). Fall t<.5: Lift 4·(1−Smooth(t/.5)); landing squash b=Land-like bump at t≈.5–.7: Tall 1−0.3b, Wide 1+0.25b; TailUp 30·Wave((t−.5)/.5,2) |
| 14b | WarmUp | Neck Roll | 1.6 | e=Hold(t,.1,.1): HeadRoll 30·sin(2πt)·e; HeadPitch 25·(1−cos(2πt))/2·e; Roll 12·Wave(t,2) |
| 15a | RevBounce | Bounce | 0.6 | Lift 0.6·Arc(t); Tall 1+0.09·Arc(t)−0.12·Land(t); Wide 1+0.09·Land(t) |
| 15b | RevBounce | Rock | 1.0 | Roll 26·Wave(t,3) |
| 18a | FadeAway | Sink | 3.0 | Out over t<.4, hidden to .75, back by 1 (pop back with a little overshoot on Tall). Out: Lift −0.6, Fade 1, HatFade 1, Tall 0.8 |
| 18b | FadeAway | Hat Left Behind | 3.0 | Body Fade 1 by t=.3, hat stays; the hat drops (HatLift −0.6 over .45–.55, a small bounce), holds; body Fade back to 0 and HatLift back over .8–1 |
| 18c | FadeAway | Blown Away | 2.4 | Gust t<.45: Shift 6·(t/.45)², Roll −25, Lift 0.8·(t/.45), Fade 1 over .3–.45; hidden to .8; drops back in .8–1 like 13c |
| 20a | SideEye | Look Back | 1.6 | slow out, fast back: h=Hold(t,.35,.1): Yaw s·45h; HeadYaw s·60h |
| 20b | SideEye | Double Take | 1.5 | Glance: HeadYaw s·50·Arc(t/.2) for t<.2. Then at .45–.85: h=Hold over that span: Yaw s·100h; HeadYaw s·60h; Lift 0.2·Arc(over .45–.6) |
| 22b | Raspberry | Tail Waggle | 1.3 | h=Hold(t,.15,.15): Pitch 30h; TailUp 70h; TailWag 70·Wave(t,4); Roll 12·Wave(t,4) |
| 24b | KnuckleCrack | Neck Crack | 1.0 | HeadRoll: snap to −40 over .1–.2, hold to .4, snap to +40 over .45–.55, hold to .75, back to 0 by 1; Lift 0.15 bump at each snap |

The numbers are first guesses. They get tuned from the captures.

### `GhostRig.cs` (plain class, no MonoBehaviour)
- `static GhostRig Find(Animator animator)`: finds the bones by path (null if Torso or Head is missing). It derives the ghost's axes from the rest pose in the model's space, so no import axis is assumed:
  - **up** = Torso→Head
  - **forward** = from Tail1 towards the midpoint of the two hands, flattened against up
  - **right** = up × forward, with its sign checked so +right is the ghost's right
  - It also finds the Torso's local axis closest to up (for Tall), the hat, and every Renderer under the model (the body and eyelids; the hat separately).
- `void Apply(GhostPose pose)`, called in LateUpdate:
  - For each bone it touches, it takes the base pose. That is the bone's current local TRS, unless it still equals what Apply wrote last frame, in which case it's the stored base from last frame.
  - It writes base plus offsets. Rotations are applied about the ghost's world axes at the bone's pivot (`Quaternion.AngleAxis(angle, worldAxis) * bone.rotation`).
  - `Tail1` is not under Torso, so Lift, Push and Shift must move `Tail1` by the same world offset. Otherwise the tail stays behind and stretches (seen in Blender). The body's Yaw/Pitch/Roll should also turn `Tail1` about the Torso pivot.
  - The hat is applied relative to its rest TRS, captured by `BeginHat()`.
  - Fade and HatFade go through a MaterialPropertyBlock (`_DitherThreshold`), with the shrink fallback if dither doesn't discard.
- `void Clear()`: restores the hat, clears the property blocks, and leaves the bones to the Animator.

### `GhostEmotes.cs` (MonoBehaviour on the Animator's GameObject)
- `static GhostEmotes On(Animator animator, System.Random random = null)` gets or adds the component and finds the rig.
- `bool Play(GhostAction action)`:
  - It uses `GhostEmoteRules.CanStart`.
  - It picks from a per-action `TitlePlaylist` over `GhostMoves.For(action)`, and picks `side` at random.
  - It returns false if the action has no versions, is cooling down, or is outranked.
- `void Play(GhostMove move, float side)` forces a version (preview and capture).
- `void Stop()`.
- `GhostMove Playing`.
- `internal void Tick(float dt)` moves the clock and the cooldowns on. The cooldown starts when a move begins.
- `LateUpdate`: `Tick(Time.deltaTime)`, then `rig.Apply(Playing.At(time/Length, side))`. Past the end it applies `Rest` once and clears.
- `OnDisable`/`OnDestroy` call `rig.Clear()`.
- **Zzz:** while a move with `Extra.Zzz` plays, it spawns a world-space TextMeshPro "z" above the head every 0.7 s. Each one rises about 1 U, grows and fades over 1.4 s, then is destroyed. It uses plain code (no DOTween needed), and every z is gone when the move stops.

## Tasks (one agent each, one at a time)
1. **Types and rules.**
   - Write `GhostAction`, `GhostPose`, `GhostMove`, `GhostEmoteRules`, and make `PodiumActs.Land` internal.
   - Tests in `GhostEmotesTests.cs`: priority ordering, cooldown blocks, equal priority doesn't interrupt, and `GhostMove.At` clamps.
2. **Catalogue.** Write `GhostMoves` with the 26 versions above. Tests:
   - every non-entrance move is at rest at t=0 and t=1, and an entrance starts with Fade 1 and ends at rest
   - no NaN at 200 samples
   - lengths > 0
   - the ids above are all present
   - Lift never drops below −0.7 U, except where the table says otherwise
3. **Rig and component.** `GhostRig` and `GhostEmotes`, verifying the dither fade. Tests, using a ghost made with `TitleActor.Create` the way the title tests do:
   - the bones are found
   - the axes are sane (up is about the world up of the model, forward points from tail to hands)
   - Apply with Rest changes nothing
   - Apply does not pile up over two frames when the Animator doesn't rewrite a bone
   - Clear restores the hat
   - Play/Tick: the picker, cooldowns, priorities
4. **Capture.**
   - A capture test (marked like `TitleScreenCapture` so it only runs when asked) builds a ghost with `TitleActor.Create` on a plain ground plane.
   - The camera sits where the in-game player camera sits relative to the scooter (find it in the player prefab or camera rig).
   - For every move, it renders five frames (t = .1, .3, .5, .7, .9) into one contact-sheet PNG per move, in the mirror's `Logs/ghost-emotes/`, plus one sheet from a front 3/4 view.

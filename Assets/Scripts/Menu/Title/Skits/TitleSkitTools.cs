using System.Collections;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// The moves the title screen's skits are made of. Everything is in the stage's space (x across the screen, y up, the
/// lane at z = 0) and every tween is linked to what it moves, so parking a ghost or halting the show kills it.
/// A ghost's root follows its path; its model (TitleActor.Model) tilts: +z of the model is the scooter's nose
/// </summary>
public static class TitleSkitTools
{
    /// <summary>Riding towards the screen's right, or its left</summary>
    public const int RIGHT = 1, LEFT = -1;

    public static readonly Color WHITE = new Color(1f, 1f, 1f, 1f);
    public static readonly Color GREEN = new Color(0.55f, 1f, 0.45f, 1f);   // the logo's green
    public static readonly Color GOLD = new Color(1f, 0.85f, 0.25f, 1f);
    public static readonly Color PINK = new Color(1f, 0.55f, 0.85f, 1f);

    /// <summary>How much quicker than the seconds a skit asks for a ride is. Raise it and every ghost drives faster</summary>
    public const float RIDE_PACE = 1.25f;
    /// <summary>How much longer than asked a reaction lasts: the pops, hops, squashes and the beats a pose is held for</summary>
    public const float EMOTE_PACE = 1.5f;
    /// <summary>The same for a package's flight from one scooter to another</summary>
    public const float STEAL_PACE = 1.3f;

    /// <summary>How long a ride asked to take this long really takes: for what has to keep time with one</summary>
    public static float Ridden(float seconds)
    {
        return seconds / RIDE_PACE;
    }

    public static Quaternion Facing(int direction)
    {
        return Quaternion.Euler(0f, direction >= 0 ? 90f : -90f, 0f);
    }

    /// <summary>Puts a ghost on the stage, facing along the lane, in its driving pose, already leaning into its speed</summary>
    public static void Place(TitleActor actor, float x, float z, int direction, float speed01 = 1f)
    {
        actor.transform.localPosition = new Vector3(x, 0f, z);
        actor.transform.localRotation = Facing(direction);
        actor.Settle(speed01);
    }

    /// <summary>Rides along the lane to x</summary>
    public static Tween Ride(TitleActor actor, float toX, float seconds, Ease ease = Ease.Linear)
    {
        return Move(actor, toX, Ridden(seconds), ease);
    }

    /// <summary>Along the lane to x in exactly this long, whatever the pace: for a skit that times a ride itself</summary>
    public static Tween Move(TitleActor actor, float toX, float seconds, Ease ease = Ease.Linear)
    {
        return actor.transform.DOLocalMoveX(toX, seconds).SetEase(ease).SetLink(actor.gameObject);
    }

    /// <summary>Rides to a point on the stage's floor</summary>
    public static Tween RideTo(TitleActor actor, float toX, float toZ, float seconds, Ease ease = Ease.Linear)
    {
        return actor.transform.DOLocalMove(new Vector3(toX, 0f, toZ), Ridden(seconds)).SetEase(ease).SetLink(actor.gameObject);
    }

    /// <summary>
    /// Rides along the lane from where it is at a steady speed (units a second, as seen: not paced) to brakeAt, then
    /// brakes evenly to a stop at stopAt. One move, so the speed never jumps where the cruise hands over to the brake, as
    /// it does between two tweens. CruiseTime says when the braking starts
    /// </summary>
    public static Tween CruiseAndBrake(TitleActor actor, float brakeAt, float stopAt, float speed)
    {
        float from = actor.transform.localPosition.x;
        float cruise = CruiseTime(actor, brakeAt, speed);
        float brake = 2f * Mathf.Abs(stopAt - brakeAt) / speed;
        float onward = Mathf.Sign(brakeAt - from), slowing = Mathf.Sign(stopAt - brakeAt);
        float time = 0f;
        return DOTween.To(() => time, t =>
            {
                time = t;
                Vector3 at = actor.transform.localPosition;
                if (t <= cruise)
                {
                    at.x = from + onward * speed * t;
                }
                else
                {
                    float s = Mathf.Min(t - cruise, brake);
                    at.x = brakeAt + slowing * (speed * s - speed * s * s / (2f * brake));
                }
                actor.transform.localPosition = at;
            }, cruise + brake, cruise + brake)
            .SetEase(Ease.Linear).SetTarget(actor.transform).SetLink(actor.gameObject);
    }

    /// <summary>Seconds from where it is to brakeAt at this speed: when CruiseAndBrake starts braking</summary>
    public static float CruiseTime(TitleActor actor, float brakeAt, float speed)
    {
        return Mathf.Abs(brakeAt - actor.transform.localPosition.x) / speed;
    }

    /// <summary>
    /// Rides off along the lane to x from a standstill: pulls away and gathers speed, rather than jumping straight to it.
    /// Takes as long as a ride asked to take this long
    /// </summary>
    public static Tween PullAway(TitleActor actor, float toX, float seconds)
    {
        return Ride(actor, toX, seconds, Ease.InSine);
    }

    /// <summary>Turns to face the other way along the lane</summary>
    public static Tween Turn(TitleActor actor, int direction, float seconds)
    {
        return actor.transform.DOLocalRotateQuaternion(Facing(direction), seconds).SetEase(Ease.InOutSine).SetLink(actor.gameObject);
    }

    /// <summary>Spins on the spot: a donut</summary>
    public static Tween Donut(TitleActor actor, float turns, float seconds, Ease ease = Ease.InOutSine)
    {
        return actor.transform.DOLocalRotate(new Vector3(0f, 360f * turns, 0f), seconds, RotateMode.LocalAxisAdd)
            .SetEase(ease).SetLink(actor.gameObject);
    }

    /// <summary>
    /// A tween of 0 to 1 over this long that puts f(share, the value it started from) on one of the tilt's channels
    /// (TitleActor.Pitch or Roll). It takes the channel over from whatever was moving it, from where that left it
    /// </summary>
    static Tween Channel(TitleActor actor, bool pitch, float seconds, System.Func<float, float, float> f)
    {
        Tween old = pitch ? actor.PitchTween : actor.RollTween;
        if (old != null && old.IsActive())
            old.Kill();
        float from = pitch ? actor.Pitch : actor.Roll;
        float share = 0f;
        Tween tween = DOTween.To(() => share, v =>
            {
                share = v;
                if (pitch)
                    actor.Pitch = f(v, from);
                else
                    actor.Roll = f(v, from);
            }, 1f, Mathf.Max(0.01f, seconds))
            .SetEase(Ease.Linear).SetTarget(actor.Model).SetLink(actor.gameObject);
        if (pitch)
            actor.PitchTween = tween;
        else
            actor.RollTween = tween;
        return tween;
    }

    /// <summary>0 to 1 with no jump in speed at either end (clamped)</summary>
    static float Smooth(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }

    /// <summary>
    /// Lifts the nose and sets it down again: up briskly, held a moment, down softly, and a small bump as the wheel lands
    /// </summary>
    public static Tween Wheelie(TitleActor actor, float degrees, float seconds)
    {
        return Channel(actor, true, seconds, (u, from) =>
            {
                float up = Smooth(u / 0.35f);
                float down = Smooth((u - 0.55f) / 0.45f);
                return Mathf.Lerp(from, degrees, up) * (1f - down);
            })
            .OnComplete(() => actor.Squash(0.08f, 0.35f));
    }

    /// <summary>Tips the nose up (or down, negative) and keeps it there</summary>
    public static Tween Pitch(TitleActor actor, float degrees, float seconds)
    {
        return Channel(actor, true, seconds, (u, from) => Mathf.Lerp(from, degrees, Smooth(u)));
    }

    /// <summary>
    /// Rocks from side to side, level again at the end. It rocks in from wherever it leans and dies away at the end, so
    /// it never jerks over to one side to start or snaps level to finish
    /// </summary>
    public static Tween Wobble(TitleActor actor, float degrees, float period, int rocks)
    {
        rocks = Mathf.Max(1, rocks);
        float edge = Mathf.Min(0.3f, 1f / rocks);
        return Channel(actor, false, period * rocks, (u, from) =>
            {
                float swing = degrees * Mathf.Sin(Mathf.PI * rocks * u) * Smooth(u / edge) * Smooth((1f - u) / edge);
                return from * (1f - Smooth(u / edge)) + swing;
            });
    }

    /// <summary>Leans over (+ to its left) and stays leaning; 0 is level again</summary>
    public static Tween Lean(TitleActor actor, float degrees, float seconds)
    {
        return Channel(actor, false, seconds, (u, from) => Mathf.Lerp(from, degrees, Smooth(u)));
    }

    /// <summary>A little jump on the spot: up, and down onto its wheels with a small squash, wherever it started from</summary>
    public static Tween Hop(TitleActor actor, float height, float seconds)
    {
        if (actor.HopTween != null && actor.HopTween.IsActive())
            actor.HopTween.Kill();
        float half = seconds * EMOTE_PACE * 0.5f;
        Sequence hop = DOTween.Sequence().SetTarget(actor.Model).SetLink(actor.gameObject);
        hop.Append(actor.Model.DOLocalMoveY(height, half).SetEase(Ease.OutQuad));
        hop.Append(actor.Model.DOLocalMoveY(0f, half).SetEase(Ease.InQuad));
        hop.OnComplete(() => actor.Squash(0.12f, 0.35f));
        actor.HopTween = hop;
        return hop;
    }

    /// <summary>Squashes and springs back: a bump, a start, a surprise</summary>
    public static void Squash(TitleActor actor, float amount = 0.25f, float seconds = 0.35f)
    {
        actor.Squash(amount, seconds * EMOTE_PACE);
    }

    /// <summary>Throws a loose package in an arc to a point on the stage</summary>
    public static Tween Toss(Transform package, Vector3 to, float height, float seconds)
    {
        return package.DOLocalJump(to, height, 1, seconds).SetEase(Ease.Linear).SetLink(package.gameObject);
    }

    /// <summary>A word over a ghost's head</summary>
    public static void Say(TitleStage stage, TitleActor actor, string word, Color colour, float seconds = 1.1f)
    {
        stage.Pop(word, actor.OverHead, seconds * EMOTE_PACE, colour);
    }

    /// <summary>Gives a ghost a package to start with</summary>
    public static Transform Load(TitleStage stage, TitleActor actor, int size = 1, bool golden = false)
    {
        Transform package = stage.TakePackage(size, golden);
        actor.Carry(package);
        return package;
    }

    /// <summary>
    /// The steal: the thief grabs, and the victim's package hops across onto the thief's scooter. Both keep riding
    /// meanwhile. Does nothing when the victim has none
    /// </summary>
    public static IEnumerator Steal(TitleActor thief, TitleActor victim, float seconds = 0.28f)
    {
        Transform package = victim.Drop();
        if (package == null)
            yield break;

        seconds *= STEAL_PACE;
        thief.Grab();
        Squash(victim, 0.18f, 0.3f);

        // It flies to where the thief's rack will be: followed each frame, since the thief is moving. It turns over
        // twice on the way and comes down square on the rack, so it doesn't snap straight as it lands
        Vector3 from = package.position;
        Quaternion turned = package.rotation;
        float height = 1.3f;
        for (float t = Time.deltaTime; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            k = Mathf.Lerp(k, k * k * (3f - 2f * k), 0.5f);   // a little softer off the victim and onto the rack
            Vector3 at = Vector3.Lerp(from, thief.RackPoint(package), k);
            at.y += Mathf.Sin(k * Mathf.PI) * height;
            package.position = at;
            package.rotation = Quaternion.Slerp(turned, thief.Model.rotation, k) * Quaternion.Euler(0f, 0f, 720f * k);
            yield return null;
        }
        thief.Carry(package);
        Squash(thief, 0.15f, 0.3f);
    }

    public static WaitForSeconds Wait(float seconds)
    {
        return new WaitForSeconds(seconds);
    }

    /// <summary>A wait that keeps time with a ride: as much shorter as rides are</summary>
    public static WaitForSeconds WaitRide(float seconds)
    {
        return new WaitForSeconds(Ridden(seconds));
    }

    /// <summary>A held beat (a pose, a look, a penny dropping): as much longer as reactions are</summary>
    public static WaitForSeconds Beat(float seconds)
    {
        return new WaitForSeconds(seconds * EMOTE_PACE);
    }
}

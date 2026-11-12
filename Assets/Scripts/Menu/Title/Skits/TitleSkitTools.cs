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

    /// <summary>Puts a ghost on the stage, facing along the lane, in its driving pose</summary>
    public static void Place(TitleActor actor, float x, float z, int direction, float speed01 = 1f)
    {
        actor.transform.localPosition = new Vector3(x, 0f, z);
        actor.transform.localRotation = Facing(direction);
        actor.Drive(speed01);
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

    /// <summary>Lifts the nose and sets it down again</summary>
    public static Tween Wheelie(TitleActor actor, float degrees, float seconds)
    {
        return actor.Model.DOLocalRotate(new Vector3(-degrees, 0f, 0f), seconds * 0.5f)
            .SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo).SetLink(actor.gameObject);
    }

    /// <summary>Tips the nose up (or down, negative) and keeps it there</summary>
    public static Tween Pitch(TitleActor actor, float degrees, float seconds)
    {
        return actor.Model.DOLocalRotate(new Vector3(-degrees, 0f, 0f), seconds).SetEase(Ease.OutQuad).SetLink(actor.gameObject);
    }

    /// <summary>Rocks from side to side, back to level at the end</summary>
    public static Tween Wobble(TitleActor actor, float degrees, float period, int rocks)
    {
        actor.Model.localRotation = Quaternion.Euler(0f, 0f, -degrees);
        return actor.Model.DOLocalRotate(new Vector3(0f, 0f, degrees), period)
            .SetEase(Ease.InOutSine).SetLoops(rocks, LoopType.Yoyo).SetLink(actor.gameObject)
            .OnComplete(() => actor.Model.localRotation = Quaternion.identity);
    }

    /// <summary>A little jump on the spot</summary>
    public static Tween Hop(TitleActor actor, float height, float seconds)
    {
        return actor.Model.DOLocalMoveY(actor.Model.localPosition.y + height, seconds * EMOTE_PACE * 0.5f)
            .SetEase(Ease.OutQuad).SetLoops(2, LoopType.Yoyo).SetLink(actor.gameObject);
    }

    /// <summary>Squashes and springs back: a bump, a start, a surprise</summary>
    public static Tween Squash(TitleActor actor, float amount = 0.25f, float seconds = 0.35f)
    {
        return actor.Model.DOPunchScale(new Vector3(amount, -amount, amount), seconds * EMOTE_PACE, 6, 0.6f).SetLink(actor.gameObject);
    }

    /// <summary>Flies across the stage in an arc (the ghost's root)</summary>
    public static Tween Leap(TitleActor actor, float toX, float toZ, float height, float seconds)
    {
        return actor.transform.DOLocalJump(new Vector3(toX, 0f, toZ), height, 1, seconds)
            .SetEase(Ease.Linear).SetLink(actor.gameObject);
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

        // It flies to where the thief's rack will be: followed each frame, since the thief is moving
        Vector3 from = package.position;
        float height = 1.3f;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            Vector3 at = Vector3.Lerp(from, thief.RackPoint(package), k);
            at.y += Mathf.Sin(k * Mathf.PI) * height;
            package.position = at;
            package.Rotate(0f, 0f, 720f * Time.deltaTime, Space.Self);
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

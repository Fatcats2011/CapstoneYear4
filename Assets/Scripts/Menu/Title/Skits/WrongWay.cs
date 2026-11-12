using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A convoy of three rides right, the first with a package; one ghost rides left straight at it on the same lane. The
/// convoy swerves around it one by one and the package is flung off in the panic. The lone ghost brakes, scoops it up,
/// cheers and rides on its way; the convoy carries on the other way, none the wiser
/// </summary>
public class WrongWay : TitleSkit
{
    public override string Name => "Wrong Way";
    public override int Cast => 4;

    const float SPEED = 4.5f;
    const float SPACING = 2.6f;
    /// <summary>Seconds a swerve takes out, and again back</summary>
    const float SWERVE = 0.7f;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor lead = cast[0], loner = cast[3];
        float off = stage.OffScreenX;

        // The convoy sets off nose to tail along the lane, the lead with the package
        for (int i = 0; i < 3; i++)
        {
            Place(cast[i], -off - i * SPACING, 0f, RIGHT, 0.7f);
            Ride(cast[i], off, (2f * off + i * SPACING) / SPEED);
        }
        Load(stage, lead);

        // The lone ghost heads straight for them at the same speed, so they meet in the middle
        Place(loner, off, 0f, LEFT, 0.7f);
        Tween approach = Ride(loner, 1.6f, (off - 1.6f) / SPEED);

        // Each convoy ghost swerves round it just before reaching it: near the camera, far, near
        float meet = Ridden(off / SPEED);
        float swerveAt = meet - SWERVE - 0.2f;
        float[] sides = { -1.3f, 1.4f, -1.2f };
        for (int i = 0; i < 3; i++)
            Swerve(cast[i], sides[i], sides[i] < 0f ? 12f : -12f, swerveAt + Ridden(i * SPACING / SPEED));

        // The lone ghost sees them coming
        yield return Wait(swerveAt - 0.2f);
        Say(stage, loner, "!", WHITE, 0.8f);
        Squash(loner, 0.2f, 0.4f);

        // It brakes as the lead squeezes past, and the lead's package goes flying
        yield return approach.WaitForCompletion();
        loner.Drive(0.3f);
        Ride(loner, -0.9f, 0.9f, Ease.OutSine);
        yield return WaitRide(0.3f);

        Transform parcel = lead.Drop();
        Say(stage, lead, "!", PINK, 0.9f);
        Squash(lead, 0.2f, 0.4f);
        yield return Toss(parcel, new Vector3(-1.5f, 0.3f, 0f), 1.6f, 0.6f).WaitForCompletion();

        // The lone ghost, stopped dead with a package at its feet, can't quite believe it
        loner.Drive(0f);
        yield return Beat(0.3f);
        Say(stage, loner, "?", WHITE, 0.7f);
        yield return Beat(0.5f);

        yield return Scoop(loner, parcel, 0.5f);
        loner.Cheer();
        Hop(loner, 0.6f, 0.4f);
        yield return Beat(0.9f);

        // Off the way it was going, nose up; the convoy has long gone the other way
        loner.Drive(1f);
        Wheelie(loner, 25f, 1.0f);
        yield return Ride(loner, -off, 1.7f, Ease.InQuad).WaitForCompletion();
    }

    /// <summary>A swerve to one side in depth and back, leaning into it. The sequence is linked, so its parts are too</summary>
    static Tween Swerve(TitleActor actor, float z, float lean, float delay)
    {
        Sequence swerve = DOTween.Sequence().SetTarget(actor.transform).SetLink(actor.gameObject);
        swerve.AppendInterval(delay);
        swerve.Append(actor.transform.DOLocalMoveZ(z, SWERVE).SetEase(Ease.InOutSine).SetLoops(2, LoopType.Yoyo));
        swerve.Join(actor.Model.DOLocalRotate(new Vector3(0f, 0f, lean), SWERVE).SetEase(Ease.InOutSine)
            .SetLoops(2, LoopType.Yoyo));
        return swerve;
    }

    /// <summary>Picks a loose package up onto the ghost's rack, in a hop (Steal's flight, without a victim)</summary>
    static IEnumerator Scoop(TitleActor actor, Transform parcel, float seconds)
    {
        actor.Grab();
        Vector3 from = parcel.position;
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            Vector3 at = Vector3.Lerp(from, actor.RackPoint(parcel), k);
            at.y += Mathf.Sin(k * Mathf.PI) * 0.8f;
            parcel.position = at;
            yield return null;
        }
        actor.Carry(parcel);
        Squash(actor, 0.15f, 0.3f);
    }
}

using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost with a package roars in from the left and flies in a high arc across the screen. At the top it lets the
/// package go; it falls to a second ghost riding along the floor, which catches it and cheers its way out. The flyer
/// lands on the other side, looks back for its package, and rides off puzzled
/// </summary>
public class AirMail : TitleSkit
{
    public override string Name => "Air Mail";
    public override int Cast => 2;

    const float RUN_UP = 0.75f;      // the ride to the take-off
    const float FLIGHT = 1.8f;       // in the air
    const float TAKE_OFF = -6f, LAND = 5.5f;
    const float TOP = 3.5f;          // the arc's height (the screen's top is near 5.5, and a ghost stands 2.5 tall)
    const float FALL = 1.0f;         // the package's drop
    const float CATCH_X = -1f;       // where the catcher is as it lands

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor flyer = cast[0], catcher = cast[1];
        float off = stage.OffScreenX;

        // The flyer comes in fast, flat out, towards a ramp that isn't there
        Place(flyer, -off, 0f, RIGHT);
        Load(stage, flyer);
        Ride(flyer, TAKE_OFF, RUN_UP);
        yield return WaitRide(RUN_UP);

        // Up and over, nose high
        Leap(flyer, LAND, 0f, TOP, FLIGHT);
        Pitch(flyer, 28f, 0.5f);
        yield return Wait(0.25f);

        // Down on the floor, the catcher rides in from the right, timed to be at CATCH_X as the package lands
        float catcherZ = -0.5f;
        float catcherOff = stage.OffX(catcherZ);
        float ride = FLIGHT * 0.5f + FALL - 0.25f;
        float speed = (catcherOff - CATCH_X) / ride;
        Place(catcher, catcherOff, catcherZ, LEFT, 0.6f);
        // Where its rack is, against its feet: a falling package aims there
        Vector3 rack = stage.transform.InverseTransformPoint(catcher.RackPoint(flyer.Package)) - catcher.transform.localPosition;
        float outX = catcherOff + 0.5f;
        Tween catcherRide = Move(catcher, -outX, (catcherOff + outX) / speed);
        yield return Wait(0.65f);

        // The top of the arc: the flyer lets go and starts to come down; the package tumbles after it
        Pitch(flyer, -25f, 0.6f);
        Transform parcel = flyer.Drop();
        Tween fall = null;
        if (parcel != null)
        {
            fall = Toss(parcel, new Vector3(CATCH_X, 0f, catcherZ) + rack, 0.4f, FALL);
            parcel.DOLocalRotate(new Vector3(0f, 0f, 540f), FALL, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLink(parcel.gameObject);
        }
        yield return Wait(FLIGHT * 0.5f);

        // The flyer lands, and looks back to where its package was
        Pitch(flyer, 0f, 0.2f);
        Squash(flyer, 0.3f, 0.4f);
        flyer.Drive(0f);
        Turn(flyer, LEFT, 0.35f);

        // Right then the package arrives on the catcher's rack
        if (fall != null)
            yield return fall.WaitForCompletion();
        if (parcel != null)
            catcher.Carry(parcel);
        Squash(catcher, 0.3f, 0.4f);
        catcher.Cheer();
        Say(stage, catcher, "!", GREEN);

        // The catcher is away; the flyer is left wondering
        yield return Beat(0.4f);
        catcher.Drive(1f);
        Say(stage, flyer, "?", WHITE, 0.9f);
        yield return Beat(0.7f);

        // Shrugs, turns round and goes off the other way
        Turn(flyer, RIGHT, 0.3f);
        yield return Wait(0.3f);
        flyer.Drive(1f);
        Tween leave = Ride(flyer, off + 0.5f, 1.4f, Ease.InQuad);

        yield return catcherRide.WaitForCompletion();
        yield return leave.WaitForCompletion();
    }
}

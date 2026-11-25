using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost with a package roars in from the left and flies in a high arc across the screen, arms up. At the top it lets
/// the package go; it falls to a second ghost riding along the floor, which catches it and punches the air on its way
/// out. The flyer lands on the other side, skids round, looks back for its package, shrugs, and rides off
/// </summary>
public class AirMail : TitleSkit
{
    public override string Name => "Air Mail";
    public override int Cast => 2;

    const float FLIGHT = 1.8f;       // in the air
    const float TAKE_OFF = -6f, LAND = 4.6f;
    const float SKID = 0.45f;        // the slide on from the landing, slowing to a stop
    const float TOP = 3.5f;          // the arc's height (the screen's top is near 5.5, and a ghost stands 2.5 tall)
    const float FALL = 1.0f;         // the package's drop
    const float CATCH_X = -1f;       // where the catcher is as it lands

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor flyer = cast[0], catcher = cast[1];
        float off = stage.OffScreenX;

        // The flyer comes in fast, flat out, towards a ramp that isn't there. Along the lane it is one move: the run-up
        // and the flight at one speed, then a skid to a stop after it lands, so it never lurches at the take-off or
        // stops dead on landing. The arc is a move of its own, up and down, from the take-off
        float flying = (LAND - TAKE_OFF) / FLIGHT;
        Place(flyer, -off, 0f, RIGHT);
        Load(stage, flyer);
        float runUp = (TAKE_OFF + off) / flying;
        CruiseAndBrake(flyer, LAND, LAND + flying * SKID * 0.5f, flying);
        Sequence leap = DOTween.Sequence().SetTarget(flyer.transform).SetLink(flyer.gameObject);
        leap.AppendInterval(runUp);
        leap.Append(flyer.transform.DOLocalMoveY(TOP, FLIGHT * 0.5f).SetEase(Ease.OutQuad));
        leap.Append(flyer.transform.DOLocalMoveY(0f, FLIGHT * 0.5f).SetEase(Ease.InQuad));
        yield return Wait(runUp);

        // Up and over, nose high, arms up
        Pitch(flyer, 28f, 0.5f);
        flyer.Emote(GhostAction.Wheee, "Arms Up");
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
            // Tumbling twice over on the way down, to land square on the catcher's rack
            fall = Toss(parcel, new Vector3(CATCH_X, 0f, catcherZ) + rack, 0.4f, FALL);
            Quaternion dropped = parcel.rotation;
            float share = 0f;
            DOTween.To(() => share, k =>
                {
                    share = k;
                    parcel.rotation = Quaternion.Slerp(dropped, catcher.Model.rotation, k) * Quaternion.Euler(0f, 0f, 720f * k);
                }, 1f, FALL).SetEase(Ease.Linear).SetTarget(parcel).SetLink(parcel.gameObject);
        }
        yield return leap.WaitForCompletion();

        // The flyer lands and skids on, turning round to look back to where its package was
        Pitch(flyer, 0f, 0.2f);
        Squash(flyer, 0.3f, 0.4f);
        flyer.EndEmote(0.25f);
        flyer.Drive(0f);
        Turn(flyer, LEFT, 0.4f);

        // Right then the package arrives on the catcher's rack
        if (fall != null)
            yield return fall.WaitForCompletion();
        if (parcel != null)
            catcher.Carry(parcel);
        Squash(catcher, 0.3f, 0.4f);
        catcher.Emote(GhostAction.DeliveryFlourish, "High Fist");
        Say(stage, catcher, "!", GREEN);

        // The catcher is away; the flyer is left wondering, and shrugs
        yield return Beat(0.4f);
        Say(stage, flyer, "?", WHITE, 0.9f);
        flyer.Emote(GhostAction.Bored, "Shrug");
        yield return Beat(0.9f);

        // Turns round and goes off the other way
        flyer.EndEmote(0.3f);
        Turn(flyer, RIGHT, 0.35f);
        yield return Wait(0.3f);
        flyer.Drive(1f);
        Tween leave = Ride(flyer, off + 0.5f, 1.4f, Ease.InQuad);

        yield return catcherRide.WaitForCompletion();
        yield return leave.WaitForCompletion();
    }
}

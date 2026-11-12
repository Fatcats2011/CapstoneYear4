using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// One ghost cruises across with a package; a second roars up from behind, grabs it and speeds off; the first gives chase
/// </summary>
public class TheSnatch : TitleSkit
{
    public override string Name => "The Snatch";
    public override int Cast => 2;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor courier = cast[0], thief = cast[1];
        float off = stage.OffScreenX;

        // The courier ambles on, minding its own business
        Place(courier, -off, 0f, RIGHT, 0.4f);
        Load(stage, courier);
        Ride(courier, 1.5f, 3.4f, Ease.OutSine);
        yield return WaitRide(1.6f);

        // The thief comes flat out, nose up, and is alongside as the courier reaches the middle
        Place(thief, -off, -0.9f, RIGHT);
        Wheelie(thief, 22f, 1.2f);
        Tween chase = Ride(thief, 0.6f, 1.7f, Ease.OutQuad);
        yield return chase.WaitForCompletion();

        yield return Steal(thief, courier);
        Say(stage, courier, "!", WHITE);
        Ride(thief, off, 1.3f, Ease.InQuad);
        Wheelie(thief, 30f, 1.0f);

        // A beat of disbelief, then after it
        yield return Beat(0.7f);
        Hop(courier, 0.5f, 0.3f);
        courier.Drive(1f);
        yield return Ride(courier, off, 1.5f, Ease.InQuad).WaitForCompletion();
    }
}

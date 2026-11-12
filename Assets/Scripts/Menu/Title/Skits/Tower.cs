using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost wobbles slowly across with five packages stacked up behind it. A small quick ghost zips past and whips the
/// bottom one out; the other four come down all over the floor. The carrier stops, slumps, and trudges off
/// leaving them
/// </summary>
public class Tower : TitleSkit
{
    public override string Name => "Tower";
    public override int Cast => 2;

    /// <summary>How far above the one below each package in the stack sits (in the model's space)</summary>
    const float STEP = 0.45f;
    const float ZIP_Z = -0.9f;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor carrier = cast[0], zipper = cast[1];
        float off = stage.OffScreenX;
        float zipOff = stage.OffX(ZIP_Z);

        // Five small packages on the carrier's back: the bottom one the carrier's own, the rest piled on it
        Place(carrier, -off, 0f, RIGHT, 0.4f);
        Transform below = Load(stage, carrier, 0);
        Transform[] stack = new Transform[4];
        for (int i = 0; i < stack.Length; i++)
        {
            Transform next = stage.TakePackage(0);
            next.SetParent(carrier.Model, false);
            next.localPosition = below.localPosition + Vector3.up * STEP;
            next.localRotation = Quaternion.identity;
            stack[i] = next;
            below = next;
        }

        // It ambles on, rocking, the stack swaying a long way above it
        Tween amble = Ride(carrier, 0.8f, 4.4f, Ease.OutSine);
        Wobble(carrier, 6f, 0.6f, 6);

        // The zipper comes from behind, flat out, in the near lane, timed to pass the carrier mid-screen
        Place(zipper, -zipOff, ZIP_Z, RIGHT, 1f);
        Ride(zipper, zipOff, 2.8f).SetDelay(Ridden(1.5f));
        yield return WaitRide(2.68f);

        // Whipped out from under the stack
        Say(stage, carrier, "!", WHITE, 1.0f);
        yield return Steal(zipper, carrier, 0.3f);
        Wheelie(zipper, 30f, 1.0f);

        // And down it all comes, bottom first, a little after each other
        float x = carrier.transform.localPosition.x;
        Vector3[] landings =
        {
            new Vector3(x - 2.6f, 0.3f, -0.5f), new Vector3(x - 1.4f, 0.3f, 0.8f),
            new Vector3(x + 1.6f, 0.3f, -0.7f), new Vector3(x + 3.0f, 0.3f, 0.6f),
        };
        for (int i = 0; i < stack.Length; i++)
        {
            stack[i].SetParent(stage.transform, true);
            Toss(stack[i], landings[i], 1.0f + 0.3f * i, 0.6f + 0.05f * i);
            yield return Wait(0.12f);
        }

        // The carrier coasts to a stop, takes it in, and slumps
        carrier.Drive(0f);
        carrier.Slump();
        Squash(carrier, 0.2f, 0.4f);
        yield return amble.WaitForCompletion();
        yield return Beat(0.8f);

        // Then trudges off, leaving the mess
        carrier.Drive(0.3f);
        yield return Ride(carrier, off, 3.0f).WaitForCompletion();
    }
}

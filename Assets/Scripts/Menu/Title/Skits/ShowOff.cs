using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost with a package pulls up centre stage, spins a donut and dances for itself. A second creeps in behind, rubs
/// its hands, lifts the package off its back while it basks, and zooms away. The show-off notices late, shakes its
/// fists, and gives chase
/// </summary>
public class ShowOff : TitleSkit
{
    public override string Name => "Show-off";
    public override int Cast => 2;

    /// <summary>The sneak keeps to the far lane, behind the show-off</summary>
    const float SNEAK_Z = 1.0f;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor star = cast[0], sneak = cast[1];
        float off = stage.OffScreenX;
        float sneakOff = stage.OffX(SNEAK_Z);

        // The show-off glides to centre stage with its package
        Place(star, -off, 0f, RIGHT, 0.6f);
        Load(stage, star);
        Tween arrive = Ride(star, 0f, 2.0f, Ease.OutSine);

        // Meanwhile the sneak sets off a moment later, and slows to a creep as it nears
        Place(sneak, -sneakOff, SNEAK_Z, RIGHT, 0.4f);
        Tween creep = Ride(sneak, -1.5f, 4.6f, Ease.OutQuad).SetDelay(0.3f);

        yield return arrive.WaitForCompletion();
        yield return Wait(0.2f);

        // The big moment: two turns of donut
        star.Drive(0.8f);
        yield return Donut(star, 2f, 1.6f).WaitForCompletion();

        // A little dance, with the package on show, back turned to the sneak
        star.Drive(0f);
        star.Emote(GhostAction.Victory, "Arm Pump Dance");
        yield return Beat(0.6f);

        // The sneak arrives, stops, and rubs its hands over its prize
        yield return creep.WaitForCompletion();
        sneak.Drive(0f);
        sneak.Emote(GhostAction.Scheme, "Hand Rub");
        yield return Beat(0.8f);
        sneak.EndEmote(0.2f);
        yield return Wait(0.2f);

        // The lift, and a getaway with its nose in the air
        yield return Steal(sneak, star, 0.3f);
        sneak.Drive(1f);
        Wheelie(sneak, 28f, 0.9f);
        Ride(sneak, sneakOff, 1.7f, Ease.InQuad);

        // The show-off is still basking... then the penny drops
        yield return Beat(0.4f);
        Say(stage, star, "!", WHITE, 0.9f);
        Squash(star, 0.3f, 0.4f);
        star.Emote(GhostAction.FistShake, "Arm Flail");
        yield return Beat(0.8f);

        // After it
        star.Drive(1f);
        yield return Ride(star, off, 1.4f, Ease.InQuad).WaitForCompletion();
    }
}

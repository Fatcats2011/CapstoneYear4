using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost steals the other's hat instead of a package and rides off wearing it; the bare-headed one chases. The one
/// skit where a wardrobe change is seen. Both are dressed again off screen before it ends
/// </summary>
public class HatThief : TitleSkit
{
    public override string Name => "Hat Thief";
    public override int Cast => 2;

    /// <summary>The victim (the first of the cast) has a hat worth stealing</summary>
    public override bool NeedsHat(int castIndex) { return castIndex == 0; }

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor victim = cast[0], thief = cast[1];
        PlayerHatInformationSO victimsHat = victim.Hat, thiefsHat = thief.Hat;
        PlayerHatInformationSO bare = stage.BareHead;
        float off = stage.OffScreenX;

        // The victim rides on from the right with a package, looking pleased with its hat
        Place(victim, off, 0f, LEFT, 0.5f);
        Load(stage, victim);
        yield return Ride(victim, 1.2f, 2.2f, Ease.OutSine).WaitForCompletion();
        victim.Drive(0f);
        victim.Cheer();

        // The thief pulls up behind, eyes the package... and takes the hat
        Place(thief, off, -0.8f, LEFT);
        yield return Ride(thief, 2.9f, 1.3f, Ease.OutQuad).WaitForCompletion();
        thief.Drive(0f);
        Say(stage, thief, "?", WHITE, 0.7f);
        yield return Beat(0.7f);

        thief.Grab();
        yield return Wait(0.15f);
        if (bare != null)
            victim.SetHat(bare);
        thief.SetHat(victimsHat);
        Squash(thief, 0.2f, 0.35f);
        Squash(victim, 0.25f, 0.35f);
        Say(stage, victim, "!", PINK);
        victim.Drive(0f);
        yield return Beat(0.5f);

        // Off it goes, past the victim and away
        thief.Drive(1f);
        Wheelie(thief, 25f, 0.9f);
        Ride(thief, -off, 1.7f, Ease.InQuad);
        yield return Beat(0.6f);

        Hop(victim, 0.6f, 0.3f);
        victim.Drive(1f);
        yield return Ride(victim, -off, 1.5f, Ease.InQuad).WaitForCompletion();

        // Out of sight: everyone's own hat back
        victim.SetHat(victimsHat);
        thief.SetHat(thiefsHat);
    }
}

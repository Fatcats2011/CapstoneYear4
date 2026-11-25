using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost steals the other's hat (just as it shows it off) instead of a package and rides off wearing it, laughing;
/// the bare-headed one shakes its fists and chases. The one skit where a wardrobe change is seen. Both are dressed again
/// off screen before it ends
/// </summary>
public class HatThief : TitleSkit
{
    public override string Name => "Hat Thief";
    public override int Cast => 2;

    /// <summary>The hat's flight from head to head: seconds (before STEAL_PACE) and how high it arcs</summary>
    const float HAT_FLIGHT = 0.45f, HAT_ARC = 1.4f;

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
        victim.Emote(GhostAction.HatShowOff, "Hat Flip");

        // The thief pulls up behind, bare-headed, eyes the package... and the hat
        if (bare != null)
            thief.SetHat(bare);
        Place(thief, off, -0.8f, LEFT);
        yield return Ride(thief, 2.9f, 1.3f, Ease.OutQuad).WaitForCompletion();
        thief.Drive(0f);
        Say(stage, thief, "?", WHITE, 0.7f);
        yield return Beat(0.7f);

        // It grabs, and the hat flies off the victim's head in an arc, spinning, onto the thief's. The thief wears it
        // hidden meanwhile, so the flying copy lands exactly where it will sit
        victim.EndEmote(0.15f);
        thief.Grab();
        yield return Wait(0.15f);
        Transform flying = victim.ThrowHat();
        if (bare != null)
            victim.SetHat(bare);
        thief.SetHat(victimsHat);
        Squash(victim, 0.25f, 0.35f);
        Say(stage, victim, "!", PINK);
        if (flying != null)
        {
            thief.ShowHat(false);
            yield return Fly(flying, thief, HAT_FLIGHT * STEAL_PACE);
            thief.ShowHat(true);
            victim.CatchHat();
        }
        Squash(thief, 0.2f, 0.35f);
        yield return Beat(0.35f);

        // Off it goes, past the victim and away, laughing
        thief.Drive(1f);
        Wheelie(thief, 25f, 0.9f);
        thief.Emote(GhostAction.Cackle, "Shoulder Shake");
        Ride(thief, -off, 1.7f, Ease.InQuad);
        yield return Beat(0.2f);
        victim.Emote(GhostAction.FistShake, "Arm Flail");
        yield return Beat(0.5f);

        victim.Drive(1f);
        yield return Ride(victim, -off, 1.5f, Ease.InQuad).WaitForCompletion();

        // Out of sight: everyone's own hat back
        victim.SetHat(victimsHat);
        thief.SetHat(thiefsHat);
    }

    /// <summary>
    /// The hat's flight onto the thief's head: an arc, one turn about its crown, easing off the victim and settling onto
    /// the thief, aimed each frame at where the thief's hat sits
    /// </summary>
    static IEnumerator Fly(Transform hat, TitleActor thief, float seconds)
    {
        Transform onto = thief.HatModel;
        Vector3 from = hat.position, size = hat.localScale;
        Quaternion turned = hat.rotation;
        for (float t = Time.deltaTime; t < seconds; t += Time.deltaTime)
        {
            float k = t / seconds;
            k = Mathf.Lerp(k, k * k * (3f - 2f * k), 0.5f);
            Vector3 at = Vector3.Lerp(from, onto.position, k);
            at.y += Mathf.Sin(k * Mathf.PI) * HAT_ARC;
            hat.SetPositionAndRotation(at, Quaternion.Slerp(turned, onto.rotation, k) * Quaternion.Euler(0f, 360f * k, 0f));
            hat.localScale = Vector3.Lerp(size, thief.InStage(onto.lossyScale), k);
            yield return null;
        }
    }
}

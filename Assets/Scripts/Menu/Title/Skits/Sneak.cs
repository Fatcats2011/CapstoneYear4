using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost rides in slowly with a package, stops and naps (the in-game Nap, Zzz and all). A second tiptoes in behind it in
/// little stop-start steps, lifts the package, tiptoes two steps away rubbing its hands, and then the sleeper wakes with
/// a leap: the thief boosts off with a wheelie and the sleeper gives chase
/// </summary>
public class Sneak : TitleSkit
{
    public override string Name => "Sneak";
    public override int Cast => 2;

    const float ARRIVE = 2.4f;     // the sleeper's ride in
    const float SLEEP_X = -1f;     // where it stops, left of centre
    const float THIEF_Z = -0.6f;   // a little nearer the camera, so the thief isn't hidden behind it
    const int STEPS = 5;           // the thief's tiptoes in

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor sleeper = cast[0], thief = cast[1];
        float off = stage.OffScreenX;
        float thiefOff = stage.OffX(THIEF_Z);

        // The sleeper cruises in from the right, slowing, and nods off
        Place(sleeper, off, 0f, LEFT, 0.4f);
        Load(stage, sleeper);
        Ride(sleeper, SLEEP_X, ARRIVE, Ease.OutSine);
        yield return WaitRide(1.2f);

        // The thief creeps in behind it. Each step is a quick shuffle and a long freeze; the sleeper drops off meanwhile
        float startX = thiefOff - 1.5f;
        float meetX = SLEEP_X + 2.4f;
        float step = (startX - meetX) / STEPS;
        Place(thief, startX, THIEF_Z, LEFT, 0.1f);

        float clock = Ridden(1.2f);
        bool asleep = false, awake = false;
        for (int i = 0; i < STEPS; i++)
        {
            if (!asleep && clock >= Ridden(ARRIVE) - 0.05f)
            {
                asleep = true;
                sleeper.Drive(0f);
                stage.StartCoroutine(Nap(sleeper, () => awake));
            }

            // A shuffle that eases off and stops, not a jump to speed
            Ride(thief, startX - step * (i + 1), 0.35f, Ease.InOutSine);
            yield return Wait(0.6f);
            clock += 0.6f;
        }

        // Right behind the sleeper: a long look, then the package is lifted off its back
        yield return Beat(0.4f);
        yield return Steal(thief, sleeper);

        // Doesn't run: turns round, and tiptoes two steps away rubbing its hands, very pleased with itself
        thief.Drive(0f);
        Turn(thief, RIGHT, 0.3f);
        thief.Emote(GhostAction.Scheme, "Sly Rub");
        yield return Wait(0.4f);
        for (int i = 0; i < 2; i++)
        {
            Ride(thief, thief.transform.localPosition.x + 1f, 0.3f, Ease.InOutSine);
            yield return Wait(0.55f);
        }

        // The sleeper wakes at last: "!" and a leap. The thief is gone in a blaze, nose up
        awake = true;
        Say(stage, sleeper, "!", WHITE);
        sleeper.Emote(GhostAction.JumpScare, "Leap");
        thief.EndEmote(0.15f);
        thief.Drive(1f);
        Wheelie(thief, 30f, 1.0f);
        Tween away = Ride(thief, thiefOff + 0.5f, 1.2f, Ease.InQuad);

        // A beat to take it in, then it gives chase
        yield return Beat(0.3f);
        Turn(sleeper, RIGHT, 0.35f);
        yield return Beat(0.3f);
        sleeper.Drive(1f);
        Tween chase = Ride(sleeper, off + 0.5f, 1.6f, Ease.InQuad);

        yield return away.WaitForCompletion();
        yield return chase.WaitForCompletion();
    }

    /// <summary>The in-game Nap, again each time it ends, until the sleeper wakes (or the show stops)</summary>
    static IEnumerator Nap(TitleActor sleeper, System.Func<bool> awake)
    {
        while (!awake())
        {
            if (sleeper.Emotes == null)
                yield break;
            if (sleeper.Emotes.Playing == null)
                sleeper.Emote(GhostAction.Bored, "Nap");
            yield return null;
        }
    }
}

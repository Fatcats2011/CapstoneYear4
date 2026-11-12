using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost rides in slowly with a package, stops and dozes off. A second tiptoes in behind it in little stop-start steps,
/// lifts the package, tiptoes two steps away, and then the sleeper wakes: the thief boosts off with a wheelie and the
/// sleeper gives chase
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
        bool asleep = false;
        Tween doze = null;
        for (int i = 0; i < STEPS; i++)
        {
            if (!asleep && clock >= Ridden(ARRIVE) - 0.05f)
            {
                asleep = true;
                sleeper.Drive(0f);
                doze = Wobble(sleeper, 4f, 0.7f, 8);
            }
            if (asleep)
                Say(stage, sleeper, "Z", WHITE, 1.0f);

            Ride(thief, startX - step * (i + 1), 0.35f, Ease.OutSine);
            yield return Wait(0.6f);
            clock += 0.6f;
        }

        // Right behind the sleeper: a long look, then the package is lifted off its back
        yield return Beat(0.4f);
        yield return Steal(thief, sleeper);

        // Doesn't run: turns round, and tiptoes two steps away, very pleased with itself
        thief.Drive(0f);
        Turn(thief, RIGHT, 0.3f);
        yield return Wait(0.4f);
        for (int i = 0; i < 2; i++)
        {
            Ride(thief, thief.transform.localPosition.x + 1f, 0.3f, Ease.OutSine);
            yield return Wait(0.55f);
        }

        // The sleeper wakes at last: "!" and a jump. The thief is gone in a blaze, nose up
        if (doze != null)
            doze.Kill();
        sleeper.Model.localRotation = Quaternion.identity;
        Say(stage, sleeper, "!", WHITE);
        Hop(sleeper, 0.8f, 0.35f);
        thief.Drive(1f);
        Wheelie(thief, 30f, 1.0f);
        Tween away = Ride(thief, thiefOff + 0.5f, 1.2f, Ease.InQuad);

        // A beat to take it in, then it gives chase
        yield return Beat(0.3f);
        Turn(sleeper, RIGHT, 0.2f);
        yield return Beat(0.3f);
        sleeper.Drive(1f);
        Tween chase = Ride(sleeper, off + 0.5f, 1.6f, Ease.InQuad);

        yield return away.WaitForCompletion();
        yield return chase.WaitForCompletion();
    }
}

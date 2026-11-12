using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// A ghost rides in proudly with a golden package. Three more pour in and mob it: all four circle fast in a spinning
/// scuffle with gold "!"s popping, and the package changes hands again and again. Then they scatter, and the ghost
/// left holding the gold is not the one that came in with it: it leaves last, cheering, with a wheelie
/// </summary>
public class GoldenParcel : TitleSkit
{
    public override string Name => "Golden Parcel";
    public override int Cast => 4;

    // The scuffle's circle (an ellipse on the floor) and how long and fast it goes
    const float RX = 2f, RZ = 1f, ZC = 0.4f;
    const float OMEGA = 6f, SCUFFLE = 2.6f;

    /// <summary>Where each ghost starts on the circle (angle): cast[0] on the left, cast[1] right, cast[2] far, cast[3] near</summary>
    static readonly float[] START = { Mathf.PI, 0f, Mathf.PI * 0.5f, Mathf.PI * 1.5f };

    // Who steals from whom, and when: 1 takes it, 3 takes it, the first owner gets it back, and then 2 takes it for good
    static readonly float[] STEAL_AT = { 0.4f, 0.95f, 1.5f, 2.05f };
    static readonly int[] THIEF = { 1, 3, 0, 2 };
    static readonly int[] VICTIM = { 0, 1, 3, 0 };

    static readonly float[] POP_X = { -1.5f, 1.2f, -0.4f, 1.8f, -1.2f, 0.5f, -0.8f };

    static Vector3 OnCircle(float angle)
    {
        return new Vector3(RX * Mathf.Cos(angle), 0f, ZC + RZ * Mathf.Sin(angle));
    }

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        // The owner rides in, slowing, and shows off its prize
        Place(cast[0], -stage.OffX(ZC), ZC, RIGHT, 0.6f);
        Load(stage, cast[0], 1, true);
        Ride(cast[0], OnCircle(START[0]).x, 2.1f, Ease.OutSine);
        yield return WaitRide(0.9f);
        Wheelie(cast[0], 20f, 0.9f);
        yield return WaitRide(1.2f);
        cast[0].Drive(0f);
        cast[0].Cheer();
        yield return Beat(0.5f);

        // The three others pour in: two from the right, one from the left, to their places round it
        Tween comeIn = null;
        for (int i = 1; i < cast.Length; i++)
        {
            Vector3 at = OnCircle(START[i]);
            int heading = i == 3 ? RIGHT : LEFT;    // the way it rides in
            Place(cast[i], -heading * stage.OffX(at.z), at.z, heading);
            comeIn = RideTo(cast[i], at.x, at.z, 1.3f, Ease.OutQuad);
        }
        yield return Beat(0.4f);
        Say(stage, cast[0], "?", WHITE, 0.9f);
        yield return comeIn.WaitForCompletion();

        // The scuffle: everyone circles the centre, spinning, squashing, "!"s popping, the package snatched about
        for (int i = 0; i < cast.Length; i++)
            cast[i].Drive(0.8f);

        IEnumerator steal = null;
        int nextSteal = 0, nextPop = 0, nextSquash = 0;
        for (float s = 0f; s < SCUFFLE; s += Time.deltaTime)
        {
            for (int i = 0; i < cast.Length; i++)
            {
                float angle = START[i] + OMEGA * s;
                Vector3 heading = new Vector3(-RX * Mathf.Sin(angle), 0f, RZ * Mathf.Cos(angle));
                float yaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
                cast[i].transform.localPosition = OnCircle(angle);
                cast[i].transform.localRotation = Quaternion.Euler(0f, yaw + 540f * s, 0f);
            }

            // A steal is stepped by hand each frame so the circling goes on while it flies
            if (steal == null && nextSteal < STEAL_AT.Length && s >= STEAL_AT[nextSteal])
            {
                steal = Steal(cast[THIEF[nextSteal]], cast[VICTIM[nextSteal]]);
                nextSteal++;
            }
            if (steal != null && !steal.MoveNext())
                steal = null;

            if (nextPop < POP_X.Length && s >= nextPop * 0.4f)
            {
                Vector3 at = new Vector3(POP_X[nextPop], 3.4f + (nextPop % 2) * 0.6f, ZC);
                stage.Pop(nextPop % 3 == 2 ? "!!" : "!", at, 0.8f, GOLD);
                nextPop++;
            }
            if (s >= nextSquash * 0.3f)
            {
                Squash(cast[nextSquash % cast.Length], 0.22f, 0.3f);
                nextSquash++;
            }
            yield return null;
        }
        while (steal != null && steal.MoveNext())
            yield return null;

        // Scatter every which way. cast[2] has the gold (the last steal) and goes last; cast[0], robbed, sits stunned first
        TitleActor winner = cast[2];
        Tween outNear = Leave(stage, cast[3], LEFT, -1.2f, 1.3f);
        yield return Wait(0.15f);
        Tween outFar = Leave(stage, cast[1], RIGHT, 1.8f, 1.3f);
        winner.Cheer();
        cast[0].Slump();
        Say(stage, cast[0], "?", WHITE, 0.9f);
        yield return Beat(0.6f);
        Tween outRobbed = Leave(stage, cast[0], LEFT, 1.4f, 1.3f);

        // The winner, a beat later, wheelies out with its prize
        yield return Beat(0.4f);
        Turn(winner, RIGHT, 0.3f);
        Wheelie(winner, 28f, 1.1f);
        Tween outWinner = RideTo(winner, stage.OffX(-0.8f) + 0.5f, -0.8f, 1.6f, Ease.InQuad);

        yield return outNear.WaitForCompletion();
        yield return outFar.WaitForCompletion();
        yield return outRobbed.WaitForCompletion();
        yield return outWinner.WaitForCompletion();
    }

    /// <summary>Turns and rides off in a direction, drifting to a depth on the way</summary>
    static Tween Leave(TitleStage stage, TitleActor ghost, int direction, float toZ, float seconds)
    {
        ghost.Drive(1f);
        Turn(ghost, direction, 0.2f);
        return RideTo(ghost, direction * (stage.OffX(toZ) + 0.5f), toZ, seconds, Ease.InQuad);
    }
}

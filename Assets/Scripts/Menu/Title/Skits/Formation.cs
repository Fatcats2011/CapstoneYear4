using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// Three ghosts ride in side by side and show off in unison: a donut each, a hop, a clap. A fourth arrives late and
/// flat out, can't stop, and bowls through them one after another; each is sent spinning off to the right, and the
/// bowler wobbles out after them
/// </summary>
public class Formation : TitleSkit
{
    public override string Name => "Formation";
    public override int Cast => 4;

    const float SPEED = 10f;    // the late one's
    const float REACH = 2f;     // how far apart a scooter's nose and the next one's tail are when they touch

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        float off = stage.OffScreenX;
        float[] spots = { -3.5f, 0f, 3.5f };

        // The trio rides in together and stops on its marks, evenly spaced
        float run = off + 4f;
        Tween arrive = null;
        for (int i = 0; i < 3; i++)
        {
            Place(cast[i], spots[i] - run, 0f, RIGHT, 0.8f);
            arrive = Ride(cast[i], spots[i], 2.4f, Ease.OutSine);
        }
        yield return arrive.WaitForCompletion();
        for (int i = 0; i < 3; i++)
            cast[i].Drive(0f);
        yield return Wait(0.2f);

        // All together: a donut...
        Tween spin = null;
        for (int i = 0; i < 3; i++)
            spin = Donut(cast[i], 1f, 1.4f);
        yield return spin.WaitForCompletion();
        yield return Wait(0.1f);

        // ...a hop...
        Tween hop = null;
        for (int i = 0; i < 3; i++)
            hop = Hop(cast[i], 0.7f, 0.5f);
        yield return hop.WaitForCompletion();

        // ...and a clap, hands high, proud
        for (int i = 0; i < 3; i++)
            cast[i].Clap();
        yield return Beat(0.8f);

        // Here comes the fourth, late, with the brakes not working: nose up, skidding
        TitleActor bowler = cast[3];
        Place(bowler, -off, 0f, RIGHT);
        float hitAt = Ridden((spots[0] - REACH + off) / SPEED);
        Tween bowl = Ride(bowler, off + 0.5f, (2f * off + 0.5f) / SPEED);
        float lead = Mathf.Min(0.5f, hitAt);
        yield return Wait(hitAt - lead);
        Wheelie(bowler, 25f, lead);
        yield return Wait(lead);

        // Strike: each is knocked off in turn, to a different depth so they miss each other
        float[] depths = { -1.3f, 1.0f, 2.4f };
        Tween[] knocked = new Tween[3];
        for (int i = 0; i < 3; i++)
        {
            knocked[i] = Knock(stage, cast[i], depths[i]);
            Squash(bowler, 0.3f, 0.3f);
            if (i == 0)
            {
                Say(stage, bowler, "!", WHITE);
                Wobble(bowler, 10f, 0.18f, 14);
            }
            if (i < 2)
                yield return WaitRide((spots[i + 1] - spots[i]) / SPEED);
        }

        // The bowler carries on out the right too, wobbling
        yield return bowl.WaitForCompletion();
        for (int i = 0; i < 3; i++)
            yield return knocked[i].WaitForCompletion();
    }

    /// <summary>One ghost hit: a squash, a "!", and off to the right spinning at high speed</summary>
    static Tween Knock(TitleStage stage, TitleActor ghost, float toZ)
    {
        ghost.Drive(1f);
        Say(stage, ghost, "!", PINK);
        Squash(ghost, 0.35f, 0.4f);

        float toX = stage.OffX(toZ) + 0.5f;
        float seconds = Mathf.Max(0.8f, (toX - ghost.transform.localPosition.x) / 11f);
        Donut(ghost, 2f, Ridden(seconds), Ease.OutQuad);
        ghost.transform.DOLocalMoveZ(toZ, 0.35f).SetEase(Ease.OutQuad).SetLink(ghost.gameObject);
        return Ride(ghost, toX, seconds);
    }
}

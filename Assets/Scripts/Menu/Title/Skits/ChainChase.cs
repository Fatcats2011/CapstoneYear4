using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// Four ghosts chase across the screen in a line, the one in front carrying a package; the ones behind steal it forward
/// along the line. They leave, and come back the other way in the reverse order with the last one now in front, and one
/// more steal on the way
/// </summary>
public class ChainChase : TitleSkit
{
    public override string Name => "Chain Chase";
    public override int Cast => 4;

    /// <summary>Between two ghosts of the line, and how fast it runs (units per second). It is flat out already, so it
    /// keeps its own time and not the rides' pace: the steals have to stay on screen</summary>
    const float GAP = 2.4f;
    const float SPEED_OUT = 9f, SPEED_BACK = 9.5f;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        float off = stage.OffScreenX;
        float trail = GAP * (cast.Length - 1);
        // Every ghost rides the same distance, so the line keeps its shape and all of it is out of sight at the end
        float run = 2f * off + trail + 0.5f;

        // Out: cast[0] leads with the package, the others string out behind it
        Tween last = null;
        for (int i = 0; i < cast.Length; i++)
        {
            float start = -off - GAP * i;
            Place(cast[i], start, 0f, RIGHT);
            last = Move(cast[i], start + run, run / SPEED_OUT);
        }
        Load(stage, cast[0]);

        // The second one lunges and lifts it off the leader
        yield return Wait(0.7f);
        Wheelie(cast[1], 20f, 0.5f);
        yield return Wait(0.2f);
        yield return Steal(cast[1], cast[0]);
        Say(stage, cast[0], "!", WHITE, 0.8f);

        // Then the third lifts it off the second: it never gets a moment to enjoy it
        yield return Wait(0.35f);
        Wheelie(cast[2], 20f, 0.5f);
        yield return Wait(0.2f);
        yield return Steal(cast[2], cast[1]);
        Say(stage, cast[1], "!", WHITE, 0.8f);

        yield return last.WaitForCompletion();

        // A beat of nothing at all. Out of sight the parcel finds its way to the tail-ender, who leads the way back
        yield return Wait(0.3f);
        Transform parcel = cast[2].Drop();
        if (parcel != null)
            cast[3].Carry(parcel);

        // Back: the line is reversed, cast[3] in front
        for (int i = 0; i < cast.Length; i++)
        {
            float start = off + GAP * (cast.Length - 1 - i);
            Place(cast[i], start, 0f, LEFT);
            last = Move(cast[i], start - run, run / SPEED_BACK);
        }

        // The one behind the leader takes it again: the leader just can't hold on to it
        yield return Wait(1.2f);
        Wheelie(cast[2], 20f, 0.5f);
        yield return Wait(0.2f);
        yield return Steal(cast[2], cast[3]);
        Say(stage, cast[3], "!", WHITE, 0.8f);

        yield return last.WaitForCompletion();
    }
}

using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// Two ghosts ride in from opposite sides, face to face, and a big package appears between them. They heave at it,
/// three rounds, each one lurching further, until it pops straight up and both are flung backwards off the screen's
/// edges, noses in the air, while it drops to the floor behind them
/// </summary>
public class TugOfWar : TitleSkit
{
    public override string Name => "Tug of War";
    public override int Cast => 2;

    /// <summary>How far each ghost stops from the middle, and how high the package hangs between them</summary>
    const float MEET = 1.7f;
    const float HANG = 1.2f;
    const float FLOOR = 0.3f;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        TitleActor left = cast[0], right = cast[1];
        float off = stage.OffScreenX;

        // They glide in and stop nose to nose
        Place(left, -off, 0f, RIGHT, 0.8f);
        Place(right, off, 0f, LEFT, 0.8f);
        Ride(left, -MEET, 1.9f, Ease.OutSine);
        Tween arrive = Ride(right, MEET, 1.9f, Ease.OutSine);

        // The package is ready between them, but unseen until they get there
        Transform parcel = stage.TakePackage(2);
        Vector3 size = parcel.localScale;
        parcel.localPosition = new Vector3(0f, HANG, 0f);
        parcel.localScale = Vector3.zero;

        yield return arrive.WaitForCompletion();
        left.Drive(0f);
        right.Drive(0f);
        parcel.DOScale(size, 0.25f).SetEase(Ease.OutBack).SetLink(parcel.gameObject);
        Say(stage, left, "!", WHITE, 0.8f);
        Say(stage, right, "!", GREEN, 0.8f);
        yield return Beat(0.6f);

        // Three rounds, each heave dragging both ghosts and the package one way, then the other, harder every time
        Transform[] pieces = { left.transform, right.transform, parcel };
        float[] home = { left.transform.localPosition.x, right.transform.localPosition.x, parcel.localPosition.x };
        float[] reach = { 0.6f, 0.8f, 1.0f };
        for (int round = 0; round < reach.Length; round++)
        {
            Wobble(left, 8f, 0.22f, 4);
            Wobble(right, 8f, 0.22f, 4);
            Squash(left, 0.18f, 0.5f);
            Squash(right, 0.18f, 0.5f);
            yield return Heave(pieces, home, -reach[round], 0.45f).WaitForCompletion();
            yield return Heave(pieces, home, reach[round], 0.45f).WaitForCompletion();
            yield return Wait(0.1f);
        }

        // Back to the middle, straining, a beat before it goes
        yield return Heave(pieces, home, 0f, 0.35f).WaitForCompletion();
        Wobble(left, 10f, 0.15f, 4);
        Wobble(right, 10f, 0.15f, 4);
        yield return Beat(0.45f);

        // It pops straight up and the ghosts are flung backwards, facing in with their noses high
        Toss(parcel, new Vector3(home[2], FLOOR, 0f), 3.0f, 1.2f);
        left.Drive(1f);
        right.Drive(1f);
        Squash(left, 0.25f, 0.4f);
        Squash(right, 0.25f, 0.4f);
        Pitch(left, 25f, 0.25f);
        Pitch(right, 25f, 0.25f);
        Ride(left, -off, 1.25f, Ease.InQuad);
        yield return Ride(right, off, 1.25f, Ease.InQuad).WaitForCompletion();
    }

    /// <summary>Moves the two ghosts and the package together to their home spots plus an offset along the lane</summary>
    static Tween Heave(Transform[] pieces, float[] home, float delta, float seconds)
    {
        for (int i = 1; i < pieces.Length; i++)
            pieces[i].DOLocalMoveX(home[i] + delta, seconds).SetEase(Ease.InOutSine).SetLink(pieces[i].gameObject);
        return pieces[0].DOLocalMoveX(home[0] + delta, seconds).SetEase(Ease.InOutSine).SetLink(pieces[0].gameObject);
    }
}

using System.Collections;
using DG.Tweening;
using UnityEngine;
using static TitleSkitTools;

/// <summary>
/// Three ghosts ride on in a line with one package between them, slow to a crawl, and it never stays put: whoever
/// fancies it snatches it, forward and back down the line, five steals in all. Then they all tear off, the last one
/// out holding it
/// </summary>
public class PassTheParcel : TitleSkit
{
    public override string Name => "Pass the Parcel";
    public override int Cast => 3;

    /// <summary>How far apart the ghosts start, and how far the line crawls while the package goes round</summary>
    const float SPACING = 2.4f;
    const float CRAWL = 1.6f;
    /// <summary>Longer than the five steals take: the crawl is cut short when they are done</summary>
    const float CRAWL_TIME = 6f;

    public override IEnumerator Play(TitleStage stage, TitleActor[] cast)
    {
        // Front, middle and back, in zig-zag lanes so that all three show and the package has a clear flight
        float[] lanes = { 0.7f, -0.7f, 0.7f };
        float[] marks = { 2.6f, 0.2f, -2.2f };

        // They ride on briskly and ease up on their marks
        Tween arrive = null;
        for (int i = 0; i < 3; i++)
        {
            Place(cast[i], -stage.OffX(lanes[i]) - i * SPACING, lanes[i], RIGHT, 0.8f);
            arrive = Ride(cast[i], marks[i], 2.2f, Ease.OutSine);
        }
        Load(stage, cast[1]);
        yield return arrive.WaitForCompletion();

        // Down to a crawl, and it starts: front takes it off the middle one
        Tween[] crawl = new Tween[3];
        for (int i = 0; i < 3; i++)
        {
            cast[i].Drive(0.2f);
            crawl[i] = Move(cast[i], marks[i] + CRAWL, CRAWL_TIME);
        }
        yield return Beat(0.2f);

        // Thief and victim, by place in the line: forward, then over the middle one's head, then back, forward, back
        int[,] steals = { { 0, 1 }, { 2, 0 }, { 1, 2 }, { 0, 1 }, { 2, 0 } };
        for (int s = 0; s < 5; s++)
        {
            TitleActor thief = cast[steals[s, 0]], victim = cast[steals[s, 1]];

            // The victim only notices as it goes
            Say(stage, victim, s % 2 == 0 ? "!" : "?", s % 2 == 0 ? WHITE : PINK, 0.8f);
            bool far = Mathf.Abs(steals[s, 0] - steals[s, 1]) > 1;
            yield return Steal(thief, victim, far ? 0.42f : 0.3f);
            yield return Beat(0.3f);
        }

        // The back one has it for keeps and shows off, while the middle one wonders where it went
        Wheelie(cast[2], 28f, 1.0f);
        Say(stage, cast[1], "?", WHITE, 0.9f);
        yield return Beat(0.4f);

        // The line tears off, the thief last
        Tween leave = null;
        for (int i = 0; i < 3; i++)
        {
            crawl[i].Kill();
            cast[i].Drive(1f);
            leave = Ride(cast[i], stage.OffX(lanes[i]) + 0.5f, 1.5f, Ease.InQuad);
        }
        yield return leave.WaitForCompletion();
    }
}

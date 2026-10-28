using System.Collections.Generic;
using UnityEngine;

/// <summary>What happened in a moment the highlight reel might show</summary>
public enum MomentKind : byte { GoldenSteal, Steal, Clash, Bump, HardDelivery, Delivery, Fall }

/// <summary>The line shown over a clip</summary>
public enum Caption : byte { Theft, Collision, Water, Delivery }

/// <summary>One thing that happened in the round, as the highlight reel logs it</summary>
public struct Moment
{
    public MomentKind Kind;

    /// <summary>The game's clock when it happened</summary>
    public double Time;

    /// <summary>The player it happened to or that did it (a thief, a bumper, a courier, whoever fell)</summary>
    public int Seat;

    /// <summary>The second player involved (a victim), or -1 for none</summary>
    public int Other;

    /// <summary>0 to 1, used only by bumps</summary>
    public float Strength;

    /// <summary>The round clock at the moment</summary>
    public float TimeLeft;

    public Vector3 Place;
}

/// <summary>A stretch of the round worth replaying: the moments in it, scored</summary>
public class Clip
{
    public double Start, End;

    /// <summary>When the heaviest moment in the clip happened</summary>
    public double KeyTime;

    public float Score;
    public Caption Caption;

    /// <summary>Bit per seat that's in the clip</summary>
    public int SeatsMask;

    /// <summary>Where the heaviest moment happened</summary>
    public Vector3 KeyPlace;

    /// <summary>The round clock when the clip starts</summary>
    public float TimeLeftAtStart;

    public double Length => End - Start;
}

/// <summary>
/// Which stretches of the round make the highlight reel: how much each moment weighs, how nearby moments join into clips,
/// and which clips get picked so every player is seen if they did anything, without passing the reel's length. Pure rules.
/// </summary>
public static class HighlightRules
{
    /// <summary>Moments this close together (seconds) can join one clip</summary>
    public const float COMBO_WINDOW = 4f;

    /// <summary>Each moment after a clip's first multiplies its score by this once more</summary>
    public const float COMBO_MULTIPLIER = 1.5f;

    /// <summary>A fall this soon (seconds) after being stolen from, clashed with or bumped counts as being knocked in</summary>
    public const float KNOCKED_IN_WINDOW = 3f;
    public const float KNOCKED_IN_BONUS = 4f;

    /// <summary>A delivery with this many seconds or fewer left on the round clock is late and weighs more</summary>
    public const float LATE_SECONDS = 10f;
    public const float LATE_BONUS = 3f;

    /// <summary>Seconds shown before a clip's first moment and after its last</summary>
    public const float LEAD = 1.5f;
    public const float TAIL = 1.5f;

    /// <summary>A clip is this long at least, and this long at most (unless clips are merged)</summary>
    public const float MIN_CLIP = 3f;
    public const float MAX_CLIP = 6f;

    /// <summary>A clip needs this score to fill the reel after everyone has had a look-in</summary>
    public const float GOOD_ENOUGH = 5f;

    /// <summary>A clip needs this score to be a player's guaranteed one</summary>
    public const float PLAYER_MINIMUM = 3f;

    /// <summary>The longest the reel gets, in seconds</summary>
    public const float MAX_REEL = 20f;

    /// <summary>What a moment weighs on its own. A fall's knocked-in bonus needs the moments before it (the list overload)</summary>
    public static float Weight(Moment m)
    {
        switch (m.Kind)
        {
            case MomentKind.GoldenSteal: return 10f;
            case MomentKind.Steal: return 8f;
            case MomentKind.Clash: return 6f;
            case MomentKind.Bump: return 1f + 3f * Mathf.Clamp01(m.Strength);
            case MomentKind.HardDelivery: return 5f + Late(m);
            case MomentKind.Delivery: return 2f + Late(m);
            case MomentKind.Fall: return 3f;
            default: return 0f;
        }
    }

    /// <summary>
    /// What the moment at <c>index</c> weighs, counting a fall's knocked-in bonus. The list must be in time order, as only
    /// the moments before the index are looked at.
    /// </summary>
    public static float Weight(IReadOnlyList<Moment> moments, int index)
    {
        Moment m = moments[index];
        float weight = Weight(m);
        if (m.Kind != MomentKind.Fall) return weight;

        for (int i = index - 1; i >= 0; i--)
        {
            Moment before = moments[i];
            if (m.Time - before.Time > KNOCKED_IN_WINDOW) break;
            bool knocks = before.Kind == MomentKind.GoldenSteal || before.Kind == MomentKind.Steal
                || before.Kind == MomentKind.Clash || before.Kind == MomentKind.Bump;
            if (knocks && before.Other == m.Seat) return weight + KNOCKED_IN_BONUS;
        }
        return weight;
    }

    static float Late(Moment m)
    {
        return m.TimeLeft <= LATE_SECONDS ? LATE_BONUS : 0f;
    }

    /// <summary>The seats a moment involves, as a bit per seat</summary>
    static int SeatsOf(Moment m)
    {
        return Bit(m.Seat) | Bit(m.Other);
    }

    static int Bit(int seat)
    {
        return seat >= 0 && seat < 31 ? 1 << seat : 0;
    }

    static Caption CaptionOf(MomentKind kind)
    {
        switch (kind)
        {
            case MomentKind.GoldenSteal:
            case MomentKind.Steal: return Caption.Theft;
            case MomentKind.Clash:
            case MomentKind.Bump: return Caption.Collision;
            case MomentKind.Fall: return Caption.Water;
            default: return Caption.Delivery;
        }
    }

    /// <summary>
    /// Joins the moments into clips. A moment joins the open clip when it's within COMBO_WINDOW of the clip's last moment
    /// and shares a seat with the clip; otherwise it starts a new one. The moments can come in any order.
    /// </summary>
    public static List<Clip> Clips(IReadOnlyList<Moment> moments)
    {
        var clips = new List<Clip>();
        if (moments == null || moments.Count == 0) return clips;

        // insertion sort keeps equal times in the order given, and the list is nearly sorted already
        var sorted = new List<Moment>(moments);
        for (int i = 1; i < sorted.Count; i++)
        {
            Moment x = sorted[i];
            int j = i - 1;
            while (j >= 0 && sorted[j].Time > x.Time) { sorted[j + 1] = sorted[j]; j--; }
            sorted[j + 1] = x;
        }

        int first = 0; // index of the open clip's first moment
        int count = 0;
        int mask = 0;
        float score = 0f;
        int heaviest = 0;
        float heaviestWeight = float.MinValue;

        for (int i = 0; i < sorted.Count; i++)
        {
            Moment m = sorted[i];
            float weight = Weight(sorted, i);
            bool joins = count > 0 && m.Time - sorted[i - 1].Time <= COMBO_WINDOW && (mask & SeatsOf(m)) != 0;
            if (!joins)
            {
                if (count > 0) clips.Add(Build(sorted, first, i - 1, mask, score * Mathf.Pow(COMBO_MULTIPLIER, count - 1), heaviest));
                first = i;
                count = 0;
                mask = 0;
                score = 0f;
                heaviestWeight = float.MinValue;
            }

            score += weight;
            count++;
            mask |= SeatsOf(m);
            if (weight > heaviestWeight) { heaviestWeight = weight; heaviest = i; }
        }
        clips.Add(Build(sorted, first, sorted.Count - 1, mask, score * Mathf.Pow(COMBO_MULTIPLIER, count - 1), heaviest));
        return clips;
    }

    static Clip Build(List<Moment> moments, int first, int last, int mask, float score, int heaviest)
    {
        double start = moments[first].Time - LEAD;
        double end = moments[last].Time + TAIL;
        if (end - start > MAX_CLIP) start = end - MAX_CLIP; // too long trims the start
        else if (end - start < MIN_CLIP) end = start + MIN_CLIP; // too short extends the end

        Moment key = moments[heaviest];
        return new Clip
        {
            Start = start,
            End = end,
            KeyTime = key.Time,
            KeyPlace = key.Place,
            Caption = CaptionOf(key.Kind),
            Score = score,
            SeatsMask = mask,
            // the round clock counts down, so the start is earlier on the clock than the first moment by the lead
            TimeLeftAtStart = moments[first].TimeLeft + (float)(moments[first].Time - start),
        };
    }

    /// <summary>
    /// Chooses the clips for the reel. First each seat in <c>seats</c> gets its best clip if it scores PLAYER_MINIMUM or
    /// more (highest first, and only while the reel fits MAX_REEL), then the best of the rest that score GOOD_ENOUGH while
    /// they fit. Clips that overlap are merged, so the reel's length is the union of what's chosen. Returned in time order.
    /// </summary>
    public static List<Clip> Pick(IReadOnlyList<Clip> clips, IEnumerable<int> seats)
    {
        var chosen = new List<Clip>();
        if (clips == null || clips.Count == 0) return chosen;

        // each seat's best clip
        var bests = new List<Clip>();
        foreach (int seat in seats)
        {
            int bit = Bit(seat);
            Clip best = null;
            foreach (Clip c in clips)
                if ((c.SeatsMask & bit) != 0 && c.Score >= PLAYER_MINIMUM && (best == null || c.Score > best.Score))
                    best = c;
            if (best != null) bests.Add(best);
        }
        bests.Sort((a, b) => b.Score.CompareTo(a.Score)); // List.Sort isn't stable, but equal scores can't change who is seen
        foreach (Clip c in bests) TryAdd(chosen, c);

        // fill up with the best of the rest
        var rest = new List<Clip>();
        foreach (Clip c in clips)
            if (c.Score >= GOOD_ENOUGH && !chosen.Contains(c)) rest.Add(c);
        rest.Sort((a, b) => b.Score.CompareTo(a.Score));
        foreach (Clip c in rest) TryAdd(chosen, c);

        return Merge(chosen);
    }

    static void TryAdd(List<Clip> chosen, Clip c)
    {
        if (chosen.Contains(c)) return;
        chosen.Add(c);
        if (UnionLength(chosen) > MAX_REEL + 1e-6) chosen.RemoveAt(chosen.Count - 1);
    }

    /// <summary>How many seconds the clips cover between them, overlaps counted once</summary>
    static double UnionLength(List<Clip> clips)
    {
        double total = 0;
        foreach (Clip c in Merge(clips)) total += c.Length;
        return total;
    }

    /// <summary>
    /// Clips that overlap in time become one: the whole range, scores added, seats combined, and the key moment and
    /// caption of the higher-scoring one. Sorted by start; the clips given are left as they are.
    /// </summary>
    static List<Clip> Merge(List<Clip> clips)
    {
        var sorted = new List<Clip>(clips);
        sorted.Sort((a, b) => a.Start.CompareTo(b.Start));

        var merged = new List<Clip>();
        Clip open = null;
        Clip openKey = null; // whichever merged clip scored highest so far
        foreach (Clip c in sorted)
        {
            if (open != null && c.Start < open.End)
            {
                open.End = System.Math.Max(open.End, c.End);
                open.Score += c.Score;
                open.SeatsMask |= c.SeatsMask;
                if (c.Score > openKey.Score)
                {
                    openKey = c;
                    open.KeyTime = c.KeyTime;
                    open.KeyPlace = c.KeyPlace;
                    open.Caption = c.Caption;
                }
                continue;
            }

            open = new Clip
            {
                Start = c.Start, End = c.End, KeyTime = c.KeyTime, Score = c.Score, Caption = c.Caption,
                SeatsMask = c.SeatsMask, KeyPlace = c.KeyPlace, TimeLeftAtStart = c.TimeLeftAtStart,
            };
            openKey = c;
            merged.Add(open);
        }
        return merged;
    }
}

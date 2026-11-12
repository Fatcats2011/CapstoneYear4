using System;
using UnityEngine;

/// <summary>How a ghost feels on the podium: first place, the places between, and last of four</summary>
public enum PodiumMood { Winner, Runner, Last }

/// <summary>
/// Where an act has the scooter at a moment, on top of how it stands: lifted (metres), turned (degrees: Nose up,
/// Yaw to its right, Lean to its right) and squashed or stretched (1 = as it is)
/// </summary>
public struct PodiumPose
{
    public float Lift;
    public float Nose;
    public float Yaw;
    public float Lean;
    public float Tall;  // height: 1 = as it is
    public float Wide;  // width and length

    public static PodiumPose Still => new PodiumPose { Tall = 1f, Wide = 1f };
}

/// <summary>
/// One little act on the podium: a couple of seconds of movement, and the pose the ghost holds meanwhile (the
/// animator's End Status; KEEP for the pose of its place)
/// </summary>
public class PodiumAct
{
    public const int KEEP = -1;

    public readonly string Name;
    public readonly float Length; // seconds
    public readonly int Pose;
    readonly Func<float, float, PodiumPose> at;

    public PodiumAct(string name, float length, int pose, Func<float, float, PodiumPose> at)
    {
        Name = name;
        Length = length;
        Pose = pose;
        this.at = at;
    }

    /// <summary>
    /// The scooter a share of the way through (0 to 1). toWinner is how far the winner is to its right, in degrees
    /// (negative: to its left), for the acts that look at them
    /// </summary>
    public PodiumPose At(float share, float toWinner)
    {
        return at(Mathf.Clamp01(share), toWinner);
    }
}

/// <summary>
/// What the ghosts do on the results' podium, so no two matches (or two ghosts) look the same: each ghost keeps the
/// pose of its place (cheering, clapping, slumped) and every few seconds puts on a short act picked from its mood's
/// list, shuffled, never the same twice running (PodiumPerformer plays them). The game has three podium animations;
/// the acts are movement on top of them (hops, spins, wheelies, glances at the winner) and short changes of pose.
/// Every act starts and ends standing still, and never sinks into the podium
/// </summary>
public static class PodiumActs
{
    // The ghost's poses on the animator's End Status (PlayerAnimator.controller)
    public const int SITTING = 0, HOORAY = 1, CLAP = 2, CLAP_TOO = 3, LOSE = 4;

    /// <summary>The longest a ghost stands still between two acts, and the shortest, in seconds</summary>
    public const float REST_MIN = 0.9f, REST_MAX = 2.4f;

    /// <summary>How far a ghost turns to look at the winner at most, in degrees</summary>
    public const float GLANCE = 45f;

    /// <summary>The mood of a finishing place (0 = first) among this many players</summary>
    public static PodiumMood MoodOf(int place, int players)
    {
        if (place <= 0)
            return PodiumMood.Winner;
        return place >= 3 ? PodiumMood.Last : PodiumMood.Runner;
    }

    /// <summary>The pose a finishing place (0 = first) holds between acts: as the podium always had them</summary>
    public static int PoseOf(int place)
    {
        return Mathf.Clamp(place, 0, 3) + 1;
    }

    public static PodiumAct[] For(PodiumMood mood)
    {
        return mood == PodiumMood.Winner ? WINNER : mood == PodiumMood.Last ? LAST : RUNNER;
    }

    static readonly PodiumAct[] WINNER =
    {
        new PodiumAct("Victory Hops", 1.5f, PodiumAct.KEEP, (t, w) => Pose(lift: 0.55f * Bounces(t, 2), tall: 1f + 0.08f * Bounces(t, 2))),
        new PodiumAct("Spin", 1.3f, PodiumAct.KEEP, (t, w) => Pose(lift: 0.7f * Arc(t), yaw: 360f * Smooth(t))),
        new PodiumAct("Wheelie", 2.0f, PodiumAct.KEEP, (t, w) => Pose(nose: 30f * Hold(t, 0.2f, 0.25f), lift: 0.12f * Hold(t, 0.2f, 0.25f))),
        new PodiumAct("Party Sway", 2.2f, PodiumAct.KEEP, (t, w) => Pose(lean: 13f * Wave(t, 3), lift: 0.12f * Mathf.Abs(Wave(t, 3)))),
        new PodiumAct("Take A Bow", 1.9f, SITTING, (t, w) => Pose(nose: -16f * Hold(t, 0.3f, 0.3f), tall: 1f - 0.06f * Hold(t, 0.3f, 0.3f))),
        new PodiumAct("Thank You Thank You", 2.4f, CLAP, (t, w) => Pose(lift: 0.1f * Bounces(t, 4), yaw: 22f * Wave(t, 1))),
        new PodiumAct("Rev It", 1.6f, SITTING, (t, w) => Pose(lean: 3f * Wave(t, 9), nose: 9f * Bounces(t, 3), lift: 0.08f * Bounces(t, 3))),
        new PodiumAct("Big Jump", 1.2f, PodiumAct.KEEP, (t, w) => Pose(lift: 1.1f * Arc(t), tall: 1f + 0.14f * Arc(t) - 0.12f * Land(t), wide: 1f + 0.1f * Land(t))),
    };

    static readonly PodiumAct[] RUNNER =
    {
        new PodiumAct("Nod", 1.3f, PodiumAct.KEEP, (t, w) => Pose(nose: -7f * Mathf.Abs(Wave(t, 2)))),
        new PodiumAct("Look At The Winner", 2.2f, PodiumAct.KEEP, (t, w) => Pose(yaw: Glance(w) * Hold(t, 0.22f, 0.22f))),
        new PodiumAct("Little Hop", 0.8f, PodiumAct.KEEP, (t, w) => Pose(lift: 0.32f * Arc(t), tall: 1f + 0.05f * Arc(t))),
        new PodiumAct("Cheer", 2.0f, HOORAY, (t, w) => Pose(lift: 0.3f * Bounces(t, 2))),
        new PodiumAct("Sway", 2.0f, PodiumAct.KEEP, (t, w) => Pose(lean: 8f * Wave(t, 2))),
        new PodiumAct("Shrug", 0.9f, PodiumAct.KEEP, (t, w) => Pose(tall: 1f + 0.09f * Wave(t, 1), wide: 1f - 0.05f * Wave(t, 1))),
        new PodiumAct("Sit Back", 2.3f, SITTING, (t, w) => Pose(nose: 8f * Hold(t, 0.25f, 0.25f))),
        new PodiumAct("Good Game", 2.0f, PodiumAct.KEEP, (t, w) => Pose(yaw: Glance(w) * Hold(t, 0.2f, 0.2f), nose: -6f * Mathf.Abs(Wave(t, 3)))),
    };

    static readonly PodiumAct[] LAST =
    {
        new PodiumAct("Sigh", 2.2f, PodiumAct.KEEP, (t, w) => Pose(tall: 1f - 0.09f * Hold(t, 0.4f, 0.3f), wide: 1f + 0.04f * Hold(t, 0.4f, 0.3f))),
        new PodiumAct("Shake Head", 1.6f, PodiumAct.KEEP, (t, w) => Pose(yaw: 15f * Wave(t, 3))),
        new PodiumAct("Glare At The Winner", 2.4f, PodiumAct.KEEP, (t, w) => Pose(yaw: Glance(w) * Hold(t, 0.15f, 0.3f), nose: -5f * Hold(t, 0.15f, 0.3f))),
        new PodiumAct("Polite Clap", 2.0f, CLAP_TOO, (t, w) => Pose(nose: -4f * Mathf.Abs(Wave(t, 2)))),
        new PodiumAct("Turn Away", 2.6f, PodiumAct.KEEP, (t, w) => Pose(yaw: -Mathf.Sign(w == 0f ? 1f : w) * 40f * Hold(t, 0.25f, 0.3f))),
        new PodiumAct("Stomp", 1.4f, SITTING, (t, w) => Pose(lift: 0.16f * Bounces(t, 3), nose: -6f * Bounces(t, 3))),
        new PodiumAct("Droop", 2.4f, PodiumAct.KEEP, (t, w) => Pose(nose: -9f * Hold(t, 0.35f, 0.3f), lean: 5f * Hold(t, 0.35f, 0.3f))),
        new PodiumAct("Wobble", 1.5f, PodiumAct.KEEP, (t, w) => Pose(lean: 6f * Wave(t, 4))),
    };

    static PodiumPose Pose(float lift = 0f, float nose = 0f, float yaw = 0f, float lean = 0f, float tall = 1f, float wide = 1f)
    {
        return new PodiumPose { Lift = lift, Nose = nose, Yaw = yaw, Lean = lean, Tall = tall, Wide = wide };
    }

    // The winner's side, never more than a glance; a ghost with nobody to look at looks a little to its right
    static float Glance(float toWinner)
    {
        return toWinner == 0f ? GLANCE * 0.6f : Mathf.Clamp(toWinner, -GLANCE, GLANCE);
    }

    /// <summary>Up and down once: 0 at both ends, 1 in the middle</summary>
    internal static float Arc(float t)
    {
        return Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
    }

    /// <summary>That many hops, each lower than the last: 0 at both ends and between hops, never below 0</summary>
    internal static float Bounces(float t, int count)
    {
        t = Mathf.Clamp01(t);
        float hop = Mathf.Abs(Mathf.Sin(Mathf.PI * count * t));
        return hop * (1f - 0.45f * t);
    }

    /// <summary>Side to side that many times, growing then dying away: 0 at both ends, between -1 and 1</summary>
    internal static float Wave(float t, int cycles)
    {
        t = Mathf.Clamp01(t);
        return Mathf.Sin(2f * Mathf.PI * cycles * t) * Mathf.Sin(Mathf.PI * t);
    }

    /// <summary>Eases up to 1 over the first share, stays, and eases back over the last: 0 at both ends</summary>
    internal static float Hold(float t, float rise, float fall)
    {
        t = Mathf.Clamp01(t);
        if (t < rise)
            return Smooth(t / rise);
        if (t > 1f - fall)
            return Smooth((1f - t) / fall);
        return 1f;
    }

    /// <summary>0 to 1, slow at both ends</summary>
    internal static float Smooth(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    // The squash of a landing: a short bump at the very end of a jump
    static float Land(float t)
    {
        return t < 0.85f ? 0f : Mathf.Sin(Mathf.PI * (t - 0.85f) / 0.15f);
    }
}

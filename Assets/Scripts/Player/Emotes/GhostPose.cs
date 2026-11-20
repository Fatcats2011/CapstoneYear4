using UnityEngine;

/// <summary>
/// How far the ghost is moved from the pose the Animator gave it. Distances are in U: the rest distance from the
/// Torso to the Head. Angles are in degrees. "Its right" and "forward" are the ghost's own directions
/// </summary>
public struct GhostPose
{
    /// <summary>Whole body up (+)</summary>
    public float Lift;
    /// <summary>Whole body forward (+)</summary>
    public float Push;
    /// <summary>Whole body to its right (+)</summary>
    public float Shift;
    /// <summary>Tips forward (+)</summary>
    public float Pitch;
    /// <summary>Turns to its right (+), seen from above</summary>
    public float Yaw;
    /// <summary>Leans to its right (+)</summary>
    public float Roll;
    /// <summary>Body scale along up (1 = rest)</summary>
    public float Tall;
    /// <summary>Body scale across (1 = rest)</summary>
    public float Wide;
    public float HeadYaw;
    /// <summary>Head looks down (+)</summary>
    public float HeadPitch;
    public float HeadRoll;
    /// <summary>Head moves up (+)</summary>
    public float HeadPop;
    /// <summary>Tail raises (+)</summary>
    public float TailUp;
    /// <summary>Tail swings to its right (+)</summary>
    public float TailWag;
    /// <summary>Hat up (+)</summary>
    public float HatLift;
    /// <summary>Hat turns about up</summary>
    public float HatSpin;
    /// <summary>Hat flips forward (+)</summary>
    public float HatTip;
    /// <summary>Body and eyelids, 0 to 1 (1 = gone)</summary>
    public float Fade;
    /// <summary>The hat, 0 to 1 (1 = gone)</summary>
    public float HatFade;

    /// <summary>No change from the Animator's pose</summary>
    public static readonly GhostPose Rest = new GhostPose { Tall = 1f, Wide = 1f };

    /// <summary>True when every field is within the tolerance of Rest</summary>
    public bool IsRest(float tolerance = 1e-3f)
    {
        GhostPose r = Rest;
        return Near(Lift, r.Lift, tolerance) && Near(Push, r.Push, tolerance) && Near(Shift, r.Shift, tolerance)
            && Near(Pitch, r.Pitch, tolerance) && Near(Yaw, r.Yaw, tolerance) && Near(Roll, r.Roll, tolerance)
            && Near(Tall, r.Tall, tolerance) && Near(Wide, r.Wide, tolerance)
            && Near(HeadYaw, r.HeadYaw, tolerance) && Near(HeadPitch, r.HeadPitch, tolerance)
            && Near(HeadRoll, r.HeadRoll, tolerance) && Near(HeadPop, r.HeadPop, tolerance)
            && Near(TailUp, r.TailUp, tolerance) && Near(TailWag, r.TailWag, tolerance)
            && Near(HatLift, r.HatLift, tolerance) && Near(HatSpin, r.HatSpin, tolerance)
            && Near(HatTip, r.HatTip, tolerance) && Near(Fade, r.Fade, tolerance)
            && Near(HatFade, r.HatFade, tolerance);
    }

    /// <summary>
    /// Field by field from a to b (k 0 to 1, clamped). Angles take the short way round, so a hat that has spun 350
    /// degrees eases back 10, not 350
    /// </summary>
    public static GhostPose Lerp(GhostPose a, GhostPose b, float k)
    {
        k = Mathf.Clamp01(k);
        return new GhostPose
        {
            Lift = Mathf.Lerp(a.Lift, b.Lift, k), Push = Mathf.Lerp(a.Push, b.Push, k),
            Shift = Mathf.Lerp(a.Shift, b.Shift, k),
            Pitch = Mathf.LerpAngle(a.Pitch, b.Pitch, k), Yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, k),
            Roll = Mathf.LerpAngle(a.Roll, b.Roll, k),
            Tall = Mathf.Lerp(a.Tall, b.Tall, k), Wide = Mathf.Lerp(a.Wide, b.Wide, k),
            HeadYaw = Mathf.LerpAngle(a.HeadYaw, b.HeadYaw, k), HeadPitch = Mathf.LerpAngle(a.HeadPitch, b.HeadPitch, k),
            HeadRoll = Mathf.LerpAngle(a.HeadRoll, b.HeadRoll, k), HeadPop = Mathf.Lerp(a.HeadPop, b.HeadPop, k),
            TailUp = Mathf.LerpAngle(a.TailUp, b.TailUp, k), TailWag = Mathf.LerpAngle(a.TailWag, b.TailWag, k),
            HatLift = Mathf.Lerp(a.HatLift, b.HatLift, k), HatSpin = Mathf.LerpAngle(a.HatSpin, b.HatSpin, k),
            HatTip = Mathf.LerpAngle(a.HatTip, b.HatTip, k),
            Fade = Mathf.Lerp(a.Fade, b.Fade, k), HatFade = Mathf.Lerp(a.HatFade, b.HatFade, k)
        };
    }

    static bool Near(float a, float b, float tolerance)
    {
        return Mathf.Abs(a - b) <= tolerance;
    }
}

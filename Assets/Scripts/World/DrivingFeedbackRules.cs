using UnityEngine;

/// <summary>
/// What a player feels while driving. Every number is here, to tune in playtests:
/// - NewCrossing: whether a touch of a boost pad starts a new crossing. A scooter resting on a pad, or on a row of pads,
///   touches one every physics step; only the first after PAD_GAP seconds off pads counts.
/// - Bursting: whether the speed lines are still at boost strength after a crossing (BURST_SECONDS).
/// - Pitch: how high the pad's sound plays, from the scooter's speed (MIN_PITCH at SLOW metres a second, MAX_PITCH at FAST).
/// - Countdown: the number a respawn countdown shows.
/// BallDriving and Respawn use these; the player on the pad is the only one who feels it.
/// </summary>
public static class DrivingFeedbackRules
{
    /// <summary>Seconds off every pad before a touch is a new crossing</summary>
    public const float PAD_GAP = 0.75f;

    /// <summary>How long the speed lines stay at boost strength after a crossing</summary>
    public const float BURST_SECONDS = 0.5f;

    /// <summary>The pad sound's pitch, at SLOW and at FAST</summary>
    public const float MIN_PITCH = 1f, MAX_PITCH = 1.25f;

    /// <summary>The speeds, in metres a second, the pitch runs between</summary>
    public const float SLOW = 15f, FAST = 40f;

    /// <summary>Whether a touch at a time starts a new crossing, given the last touch (negative: never touched)</summary>
    public static bool NewCrossing(float lastTouch, float now)
    {
        return lastTouch < 0f || now - lastTouch >= PAD_GAP;
    }

    /// <summary>Whether a crossing at a time (negative: never) is still in its burst at a later time</summary>
    public static bool Bursting(float crossedAt, float now)
    {
        return crossedAt >= 0f && now - crossedAt < BURST_SECONDS;
    }

    /// <summary>The pad sound's pitch at a speed: MIN_PITCH up to MAX_PITCH, never outside them</summary>
    public static float Pitch(float speed)
    {
        return Mathf.Lerp(MIN_PITCH, MAX_PITCH, Mathf.InverseLerp(SLOW, FAST, speed));
    }

    /// <summary>
    /// The shortest respawn that is counted down, in seconds. The game's own takes about 2 (the player prefab's wisp and
    /// lift times), which is over before a count could help, so today no numbers show: only the "fell" line. A longer
    /// respawn would be counted
    /// </summary>
    public const float MIN_COUNTED_WAIT = 3f;

    /// <summary>Whether a respawn with this long to go when the rise begins is counted down</summary>
    public static bool Counted(float secondsLeft)
    {
        return secondsLeft >= MIN_COUNTED_WAIT;
    }

    /// <summary>
    /// The number a respawn countdown shows with this long left: the whole seconds, rounded up, at least 1; 0 when none is left
    /// </summary>
    public static int Countdown(float secondsLeft)
    {
        return secondsLeft <= 0f ? 0 : Mathf.Max(1, Mathf.CeilToInt(secondsLeft));
    }
}

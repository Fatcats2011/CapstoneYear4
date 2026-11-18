using UnityEngine;

/// <summary>How serious a rival is to a carrier: not at all, close enough to watch, or boosting straight at them</summary>
public enum ThreatLevel : byte { None, Near, Charging }

/// <summary>Where a warning sits on the screen: none (the rival is in view), at the left or right edge, or behind</summary>
public enum ThreatSide : byte { None, Left, Right, Behind }

/// <summary>The one rival a carrier is warned about: how serious, which side, and how far (RivalWarning)</summary>
public readonly struct Threat
{
    public readonly ThreatLevel Level;
    public readonly ThreatSide Side;
    public readonly float Distance;

    public Threat(ThreatLevel level, ThreatSide side, float distance)
    {
        Level = level;
        Side = side;
        Distance = distance;
    }

    /// <summary>No warning at all: nobody is a threat</summary>
    public static readonly Threat Nothing = new Threat(ThreatLevel.None, ThreatSide.None, float.MaxValue);
}

/// <summary>
/// When a carrier is warned of a rival who could steal from them. Every number is here, to tune in playtests:
/// - Counts: whether a rival can be a threat at all.
/// - Level: how serious one is, from how far away they are and whether they are boosting at the carrier.
/// - Side: which edge of the screen to warn on, from the camera's view. A rival in front is already on screen.
/// - Worse: which of two rivals to warn about.
/// - Alpha: how solid the warning mark is: steady for near, pulsing for charging.
/// RivalWarning reads these; it works them out from what each machine already has, so nothing goes over the network.
/// </summary>
public static class RivalWarningRules
{
    /// <summary>A rival farther than this is no threat, in metres</summary>
    public const float NEAR_METRES = 30f;

    /// <summary>A rival within this angle of the camera's forward is on screen: no mark</summary>
    public const float FRONT_DEGREES = 50f;

    /// <summary>A rival past this angle from the camera's forward is behind. Between the two it is at a side</summary>
    public const float BEHIND_DEGREES = 130f;

    /// <summary>How long one pulse of a charging warning takes</summary>
    public const float PULSE_SECONDS = 0.5f;

    /// <summary>How solid a near warning is, and the dimmest a charging pulse gets (1 is solid)</summary>
    public const float NEAR_ALPHA = 0.45f;

    /// <summary>The least time between two warning rumbles, in seconds</summary>
    public const float RUMBLE_GAP_SECONDS = 2f;

    /// <summary>Below this length a flattened vector has no direction</summary>
    const float MIN_FLAT = 0.001f;

    /// <summary>Whether a rival can be a threat at all: a match is on, steals are on, the carrier holds an order, the rival has room for it, and neither is respawning</summary>
    public static bool Counts(bool playing, bool stealsOn, bool carrierHolds, bool rivalHasRoom, bool eitherRespawning)
    {
        return playing && stealsOn && carrierHolds && rivalHasRoom && !eitherRespawning;
    }

    /// <summary>How serious a rival is: none past NEAR_METRES; charging when boosting and closing in; otherwise near</summary>
    public static ThreatLevel Level(float distance, bool rivalBoosting, bool closing)
    {
        if (distance > NEAR_METRES)
            return ThreatLevel.None;
        return rivalBoosting && closing ? ThreatLevel.Charging : ThreatLevel.Near;
    }

    /// <summary>
    /// Which side of the screen a rival is on, looking along the camera's forward. Height is ignored. None when the rival
    /// is within FRONT_DEGREES (on screen), and when either vector has no flat direction (on top of the carrier, or straight above)
    /// </summary>
    /// <param name="toRival">From the carrier to the rival</param>
    /// <param name="cameraForward">Where the camera looks</param>
    public static ThreatSide Side(Vector3 toRival, Vector3 cameraForward)
    {
        toRival.y = 0f;
        cameraForward.y = 0f;
        if (toRival.magnitude < MIN_FLAT || cameraForward.magnitude < MIN_FLAT)
            return ThreatSide.None;

        float angle = Vector3.Angle(cameraForward, toRival);
        if (angle <= FRONT_DEGREES)
            return ThreatSide.None;
        if (angle >= BEHIND_DEGREES)
            return ThreatSide.Behind;

        // As Compass.GetPosOnCompass: a negative cross is to the left
        return Vector3.Cross(cameraForward, toRival).y < 0f ? ThreatSide.Left : ThreatSide.Right;
    }

    /// <summary>The threat to warn about. One with no level or no side (nothing, or on screen) never beats one with both; otherwise the higher level, then the nearer</summary>
    public static Threat Worse(Threat a, Threat b)
    {
        bool aShows = a.Level != ThreatLevel.None && a.Side != ThreatSide.None;
        bool bShows = b.Level != ThreatLevel.None && b.Side != ThreatSide.None;
        if (aShows != bShows)
            return aShows ? a : b;

        if (a.Level != b.Level)
            return a.Level > b.Level ? a : b;
        return b.Distance < a.Distance ? b : a;
    }

    /// <summary>
    /// How solid the mark is, 0 to 1: none is invisible, near is NEAR_ALPHA, charging pulses between solid and NEAR_ALPHA
    /// over PULSE_SECONDS (solid at age 0). Reduced motion keeps a charging mark solid
    /// </summary>
    /// <param name="age">Seconds since the mark came up at this level</param>
    public static float Alpha(ThreatLevel level, float age, bool reduceMotion)
    {
        switch (level)
        {
            case ThreatLevel.Near:
                return NEAR_ALPHA;
            case ThreatLevel.Charging:
                if (reduceMotion)
                    return 1f;
                float wave = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * age / PULSE_SECONDS);
                return NEAR_ALPHA + (1f - NEAR_ALPHA) * wave;
            default:
                return 0f;
        }
    }
}

using UnityEngine;

/// <summary>
/// What a scooter is doing, packed in one byte: boosting, drifting (which way, and which spark tier), on the ground,
/// phasing through a building. Online, a scooter's own machine sends it with the pose (OnlineScooter), and other machines
/// show the boost trail, skid marks and drift sparks from it (BallDriving.ShowRemote)
/// </summary>
public readonly struct DriveFlags
{
    const int BOOSTING = 1 << 0;
    const int DRIFTING = 1 << 1;
    const int DRIFT_RIGHT = 1 << 2;
    const int TIER_SHIFT = 3;
    const int TIER_MASK = 3 << TIER_SHIFT; // tiers 0-3, in two bits
    const int GROUNDED = 1 << 5;
    const int PHASING = 1 << 6;

    /// <summary>The byte that travels</summary>
    public readonly byte Value;

    public DriveFlags(byte value)
    {
        Value = value;
    }

    public DriveFlags(bool boosting, bool drifting, bool driftRight, int driftTier, bool grounded, bool phasing)
    {
        int value = Mathf.Clamp(driftTier, 0, 3) << TIER_SHIFT;
        if (boosting)
            value |= BOOSTING;
        if (drifting)
            value |= DRIFTING;
        if (driftRight)
            value |= DRIFT_RIGHT;
        if (grounded)
            value |= GROUNDED;
        if (phasing)
            value |= PHASING;
        Value = (byte)value;
    }

    public bool Boosting { get { return (Value & BOOSTING) != 0; } }

    public bool Drifting { get { return (Value & DRIFTING) != 0; } }

    /// <summary>Which way it drifts: right when true, left when false (the sparks come off the other side)</summary>
    public bool DriftRight { get { return (Value & DRIFT_RIGHT) != 0; } }

    /// <summary>The drift's spark tier: 0 (no sparks yet) to 3</summary>
    public int DriftTier { get { return (Value & TIER_MASK) >> TIER_SHIFT; } }

    public bool Grounded { get { return (Value & GROUNDED) != 0; } }

    public bool Phasing { get { return (Value & PHASING) != 0; } }
}

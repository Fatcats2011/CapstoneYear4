using System;
using UnityEngine;

/// <summary>
/// What no honest game sends, refused by every machine: numbers that aren't finite, places outside the world, undefined
/// enum values (another machine's game could send any number), and a scooter pose with a broken rotation. The host checks
/// clients' requests (OnlineOrders, OnlineRespawns); clients check what the host sends where it arrives (OnlineMatch) and
/// the scooters' poses (OnlineDriving). It isn't anti-cheat: a value the rules allow is let through. See
/// docs/online-safety.md
/// </summary>
public static class NetChecks
{
    /// <summary>How far from the world's centre a place can be, on each axis: the maps are a few hundred metres across</summary>
    public const float WORLD_EXTENT = 5000f;

    public static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// <summary>A place inside the world's bounds (finite, within WORLD_EXTENT on each axis)</summary>
    public static bool InWorld(Vector3 place)
    {
        return Within(place.x) && Within(place.y) && Within(place.z);
    }

    /// <summary>One of the enum's own values</summary>
    public static bool Defined<T>(T value) where T : Enum
    {
        return Enum.IsDefined(typeof(T), value);
    }

    /// <summary>Another machine's scooter pose: places in the world, a finite heading, and a real rotation</summary>
    public static bool Sane(ScooterPose pose)
    {
        if (!InWorld(pose.Ball) || !InWorld(pose.ModelPosition) || !Finite(pose.Heading))
            return false;

        Quaternion r = pose.ModelRotation;
        if (!Finite(r.x) || !Finite(r.y) || !Finite(r.z) || !Finite(r.w))
            return false;

        float length = Mathf.Sqrt(r.x * r.x + r.y * r.y + r.z * r.z + r.w * r.w);
        return Mathf.Abs(length - 1f) < 0.01f;
    }

    /// <summary>An order change from the host: its kind, places and heights</summary>
    public static bool Sane(OrderChange change)
    {
        return Defined(change.Kind) && InWorld(change.Spot) && InWorld(change.Spot2)
            && Within(change.Height) && Within(change.Height2);
    }

    static bool Within(float value)
    {
        return Finite(value) && Mathf.Abs(value) <= WORLD_EXTENT;
    }
}

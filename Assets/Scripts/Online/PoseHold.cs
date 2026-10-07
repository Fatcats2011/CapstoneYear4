using UnityEngine;

/// <summary>
/// Whether another machine's scooter proxy follows the poses its owner sends: not while a pose isn't numbers (a modified
/// game's NaN or infinity would make Unity log an error every frame as Netcode applies it), then again once good poses
/// have come for CLEAR_AFTER seconds, which clears Netcode's interpolation of the bad one. Until then the proxy stays at
/// its last good pose. Places far outside the map are fine here: proxies wait parked far below it before the first pose.
/// Used by OwnerNetworkTransform. See docs/online-safety.md
/// </summary>
public class PoseHold
{
    /// <summary>How long after the latest bad pose the proxy follows again, in seconds</summary>
    public const float CLEAR_AFTER = 1f;

    float lastBad = float.NegativeInfinity;

    /// <summary>A pose arrived (now: real time in seconds)</summary>
    public void Received(Vector3 position, Quaternion rotation, float now)
    {
        if (!Numbers(position) || !NetChecks.Finite(rotation.x) || !NetChecks.Finite(rotation.y)
            || !NetChecks.Finite(rotation.z) || !NetChecks.Finite(rotation.w))
            lastBad = now;
    }

    /// <summary>Whether the proxy follows its poses now</summary>
    public bool Following(float now)
    {
        return now - lastBad >= CLEAR_AFTER;
    }

    static bool Numbers(Vector3 v)
    {
        return NetChecks.Finite(v.x) && NetChecks.Finite(v.y) && NetChecks.Finite(v.z);
    }
}

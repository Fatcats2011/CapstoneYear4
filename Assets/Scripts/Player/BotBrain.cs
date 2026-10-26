using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A CPU driver's choices (bots, phase G1 of docs/superpowers/specs/2026-10-23-modern-features-handoff.md, section 2):
/// where to go, how to steer, when to boost. It steers at the next corner of its path to the target (BotDriver asks the
/// Scooter NavMesh for it), boosting lined up on a far corner. BotDriver feeds it the scene
/// </summary>
public static class BotBrain
{
    /// <summary>Full lock this far off the target's bearing, in degrees</summary>
    public const float FULL_LOCK = 45f;
    /// <summary>Boosts only within this many degrees of the target...</summary>
    public const float BOOST_ANGLE = 15f;
    /// <summary>...and further than this, in metres (closer, it would overshoot)</summary>
    public const float BOOST_DISTANCE = 40f;
    /// <summary>The least throttle in the sharpest turn</summary>
    public const float TURN_THROTTLE = 0.4f;
    /// <summary>A path corner this close on the ground (metres) counts as reached</summary>
    public const float CORNER_REACHED = 4f;

    /// <summary>The spot nearest on the ground (height ignored), or -1 with none</summary>
    public static int Nearest(Vector3 from, IReadOnlyList<Vector3> spots)
    {
        int best = -1;
        float bestDistance = float.MaxValue;
        for (int i = 0; i < spots.Count; i++)
        {
            float distance = Flat(spots[i] - from).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// Which path corner to steer at, going on from the one it steered at last (1 for a new path: the first corner is
    /// where the path starts): the first from there not reached yet, else the last. -1 with no corners
    /// </summary>
    public static int NextCorner(Vector3 from, IReadOnlyList<Vector3> corners, int current)
    {
        if (corners.Count == 0)
            return -1;
        int i = Mathf.Clamp(current, 0, corners.Count - 1);
        while (i < corners.Count - 1 && Flat(corners[i] - from).magnitude <= CORNER_REACHED)
            i++;
        return i;
    }

    /// <summary>The bearing to a target from the way the scooter faces, in degrees: negative left, positive right</summary>
    public static float Bearing(Vector3 forward, Vector3 toTarget)
    {
        Vector3 flatForward = Flat(forward), flatTarget = Flat(toTarget);
        if (flatForward == Vector3.zero || flatTarget == Vector3.zero)
            return 0f;
        return Vector3.SignedAngle(flatForward, flatTarget, Vector3.up);
    }

    /// <summary>Steering, -1 to 1, towards a target</summary>
    public static float Steer(Vector3 forward, Vector3 toTarget)
    {
        return Mathf.Clamp(Bearing(forward, toTarget) / FULL_LOCK, -1f, 1f);
    }

    /// <summary>Throttle for how far off the bearing it is (degrees): full ahead, easing in a sharp turn</summary>
    public static float Throttle(float bearing)
    {
        float off = Mathf.Abs(bearing);
        if (off <= FULL_LOCK)
            return 1f;
        return Mathf.Lerp(1f, TURN_THROTTLE, (off - FULL_LOCK) / (180f - FULL_LOCK));
    }

    /// <summary>Whether to boost: lined up (bearing in degrees) on a target far enough away (metres)</summary>
    public static bool Boosts(float bearing, float distance)
    {
        return Mathf.Abs(bearing) <= BOOST_ANGLE && distance > BOOST_DISTANCE;
    }

    static Vector3 Flat(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}

/// <summary>
/// Whether a CPU driver is stuck: under a metre of progress in two seconds (a wall, a kerb, a corner) backs it out for a
/// second, then it drives again
/// </summary>
public class BotStuck
{
    public const float STUCK_AFTER = 2f;
    public const float PROGRESS = 1f;
    public const float REVERSE_FOR = 1f;

    Vector3 since;
    float watched;
    float reversing;
    bool started;

    public bool Reversing => reversing > 0f;

    /// <summary>A frame went by at this position: whether it's backing out now</summary>
    public bool Tick(Vector3 position, float deltaTime)
    {
        if (!started)
        {
            started = true;
            since = position;
            return false;
        }

        if (reversing > 0f)
        {
            reversing -= deltaTime;
            if (reversing <= 0f)
            {
                since = position;
                watched = 0f;
            }
            return Reversing;
        }

        watched += deltaTime;
        if ((position - since).magnitude >= PROGRESS)
        {
            since = position;
            watched = 0f;
        }
        else if (watched >= STUCK_AFTER)
        {
            reversing = REVERSE_FOR;
            watched = 0f;
        }
        return Reversing;
    }
}

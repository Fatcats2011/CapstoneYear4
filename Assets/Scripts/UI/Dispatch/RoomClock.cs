using System;
using UnityEngine;

/// <summary>
/// The Dispatch room's wall clock tells the computer's time. Its hands were modelled at ten to two (Blender's
/// tools/dispatch-room.py), pivoting at the clock's centre: from where they point, the clock works out where twelve and
/// three are, so it's right whichever way the model came in (mirrored or not). Set Up Room Scene wires the hands
/// </summary>
public class RoomClock : MonoBehaviour
{
    const float MODELLED_HOUR = 60f;    // two o'clock, clockwise from twelve
    const float MODELLED_MINUTE = 300f; // ten

    [SerializeField] internal Transform hourHand;
    [SerializeField] internal Transform minuteHand;

    bool learnt;
    Vector3 up, right, face;                 // twelve, three, and the face's normal (clockwise turns about it)
    Vector3 hourBase, minuteBase;            // where each hand points as modelled
    Quaternion hourRotation, minuteRotation; // each hand's rotation as modelled
    int shownSecond = -1;

    /// <summary>The hour hand's angle, clockwise from twelve (degrees)</summary>
    public static float HourAngle(DateTime time)
    {
        return (time.Hour % 12 + time.Minute / 60f + time.Second / 3600f) * 30f;
    }

    /// <summary>The minute hand's angle, clockwise from twelve (degrees)</summary>
    public static float MinuteAngle(DateTime time)
    {
        return (time.Minute + time.Second / 60f) * 6f;
    }

    void Start()
    {
        Learn();
    }

    void Update()
    {
        DateTime now = DateTime.Now;
        if (now.Second == shownSecond)
            return;
        shownSecond = now.Second;
        Show(now);
    }

    /// <summary>Where twelve and three are, from the hands as modelled (call once, before they move)</summary>
    internal void Learn()
    {
        if (hourHand == null || minuteHand == null)
            return;

        hourBase = Pointing(hourHand);
        minuteBase = Pointing(minuteHand);
        hourRotation = hourHand.rotation;
        minuteRotation = minuteHand.rotation;

        // Two and ten mirror each other about twelve
        up = (hourBase + minuteBase).normalized;
        right = (hourBase - minuteBase).normalized;
        face = Vector3.Cross(up, right).normalized;
        learnt = up != Vector3.zero && right != Vector3.zero;
    }

    /// <summary>The hands at a time</summary>
    public void Show(DateTime time)
    {
        if (!learnt)
            return;

        Turn(hourHand, hourRotation, hourBase, HourAngle(time));
        Turn(minuteHand, minuteRotation, minuteBase, MinuteAngle(time));
    }

    void Turn(Transform hand, Quaternion modelled, Vector3 from, float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 to = up * Mathf.Cos(radians) + right * Mathf.Sin(radians);
        hand.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(from, to, face), face) * modelled;
    }

    // Where a hand points: from its pivot to the middle of its mesh
    static Vector3 Pointing(Transform hand)
    {
        Renderer mesh = hand.GetComponentInChildren<Renderer>(true);
        return mesh == null ? Vector3.zero : (mesh.bounds.center - hand.position).normalized;
    }
}

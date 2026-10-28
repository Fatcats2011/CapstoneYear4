using UnityEngine;

/// <summary>
/// One moment of one scooter, as the highlight reel records it: its pose, what it's doing, and the best order it
/// carries
/// </summary>
public struct ReelSample
{
    /// <summary>When it was recorded (the game's clock)</summary>
    public double Time;

    public ScooterPose Pose;

    public DriveFlags Flags;

    /// <summary>The best carried Constants.OrderValue + 1, or 0 for none</summary>
    public byte Carrying;
}

/// <summary>
/// What the highlight reel records through the main round: a fixed ring of timed samples per seat, so it only ever
/// holds the last stretch. Samples are read back at any time between the first and last, interpolated.
/// </summary>
public class ReelRecording
{
    /// <summary>Samples a second, for whatever records into this</summary>
    public const float SAMPLE_RATE = 20f;

    readonly ReelSample[][] rings = new ReelSample[Constants.MAX_PLAYERS][];
    readonly int[] starts = new int[Constants.MAX_PLAYERS]; // where each ring's oldest sample is
    readonly int[] counts = new int[Constants.MAX_PLAYERS];

    public ReelRecording(int capacityPerSeat)
    {
        for (int seat = 0; seat < rings.Length; seat++)
            rings[seat] = new ReelSample[capacityPerSeat];
    }

    /// <summary>
    /// Records a sample. A time that isn't after the seat's last is dropped, and past capacity the oldest goes
    /// </summary>
    public void Add(int seat, ReelSample sample)
    {
        int count = counts[seat];
        if (count > 0 && sample.Time <= Last(seat))
            return;

        ReelSample[] ring = rings[seat];
        if (count < ring.Length)
        {
            ring[(starts[seat] + count) % ring.Length] = sample;
            counts[seat] = count + 1;
        }
        else
        {
            ring[starts[seat]] = sample;
            starts[seat] = (starts[seat] + 1) % ring.Length;
        }
    }

    /// <summary>
    /// The seat's sample at a time, interpolated between the two around it. Its flags and carrying are the earlier
    /// sample's. False when the time is outside what the seat has recorded.
    /// </summary>
    public bool TryGet(int seat, double time, out ReelSample sample)
    {
        sample = default;
        int count = counts[seat];
        if (count == 0 || time < First(seat) || time > Last(seat))
            return false;

        // The last index whose time is <= time
        int low = 0;
        int high = count - 1;
        while (low < high)
        {
            int mid = (low + high + 1) / 2;
            if (At(seat, mid).Time <= time)
                low = mid;
            else
                high = mid - 1;
        }

        ReelSample a = At(seat, low);
        if (low == count - 1 || a.Time == time)
        {
            sample = a;
            return true;
        }

        ReelSample b = At(seat, low + 1);
        float t = (float)((time - a.Time) / (b.Time - a.Time));
        sample = a;
        sample.Time = time;
        sample.Pose = new ScooterPose
        {
            Ball = Vector3.Lerp(a.Pose.Ball, b.Pose.Ball, t),
            Heading = Mathf.LerpAngle(a.Pose.Heading, b.Pose.Heading, t),
            ModelPosition = Vector3.Lerp(a.Pose.ModelPosition, b.Pose.ModelPosition, t),
            ModelRotation = Quaternion.Slerp(a.Pose.ModelRotation, b.Pose.ModelRotation, t),
        };
        return true;
    }

    /// <summary>The time of the seat's oldest sample (0 when it has none)</summary>
    public double First(int seat)
    {
        return counts[seat] == 0 ? 0 : At(seat, 0).Time;
    }

    /// <summary>The time of the seat's newest sample (0 when it has none)</summary>
    public double Last(int seat)
    {
        return counts[seat] == 0 ? 0 : At(seat, counts[seat] - 1).Time;
    }

    /// <summary>Forgets every seat's samples (the ring stays allocated)</summary>
    public void Clear()
    {
        for (int seat = 0; seat < rings.Length; seat++)
        {
            starts[seat] = 0;
            counts[seat] = 0;
        }
    }

    // The seat's nth oldest sample
    ReelSample At(int seat, int n)
    {
        ReelSample[] ring = rings[seat];
        return ring[(starts[seat] + n) % ring.Length];
    }
}

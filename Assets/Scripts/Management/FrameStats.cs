using System;
using System.Globalization;

/// <summary>
/// The frame rate over the latest frames: the average, and the 1% low (the average frame time of the slowest 1% of
/// frames, as frames per second). PerfOverlay feeds it and logs a "Perf:" line. See docs/performance.md
/// </summary>
public class FrameStats
{
    readonly float[] frames; // seconds per frame, a ring
    int next;

    public FrameStats(int capacity = 600)
    {
        frames = new float[Math.Max(1, capacity)];
    }

    /// <summary>How many frames are kept now (up to the capacity)</summary>
    public int Count { get; private set; }

    /// <summary>A frame's time, in seconds</summary>
    public void Add(float seconds)
    {
        frames[next] = seconds;
        next = (next + 1) % frames.Length;
        if (Count < frames.Length)
            Count++;
    }

    /// <summary>Frames per second over the kept frames (0 with none)</summary>
    public float AverageFps
    {
        get
        {
            float total = 0f;
            for (int i = 0; i < Count; i++)
                total += frames[i];
            return total > 0f ? Count / total : 0f;
        }
    }

    /// <summary>Frames per second of the slowest 1% of the kept frames (at least one frame; 0 with none)</summary>
    public float OnePercentLowFps
    {
        get
        {
            if (Count == 0)
                return 0f;

            float[] sorted = new float[Count];
            Array.Copy(frames, sorted, Count);
            Array.Sort(sorted);
            int slowest = Math.Max(1, Count / 100);
            float total = 0f;
            for (int i = Count - slowest; i < Count; i++)
                total += sorted[i];
            return total > 0f ? slowest / total : 0f;
        }
    }

    /// <summary>The line logged every 30 s, which the opt-in session log keeps</summary>
    public static string Line(float averageFps, float lowFps, int players, int cameras)
    {
        return string.Format(CultureInfo.InvariantCulture, "Perf: {0} players, {1:0} fps average, {2:0} fps 1% low, {3} cameras",
            players, averageFps, lowFps, cameras);
    }
}

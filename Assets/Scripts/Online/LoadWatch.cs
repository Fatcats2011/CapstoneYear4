using System.Globalization;

/// <summary>
/// Times a match load on this machine, for the log: how long until the scene was ready here (loaded and held), and the
/// longest frame from the load's start until just after the scene showed. A frame is the time between two ticks, so the
/// frames the scene shows in count too (FRAMES_AFTER_UP). That's how to time a build on a slow PC: a machine whose frame
/// takes longer than its session's timeout is dropped (OnlineSession.DIRECT_DISCONNECT_MS). OnlineSceneFlow runs it
/// </summary>
public class LoadWatch
{
    /// <summary>The ticks after the scene shows that still count: its activation's frame, then its scripts' Start's</summary>
    public const int FRAMES_AFTER_UP = 2;

    MatchScene scene;
    float started;  // when the load started
    float ready;    // when the held scene was loaded
    float lastTick; // the last tick, or the start
    float longest;  // the longest frame so far
    bool up;        // the scene is shown
    int ticksAfterUp;

    /// <summary>Whether a load is being timed</summary>
    public bool Running { get; private set; }

    /// <summary>A load starts: timing starts over</summary>
    public void Start(MatchScene loading, float now)
    {
        scene = loading;
        started = ready = lastTick = now;
        longest = 0f;
        up = false;
        ticksAfterUp = 0;
        Running = true;
    }

    /// <summary>The held scene is loaded here, ready to show</summary>
    public void Ready(float now)
    {
        ready = now;
    }

    /// <summary>The scene is shown</summary>
    public void Up()
    {
        up = true;
    }

    /// <summary>
    /// Called every frame: the time since the last tick (or the start) is a frame. Gives the report on the
    /// FRAMES_AFTER_UP-th tick after Up, once, and stops; null otherwise, and while nothing is timed
    /// </summary>
    public string Tick(float now)
    {
        if (!Running)
            return null;

        float frame = now - lastTick;
        lastTick = now;
        if (frame > longest)
            longest = frame;

        if (!up || ++ticksAfterUp < FRAMES_AFTER_UP)
            return null;

        Running = false;
        return Report(scene, ready - started, longest);
    }

    /// <summary>Stops timing without a report (the session ended)</summary>
    public void Stop()
    {
        Running = false;
    }

    /// <summary>The log line for a load: the same in every language Windows runs in</summary>
    public static string Report(MatchScene scene, float readySeconds, float longestFrame)
    {
        return "Online: the " + scene + " scene was ready here after " + readySeconds.ToString("0.0", CultureInfo.InvariantCulture)
            + " s; its longest frame took " + longestFrame.ToString("0.0", CultureInfo.InvariantCulture) + " s";
    }
}

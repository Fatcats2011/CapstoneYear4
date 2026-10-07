/// <summary>
/// The boost's wide view: asked for every frame while boosting, it widens the view a delay after the boost starts, and
/// keeps it wide for the same delay after the last ask. It replaced a coroutine started every frame (each setting the
/// view wide after the delay), with the same timing. See docs/performance.md
/// </summary>
public class FovHold
{
    readonly float delay;
    float since = float.NegativeInfinity;      // when this run of asks began
    float lastAsked = float.NegativeInfinity; // the latest ask

    public FovHold(float delay)
    {
        this.delay = delay;
    }

    /// <summary>Asks for the wide view now (each frame while boosting)</summary>
    public void Request(float now)
    {
        // A new run begins once the last one's wide view is over
        if (now > lastAsked + delay)
            since = now;
        lastAsked = now;
    }

    /// <summary>Whether the view should be wide now</summary>
    public bool Active(float now)
    {
        return now >= since + delay - 1e-4f && now <= lastAsked + delay;
    }
}

using System;

/// <summary>
/// A client's tutorial messages on their way out of the game code (online): its player finished the tutorial, or
/// boosted into their lane's cutout. OnlineTutorial sends them to the host. Offline and on the host nothing listens:
/// those machines act at once. See docs/online.md
/// </summary>
public static class TutorialSync
{
    /// <summary>Online client: its player in this seat finished the tutorial</summary>
    public static event Action<int> Finished;

    /// <summary>Online client: its player boosted into the cutout of this seat's lane</summary>
    public static event Action<int> CutoutAsked;

    /// <summary>
    /// Online client: its player in a seat finished the tutorial: the host counts them
    /// </summary>
    public static void Finish(int seat)
    {
        Finished?.Invoke(seat);
    }

    /// <summary>
    /// Online client: its player boosted into a lane's cutout: the host hands out the cutout's order
    /// </summary>
    public static void AskCutout(int seat)
    {
        CutoutAsked?.Invoke(seat);
    }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        Finished = null;
        CutoutAsked = null;
    }
}

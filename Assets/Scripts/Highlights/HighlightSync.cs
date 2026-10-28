using System;

/// <summary>
/// The highlight reel's list on its way between the game code and the network: the host's director shares it
/// (OnlineHighlights sends it), and a client's director plays what arrives (HighlightDirector)
/// </summary>
public static class HighlightSync
{
    /// <summary>The rules' machine has its reel list: the host shares it (OnlineHighlights)</summary>
    public static event Action<ReelList> Shared;

    /// <summary>Client: the host's reel list arrived</summary>
    public static event Action<ReelList> Arrived;

    public static void Share(ReelList list)
    {
        Shared?.Invoke(list);
    }

    public static void Arrive(ReelList list)
    {
        Arrived?.Invoke(list);
    }

    /// <summary>Forgets every listener (tests, a new Play Mode session)</summary>
    public static void Reset()
    {
        Shared = null;
        Arrived = null;
    }
}

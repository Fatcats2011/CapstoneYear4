using System;

/// <summary>
/// One-shots on their way out of the game code (online):
/// - This machine's player made a one-shot: their scooter's sound pool played it, or they rise from a grave (Played).
/// - This machine's match clock rang: it runs only where game states are decided (Rang).
/// OnlineCues sends them to the other machines. Offline nothing listens. See docs/online.md
/// </summary>
public static class CueSync
{
    /// <summary>Online: this machine's player (their seat) made a one-shot</summary>
    public static event Action<int, ScooterCue> Played;

    /// <summary>This machine's match clock rang (it runs only where states are decided: offline, or on the host)</summary>
    public static event Action<ClockCue> Rang;

    /// <summary>
    /// This machine's player (their seat) made a one-shot: online, the other machines play it on their scooter
    /// </summary>
    public static void Play(int seat, ScooterCue cue)
    {
        Played?.Invoke(seat, cue);
    }

    /// <summary>
    /// This machine's match clock rang: online, every client hears it
    /// </summary>
    public static void Ring(ClockCue cue)
    {
        Rang?.Invoke(cue);
    }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        Played = null;
        Rang = null;
    }
}

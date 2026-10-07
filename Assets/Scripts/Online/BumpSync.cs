using System;

/// <summary>
/// A machine's bumps on their way out of the game code (online): its player drove into another machine's scooter, which
/// is kinematic here, so only the host can pass the push on (BumpRules). OnlineBumps listens. Offline nothing listens:
/// a local match's scooters push each other by physics. See docs/online.md
/// </summary>
public static class BumpSync
{
    /// <summary>Online: this machine's player (the bumper's seat) bumped another machine's (the victim's seat) at a closing speed</summary>
    public static event Action<int, int, float> Reported;

    /// <summary>
    /// Online: this machine's player bumped another machine's player at a closing speed, in m/s
    /// </summary>
    public static void Report(int bumper, int victim, float speed)
    {
        Reported?.Invoke(bumper, victim, speed);
    }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        Reported = null;
    }
}

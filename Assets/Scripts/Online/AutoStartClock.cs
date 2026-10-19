using System;
using System.Collections.Generic;

/// <summary>
/// Host: when an open lobby starts by itself (MatchRules.AutoStartAt), in server time.
/// - The count restarts only when a machine sits down for the first time in this hosted session. Its seats asked for or
///   given back, or the machine leaving and coming back, don't restart it: join and leave spam can't put the start off.
/// - A count that begins (Fill turned on, or back in player select) runs from then.
/// - A count that stopped as the lobby dropped below two machines runs on from the last first seat when it resumes,
///   unless that start has passed: then it runs from then, so a stale start doesn't fire at once (or never).
/// Plain C#, so it's tested without a network. OnlineGame runs it. See docs/online.md
/// </summary>
public class AutoStartClock
{
    readonly HashSet<ulong> seen = new HashSet<ulong>(); // the machines that have sat down in this session
    double countFrom;   // when the last first seat was taken, or the count last began
    bool wasOpen;       // last frame, the lobby was open to anyone in player select
    double at;          // the start given last frame (0 = none)

    /// <summary>
    /// A machine's player sat down. machine is who it is for the whole session (OnlineSession.MachineOf): only its
    /// first seat restarts the count
    /// </summary>
    public void Seated(ulong machine, double now)
    {
        if (seen.Add(machine))
            countFrom = now;
    }

    /// <summary>
    /// Every frame: when the lobby starts by itself, or 0 when it doesn't
    /// </summary>
    /// <param name="open">Whether the lobby is open to anyone (Fill) and the host is in player select</param>
    /// <param name="machinesSeated">How many machines have a player seated, the host's included (two or more count)</param>
    public double StartAt(bool open, int machinesSeated, double now)
    {
        if (open && !wasOpen)
            countFrom = Math.Max(countFrom, now);
        wasOpen = open;

        if (!open || machinesSeated < 2)
        {
            at = 0;
            return 0;
        }

        double next = MatchRules.AutoStartAt(countFrom);
        if (at == 0 && next <= now)
        {
            countFrom = now;
            next = MatchRules.AutoStartAt(now);
        }
        at = next;
        return at;
    }

    /// <summary>
    /// The session ended: the next one's server time starts again, and every machine is new to it
    /// </summary>
    public void Reset()
    {
        seen.Clear();
        countFrom = 0;
        wasOpen = false;
        at = 0;
    }
}

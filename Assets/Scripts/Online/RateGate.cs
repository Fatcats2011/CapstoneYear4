using System;
using System.Collections.Generic;

/// <summary>
/// The requests a client may send the host (OnlineMatch's ServerRpcs)
/// </summary>
public enum RpcKind { Cue, Drop, Steal, Respawn, Learnt, Cutout, Loaded, Bump, Seat }

/// <summary>
/// Host: how many requests each machine may send, per kind, as a token bucket: a burst, refilled over time. An honest
/// game sends far below these; the excess is dropped, and a machine that keeps flooding (KICK_DROPS within
/// KICK_WINDOW seconds) is to be disconnected: that's a modified game, not lag. See docs/online-safety.md
/// </summary>
public class RateGate
{
    public const int KICK_DROPS = 200;
    public const double KICK_WINDOW = 10;

    readonly Func<double> clock;
    readonly Dictionary<(ulong, RpcKind), Bucket> buckets = new Dictionary<(ulong, RpcKind), Bucket>();
    readonly Dictionary<ulong, Queue<double>> drops = new Dictionary<ulong, Queue<double>>();

    class Bucket
    {
        public double Tokens;
        public double Time;
    }

    /// <param name="clock">Real time in seconds</param>
    public RateGate(Func<double> clock)
    {
        this.clock = clock;
    }

    /// <summary>A kind's burst and refill per second</summary>
    public static void Allowance(RpcKind kind, out int burst, out double perSecond)
    {
        switch (kind)
        {
            case RpcKind.Cue:
                burst = 20;
                perSecond = 10;
                break;
            case RpcKind.Drop:
            case RpcKind.Steal:
            case RpcKind.Respawn:
            case RpcKind.Bump:
                burst = 10;
                perSecond = 5;
                break;
            default:
                burst = 5;
                perSecond = 1;
                break;
        }
    }

    /// <summary>
    /// Whether a request is let through; one that isn't counts towards disconnecting its sender. A machine with several
    /// players (sharing its screen) gets each allowance that many times over
    /// </summary>
    public bool Allow(ulong sender, RpcKind kind, int players = 1)
    {
        double now = clock();
        Allowance(kind, out int burst, out double perSecond);
        // A machine with several players (sharing its screen) sends several players' requests
        players = Math.Max(1, players);
        burst *= players;
        perSecond *= players;

        if (!buckets.TryGetValue((sender, kind), out Bucket bucket))
        {
            bucket = new Bucket { Tokens = burst, Time = now };
            buckets[(sender, kind)] = bucket;
        }

        bucket.Tokens = Math.Min(burst, bucket.Tokens + (now - bucket.Time) * perSecond);
        bucket.Time = now;
        if (bucket.Tokens >= 1)
        {
            bucket.Tokens -= 1;
            return true;
        }

        if (!drops.TryGetValue(sender, out Queue<double> times))
        {
            times = new Queue<double>();
            drops[sender] = times;
        }
        times.Enqueue(now);
        return false;
    }

    /// <summary>
    /// Whether a sender has had KICK_DROPS requests dropped within the last KICK_WINDOW seconds
    /// </summary>
    public bool ShouldKick(ulong sender)
    {
        if (!drops.TryGetValue(sender, out Queue<double> times))
            return false;

        double now = clock();
        while (times.Count > 0 && now - times.Peek() > KICK_WINDOW)
            times.Dequeue();
        return times.Count >= KICK_DROPS;
    }

    /// <summary>
    /// A sender left: their allowances and drops are forgotten
    /// </summary>
    public void Forget(ulong sender)
    {
        drops.Remove(sender);
        foreach (RpcKind kind in Enum.GetValues(typeof(RpcKind)))
            buckets.Remove((sender, kind));
    }
}

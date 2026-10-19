using System;
using System.Collections.Generic;

/// <summary>
/// A stranger connecting and dropping over and over (now that strangers can join a public lobby): the host refuses a
/// Steam ID that has been let in more than CHURN_LIMIT times within CHURN_WINDOW, and keeps refusing it for the rest of the
/// session (this object's life; a new hosting session makes a new one). OnlineLobby.LetsIn asks once per accepted
/// connection. At most MAX_TRACKED Steam IDs are remembered, so it can't grow without bound: at the cap the oldest ID that
/// isn't refused is dropped to make room (a refused ID is dropped only when every tracked ID is refused, oldest first).
/// See docs/online-safety.md
/// </summary>
public class JoinChurn
{
    /// <summary>Connections one Steam ID may make within CHURN_WINDOW; the next one is refused</summary>
    public const int CHURN_LIMIT = 5;

    /// <summary>The span connections are counted over, in seconds</summary>
    public const double CHURN_WINDOW = 60.0;

    /// <summary>How many Steam IDs' histories are kept</summary>
    public const int MAX_TRACKED = 64;

    class History
    {
        public readonly Queue<double> Times = new Queue<double>();
        public bool Refused;
    }

    readonly Func<double> clock;
    readonly Dictionary<ulong, History> histories = new Dictionary<ulong, History>();
    readonly List<ulong> order = new List<ulong>();   // oldest first

    /// <summary>How many Steam IDs are remembered (at most MAX_TRACKED)</summary>
    internal int Tracked => histories.Count;

    /// <param name="clock">Seconds, never going back (the game passes realtime)</param>
    public JoinChurn(Func<double> clock)
    {
        this.clock = clock;
    }

    /// <summary>
    /// Records an accepted connection from a Steam ID. False once it has made more than CHURN_LIMIT within CHURN_WINDOW,
    /// and from then on for the rest of the session
    /// </summary>
    public bool Allow(ulong steamId)
    {
        if (!histories.TryGetValue(steamId, out History history))
        {
            MakeRoom();
            history = new History();
            histories[steamId] = history;
            order.Add(steamId);
        }

        if (history.Refused)
            return false;

        double now = clock();
        while (history.Times.Count > 0 && now - history.Times.Peek() >= CHURN_WINDOW)
            history.Times.Dequeue();
        history.Times.Enqueue(now);

        if (history.Times.Count > CHURN_LIMIT)
        {
            history.Refused = true;
            history.Times.Clear();
            return false;
        }

        return true;
    }

    // The oldest ID that isn't refused goes first; if all are refused, the oldest of them
    void MakeRoom()
    {
        if (histories.Count < MAX_TRACKED)
            return;

        int drop = 0;
        for (int i = 0; i < order.Count; i++)
        {
            if (!histories[order[i]].Refused)
            {
                drop = i;
                break;
            }
        }

        histories.Remove(order[drop]);
        order.RemoveAt(drop);
    }
}

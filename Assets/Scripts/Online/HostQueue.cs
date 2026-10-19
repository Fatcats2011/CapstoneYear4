using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Client: the host's messages (its states, order changes, hits and respawn points), in the order they came. While this machine's scene
/// changes they wait, so the new scene's objects hear them, then go in order. A message that comes while others still
/// wait goes behind them. One that throws is logged and doesn't hold up the rest. At most MAX_WAITING wait (another
/// machine's host could hold the change for good): the rest are dropped, with one warning
/// </summary>
public class HostQueue
{
    readonly Func<bool> holding;
    readonly Queue<Action> waiting = new Queue<Action>();

    /// <summary>The most messages that wait</summary>
    public const int MAX_WAITING = 1024;

    /// <param name="holding">Whether messages wait now (this machine's scene is changing)</param>
    public HostQueue(Func<bool> holding)
    {
        this.holding = holding;
    }

    /// <summary>How many messages wait</summary>
    public int Waiting { get { return waiting.Count; } }

    /// <summary>
    /// A message from the host: it goes now, unless messages wait (the scene is changing, or others came first)
    /// </summary>
    public void Add(Action message)
    {
        if (holding() || waiting.Count > 0)
        {
            // Another machine's host could hold this machine's scene change for good and keep sending
            if (waiting.Count >= MAX_WAITING)
            {
                WarnDropped();
                return;
            }
            waiting.Enqueue(message);
            return;
        }
        Run(message);
    }

    /// <summary>
    /// The scene is up: the waiting messages go, in order, as long as nothing holds them again
    /// </summary>
    public void Release()
    {
        while (waiting.Count > 0 && !holding())
            Run(waiting.Dequeue());
    }

    /// <summary>
    /// Forgets the waiting messages (the session ended)
    /// </summary>
    public void Clear()
    {
        waiting.Clear();
    }

    bool warnedDropped;

    // Logged once per queue: the host sent more than a scene change holds
    void WarnDropped()
    {
        if (warnedDropped)
            return;

        warnedDropped = true;
        Debug.LogWarning("Online: the host sent more messages than a scene change holds; the rest were dropped.");
    }

    static void Run(Action message)
    {
        try
        {
            message();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
    }
}

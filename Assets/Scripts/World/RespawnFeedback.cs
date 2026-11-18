using System;
using UnityEngine;

/// <summary>
/// Tells this machine that a player it drives went in the water, how long until they drive again, and that they do, so
/// that player's callout can say so (StealCallout). Respawn says it; each machine does this for the players it drives and
/// nothing is sent. Players go by their seat
/// </summary>
public static class RespawnFeedback
{
    /// <summary>A player went in the water: their seat</summary>
    public static event Action<int> Fell;

    /// <summary>A player's respawn began its rise: their seat, and the seconds until they drive again</summary>
    public static event Action<int, float> Rising;

    /// <summary>A player drives again, or their respawn was stopped: their seat</summary>
    public static event Action<int> Back;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        Reset();
    }

    /// <summary>Forgets every subscriber</summary>
    internal static void Reset()
    {
        Fell = null;
        Rising = null;
        Back = null;
    }

    /// <summary>A player went in the water</summary>
    public static void AnnounceFell(int seat)
    {
        Fell?.Invoke(seat);
    }

    /// <summary>A player's respawn began its rise, with this long until they drive again</summary>
    public static void AnnounceRising(int seat, float secondsLeft)
    {
        Rising?.Invoke(seat, secondsLeft);
    }

    /// <summary>A player drives again, or their respawn was stopped</summary>
    public static void AnnounceBack(int seat)
    {
        Back?.Invoke(seat);
    }
}

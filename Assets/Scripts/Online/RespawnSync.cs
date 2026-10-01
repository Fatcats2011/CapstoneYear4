using System;
using UnityEngine;

/// <summary>
/// A client's respawn requests on their way out of the game code (online): its player fell in the water. The host picks
/// where they rise, for every player, so no two rise on one point, and drops their orders there. OnlineRespawns sends
/// the requests to the host. Offline and on the host nothing listens: those machines pick at once. See docs/online.md
/// </summary>
public static class RespawnSync
{
    /// <summary>Online client: its player in this seat fell in the water, last on the ground here</summary>
    public static event Action<int, Vector3> Asked;

    /// <summary>
    /// Online client: its player in a seat fell in the water, last on the ground there: the host picks where they rise
    /// </summary>
    public static void Ask(int seat, Vector3 lastGrounded)
    {
        Asked?.Invoke(seat, lastGrounded);
    }

    /// <summary>
    /// Forgets every listener (tests)
    /// </summary>
    public static void Reset()
    {
        Asked = null;
    }
}

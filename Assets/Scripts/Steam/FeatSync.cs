using System;

/// <summary>
/// Deliveries on their way out of the game code, for achievements: raised only by the machine that runs the rules
/// (this PC offline, the host online), never by a client's replay of the host's. SteamFeatures counts them (MatchFeats).
/// See docs/steam/in-game-features.md
/// </summary>
public static class FeatSync
{
    /// <summary>A player (their seat) delivered an order, and whether it was the golden order</summary>
    public static event Action<int, bool> Delivered;

    /// <summary>
    /// A player (their seat) delivered an order
    /// </summary>
    public static void Deliver(int seat, bool golden)
    {
        Delivered?.Invoke(seat, golden);
    }

    /// <summary>
    /// Forgets every listener (a new Play Mode session)
    /// </summary>
    public static void Reset()
    {
        Delivered = null;
    }
}

using System;

/// <summary>
/// What players do, on its way out of the game code, for achievements. SteamFeatures counts them (MatchFeats). See
/// docs/steam/in-game-features.md
/// - Deliveries, steals and tutorial finishes are raised only by the machine that runs the rules (this PC offline, the
///   host online), never by a client's replay of the host's.
/// - Falls in the water are raised by the machine driving the player, where they unlock.
/// </summary>
public static class FeatSync
{
    /// <summary>A player (their seat) delivered an order, and what it was worth</summary>
    public static event Action<int, Constants.OrderValue> Delivered;

    /// <summary>A player (their seat) stole an order, and whether it was the golden order</summary>
    public static event Action<int, bool> Stole;

    /// <summary>A player (their seat) finished the tutorial</summary>
    public static event Action<int> Learnt;

    /// <summary>A player driven on this machine fell in the water</summary>
    public static event Action Fell;

    /// <summary>
    /// A player (their seat) delivered an order
    /// </summary>
    public static void Deliver(int seat, Constants.OrderValue value)
    {
        Delivered?.Invoke(seat, value);
    }

    /// <summary>
    /// A player (their seat) stole an order
    /// </summary>
    public static void Steal(int seat, bool golden)
    {
        Stole?.Invoke(seat, golden);
    }

    /// <summary>
    /// A player (their seat) finished the tutorial
    /// </summary>
    public static void Learn(int seat)
    {
        Learnt?.Invoke(seat);
    }

    /// <summary>
    /// A player driven on this machine fell in the water
    /// </summary>
    public static void Fall()
    {
        Fell?.Invoke();
    }

    /// <summary>
    /// Forgets every listener (a new Play Mode session)
    /// </summary>
    public static void Reset()
    {
        Delivered = null;
        Stole = null;
        Learnt = null;
        Fell = null;
    }
}

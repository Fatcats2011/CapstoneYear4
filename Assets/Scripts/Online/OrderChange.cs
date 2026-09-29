using Unity.Netcode;
using UnityEngine;

/// <summary>
/// What an order change does (OrderChange)
/// </summary>
public enum OrderChangeKind : byte { Spawn, Pickup, Deliver, Drop, Erase, EraseGold }

/// <summary>
/// One change to an order, as it travels online. The host makes it, and every client replays it through the same game
/// code (OnlineOrders). Orders go by their key (OrderBook), players by their seat
/// </summary>
public struct OrderChange : INetworkSerializable
{
    public OrderChangeKind Kind;
    /// <summary>The order's key (OrderBook.NONE when none)</summary>
    public int Order;
    /// <summary>Pickup, Deliver, Drop: the player's seat; -1 otherwise</summary>
    public int Seat;
    /// <summary>Spawn: the order joins the order manager's active orders (InitOrder's shouldAdd). Drop: the player spins out</summary>
    public bool Flag;
    /// <summary>Drop: the order in the second slot (OrderBook.NONE when it's empty)</summary>
    public int Order2;
    /// <summary>Drop: where the first order lands</summary>
    public Vector3 Spot;
    /// <summary>Drop: how high the first order flies first</summary>
    public float Height;
    /// <summary>Drop: where the second order lands</summary>
    public Vector3 Spot2;
    /// <summary>Drop: how high the second order flies first</summary>
    public float Height2;

    /// <summary>The order appears at its pickup</summary>
    public static OrderChange Spawn(int order, bool addToActive)
    {
        return new OrderChange { Kind = OrderChangeKind.Spawn, Order = order, Seat = -1, Flag = addToActive };
    }

    /// <summary>A player picks the order up</summary>
    public static OrderChange Pickup(int order, int seat)
    {
        return new OrderChange { Kind = OrderChangeKind.Pickup, Order = order, Seat = seat };
    }

    /// <summary>A player delivers the order</summary>
    public static OrderChange Deliver(int order, int seat)
    {
        return new OrderChange { Kind = OrderChangeKind.Deliver, Order = order, Seat = seat };
    }

    /// <summary>A player drops what they hold: each order flies up and lands on its spot</summary>
    public static OrderChange Drop(int seat, int order1, Vector3 spot1, float height1, int order2, Vector3 spot2, float height2, bool spinOut)
    {
        return new OrderChange
        {
            Kind = OrderChangeKind.Drop, Seat = seat, Flag = spinOut,
            Order = order1, Spot = spot1, Height = height1,
            Order2 = order2, Spot2 = spot2, Height2 = height2
        };
    }

    /// <summary>The order goes back to the pool</summary>
    public static OrderChange Erase(int order)
    {
        return new OrderChange { Kind = OrderChangeKind.Erase, Order = order, Seat = -1 };
    }

    /// <summary>The golden order goes without being delivered</summary>
    public static OrderChange EraseGold(int order)
    {
        return new OrderChange { Kind = OrderChangeKind.EraseGold, Order = order, Seat = -1 };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Kind);
        serializer.SerializeValue(ref Order);
        serializer.SerializeValue(ref Seat);
        serializer.SerializeValue(ref Flag);
        serializer.SerializeValue(ref Order2);
        serializer.SerializeValue(ref Spot);
        serializer.SerializeValue(ref Height);
        serializer.SerializeValue(ref Spot2);
        serializer.SerializeValue(ref Height2);
    }

    public override string ToString()
    {
        switch (Kind)
        {
            case OrderChangeKind.Spawn:
                return "Spawn " + Order + (Flag ? " (active)" : "");
            case OrderChangeKind.Pickup:
            case OrderChangeKind.Deliver:
                return Kind + " " + Order + " seat " + Seat;
            case OrderChangeKind.Drop:
                return "Drop seat " + Seat + ": " + Order + " at " + Spot + " from " + Height + ", " + Order2 + " at " + Spot2 + " from " + Height2
                    + (Flag ? ", spins out" : "");
            default:
                return Kind + " " + Order;
        }
    }
}

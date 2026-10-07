using UnityEngine;

/// <summary>
/// The session's orders on this machine (online):
/// - Host: each change it makes to an order goes to every client (OrderSync.Changed, OnlineMatch.SendOrder). A client's
///   drop request (its player fell in the water) drops that client's own player's orders, for everyone. Every player's
///   score and the golden order's value go out as they change.
/// - Client: each of the host's changes replays here through the same game code (Show; OnlineGame queues them with the
///   host's states). Its player's drops go to the host. Scores and the golden order's value are the host's.
/// OnlineGame adds it next to OnlineDriving. See docs/online.md
/// </summary>
public class OnlineOrders : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose drop requests the host hears

    /// <summary>
    /// Starts sharing this session's orders
    /// </summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        OrderSync.Changed += SendChange;
        OrderSync.DropAsked += AskHostToDrop;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // OrderSync's events are static: an ended session never keeps listening
    void OnDestroy()
    {
        OrderSync.Changed -= SendChange;
        OrderSync.DropAsked -= AskHostToDrop;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.DropAsked += DropFor;
    }

    void StopListening()
    {
        if (listening != null)
            listening.DropAsked -= DropFor;
        listening = null;
    }

    /// <summary>
    /// Client: replays one of the host's changes here. A change for an order that isn't here (its scene went) or a
    /// player who isn't (they left) is dropped
    /// </summary>
    public void Show(OrderChange change)
    {
        Order order = OrderBook.Find(change.Order);
        OrderHandler handler = OrderSync.HandlerIn(change.Seat);
        OrderHandler victim = OrderSync.HandlerIn(change.Seat2); // a steal's
        if (!CanShow(change.Kind, order, handler, victim))
            return;

        // A fall still landing or a throw still flying ends first (the throw's own erase waits for the host's)
        if (order != null)
            order.FinishMoves();
        Order second = OrderBook.Find(change.Order2);
        if (second != null)
            second.FinishMoves();

        OrderSync.Show(() => Replay(change, order, handler, victim));
    }

    static bool CanShow(OrderChangeKind kind, Order order, OrderHandler handler, OrderHandler victim)
    {
        switch (kind)
        {
            case OrderChangeKind.Pickup:
            case OrderChangeKind.Deliver:
                return order != null && handler != null;
            case OrderChangeKind.Steal:
                return order != null && handler != null && victim != null;
            case OrderChangeKind.Drop:
                return handler != null;
            default:
                return order != null;
        }
    }

    static void Replay(OrderChange change, Order order, OrderHandler handler, OrderHandler victim)
    {
        switch (change.Kind)
        {
            case OrderChangeKind.Spawn:
                order.InitOrder(change.Flag);
                break;
            case OrderChangeKind.Pickup:
                handler.AddOrder(order);
                break;
            case OrderChangeKind.Steal:
                handler.TakeOrderFrom(victim, order);
                break;
            case OrderChangeKind.Deliver:
                handler.DeliverOrder(order);
                break;
            case OrderChangeKind.Drop:
                handler.DropHeld(change.Spot, change.Height, change.Spot2, change.Height2, change.Flag);
                break;
            case OrderChangeKind.Erase:
                order.EraseOrder();
                break;
            case OrderChangeKind.EraseGold:
                order.EraseGoldWithoutDelivering();
                break;
        }
    }

    // Host: each change it makes to an order goes to every client
    void SendChange(OrderChange change)
    {
        OnlineMatch match = session.Match;
        if (match != null && match.IsServer)
            match.SendOrder(change);
    }

    // Client: its player fell in the water holding orders: the host drops them, for everyone
    void AskHostToDrop(OrderHandler handler, Vector3 spot1, Vector3 spot2, bool spinOut)
    {
        OnlineMatch match = session.Match;
        int seat = OrderSync.SeatOf(handler);
        if (match != null && !match.IsServer && seat >= 0)
            match.AskDrop(seat, spot1, spot2, spinOut);
    }

    // Host: a client's player fell in the water holding orders. They drop where the client says, from heights the host
    // picks, and every machine sees it. A machine only drops its own player's orders
    /// <summary>
    /// Host: whether a client's drop request names places in the world (NetChecks): another machine could send any
    /// number, and a drop at none would lose the orders for everyone
    /// </summary>
    public static bool AcceptsDrop(Vector3 spot1, Vector3 spot2)
    {
        return NetChecks.InWorld(spot1) && NetChecks.InWorld(spot2);
    }

    void DropFor(ulong machine, int seat, Vector3 spot1, Vector3 spot2, bool spinOut)
    {
        if (!session.Owns(machine, seat) || !AcceptsDrop(spot1, spot2))
            return;

        OrderHandler handler = OrderSync.HandlerIn(seat);
        if (handler != null)
            handler.DropEverything(spot1, spot2, spinOut);
    }

    void Update()
    {
        OnlineMatch match = session.Match;
        if (match == null || !session.IsRunning)
            return;

        if (match.IsServer)
            ShareScores(match);
        else
            FollowScores(match);
    }

    // Host: scores are the host's, and each player's goes out on their OnlinePlayer. The golden order's value grows here
    // during the golden round
    void ShareScores(OnlineMatch match)
    {
        foreach (OnlinePlayer player in session.Players)
        {
            OrderHandler handler = OrderSync.HandlerIn(player.Seat);
            if (handler != null)
                player.ShareScore(handler.Score);
        }

        if (OrderManager.Instance != null)
            match.ShareGoldenValue(OrderManager.Instance.FinalOrderValue);
    }

    // Client: the host's scores show in each player's HUD, with the placings, and the golden order is worth what the
    // host says
    void FollowScores(OnlineMatch match)
    {
        bool changed = false;
        foreach (OnlinePlayer player in session.Players)
        {
            OrderHandler handler = OrderSync.HandlerIn(player.Seat);
            if (handler != null && handler.Score != player.Score)
            {
                handler.ShowHostScore(player.Score);
                changed = true;
            }
        }
        if (changed && ScoreManager.Instance != null)
            ScoreManager.Instance.UpdatePlacement();

        if (OrderManager.Instance != null)
            OrderManager.Instance.FinalOrderValue = match.GoldenValue;
    }
}

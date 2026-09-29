using System;
using UnityEngine;

/// <summary>
/// Who may change orders, and the host's changes going out (online):
/// - Offline and on the host, orders change as they always have.
/// - On a client they only change while it replays one of the host's changes (Show). Its own beacons, spawner and
///   respawns wait for the host.
/// - On an online host each change goes out once (Changed), as the outermost change. What it does on the way (a delivery
///   erasing its order) is part of it, and happens on every client when the change replays there.
/// OnlineOrders sends and replays the changes. See docs/online.md
/// </summary>
public static class OrderSync
{
    static int changing; // changes in progress, nested
    static int showing;  // host changes being replayed, nested

    /// <summary>Whether this machine may change orders now: offline, on the host, or on a client replaying the host's change</summary>
    public static bool MayChange { get { return GameAuthority.IsAuthority || Showing; } }

    /// <summary>Client: whether it's replaying one of the host's changes</summary>
    public static bool Showing { get { return showing > 0; } }

    /// <summary>Online host: each outermost order change</summary>
    public static event Action<OrderChange> Changed;

    /// <summary>Online client: its player drops what they hold (spot1, spot2, spinOut): the host drops them</summary>
    public static event Action<OrderHandler, Vector3, Vector3, bool> DropAsked;

    /// <summary>
    /// An order change: using (OrderSync.Change(...)) { the change }. On an online host the outermost one goes out first
    /// </summary>
    public static Scope Change(OrderChange change)
    {
        if (changing == 0 && GameAuthority.IsOnline && GameAuthority.IsAuthority && !Showing)
            Changed?.Invoke(change);

        changing++;
        return new Scope(true);
    }

    /// <summary>
    /// A change in progress; disposing it ends the change
    /// </summary>
    public struct Scope : IDisposable
    {
        bool open;

        internal Scope(bool open)
        {
            this.open = open;
        }

        public void Dispose()
        {
            if (!open)
                return;

            open = false;
            changing--;
        }
    }

    /// <summary>
    /// Client: replays one of the host's changes, which it may make while this runs
    /// </summary>
    public static void Show(Action change)
    {
        showing++;
        try
        {
            change();
        }
        finally
        {
            showing--;
        }
    }

    /// <summary>
    /// Online client: its player drops what they hold (a respawn). The host drops them, for everyone
    /// </summary>
    public static void AskDrop(OrderHandler handler, Vector3 spot1, Vector3 spot2, bool spinOut)
    {
        DropAsked?.Invoke(handler, spot1, spot2, spinOut);
    }

    /// <summary>
    /// The seat (roster slot) of the player a part belongs to, or -1 when it's nobody's
    /// </summary>
    public static int SeatOf(Component part)
    {
        if (part == null || PlayerInstantiate.Instance == null)
            return -1;

        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            if (slot.Player != null && part.transform.IsChildOf(slot.Player.transform))
                return slot.Index;
        }
        return -1;
    }

    /// <summary>
    /// The order handler of the player in a seat, or null for an empty or unknown seat
    /// </summary>
    public static OrderHandler HandlerIn(int seat)
    {
        if (PlayerInstantiate.Instance == null || seat < 0 || seat >= Constants.MAX_PLAYERS)
            return null;

        PlayerSlot slot = PlayerInstantiate.Instance.Roster[seat];
        return slot != null && slot.Player != null ? slot.Player.GetComponentInChildren<OrderHandler>(true) : null;
    }

    /// <summary>
    /// Forgets every listener and any change in progress (tests)
    /// </summary>
    public static void Reset()
    {
        Changed = null;
        DropAsked = null;
        changing = 0;
        showing = 0;
    }
}

using System;
using UnityEngine;

/// <summary>
/// On a player's ball (offline only): when it runs into another player's scooter without either boosting, it tells the
/// highlight reel (FeatSync.Bump) with how hard, as a share of a clash's closing speed. Both balls feel one bump, so the
/// pair's cooldown (BumpRules.TooSoon, shared by every watcher) lets it through once. Online the host reports bumps
/// (OnlineBumps). Boosting into someone is a steal or a clash (OrderHandler), not a bump
/// </summary>
public class BumpWatcher : MonoBehaviour
{
    /// <summary>The pair cooldown every watcher shares, so a bump felt by both balls counts once</summary>
    internal static BumpRules Rules = new BumpRules();

    /// <summary>The time bumps are dated by (tests set it)</summary>
    internal static Func<float> Clock = () => Time.time;

    BallDriving driving;
    int seat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Rules = new BumpRules();
        Clock = () => Time.time;
    }

    /// <summary>The ball's player: their scooter and their seat</summary>
    public void Begin(BallDriving ballDriving, int playerSeat)
    {
        driving = ballDriving;
        seat = playerSeat;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.contactCount == 0)
            return;

        float closing = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
        Hit(collision.collider, closing);
    }

    /// <summary>
    /// This ball touched a collider at a closing speed (m/s): a bump if it is another player's scooter and neither is
    /// boosting, once per pair per cooldown
    /// </summary>
    internal void Hit(Collider other, float closing)
    {
        if (GameAuthority.IsOnline || driving == null || driving.Boosting || other == null)
            return;

        // The other scooter's player, found as the cardboard cutout finds a scooter's (CutoutHandler)
        Transform parent = other.transform.parent;
        OrderHandler player = parent != null ? parent.GetComponentInChildren<OrderHandler>(true) : null;
        if (player == null || player.IsBoosting)
            return;

        int otherSeat = OrderSync.SeatOf(player);
        if (seat < 0 || otherSeat < 0 || otherSeat == seat || Rules.TooSoon(seat, otherSeat, Clock(), closing))
            return;

        FeatSync.Bump(seat, otherSeat, Mathf.Clamp01(closing / BumpRules.MAX_SPEED));
    }
}

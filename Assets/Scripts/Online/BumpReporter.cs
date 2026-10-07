using UnityEngine;

/// <summary>
/// On this machine's player's ball (online; OnlineBumps adds it): when it runs into another machine's scooter, which is
/// kinematic here and isn't pushed, it reports the bump with the closing speed (BumpSync). Boosting into someone is a
/// steal or a clash instead (OrderHandler), so a boosting ball reports nothing. See docs/online.md
/// </summary>
public class BumpReporter : MonoBehaviour
{
    BallDriving driving;
    OrderHandler player;

    /// <summary>The ball's player</summary>
    public void Begin(BallDriving ballDriving, OrderHandler orders)
    {
        driving = ballDriving;
        player = orders;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!GameAuthority.IsOnline || driving == null || player == null || driving.Boosting || collision.contactCount == 0)
            return;

        // The other scooter's player, found as the cardboard cutout finds a scooter's (CutoutHandler)
        Transform parent = collision.collider.transform.parent;
        OrderHandler other = parent != null ? parent.GetComponentInChildren<OrderHandler>(true) : null;
        if (other == null || other == player || !RemoteAvatar.IsRemote(other))
            return;

        float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
        BumpSync.Report(OrderSync.SeatOf(player), OrderSync.SeatOf(other), speed);
    }
}

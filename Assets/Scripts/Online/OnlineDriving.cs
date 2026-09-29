using UnityEngine;

/// <summary>
/// Online, every frame:
/// - This machine's scooter goes into its OnlineScooter.
/// - Every other machine's OnlineScooter moves that player's RemoteAvatar: its pose, plus its boost trail, skid marks,
///   drift sparks and rider, hidden while its owner's rider is (BallDriving.ShowRemote). It waits where it is until its
///   owner has shared a pose.
/// It runs after everything else. So it sends this machine's scooter as the frame left it, and shows the others after
/// Netcode moved their proxies. OnlineGame adds it. See docs/online.md
/// </summary>
[DefaultExecutionOrder(1000)]
public class OnlineDriving : MonoBehaviour
{
    /// <summary>How much of the gap the rider's shown speed closes each frame</summary>
    const float SPEED_SMOOTHING = 0.2f;

    /// <summary>The fastest speed the rider shows (a jump isn't speed)</summary>
    const float TOP_SPEED = 60f;

    OnlineSession session;

    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
    }

    void LateUpdate()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (session == null || players == null)
            return;

        foreach (OnlineScooter scooter in session.Scooters)
        {
            int seat = scooter.Seat;
            if (seat < 0 || seat >= Constants.MAX_PLAYERS)
                continue;

            PlayerSlot slot = players.Roster[seat];
            if (slot == null)
                continue; // nobody in that seat here yet (this machine's player may be moving into it)

            BallDriving driving = slot.Player.GetComponentInChildren<BallDriving>(true);
            if (scooter.IsOwner)
            {
                if (slot.IsLocal)
                    scooter.Share(ScooterPose.Read(driving), driving.Flags);
            }
            else if (!slot.IsLocal && scooter.HasPose)
                Show(scooter, driving);
        }
    }

    // Another machine's scooter, where its owner has it, doing what it does
    static void Show(OnlineScooter scooter, BallDriving driving)
    {
        ScooterPose pose = scooter.Pose;
        float speed = 0f;
        if (scooter.Shown && Time.deltaTime > 0f)
            speed = Mathf.Min(Vector3.Distance(scooter.ShownBall, pose.Ball) / Time.deltaTime, TOP_SPEED);
        scooter.ShownSpeed = Mathf.Lerp(scooter.ShownSpeed, speed, SPEED_SMOOTHING);
        scooter.ShownBall = pose.Ball;
        scooter.Shown = true;

        ScooterPose.Apply(driving, pose);
        driving.ShowRemote(scooter.Flags, scooter.ShownSpeed);
    }
}

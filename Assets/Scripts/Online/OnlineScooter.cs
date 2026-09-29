using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One player's scooter in an online match, on every machine: where it is, and what it's doing (DriveFlags).
/// - Its own machine copies the scooter into it every frame (OnlineDriving). The other machines show it on that
///   player's RemoteAvatar.
/// - Two proxies carry the pose, each moved by its owner through an OwnerNetworkTransform:
///   - Ball: the ball's position and the scooter's heading.
///   - Model: the model's world pose.
/// - The proxies wait parked far below the map until the owner's first pose, so a scooter never flies in from the
///   world's origin.
/// - The host spawns one per machine, beside its OnlinePlayer. See docs/online.md
/// </summary>
public class OnlineScooter : NetworkBehaviour
{
    /// <summary>Where the proxies wait until their owner shares a pose: far below any map</summary>
    public const float PARKED_Y = -10000f;

    /// <summary>A move longer than this in one frame (a spawn point, a respawn) jumps on other machines instead of sliding</summary>
    public const float TELEPORT_DISTANCE = 10f;

    [SerializeField] Transform ball;
    [SerializeField] Transform model;

    readonly NetworkVariable<int> seat = new NetworkVariable<int>(-1);
    readonly NetworkVariable<byte> flags = new NetworkVariable<byte>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    OnlineSession session;
    OwnerNetworkTransform ballSync;
    OwnerNetworkTransform modelSync;

    // Other machines: what OnlineDriving last showed of it (for the rider's speed)
    internal bool Shown;
    internal Vector3 ShownBall;
    internal float ShownSpeed;

    /// <summary>The seat of the player whose scooter this is: their slot on every machine (-1 until the host gives one)</summary>
    public int Seat { get { return seat.Value; } }

    /// <summary>What the scooter is doing</summary>
    public DriveFlags Flags { get { return new DriveFlags(flags.Value); } }

    /// <summary>Whether its owner has shared a pose yet (until then it's parked)</summary>
    public bool HasPose { get { return ball.position.y > PARKED_Y / 2; } }

    /// <summary>Its latest pose (on other machines, smoothed by Netcode)</summary>
    public ScooterPose Pose
    {
        get
        {
            return new ScooterPose
            {
                Ball = ball.position,
                Heading = ball.eulerAngles.y,
                ModelPosition = model.position,
                ModelRotation = model.rotation,
            };
        }
    }

    public override void OnNetworkSpawn()
    {
        session = NetworkManager.GetComponent<OnlineSession>();
        ballSync = ball.GetComponent<OwnerNetworkTransform>();
        modelSync = model.GetComponent<OwnerNetworkTransform>();

        // The host seats it as it spawns it: a value set here goes out with the spawn itself
        if (IsServer && session != null)
            seat.Value = session.SeatOf(OwnerClientId);

        DontDestroyOnLoad(gameObject);

        if (session != null)
            session.AddScooter(this);
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
            session.RemoveScooter(this);
    }

    /// <summary>
    /// This machine's scooter: shares its pose and what it's doing with every machine. The first pose, a long move (a
    /// spawn point, a respawn) and any move while it's hidden (its wisp flying to its grave) jump, so it shows again
    /// where its owner has it. Does nothing for another machine's scooter
    /// </summary>
    public void Share(ScooterPose pose, DriveFlags driveFlags)
    {
        if (!IsOwner || !IsSpawned)
            return;

        Quaternion heading = Quaternion.Euler(0, pose.Heading, 0);
        if (!HasPose || driveFlags.Hidden || Vector3.Distance(ball.position, pose.Ball) > TELEPORT_DISTANCE)
        {
            ballSync.Teleport(pose.Ball, heading, Vector3.one);
            modelSync.Teleport(pose.ModelPosition, pose.ModelRotation, Vector3.one);
        }
        else
        {
            ball.SetPositionAndRotation(pose.Ball, heading);
            model.SetPositionAndRotation(pose.ModelPosition, pose.ModelRotation);
        }

        if (flags.Value != driveFlags.Value)
            flags.Value = driveFlags.Value;
    }
}

using UnityEngine;

/// <summary>
/// Where a scooter is, as other machines show it: its ball, its heading, and its model's world pose. The model's pose
/// already has the slope, the lean, the drift angle, the hop and the wheelie in it. See docs/online.md
/// </summary>
public struct ScooterPose
{
    /// <summary>The ball's position: the scooter follows it</summary>
    public Vector3 Ball;

    /// <summary>The scooter's heading, in degrees about Y</summary>
    public float Heading;

    /// <summary>The model's world position</summary>
    public Vector3 ModelPosition;

    /// <summary>The model's world rotation</summary>
    public Quaternion ModelRotation;

    /// <summary>
    /// A scooter's pose now
    /// </summary>
    public static ScooterPose Read(BallDriving driving)
    {
        Transform model = driving.ScooterModel;
        return new ScooterPose
        {
            Ball = driving.Sphere.transform.position,
            Heading = driving.transform.eulerAngles.y,
            ModelPosition = model.position,
            ModelRotation = model.rotation,
        };
    }

    /// <summary>
    /// Puts another machine's scooter (a RemoteAvatar, whose ball is kinematic and controls are off) in a pose
    /// </summary>
    public static void Apply(BallDriving driving, ScooterPose pose)
    {
        driving.Sphere.transform.position = pose.Ball;
        driving.transform.SetPositionAndRotation(pose.Ball - new Vector3(0, BallDriving.SCOOTER_BELOW_BALL, 0), Quaternion.Euler(0, pose.Heading, 0));
        driving.ScooterModel.SetPositionAndRotation(pose.ModelPosition, pose.ModelRotation);
    }
}

using UnityEngine;

/// <summary>
/// The Speed and Low Gravity mutators (MATCH RULES) on this machine: BallDriving scales every scooter's force by
/// SpeedScale, and gravity is the project's -13 or half that. Applied as the match begins (OrderManager.InitGame; an
/// online client: OnlineGame, from the host's rules) and reset on the way to the menus, on a rematch and on quitting
/// (in the editor a changed Physics.gravity would otherwise stay in the project settings)
/// </summary>
public static class MatchPhysics
{
    /// <summary>ProjectSettings/DynamicsManager.asset's gravity</summary>
    public const float BASE_GRAVITY = -13f;

    public static float SpeedScale { get; private set; } = 1f;

    public static void Apply(HouseRules rules)
    {
        SpeedScale = rules.SpeedScale;
        Physics.gravity = new Vector3(0f, BASE_GRAVITY * rules.GravityScale, 0f);
    }

    public static void Reset()
    {
        SpeedScale = 1f;
        Physics.gravity = new Vector3(0f, BASE_GRAVITY, 0f);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ResetOnQuit()
    {
        Application.quitting -= Reset;
        Application.quitting += Reset;
    }
}

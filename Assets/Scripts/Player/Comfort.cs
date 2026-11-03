/// <summary>
/// Options → Accessibility → Reduce motion: with it on, boosting never widens the view (OrbitalCamera) and the speed
/// lines never show (BallDriving). Rumble: with it off, no pad shakes (Rumbler). Saved in settings.cfg
/// </summary>
public static class Comfort
{
    /// <summary>The speed lines' material value that shows none (lower shows more)</summary>
    public const float NO_SPEED_LINES = 1f;

    public static bool ReduceMotion;

    /// <summary>Whether pads shake</summary>
    public static bool Rumble = true;

    /// <summary>A pad's motor speed to set: none with rumble off</summary>
    public static float Motor(float wanted)
    {
        return Rumble ? wanted : 0f;
    }

    /// <summary>The field of view a camera heads for: the boost's wide one only while boosting, and only with motion on</summary>
    public static float TargetFov(bool boostWide, float boostFov, float normalFov)
    {
        return boostWide && !ReduceMotion ? boostFov : normalFov;
    }

    /// <summary>The speed lines' value to show: none with reduced motion</summary>
    public static float SpeedLines(float wanted)
    {
        return ReduceMotion ? NO_SPEED_LINES : wanted;
    }
}

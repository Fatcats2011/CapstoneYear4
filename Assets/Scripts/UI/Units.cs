using UnityEngine;

/// <summary>
/// Options → Display → Units: distances in metres (the world's own unit) or feet. Saved in settings.cfg
/// </summary>
public static class Units
{
    public const float FEET_PER_METRE = 3.28084f;

    public static bool Imperial;

    /// <summary>A distance in metres as the player reads it: "120m", or "394ft"</summary>
    public static string Distance(int metres)
    {
        return Imperial ? Mathf.RoundToInt(metres * FEET_PER_METRE) + "ft" : metres + "m";
    }
}

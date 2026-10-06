/// <summary>
/// The game's Steam achievements. Who earns them is MatchFeats; their names in the Steamworks dashboard are
/// AchievementNames. See docs/steam/in-game-features.md
/// </summary>
public enum Achievement
{
    /// <summary>Deliver an order in a match (the tutorial's don't count)</summary>
    FirstDelivery,

    /// <summary>Deliver the golden order, then finish first (a tie for first counts)</summary>
    GoldenWin
}

/// <summary>
/// The achievements' API names, as the Steamworks dashboard defines them
/// </summary>
public static class AchievementNames
{
    public static string ApiName(Achievement achievement)
    {
        switch (achievement)
        {
            case Achievement.FirstDelivery:
                return "FIRST_DELIVERY";
            case Achievement.GoldenWin:
                return "GOLDEN_WIN";
            default:
                return achievement.ToString();
        }
    }
}

/// <summary>
/// The game's Steam achievements. Who earns them is MatchFeats; their names in the Steamworks dashboard are
/// AchievementNames. See docs/steam/in-game-features.md
/// </summary>
public enum Achievement
{
    /// <summary>Deliver an order in a match (the tutorial's don't count)</summary>
    FirstDelivery,

    /// <summary>Deliver the golden order, then finish first (a tie for first counts)</summary>
    GoldenWin,

    /// <summary>Finish the tutorial (skipping it doesn't count)</summary>
    TutorialDone,

    /// <summary>Steal an order in a match (the tutorial's don't count)</summary>
    Steal,

    /// <summary>Steal the golden order</summary>
    GoldenSteal,

    /// <summary>Deliver a hard order in a match</summary>
    HardDelivery,

    /// <summary>Finish first against someone who scored less (a tie for first counts)</summary>
    Win,

    /// <summary>Be last when the golden round starts, then win</summary>
    LastToFirst,

    /// <summary>Fall in the water</summary>
    FellInWater
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
            case Achievement.TutorialDone:
                return "TUTORIAL_DONE";
            case Achievement.Steal:
                return "STEAL";
            case Achievement.GoldenSteal:
                return "GOLDEN_STEAL";
            case Achievement.HardDelivery:
                return "HARD_DELIVERY";
            case Achievement.Win:
                return "WIN";
            case Achievement.LastToFirst:
                return "LAST_TO_FIRST";
            case Achievement.FellInWater:
                return "FELL_IN_WATER";
            default:
                return null; // not one of the game's (another machine could send any number)
        }
    }
}

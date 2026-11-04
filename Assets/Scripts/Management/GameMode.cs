/// <summary>What kind of match is played (MATCH RULES). Standard is the game as designed. See docs/game-modes-ideas.md</summary>
public enum GameMode { Standard, GoldRush, KingsCrown, Infection, HotPotato }

/// <summary>Which modes can be picked: a mode joins the list once it's built (plans 2026-11-04-*)</summary>
public static class GameModes
{
    public static GameMode[] Playable { get; internal set; } = { GameMode.Standard, GameMode.GoldRush, GameMode.KingsCrown, GameMode.Infection, GameMode.HotPotato };

    /// <summary>The mode's name key, e.g. "rules.mode.goldrush"</summary>
    public static string Key(GameMode mode)
    {
        return "rules.mode." + mode.ToString().ToLowerInvariant();
    }
}

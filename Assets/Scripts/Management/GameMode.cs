/// <summary>What kind of match is played (MATCH RULES). Standard is the game as designed. See docs/game-modes-ideas.md</summary>
public enum GameMode { Standard, GoldRush, KingsCrown, Infection, HotPotato }

/// <summary>Which modes can be picked: a mode joins the list once it's built (plans 2026-11-04-*)</summary>
public static class GameModes
{
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        HouseRules.ResetForPlay();
    }

    public static GameMode[] Playable { get; internal set; } = { GameMode.Standard, GameMode.GoldRush, GameMode.KingsCrown, GameMode.Infection, GameMode.HotPotato };

    /// <summary>The mode's name key, e.g. "rules.mode.goldrush"</summary>
    public static string Key(GameMode mode)
    {
        return "rules.mode." + mode.ToString().ToLowerInvariant();
    }

    /// <summary>The key of the mode's rules in a sentence or two, for MATCH RULES: "rules.desc.goldrush"</summary>
    public static string DescriptionKey(GameMode mode)
    {
        return "rules.desc." + mode.ToString().ToLowerInvariant();
    }

    /// <summary>The key of what to do in the mode, as its match starts (ModeInfoBox): "mode.start.goldrush"</summary>
    public static string InstructionKey(GameMode mode)
    {
        return "mode.start." + mode.ToString().ToLowerInvariant();
    }
}

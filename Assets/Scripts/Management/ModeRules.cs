/// <summary>A row of MATCH RULES</summary>
public enum Rule { Speed, LowGravity, Waves, WaveLength, RoundTimer, Steals, GoldenOrder, Bots }

/// <summary>How a mode treats a rule: shown, hidden (off / not used), or locked on (the mode needs it)</summary>
public enum RuleShow { Shown, Hidden, LockedOn }

/// <summary>
/// Which MATCH RULES rows each mode shows, hides or locks: the "Mutators per Mode" table in docs/game-modes-ideas.md.
/// Modes without deliveries have no waves or golden order; they use the Round Timer instead
/// </summary>
public static class ModeRules
{
    public static RuleShow For(GameMode mode, Rule rule)
    {
        switch (rule)
        {
            case Rule.Speed:
            case Rule.LowGravity:
            case Rule.Bots:
                return RuleShow.Shown;
            case Rule.Waves:
            case Rule.WaveLength:
            case Rule.GoldenOrder:
                return HasDeliveries(mode) ? RuleShow.Shown : RuleShow.Hidden;
            case Rule.RoundTimer:
                // Hot Potato plays until one player is left: its clock is the fuse
                return HasDeliveries(mode) || mode == GameMode.HotPotato ? RuleShow.Hidden : RuleShow.Shown;
            case Rule.Steals:
                if (mode == GameMode.Standard)
                    return RuleShow.Shown;
                return mode == GameMode.Infection ? RuleShow.Hidden : RuleShow.LockedOn;
        }
        return RuleShow.Hidden;
    }

    /// <summary>Whether the mode plays waves of deliveries (and so may end in the golden round)</summary>
    public static bool HasDeliveries(GameMode mode)
    {
        return mode == GameMode.Standard || mode == GameMode.GoldRush;
    }
}

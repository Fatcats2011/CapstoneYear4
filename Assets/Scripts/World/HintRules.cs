using System;

/// <summary>The four rules a player is told once, the first time they matter (Hints). The order is the bit each takes in the saved mask</summary>
public enum HintKind : byte { BoostToSteal, BoostToBlock, StolenPaysDouble, HoldGold }

/// <summary>
/// First-time hints: one line each, in the player's own callout (StealCallout.HearHint), held a little longer than an event
/// line. Every number and word choice is here, to tune in playtests:
/// - Key: the line each hint reads (a strings.csv key).
/// - ShowsIn: the tutorial teaches these itself, so it shows none.
/// - Seen, Mark: the saved mask (settings.cfg), one bit per hint.
/// - Alpha: how a hint fades in and out over HINT_SECONDS.
/// </summary>
public static class HintRules
{
    /// <summary>How long a hint is up, its fades included (an event line is up StealFeedbackRules.CALLOUT_SECONDS)</summary>
    public const float HINT_SECONDS = 4f;

    public static string Key(HintKind kind)
    {
        switch (kind)
        {
            case HintKind.BoostToSteal: return "hint.boost_to_steal";
            case HintKind.BoostToBlock: return "hint.boost_to_block";
            case HintKind.StolenPaysDouble: return "hint.stolen_double";
            case HintKind.HoldGold: return "hint.hold_gold";
            default: return "";
        }
    }

    /// <summary>Whether hints are given in a game state: every one but the tutorial</summary>
    public static bool ShowsIn(GameState state)
    {
        return state != GameState.Tutorial;
    }

    public static bool Seen(int mask, HintKind kind)
    {
        return (mask & (1 << (int)kind)) != 0;
    }

    public static int Mark(int mask, HintKind kind)
    {
        return mask | (1 << (int)kind);
    }

    /// <summary>How solid a hint is at a time since it came up: fades in, holds, fades out, gone by HINT_SECONDS</summary>
    public static float Alpha(float age)
    {
        if (age < 0f || age >= HINT_SECONDS)
            return 0f;
        return Math.Min(1f, Math.Min(age, HINT_SECONDS - age) / StealFeedbackRules.CALLOUT_FADE);
    }
}

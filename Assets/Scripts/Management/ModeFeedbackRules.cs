using System.Collections.Generic;

/// <summary>What a machine knows of a mode's state at one moment. Crown and Potato are seats or ModeMatch.NONE; the rest are seat masks (bit n = seat n)</summary>
public struct ModeView
{
    public GameMode Mode;
    public int Players, Crown, Potato, Infected, Out;

    /// <summary>No match seen yet: Standard, nobody playing, no crown, no potato, nobody infected or out</summary>
    public static ModeView Nothing
    {
        get { return new ModeView { Mode = GameMode.Standard, Players = 0, Crown = ModeMatch.NONE, Potato = ModeMatch.NONE, Infected = 0, Out = 0 }; }
    }
}

/// <summary>What happened to a player in a timed mode (ModeFeedbackRules.Lines). Each kind has a callout line (ModeFeedbackRules.Key)</summary>
public enum ModeLineKind : byte { CrownYours, CrownTaken, Infected, Caught, LastFree, PotatoYours, PotatoPassed, PlayerOut }

/// <summary>A player's role in a timed mode, which the goal tag names (ModeFeedbackRules.GoalFor)</summary>
public enum ModeRole : byte { None, KeepCrown, GetCrown, Catch, DontGetCaught, PassIt, StayAway }

/// <summary>One line for the callout. Seat: who reads it, or EVERYONE_ELSE for every player but Other. Other: the player it names, or ModeMatch.NONE</summary>
public readonly struct ModeLine
{
    public const int EVERYONE_ELSE = -2;

    public readonly int Seat;
    public readonly ModeLineKind Kind;
    public readonly int Other;

    public ModeLine(int seat, ModeLineKind kind, int other)
    {
        Seat = seat;
        Kind = kind;
        Other = other;
    }
}

/// <summary>
/// What players are told in Kings Crown, Infection and Hot Potato. Other machines receive only a mode's state, never its
/// events, so every machine works the events out the same way, from the state before and after. Every number and word
/// choice is here, to tune in playtests:
/// - Lines, Key, Tone: the lines a change of state gives, and how each reads.
/// - GoalFor, GoalKey, GoalAlpha: the goal tag of each role, and how it fades in and out over GOAL_SECONDS.
/// - WarnsIn, CrossedWarning, CountdownKey: the warning 30 seconds before a timed round ends, and the countdown's words.
/// ModeWatcher announces the lines; StealCallout and GoalTag show them.
/// </summary>
public static class ModeFeedbackRules
{
    /// <summary>How long the goal tag is up, its fades included, and how long each fade is</summary>
    public const float GOAL_SECONDS = 25f;
    public const float GOAL_FADE = 0.3f;

    /// <summary>The seconds left on a timed round's clock at which everyone is warned</summary>
    public const float WARN_SECONDS = 30f;

    /// <summary>
    /// The lines the change from one state to the next gives. A new match, or another mode, is compared against nothing.
    /// Nobody who has left is addressed or named, except a player who is out (they are still playing). Order: out, yours,
    /// taken or passed, infected, caught, last free
    /// </summary>
    public static List<ModeLine> Lines(ModeView before, ModeView after)
    {
        List<ModeLine> lines = new List<ModeLine>();
        if (before.Mode != after.Mode || before.Players == 0)
        {
            before = ModeView.Nothing;
            before.Mode = after.Mode;
        }

        switch (after.Mode)
        {
            case GameMode.KingsCrown:
                if (after.Crown != before.Crown && Playing(after, after.Crown))
                {
                    lines.Add(new ModeLine(after.Crown, ModeLineKind.CrownYours, ModeMatch.NONE));
                    if (Playing(after, before.Crown))
                        lines.Add(new ModeLine(before.Crown, ModeLineKind.CrownTaken, after.Crown));
                }
                break;

            case GameMode.Infection:
                InfectionLines(before, after, lines);
                break;

            case GameMode.HotPotato:
                PotatoLines(before, after, lines);
                break;
        }
        return lines;
    }

    static void InfectionLines(ModeView before, ModeView after, List<ModeLine> lines)
    {
        int newly = after.Infected & ~before.Infected & after.Players;
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
            if (InMask(newly, seat))
                lines.Add(new ModeLine(seat, ModeLineKind.Infected, ModeMatch.NONE));

        int hunters = before.Infected & after.Players;
        if (Count(hunters) == 1 && (hunters & newly) == 0)
        {
            int hunter = SeatOf(hunters);
            for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
                if (InMask(newly, seat))
                    lines.Add(new ModeLine(hunter, ModeLineKind.Caught, seat));
        }

        int freeNow = after.Players & ~after.Infected;
        int freeBefore = before.Players & ~before.Infected;
        if (Count(freeNow) == 1 && Count(freeBefore) > 1)
            lines.Add(new ModeLine(SeatOf(freeNow), ModeLineKind.LastFree, ModeMatch.NONE));
    }

    static void PotatoLines(ModeView before, ModeView after, List<ModeLine> lines)
    {
        int newlyOut = after.Out & ~before.Out;
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
            if (InMask(newlyOut, seat))
                lines.Add(new ModeLine(ModeLine.EVERYONE_ELSE, ModeLineKind.PlayerOut, seat));

        if (after.Potato != before.Potato && Playing(after, after.Potato))
        {
            lines.Add(new ModeLine(after.Potato, ModeLineKind.PotatoYours, ModeMatch.NONE));
            if (Playing(after, before.Potato) && !InMask(newlyOut, before.Potato))
                lines.Add(new ModeLine(before.Potato, ModeLineKind.PotatoPassed, after.Potato));
        }
    }

    /// <summary>The line's strings.csv key. A name goes in {0}</summary>
    public static string Key(ModeLineKind kind)
    {
        switch (kind)
        {
            case ModeLineKind.CrownYours: return "mode.line.crown.yours";
            case ModeLineKind.CrownTaken: return "mode.line.crown.taken";
            case ModeLineKind.Infected: return "mode.line.infected";
            case ModeLineKind.Caught: return "mode.line.caught";
            case ModeLineKind.LastFree: return "mode.line.lastfree";
            case ModeLineKind.PotatoYours: return "mode.line.potato.yours";
            case ModeLineKind.PotatoPassed: return "mode.line.potato.passed";
            default: return "mode.line.out";
        }
    }

    /// <summary>The line's colour: gold for what the player gained or did, red for what happened to them, white for news</summary>
    public static CalloutTone Tone(ModeLineKind kind)
    {
        switch (kind)
        {
            case ModeLineKind.CrownYours:
            case ModeLineKind.Caught:
            case ModeLineKind.PotatoPassed:
                return CalloutTone.Thief;
            case ModeLineKind.CrownTaken:
            case ModeLineKind.Infected:
            case ModeLineKind.PotatoYours:
                return CalloutTone.Victim;
            default:
                return CalloutTone.Plain;
        }
    }

    /// <summary>A seat's goal now. A seat that is not playing, or is out, has none, and so do the modes without a tag</summary>
    public static ModeRole GoalFor(ModeView view, int seat)
    {
        if (!Playing(view, seat) || InMask(view.Out, seat))
            return ModeRole.None;

        switch (view.Mode)
        {
            case GameMode.KingsCrown: return view.Crown == seat ? ModeRole.KeepCrown : ModeRole.GetCrown;
            case GameMode.Infection: return InMask(view.Infected, seat) ? ModeRole.Catch : ModeRole.DontGetCaught;
            case GameMode.HotPotato: return view.Potato == seat ? ModeRole.PassIt : ModeRole.StayAway;
            default: return ModeRole.None;
        }
    }

    /// <summary>The goal's strings.csv key, or null for no goal</summary>
    public static string GoalKey(ModeRole goal)
    {
        switch (goal)
        {
            case ModeRole.KeepCrown: return "mode.goal.keepcrown";
            case ModeRole.GetCrown: return "mode.goal.getcrown";
            case ModeRole.Catch: return "mode.goal.catch";
            case ModeRole.DontGetCaught: return "mode.goal.dontgetcaught";
            case ModeRole.PassIt: return "mode.goal.passit";
            case ModeRole.StayAway: return "mode.goal.stayaway";
            default: return null;
        }
    }

    /// <summary>How solid the goal tag is at a time since it appeared: gone outside 0 to GOAL_SECONDS, fading over GOAL_FADE at each end, solid between</summary>
    public static float GoalAlpha(float age)
    {
        if (age < 0f || age >= GOAL_SECONDS)
            return 0f;
        if (age < GOAL_FADE)
            return age / GOAL_FADE;
        float left = GOAL_SECONDS - age;
        return left >= GOAL_FADE ? 1f : left / GOAL_FADE;
    }

    /// <summary>Whether this mode's round has a timer worth a warning (Hot Potato's clock is the fuse)</summary>
    public static bool WarnsIn(GameMode mode)
    {
        return mode == GameMode.KingsCrown || mode == GameMode.Infection;
    }

    /// <summary>Whether the clock went from above WARN_SECONDS to at or below it, with time still left</summary>
    public static bool CrossedWarning(float before, float after)
    {
        return before > WARN_SECONDS && after <= WARN_SECONDS && after > 0f;
    }

    /// <summary>The key of the last-10-seconds countdown's words: the golden order's with a golden round, else "Time's Up In..."</summary>
    public static string CountdownKey(bool goldenRound)
    {
        return goldenRound ? "hud.goldenin" : "hud.timesupin";
    }

    static bool Playing(ModeView view, int seat)
    {
        return seat >= 0 && seat < Constants.MAX_PLAYERS && InMask(view.Players, seat);
    }

    static bool InMask(int mask, int seat)
    {
        return seat >= 0 && (mask & (1 << seat)) != 0;
    }

    static int Count(int mask)
    {
        int count = 0;
        for (; mask != 0; mask &= mask - 1)
            count++;
        return count;
    }

    static int SeatOf(int singleBit)
    {
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
            if (InMask(singleBit, seat))
                return seat;
        return ModeMatch.NONE;
    }
}

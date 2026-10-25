using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Who gets which award at the results, one per player:
/// 1. each player takes their best award that they top alone;
/// 2. a player still without one takes the best of what's left: for each award nobody has, by rank, the player still
///    without one with the highest count (above 0), ties to the lower seat;
/// 3. anyone still without is a TOURIST (it can repeat).
/// Its card says the joke name (Name) and a plain count (Line), never "most": step 2 can give it to someone who wasn't
/// top. See docs/superpowers/specs/2026-10-24-awards-design.md
/// </summary>
public static class AwardRules
{
    /// <summary>The awards a count can win, best first</summary>
    public static readonly Award[] PRIORITY =
    {
        Award.GoldenGrip, Award.Kleptoghost, Award.BusyBee, Award.HighRoller, Award.Butterfingers, Award.FishFood,
    };

    /// <summary>Each playing seat's award</summary>
    public static Dictionary<int, Award> Pick(MatchStats stats, IEnumerable<int> seats)
    {
        int[] playing = seats.Distinct().OrderBy(seat => seat).ToArray();
        Dictionary<int, Award> awards = new Dictionary<int, Award>();
        HashSet<Award> taken = new HashSet<Award>();

        foreach (int seat in playing)
        {
            foreach (Award award in PRIORITY)
            {
                if (TopsAlone(stats, playing, seat, award))
                {
                    awards[seat] = award;
                    taken.Add(award);
                    break;
                }
            }
        }

        foreach (Award award in PRIORITY)
        {
            if (taken.Contains(award))
                continue;
            int best = -1;
            foreach (int seat in playing)
            {
                if (!awards.ContainsKey(seat) && stats.Count(seat, award) > 0
                    && (best < 0 || stats.Count(seat, award) > stats.Count(best, award)))
                    best = seat;
            }
            if (best >= 0)
            {
                awards[best] = award;
                taken.Add(award);
            }
        }

        foreach (int seat in playing)
        {
            if (!awards.ContainsKey(seat))
                awards[seat] = Award.Tourist;
        }
        return awards;
    }

    /// <summary>The card's title</summary>
    public static string Name(Award award)
    {
        switch (award)
        {
            case Award.GoldenGrip: return Loc.Get("award.goldengrip");
            case Award.Kleptoghost: return Loc.Get("award.kleptoghost");
            case Award.BusyBee: return Loc.Get("award.busybee");
            case Award.HighRoller: return Loc.Get("award.highroller");
            case Award.Butterfingers: return Loc.Get("award.butterfingers");
            case Award.FishFood: return Loc.Get("award.fishfood");
            case Award.Tourist: return Loc.Get("award.tourist");
            default: return "";
        }
    }

    /// <summary>The card's plain line: what the count was</summary>
    public static string Line(Award award, int count)
    {
        switch (award)
        {
            case Award.GoldenGrip: return Loc.Get("award.line.goldengrip", count);
            case Award.Kleptoghost: return Counted(count, "steal");
            case Award.BusyBee: return Counted(count, "delivery");
            case Award.HighRoller: return Counted(count, "hard");
            case Award.Butterfingers: return Counted(count, "robbed");
            case Award.FishFood: return Counted(count, "swim");
            case Award.Tourist: return Loc.Get("award.line.tourist");
            default: return "";
        }
    }

    // "1 steal" or "3 steals": a line per count, in the language spoken (award.line.<what>.one / .many)
    static string Counted(int count, string what)
    {
        return count == 1 ? Loc.Get("award.line." + what + ".one") : Loc.Get("award.line." + what + ".many", count);
    }

    static bool TopsAlone(MatchStats stats, int[] playing, int seat, Award award)
    {
        int mine = stats.Count(seat, award);
        if (mine <= 0)
            return false;
        foreach (int other in playing)
        {
            if (other != seat && stats.Count(other, award) >= mine)
                return false;
        }
        return true;
    }
}

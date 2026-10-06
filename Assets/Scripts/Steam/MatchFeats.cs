using System;
using System.Collections.Generic;

/// <summary>
/// An achievement earned by the player in a seat
/// </summary>
public readonly struct Feat : IEquatable<Feat>
{
    public readonly int Seat;
    public readonly Achievement Achievement;

    public Feat(int seat, Achievement achievement)
    {
        Seat = seat;
        Achievement = achievement;
    }

    public bool Equals(Feat other) { return Seat == other.Seat && Achievement == other.Achievement; }
    public override bool Equals(object obj) { return obj is Feat other && Equals(other); }
    public override int GetHashCode() { return Seat * 31 + (int)Achievement; }
    public override string ToString() { return Achievement + " for seat " + Seat; }
}

/// <summary>
/// Who earns which achievement in a match, decided by the machine that runs the rules (this PC offline, the host
/// online), from its deliveries and its results. Each one is earned once per seat per match; a new match starts at the
/// menus (NewMatch), not at a load, since the golden round is a load of its own. SteamFeatures feeds it. See
/// docs/steam/in-game-features.md
/// </summary>
public class MatchFeats
{
    readonly HashSet<Feat> earned = new HashSet<Feat>();
    int goldenSeat = -1; // who delivered the golden order in the golden round, or -1

    /// <summary>
    /// A player delivered an order. The tutorial's deliveries earn nothing, and the golden order counts only in the
    /// golden round (where delivering it ends the match)
    /// </summary>
    public List<Feat> Delivered(int seat, bool golden, GameState state)
    {
        List<Feat> feats = new List<Feat>();
        if (state == GameState.Tutorial)
            return feats;

        if (golden && state == GameState.FinalPackage)
            goldenSeat = seat;

        Earn(new Feat(seat, Achievement.FirstDelivery), feats);
        return feats;
    }

    /// <summary>
    /// The match's results: the golden order's deliverer earns a golden finish when nobody scored more (a tie counts)
    /// </summary>
    public List<Feat> Results(IReadOnlyDictionary<int, int> scoresBySeat)
    {
        List<Feat> feats = new List<Feat>();
        if (goldenSeat < 0 || !scoresBySeat.TryGetValue(goldenSeat, out int goldenScore))
            return feats;

        foreach (int score in scoresBySeat.Values)
        {
            if (score > goldenScore)
                return feats;
        }

        Earn(new Feat(goldenSeat, Achievement.GoldenWin), feats);
        return feats;
    }

    /// <summary>
    /// Forgets the last match
    /// </summary>
    public void NewMatch()
    {
        earned.Clear();
        goldenSeat = -1;
    }

    /// <summary>
    /// Whether an earned achievement unlocks on this machine: offline every seat is this PC's Steam user; online only
    /// this machine's own seat is
    /// </summary>
    public static bool UnlocksHere(bool online, int seat, int ownSeat)
    {
        return !online || seat == ownSeat;
    }

    void Earn(Feat feat, List<Feat> feats)
    {
        if (earned.Add(feat))
            feats.Add(feat);
    }
}

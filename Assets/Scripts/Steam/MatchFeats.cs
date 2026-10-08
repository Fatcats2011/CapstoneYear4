using System;
using System.Collections.Generic;
using UnityEngine;

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
/// online), from its deliveries, steals, tutorial finishes and scores. Each one is earned once per seat per match; a new
/// match starts at the menus (NewMatch), not at a load, since the golden round is a load of its own. SteamFeatures feeds
/// it. Falling in the water isn't here: the machine driving the player decides it. See docs/steam/in-game-features.md
/// </summary>
public class MatchFeats
{
    readonly HashSet<Feat> earned = new HashSet<Feat>();
    readonly HashSet<int> lastAtGolden = new HashSet<int>(); // who was last when the golden round started
    int goldenSeat = -1; // who delivered the golden order in the golden round, or -1

    /// <summary>
    /// A player delivered an order. The tutorial's deliveries earn nothing, and the golden order counts only in the
    /// golden round (where delivering it ends the match)
    /// </summary>
    public List<Feat> Delivered(int seat, Constants.OrderValue value, GameState state)
    {
        List<Feat> feats = new List<Feat>();
        if (state == GameState.Tutorial)
            return feats;

        if (value == Constants.OrderValue.Golden && state == GameState.FinalPackage)
            goldenSeat = seat;

        Earn(new Feat(seat, Achievement.FirstDelivery), feats);
        if (value == Constants.OrderValue.Hard)
            Earn(new Feat(seat, Achievement.HardDelivery), feats);
        return feats;
    }

    /// <summary>
    /// A player stole an order off another's scooter. The tutorial's steals (from a cutout) earn nothing
    /// </summary>
    public List<Feat> Stole(int seat, bool golden, GameState state)
    {
        List<Feat> feats = new List<Feat>();
        if (state == GameState.Tutorial)
            return feats;

        Earn(new Feat(seat, Achievement.Steal), feats);
        if (golden)
            Earn(new Feat(seat, Achievement.GoldenSteal), feats);
        return feats;
    }

    /// <summary>
    /// A player finished the tutorial (a skipped tutorial never says so)
    /// </summary>
    public List<Feat> Learnt(int seat)
    {
        List<Feat> feats = new List<Feat>();
        Earn(new Feat(seat, Achievement.TutorialDone), feats);
        return feats;
    }

    /// <summary>
    /// The golden round starts: whoever is last now (ties included) can come back to win. Nobody is last when everyone
    /// is level
    /// </summary>
    public void GoldenRound(IReadOnlyDictionary<int, int> scoresBySeat)
    {
        lastAtGolden.Clear();
        if (!ScoreRange(scoresBySeat, out int lowest, out int highest) || lowest == highest)
            return;

        foreach (KeyValuePair<int, int> seatScore in scoresBySeat)
        {
            if (seatScore.Value == lowest)
                lastAtGolden.Add(seatScore.Key);
        }
    }

    /// <summary>
    /// The match's results:
    /// - the winners (the top score, ties included, when someone scored less) earn a win, and a comeback when they were
    ///   last as the golden round started;
    /// - the golden order's deliverer earns a golden finish when nobody scored more (a tie counts, and so does playing
    ///   alone).
    /// </summary>
    public List<Feat> Results(IReadOnlyDictionary<int, int> scoresBySeat)
    {
        List<Feat> feats = new List<Feat>();
        if (!ScoreRange(scoresBySeat, out int lowest, out int highest))
            return feats;

        if (lowest < highest)
        {
            foreach (KeyValuePair<int, int> seatScore in scoresBySeat)
            {
                if (seatScore.Value != highest)
                    continue;
                Earn(new Feat(seatScore.Key, Achievement.Win), feats);
                if (lastAtGolden.Contains(seatScore.Key))
                    Earn(new Feat(seatScore.Key, Achievement.LastToFirst), feats);
            }
        }

        if (goldenSeat >= 0 && scoresBySeat.TryGetValue(goldenSeat, out int goldenScore) && goldenScore == highest)
            Earn(new Feat(goldenSeat, Achievement.GoldenWin), feats);
        return feats;
    }

    /// <summary>
    /// Forgets the last match
    /// </summary>
    public void NewMatch()
    {
        earned.Clear();
        lastAtGolden.Clear();
        goldenSeat = -1;
    }

    /// <summary>
    /// Whether an earned achievement unlocks on this machine: offline every seat is this PC's Steam user; online only
    /// this machine's own seats are (several players can share its screen, all on its Steam user, as offline)
    /// </summary>
    public static bool UnlocksHere(bool online, int seat, ICollection<int> ownSeats)
    {
        return !online || (ownSeats != null && ownSeats.Contains(seat));
    }

    // The lowest and highest scores, or false without any
    static bool ScoreRange(IReadOnlyDictionary<int, int> scoresBySeat, out int lowest, out int highest)
    {
        lowest = int.MaxValue;
        highest = int.MinValue;
        foreach (int score in scoresBySeat.Values)
        {
            lowest = Mathf.Min(lowest, score);
            highest = Mathf.Max(highest, score);
        }
        return scoresBySeat.Count > 0;
    }

    void Earn(Feat feat, List<Feat> feats)
    {
        if (earned.Add(feat))
            feats.Add(feat);
    }
}

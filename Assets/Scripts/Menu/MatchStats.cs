using System.Collections.Generic;

/// <summary>
/// An end-of-match award (AwardRules picks one per player). The order is the awards' rank, best first; None is no card
/// </summary>
public enum Award : byte
{
    None,
    GoldenGrip,
    Kleptoghost,
    BusyBee,
    HighRoller,
    Butterfingers,
    FishFood,
    Tourist,
}

/// <summary>
/// What each seat did in a match, for the awards, counted by the machine that runs the rules (this PC offline, the host
/// online). The tutorial's count for nothing, as MatchFeats. A new match starts at the menus or a rematch (NewMatch),
/// not at a load: the golden round is a load of its own
/// </summary>
public class MatchStats
{
    readonly Dictionary<int, int>[] counts = new Dictionary<int, int>[(int)Award.Tourist];
    readonly Dictionary<int, float> goldenSeconds = new Dictionary<int, float>();

    public MatchStats()
    {
        for (int i = 0; i < counts.Length; i++)
            counts[i] = new Dictionary<int, int>();
    }

    /// <summary>A player delivered an order: a delivery, and a hard one when it was</summary>
    public void Delivered(int seat, Constants.OrderValue value, GameState state)
    {
        if (state == GameState.Tutorial)
            return;
        Add(seat, Award.BusyBee);
        if (value == Constants.OrderValue.Hard)
            Add(seat, Award.HighRoller);
    }

    /// <summary>A player stole an order</summary>
    public void Stole(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.Kleptoghost);
    }

    /// <summary>A player had an order stolen</summary>
    public void Robbed(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.Butterfingers);
    }

    /// <summary>A player fell in the water</summary>
    public void Sank(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.FishFood);
    }

    /// <summary>A player held the golden order a while longer</summary>
    public void HeldGolden(int seat, float seconds)
    {
        goldenSeconds.TryGetValue(seat, out float held);
        goldenSeconds[seat] = held + seconds;
    }

    /// <summary>A seat's count towards an award: golden time in seconds, to the nearest</summary>
    public int Count(int seat, Award award)
    {
        if (award == Award.GoldenGrip)
            return goldenSeconds.TryGetValue(seat, out float held) ? (int)(held + 0.5f) : 0;
        if (award == Award.None || award >= Award.Tourist)
            return 0;
        return counts[(int)award].TryGetValue(seat, out int count) ? count : 0;
    }

    /// <summary>Forgets the last match</summary>
    public void NewMatch()
    {
        foreach (Dictionary<int, int> count in counts)
            count.Clear();
        goldenSeconds.Clear();
    }

    void Add(int seat, Award award)
    {
        Dictionary<int, int> count = counts[(int)award];
        count.TryGetValue(seat, out int now);
        count[seat] = now + 1;
    }
}

using System.Collections.Generic;

/// <summary>
/// An end-of-match award (AwardRules picks one per player). None is no card; AwardRules.PRIORITY is their rank
/// </summary>
public enum Award : byte
{
    None,
    GoldenGrip,
    Kleptoghost,
    BusyBee,
    HighRoller,
    FrontRunner,
    Hothead,
    RoadHog,
    Butterfingers,
    FishFood,
    RoadTrip,
    Tourist,
}

/// <summary>
/// What each seat did in a match, for the awards, counted by the machine that runs the rules
/// (this PC offline, the host online). The tutorial's count for nothing, as MatchFeats. A new match starts at the menus
/// or a rematch (NewMatch), not at a load: the golden round is a load of its own
/// </summary>
public class MatchStats
{
    // Each award's total per seat: counts, seconds (golden time, time in the lead) or metres (distance)
    readonly Dictionary<int, float>[] totals = new Dictionary<int, float>[(int)Award.Tourist];

    public MatchStats()
    {
        for (int i = 0; i < totals.Length; i++)
            totals[i] = new Dictionary<int, float>();
    }

    /// <summary>A player delivered an order: a delivery, and a hard one when it was</summary>
    public void Delivered(int seat, Constants.OrderValue value, GameState state)
    {
        if (state == GameState.Tutorial)
            return;
        Add(seat, Award.BusyBee, 1f);
        if (value == Constants.OrderValue.Hard)
            Add(seat, Award.HighRoller, 1f);
    }

    /// <summary>A player stole an order</summary>
    public void Stole(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.Kleptoghost, 1f);
    }

    /// <summary>A player had an order stolen</summary>
    public void Robbed(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.Butterfingers, 1f);
    }

    /// <summary>A player fell in the water</summary>
    public void Sank(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.FishFood, 1f);
    }

    /// <summary>A player bumped another without boosting</summary>
    public void Bumped(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.RoadHog, 1f);
    }

    /// <summary>A player met another head-on, both boosting</summary>
    public void Clashed(int seat, GameState state)
    {
        if (state != GameState.Tutorial)
            Add(seat, Award.Hothead, 1f);
    }

    /// <summary>A player held the golden order a while longer</summary>
    public void HeldGolden(int seat, float seconds)
    {
        Add(seat, Award.GoldenGrip, seconds);
    }

    /// <summary>A player was alone in first place a while longer</summary>
    public void Led(int seat, float seconds)
    {
        Add(seat, Award.FrontRunner, seconds);
    }

    /// <summary>A player drove a little further, in metres</summary>
    public void Drove(int seat, float metres)
    {
        Add(seat, Award.RoadTrip, metres);
    }

    /// <summary>A seat's count towards an award: times and distance to the nearest second or metre</summary>
    public int Count(int seat, Award award)
    {
        if (award == Award.None || award >= Award.Tourist)
            return 0;
        return totals[(int)award].TryGetValue(seat, out float total) ? (int)(total + 0.5f) : 0;
    }

    /// <summary>Forgets the last match</summary>
    public void NewMatch()
    {
        foreach (Dictionary<int, float> total in totals)
            total.Clear();
    }

    void Add(int seat, Award award, float amount)
    {
        Dictionary<int, float> total = totals[(int)award];
        total.TryGetValue(seat, out float now);
        total[seat] = now + amount;
    }
}

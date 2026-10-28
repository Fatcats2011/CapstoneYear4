using UnityEngine;

/// <summary>
/// A CPU driver's round, counted for the log (BotDriver writes the line as the round ends), so a playtest can check the
/// personalities really play apart: steal tries and steals, pickups, the average value taken and how far it went for it
/// </summary>
public class BotStats
{
    int stealTries, steals, pickups;
    float totalValue, totalPickupDistance;

    /// <summary>It boosted into a victim</summary>
    public void Tried()
    {
        stealTries++;
    }

    /// <summary>It took an order off a victim</summary>
    public void Stole()
    {
        steals++;
    }

    /// <summary>It picked up an order worth this, chosen this far away (metres)</summary>
    public void PickedUp(int worth, float distance)
    {
        pickups++;
        totalValue += worth;
        totalPickupDistance += distance;
    }

    /// <summary>The round in a line</summary>
    public string Line(BotKind kind, int seat)
    {
        int value = pickups > 0 ? Mathf.RoundToInt(totalValue / pickups) : 0;
        int distance = pickups > 0 ? Mathf.RoundToInt(totalPickupDistance / pickups) : 0;
        return "Bot seat " + seat + " " + kind + ": " + stealTries + " steal tries, " + steals + " steals, " + pickups
            + " pickups, avg value " + value + ", avg pickup " + distance + " m";
    }
}

using System;

/// <summary>Where money came from; also the order the results cards list it</summary>
public enum MoneySource : byte { Deliveries, Steals, Golden, Mode }

/// <summary>
/// How a score's money divides among the sources. Every gain to a score names one, so a seat's four always add up to its
/// score
/// </summary>
public static class MoneyRules
{
    public const int SOURCES = 4;

    /// <summary>
    /// What a delivered order's pay counts as. The golden order: all Golden. The Mother Lode: all Deliveries. Otherwise
    /// Deliveries is the tier's plain price and Steals is the rest (never negative: the parts always add up to worth)
    /// </summary>
    public static void SplitDelivery(Constants.OrderValue tier, bool isLode, int worth, out int deliveries, out int steals, out int golden)
    {
        deliveries = steals = golden = 0;
        if (isLode)
            deliveries = worth;
        else if (tier == Constants.OrderValue.Golden)
            golden = worth;
        else
        {
            deliveries = Math.Min(StealValueRules.Price(tier), worth);
            steals = worth - deliveries;
        }
    }
}

/// <summary>Money by source, per seat, for the match so far</summary>
public class MoneyTally
{
    readonly int[,] amounts = new int[Constants.MAX_PLAYERS, MoneyRules.SOURCES];

    /// <summary>Counts some money. Nothing for a seat that doesn't exist or for no money</summary>
    public void Add(int seat, MoneySource source, int amount)
    {
        if (seat < 0 || seat >= Constants.MAX_PLAYERS || amount <= 0)
            return;
        amounts[seat, (int)source] += amount;
    }

    public int Of(int seat, MoneySource source)
    {
        if (seat < 0 || seat >= Constants.MAX_PLAYERS)
            return 0;
        return amounts[seat, (int)source];
    }

    /// <summary>All four sources of a seat summed</summary>
    public int Total(int seat)
    {
        int total = 0;
        for (int source = 0; source < MoneyRules.SOURCES; source++)
            total += Of(seat, (MoneySource)source);
        return total;
    }

    public void NewMatch()
    {
        Array.Clear(amounts, 0, amounts.Length);
    }
}

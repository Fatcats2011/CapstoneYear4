using System.Collections.Generic;

/// <summary>
/// The money breakdown on a results card: which of a seat's sources paid something (listed two to a row, in
/// MoneySource order), how many rows that makes, and how much taller they make the card
/// </summary>
public static class BreakdownRules
{
    /// <summary>One row's height on the card, in the canvas's 1080</summary>
    public const float ROW = 22f;
    public const float TEXT = 16f;

    /// <summary>The label's string key</summary>
    public static string Key(MoneySource source)
    {
        switch (source)
        {
            case MoneySource.Steals: return "results.steals";
            case MoneySource.Golden: return "results.golden";
            case MoneySource.Mode: return "results.mode";
            default: return "results.deliveries";
        }
    }

    /// <summary>The rows a number of sources fill: two to a row</summary>
    public static int Rows(int shown)
    {
        return shown <= 0 ? 0 : (shown + 1) / 2;
    }

    public static float ExtraHeight(int shown)
    {
        return Rows(shown) * ROW;
    }

    /// <summary>Fills the list with the seat's sources that paid something, in MoneySource order</summary>
    public static void Shown(AwardBoard board, int seat, List<MoneySource> into)
    {
        into.Clear();
        for (int i = 0; i < MoneyRules.SOURCES; i++)
        {
            MoneySource source = (MoneySource)i;
            if (board.MoneyOf(seat, source) > 0)
                into.Add(source);
        }
    }
}

/// <summary>
/// What an order's price reads: PriceText, "$" and its worth, on the order's beacon (PriceTag) and on its drop-off's compass mark.
/// Never on the golden order, which pays no price. The Mother Lode is golden too, but it does
/// </summary>
public static class OrderFeedbackRules
{
    /// <summary>"" for the golden order (not the lode); otherwise "$" and what the order is worth now</summary>
    public static string PriceText(Constants.OrderValue tier, bool isLode, int worth)
    {
        if (tier == Constants.OrderValue.Golden && !isLode)
            return "";
        return "$" + worth;
    }
}

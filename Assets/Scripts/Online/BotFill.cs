/// <summary>
/// Online bots: how many CPU drivers an online match starts with. Empty seats fill up to the full four unless the
/// host's match rules turned Bots off. See docs/superpowers/specs/2026-10-28-online-bots-design.md
/// </summary>
public static class BotFill
{
    /// <summary>
    /// How many bots to add as the match starts
    /// </summary>
    /// <param name="seated">The human players seated</param>
    /// <param name="botsOn">The match's Bots rule</param>
    public static int Needed(int seated, bool botsOn)
    {
        if (!botsOn || seated < 1)
            return 0;

        int empty = Constants.MAX_PLAYERS - seated;
        return empty < 0 ? 0 : empty;
    }
}

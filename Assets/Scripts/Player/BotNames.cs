using System.Collections.Generic;

/// <summary>
/// A CPU driver's name: a plain human name that fits its personality (playtest, 2026-11-02), shown wherever a player's
/// name is (the scoreboard, Dispatch, the pause menu). Never one another player in the session already has
/// </summary>
public static class BotNames
{
    static readonly string[] thieves = { "Vinnie", "Rocco", "Nicky", "Frankie", "Sal", "Lenny" };
    static readonly string[] hunters = { "Hank", "Buck", "Wade", "Earl", "Duke", "Colt" };
    static readonly string[] couriers = { "Penny", "Skip", "Zoe", "Max", "Dot", "Benny" };

    /// <summary>The names a personality draws from</summary>
    public static IReadOnlyList<string> For(BotKind kind)
    {
        switch (kind)
        {
            case BotKind.Thief: return thieves;
            case BotKind.Hunter: return hunters;
            default: return couriers;
        }
    }

    /// <summary>
    /// A name for a bot of this personality that isn't taken, chosen by roll (0 up to any number); every name taken: the
    /// first, with its rank on the end so it still differs ("Vinnie 2")
    /// </summary>
    public static string Pick(BotKind kind, ICollection<string> taken, int roll)
    {
        IReadOnlyList<string> names = For(kind);
        int start = (roll % names.Count + names.Count) % names.Count;
        for (int i = 0; i < names.Count; i++)
        {
            string name = names[(start + i) % names.Count];
            if (!taken.Contains(name))
                return name;
        }
        for (int n = 2; ; n++)
        {
            string name = names[start] + " " + n;
            if (!taken.Contains(name))
                return name;
        }
    }
}

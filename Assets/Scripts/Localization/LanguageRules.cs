using UnityEngine;

/// <summary>
/// Which language the game speaks: the one saved (Options → LANGUAGE), else the player's Steam language, else the PC's,
/// else English. Steam's names ("french", "latam", "brazilian"…) and the PC's map to the game's locales (LocaleCodes).
/// The LANGUAGE row steps through them in order and shows each by its own name
/// </summary>
public static class LanguageRules
{
    static readonly string[,] STEAM =
    {
        { "english", "en" }, { "french", "fr" }, { "german", "de" }, { "italian", "it" }, { "dutch", "nl" },
        { "spanish", "es" }, { "latam", "es-419" }, { "portuguese", "pt" }, { "brazilian", "pt-BR" },
        { "polish", "pl" }, { "turkish", "tr" }, { "czech", "cs" }, { "romanian", "ro" }, { "hungarian", "hu" },
        { "swedish", "sv" }, { "danish", "da" },
    };

    static readonly string[,] NAMES =
    {
        { "en", "ENGLISH" }, { "fr", "FRANÇAIS" }, { "de", "DEUTSCH" }, { "it", "ITALIANO" }, { "nl", "NEDERLANDS" },
        { "es", "ESPAÑOL" }, { "es-419", "ESPAÑOL (LATAM)" }, { "pt", "PORTUGUÊS" }, { "pt-BR", "PORTUGUÊS (BRASIL)" },
        { "pl", "POLSKI" }, { "tr", "TÜRKÇE" }, { "cs", "ČEŠTINA" }, { "ro", "ROMÂNĂ" }, { "hu", "MAGYAR" },
        { "sv", "SVENSKA" }, { "da", "DANSK" },
    };

    /// <summary>The locale for Steam's language name, or null for one the game doesn't speak</summary>
    public static string FromSteam(string steamLanguage)
    {
        for (int i = 0; i < STEAM.GetLength(0); i++)
        {
            if (STEAM[i, 0] == steamLanguage)
                return STEAM[i, 1];
        }
        return null;
    }

    /// <summary>The locale for the PC's language, or null for one the game doesn't speak</summary>
    public static string FromSystem(SystemLanguage language)
    {
        switch (language)
        {
            case SystemLanguage.English: return "en";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Dutch: return "nl";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.Polish: return "pl";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Czech: return "cs";
            case SystemLanguage.Romanian: return "ro";
            case SystemLanguage.Hungarian: return "hu";
            case SystemLanguage.Swedish: return "sv";
            case SystemLanguage.Danish: return "da";
            default: return null;
        }
    }

    /// <summary>The language to speak: the saved one if the game knows it, else Steam's, else the PC's, else English</summary>
    public static string Pick(string saved, string steamLanguage, SystemLanguage system)
    {
        if (LocaleCodes.Known(saved))
            return saved;
        return FromSteam(steamLanguage) ?? FromSystem(system) ?? LocaleCodes.ENGLISH;
    }

    /// <summary>The LANGUAGE row's next language right (true) or left, stopping at the ends</summary>
    public static string Next(string code, bool right)
    {
        int index = System.Array.IndexOf(LocaleCodes.All, code);
        if (index < 0)
            return LocaleCodes.ENGLISH;
        int next = Mathf.Clamp(index + (right ? 1 : -1), 0, LocaleCodes.All.Length - 1);
        return LocaleCodes.All[next];
    }

    /// <summary>A language's name in itself, in capitals ("FRANÇAIS")</summary>
    public static string Name(string code)
    {
        for (int i = 0; i < NAMES.GetLength(0); i++)
        {
            if (NAMES[i, 0] == code)
                return NAMES[i, 1];
        }
        return "";
    }
}

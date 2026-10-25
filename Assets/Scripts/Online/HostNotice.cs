using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// What a host tells a player it turns away or sends off (a full match, another version, taken out of the rematch):
/// sent in Netcode's reason as a key and its arguments ("#loc:online.version|1.2|1.1"), so each player reads it in their
/// own language (Loc). A reason without a key (Netcode's own, an older build's) shows as it came, and so does a "key" the
/// game doesn't know: a host's words are never taken for markup or for a key. See docs/online.md
/// </summary>
public static class HostNotice
{
    public const string PREFIX = "#loc:";
    const char SPLIT = '|';
    static readonly Regex KEY = new Regex("^[a-z0-9.]{1,64}$");
    static readonly Regex PLACEHOLDER = new Regex(@"\{(\d+)");

    /// <summary>A notice as the host sends it</summary>
    public static string Encode(string key, params string[] args)
    {
        StringBuilder notice = new StringBuilder(PREFIX).Append(key);
        foreach (string arg in args)
            notice.Append(SPLIT).Append((arg ?? "").Replace(SPLIT, '/'));
        return notice.ToString();
    }

    /// <summary>A reason as the player reads it: a known key's text with its arguments, else the reason as it came</summary>
    public static string Shown(string reason)
    {
        if (reason == null || !reason.StartsWith(PREFIX))
            return reason;
        string[] parts = reason.Substring(PREFIX.Length).Split(SPLIT);
        string key = parts[0];
        string english = KEY.IsMatch(key) ? Loc.Csv.Get(key, LocaleCodes.ENGLISH) : null;
        if (english == null || Arguments(english) != parts.Length - 1)
            return reason;
        object[] args = new object[parts.Length - 1];
        for (int i = 1; i < parts.Length; i++)
            args[i - 1] = parts[i];
        return Loc.Get(key, args);
    }

    // How many arguments a text takes: its highest {n}, plus one
    static int Arguments(string text)
    {
        int count = 0;
        foreach (Match placeholder in PLACEHOLDER.Matches(text))
            count = System.Math.Max(count, int.Parse(placeholder.Groups[1].Value) + 1);
        return count;
    }
}

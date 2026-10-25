using System;
using System.Text;

/// <summary>A drawn accent mark, placed over (or under, or across) a letter whose accented form a font lacks</summary>
public enum AccentMark { Caron, Breve, Dot, DoubleAcute, Ring, Ogonek, CommaBelow, Acute, LStroke }

/// <summary>A character as its base letter and the mark that makes it</summary>
public readonly struct Accented
{
    public readonly char Base;
    public readonly AccentMark Mark;

    public Accented(char baseLetter, AccentMark mark)
    {
        Base = baseLetter;
        Mark = mark;
    }
}

/// <summary>
/// The accented letters the game's fonts lack (Jack's Candlestick and Bangers stop at Western Europe's): Polish,
/// Czech, Turkish, Romanian and Hungarian ones. Each is shown as its base letter with a drawn mark placed on it
/// (AccentOverlay), so the fonts stay untouched. Capital sharp s is written SS, as German does in capitals.
/// See docs/superpowers/specs/2026-10-24-localization-design.md
/// </summary>
public static class AccentRules
{
    const string LETTERS = "ĂăĄąĆćČčĎďĘęĚěĞğİŁłŃńŇňŐőŘřŚśŞşŤťŮůŰűŹźŻżȘșȚțŠšŽž";
    const string BASES   = "AaAaCcCcDdEeEeGgILlNnNnOoRrSsSsTtUuUuZzZzSsTtSsZz";

    static readonly AccentMark[] MARKS =
    {
        AccentMark.Breve, AccentMark.Breve, AccentMark.Ogonek, AccentMark.Ogonek, AccentMark.Acute, AccentMark.Acute,
        AccentMark.Caron, AccentMark.Caron, AccentMark.Caron, AccentMark.Caron, AccentMark.Ogonek, AccentMark.Ogonek,
        AccentMark.Caron, AccentMark.Caron, AccentMark.Breve, AccentMark.Breve, AccentMark.Dot, AccentMark.LStroke,
        AccentMark.LStroke, AccentMark.Acute, AccentMark.Acute, AccentMark.Caron, AccentMark.Caron, AccentMark.DoubleAcute,
        AccentMark.DoubleAcute, AccentMark.Caron, AccentMark.Caron, AccentMark.Acute, AccentMark.Acute, AccentMark.CommaBelow,
        AccentMark.CommaBelow, AccentMark.Caron, AccentMark.Caron, AccentMark.Ring, AccentMark.Ring, AccentMark.DoubleAcute,
        AccentMark.DoubleAcute, AccentMark.Acute, AccentMark.Acute, AccentMark.Dot, AccentMark.Dot, AccentMark.CommaBelow,
        AccentMark.CommaBelow, AccentMark.CommaBelow, AccentMark.CommaBelow, AccentMark.Caron, AccentMark.Caron,
        AccentMark.Caron, AccentMark.Caron,
    };

    /// <summary>Whether a character is one the overlay draws, and how</summary>
    public static bool TryGet(char c, out Accented accented)
    {
        int index = LETTERS.IndexOf(c);
        if (index < 0)
        {
            accented = default;
            return false;
        }
        accented = new Accented(BASES[index], MARKS[index]);
        return true;
    }

    /// <summary>
    /// Text with every character the font lacks that the overlay draws replaced by its base letter, and capital sharp s
    /// by SS. Text that needs nothing comes back as it was (the same string)
    /// </summary>
    public static string Replace(string text, Func<char, bool> fontHas)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        StringBuilder replaced = null;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            string with = null;
            if (c == 'ẞ' && !fontHas(c))
                with = "SS";
            else if (TryGet(c, out Accented accented) && !fontHas(c))
                with = accented.Base.ToString();

            if (with != null && replaced == null)
                replaced = new StringBuilder(text, 0, i, text.Length + 4);
            if (replaced != null)
                replaced.Append(with ?? c.ToString());
        }
        return replaced != null ? replaced.ToString() : text;
    }
}

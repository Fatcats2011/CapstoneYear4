/// <summary>
/// An entry on the title screen
/// </summary>
public enum TitleEntry { None, Local, Online, Options, Credits, Quit }

/// <summary>
/// What each place on the title screen does. The entries are images in the scene (MainMenu's selector objects), so how
/// many there are decides it: with 5, Local Play, Online Play, Options, Credits, Quit; with 4 (before the Online Play
/// art was added), the old title screen, where Y in player select goes online instead (OnlinePlay.YAction)
/// </summary>
public static class TitleEntries
{
    static readonly TitleEntry[] withOnline =
        { TitleEntry.Local, TitleEntry.Online, TitleEntry.Options, TitleEntry.Credits, TitleEntry.Quit };
    static readonly TitleEntry[] withoutOnline =
        { TitleEntry.Local, TitleEntry.Options, TitleEntry.Credits, TitleEntry.Quit };

    /// <summary>
    /// The entry at a place on a title screen with this many entries (None outside them, or for a count it doesn't know)
    /// </summary>
    public static TitleEntry At(int index, int count)
    {
        TitleEntry[] entries = count == withOnline.Length ? withOnline
            : count == withoutOnline.Length ? withoutOnline
            : null;

        if (entries == null || index < 0 || index >= entries.Length)
            return TitleEntry.None;

        return entries[index];
    }

    /// <summary>
    /// Whether a title screen with this many entries has Online Play
    /// </summary>
    public static bool HasOnline(int count)
    {
        return count == withOnline.Length;
    }
}

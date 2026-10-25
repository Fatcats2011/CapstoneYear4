using System.Collections.Generic;

/// <summary>
/// The Options screen's Resolution row (ResolutionRow): the sizes it offers (the monitor's, each once, smallest first),
/// where it starts, how left and right step (stopping at both ends, like the other rows), and what it says. A picked
/// size waits for A before it's applied. See docs/controls.md
/// </summary>
public static class ResolutionRules
{
    /// <summary>
    /// Each size the monitor lists, once (it lists one per refresh rate), smallest first
    /// </summary>
    public static List<ScreenSize> Choices(IEnumerable<ScreenSize> listed)
    {
        List<ScreenSize> choices = new List<ScreenSize>();
        foreach (ScreenSize size in listed)
        {
            if (!choices.Contains(size))
                choices.Add(size);
        }
        choices.Sort(Compare);
        return choices;
    }

    /// <summary>
    /// Where the row starts: the saved size if the monitor lists it, else the current size, else the largest
    /// </summary>
    public static int StartIndex(List<ScreenSize> choices, ScreenSize saved, ScreenSize current)
    {
        int index = choices.IndexOf(saved);
        if (index < 0)
            index = choices.IndexOf(current);
        return index >= 0 ? index : choices.Count - 1;
    }

    /// <summary>
    /// The next size left or right, stopping at both ends
    /// </summary>
    public static int Step(int index, int count, bool right)
    {
        if (right)
            return index < count - 1 ? index + 1 : index;
        return index > 0 ? index - 1 : index;
    }

    /// <summary>
    /// What the row shows: the size, and that A applies it while it isn't applied yet
    /// </summary>
    public static string Label(ScreenSize size, bool waitingForA)
    {
        string label = "<  " + size.Width + " x " + size.Height + "  >";
        return waitingForA ? label + "   " + Loc.Get("options.resolution.apply") : label;
    }

    static int Compare(ScreenSize a, ScreenSize b)
    {
        return a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height);
    }
}

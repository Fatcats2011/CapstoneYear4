using System;
using System.Collections.Generic;

/// <summary>
/// One of the Options screen's code-built rows: its label, its choices (values, each with the words shown), the one
/// picked, and whether it's dimmed (it does nothing now, like Frame cap while VSync is on). Left and right step the
/// choices and stop at the ends, as the drawn rows do. A saved value that isn't one of the choices (settings.cfg edited
/// by hand) is added among them in order, so nothing changes until another is picked
/// </summary>
public class OptionSetting
{
    readonly List<int> values;
    readonly Func<int, string> labelOf;
    int index;

    /// <summary>Says when the row isn't in use (null: never)</summary>
    public Func<bool> Dimmed;

    /// <summary>A step picked another choice (its value)</summary>
    public event Action<int> Changed;

    readonly string label;

    /// <summary>The key of the label's text (Loc), shown in the language spoken now; null: the label as given</summary>
    public string LabelKey { get; set; }

    public string Label => LabelKey != null ? Loc.Get(LabelKey) : label;
    public int Value => values[index];
    public string ValueLabel => labelOf(Value);
    public bool IsDimmed => Dimmed != null && Dimmed();

    public OptionSetting(string label, IEnumerable<int> choices, Func<int, string> labelOf, int value)
    {
        this.label = label;
        this.labelOf = labelOf;
        values = new List<int>(choices);
        index = values.IndexOf(value);
        if (index < 0)
        {
            index = values.FindIndex(listed => listed > value);
            if (index < 0)
                index = values.Count;
            values.Insert(index, value);
        }
    }

    /// <summary>A row of two choices: 0 shows off, 1 shows on</summary>
    public static OptionSetting Toggle(string label, string off, string on, int value)
    {
        string[] words = { off, on };
        return new OptionSetting(label, new[] { 0, 1 }, choice => words[choice], value == 1 ? 1 : 0);
    }

    /// <summary>A row in the language spoken: its label and choices are keys (Loc), read each time they show</summary>
    public static OptionSetting Keyed(string labelKey, IEnumerable<int> choices, Func<int, string> choiceKey, int value)
    {
        return new OptionSetting(labelKey, choices, choice => Loc.Get(choiceKey(choice)), value) { LabelKey = labelKey };
    }

    /// <summary>A two-choice row in the language spoken: off and on are keys</summary>
    public static OptionSetting KeyedToggle(string labelKey, string offKey, string onKey, int value)
    {
        string[] keys = { offKey, onKey };
        return Keyed(labelKey, new[] { 0, 1 }, choice => keys[choice], value == 1 ? 1 : 0);
    }

    /// <summary>Right (true) or left: the next choice. False at the end, or while dimmed</summary>
    public bool Step(bool right)
    {
        int next = index + (right ? 1 : -1);
        if (IsDimmed || next < 0 || next >= values.Count)
            return false;

        index = next;
        Changed?.Invoke(Value);
        return true;
    }
}

/// <summary>A page of the Options screen: its name and its code-built rows (none for the drawn GENERAL page)</summary>
public class OptionsPage
{
    readonly string name;

    /// <summary>The key of the page's name (Loc); null: the name as given</summary>
    public string NameKey { get; set; }

    public string Name => NameKey != null ? Loc.Get(NameKey) : name;
    public IReadOnlyList<OptionSetting> Settings { get; }

    public OptionsPage(string name, params OptionSetting[] settings)
    {
        this.name = name;
        Settings = settings;
    }

    /// <summary>A page whose name is a key (Loc)</summary>
    public static OptionsPage Keyed(string nameKey, params OptionSetting[] settings)
    {
        return new OptionsPage(nameKey, settings) { NameKey = nameKey };
    }
}

/// <summary>
/// The Options screen's pages and where the selector is: the shoulders turn the pages round (each starts at its top row),
/// up and down wrap within a page, and left and right step the selected row. OptionsMenu shows it
/// </summary>
public class OptionsBook
{
    readonly OptionsPage[] pages;

    public int Page { get; private set; }
    public int Row { get; private set; }
    public IReadOnlyList<OptionsPage> Pages => pages;
    public OptionsPage Current => pages[Page];

    /// <summary>The selected row; null on a page with none</summary>
    public OptionSetting Selected => Current.Settings.Count > 0 ? Current.Settings[Row] : null;

    public OptionsBook(params OptionsPage[] pages)
    {
        this.pages = pages;
    }

    /// <summary>Right shoulder (true) or left: the next page, round from the last to the first</summary>
    public void TurnPage(bool right)
    {
        Page = (Page + (right ? 1 : pages.Length - 1)) % pages.Length;
        Row = 0;
    }

    /// <summary>Down (true) or up: the next row on this page, round from the last to the first</summary>
    public void Move(bool down)
    {
        int count = Current.Settings.Count;
        if (count > 0)
            Row = (Row + (down ? 1 : count - 1)) % count;
    }

    /// <summary>Picks a row on this page (the mouse)</summary>
    public void Select(int row)
    {
        if (row >= 0 && row < Current.Settings.Count)
            Row = row;
    }

    /// <summary>Right (true) or left on the selected row. False when nothing changed</summary>
    public bool Step(bool right)
    {
        return Selected != null && Selected.Step(right);
    }

    /// <summary>Leaving Options: back to the first page's top</summary>
    public void Reset()
    {
        Page = 0;
        Row = 0;
    }
}

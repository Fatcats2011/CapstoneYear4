using TMPro;
using UnityEngine;

/// <summary>
/// A row on one of the Options pages, built by code like the Resolution row (same sizes and spacing): its setting's
/// label and the choice it's on, in the screen's own font and colour, greyed while the setting is dimmed (Frame cap with
/// VSync on). OptionsMenu calls Refresh after a step
/// </summary>
public class OptionRow : MonoBehaviour
{
    const float LABEL_SIZE = 64f;
    const float VALUE_SIZE = 40f;
    const float LABEL_LEFT = -160f; // as ResolutionRow: the label's left edge, from the row's centre
    const float VALUE_X = 665f;     // as ResolutionRow: the value's centre, from the row's centre
    const float DIMMED_ALPHA = 0.35f;
    const float GAP = 40f;          // at least, between a label's end and its value
    const float VALUE_RIGHT = 1050f; // the furthest a value's right edge goes, from the row's centre (the screen's edge)
    const float LABEL_MIN_SIZE = 40f;

    OptionSetting setting;
    TMP_Text label;
    Color colour;

    public TMP_Text Value { get; private set; }
    internal TMP_Text Label => label;
    public string LabelText => label.text;
    public OptionSetting Setting => setting;

    /// <summary>Builds a row for a setting on a page, at a spot on it</summary>
    public static OptionRow Create(RectTransform page, TMP_Text style, OptionSetting setting, Vector2 position)
    {
        GameObject holder = new GameObject(setting.Label, typeof(RectTransform));
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.SetParent(page, false);
        rect.sizeDelta = new Vector2(100f, 100f);
        rect.anchoredPosition = position;

        OptionRow row = holder.AddComponent<OptionRow>();
        row.setting = setting;
        row.label = ResolutionRow.Text(rect, "Label", style, LABEL_SIZE, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(LABEL_LEFT, 0f), 700f);
        row.label.text = setting.Label;
        row.Value = ResolutionRow.Text(rect, "Value", style, VALUE_SIZE, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(VALUE_X, 0f), 700f);
        row.colour = row.Value.color;
        row.Refresh();
        return row;
    }

    /// <summary>Shows the setting as it is now</summary>
    public void Refresh()
    {
        label.text = setting.Label;
        Value.text = setting.ValueLabel;
        Color shown = colour;
        if (setting.IsDimmed)
            shown.a *= DIMMED_ALPHA;
        Value.color = shown;
        Color labelColour = label.color;
        labelColour.a = shown.a;
        label.color = labelColour;
    }

    /// <summary>
    /// A page's values in one column, clear of its longest label: a translation can be twice as long as the English
    /// (playtest, 2026-10-24: Hungarian ran into its values). The column moves right, as far as the screen allows; only a
    /// label too long even then shrinks, just enough
    /// </summary>
    public static void AlignColumn(System.Collections.Generic.IReadOnlyList<OptionRow> rows)
    {
        float column = VALUE_X;
        float widestValue = 0f;
        foreach (OptionRow row in rows)
        {
            row.label.fontSize = LABEL_SIZE;
            float valueHalf = row.Value.GetPreferredValues(row.Value.text).x / 2f;
            widestValue = Mathf.Max(widestValue, valueHalf);
            column = Mathf.Max(column, LABEL_LEFT + row.label.GetPreferredValues(row.label.text).x + GAP + valueHalf);
        }
        column = Mathf.Min(column, Mathf.Max(VALUE_X, VALUE_RIGHT - widestValue));

        foreach (OptionRow row in rows)
        {
            RectTransform value = row.Value.rectTransform;
            value.anchoredPosition = new Vector2(column, value.anchoredPosition.y);

            float room = column - row.Value.GetPreferredValues(row.Value.text).x / 2f - GAP - LABEL_LEFT;
            float width = row.label.GetPreferredValues(row.label.text).x;
            if (width > room)
                row.label.fontSize = Mathf.Max(LABEL_MIN_SIZE, Mathf.Floor(LABEL_SIZE * room / width));
        }
    }
}

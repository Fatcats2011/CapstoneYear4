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

    OptionSetting setting;
    TMP_Text label;
    Color colour;

    public TMP_Text Value { get; private set; }
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
}

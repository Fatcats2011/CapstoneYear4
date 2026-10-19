using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The Options screen's Resolution row, built by code under its last row (the screen's rows are drawn art, and this one
/// shows a size that depends on the monitor): "RESOLUTION" and the size, in the screen's own font and colour. Left and
/// right pick a size (ResolutionRules); A applies it (OptionsMenu); leaving Options without A keeps the applied one.
/// See docs/controls.md
/// </summary>
public class ResolutionRow : MonoBehaviour
{
    /// <summary>How far below the row above this one sits, in canvas units (the drawn rows are about 105 apart)</summary>
    public const float ROW_SPACING = 110f;

    const float LABEL_SIZE = 64f;
    const float VALUE_SIZE = 40f;
    const float LABEL_LEFT = -160f; // the label's left edge, from the row's centre (the drawn labels start about there)
    const float VALUE_X = 665f;     // the size's centre, from the row's centre (under the Fullscreen row's Off and On)

    List<ScreenSize> choices;
    int shown;
    int applied;
    TMP_Text label;

    /// <summary>The size text</summary>
    public TMP_Text Value { get; private set; }

    public string LabelText => label.text;

    /// <summary>The size on show (picked with left and right)</summary>
    public ScreenSize Shown => choices[shown];

    /// <summary>The size last applied (or saved)</summary>
    public ScreenSize Applied => choices[applied];

    /// <summary>
    /// Builds the row under another, as its sibling
    /// </summary>
    /// <param name="rowAbove">The screen's last row (its selector spot)</param>
    /// <param name="style">A text on the screen whose font, material and colour the row takes (null: TextMesh Pro's default)</param>
    /// <param name="listed">The monitor's sizes</param>
    /// <param name="saved">The saved size (0 x 0: none)</param>
    /// <param name="current">The size the game shows at now</param>
    public static ResolutionRow Create(RectTransform rowAbove, TMP_Text style, IEnumerable<ScreenSize> listed, ScreenSize saved, ScreenSize current)
    {
        GameObject holder = new GameObject("Resolution", typeof(RectTransform));
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.SetParent(rowAbove.parent, false);
        rect.anchorMin = rowAbove.anchorMin;
        rect.anchorMax = rowAbove.anchorMax;
        rect.sizeDelta = new Vector2(100f, 100f);
        rect.anchoredPosition = new Vector2(rowAbove.anchoredPosition.x, rowAbove.anchoredPosition.y - ROW_SPACING);

        ResolutionRow row = holder.AddComponent<ResolutionRow>();
        row.choices = ResolutionRules.Choices(listed);
        if (row.choices.Count == 0)
            row.choices.Add(current);
        row.shown = row.applied = ResolutionRules.StartIndex(row.choices, saved, current);

        row.label = Text(rect, "Label", style, LABEL_SIZE, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(LABEL_LEFT, 0f), 700f);
        row.label.text = "RESOLUTION";
        row.Value = Text(rect, "Size", style, VALUE_SIZE, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(VALUE_X, 0f), 700f);
        row.Show();
        return row;
    }

    static TMP_Text Text(RectTransform parent, string name, TMP_Text style, float size, TextAlignmentOptions alignment, Vector2 pivot, Vector2 position, float width)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.pivot = pivot;
        rect.sizeDelta = new Vector2(width, size * 1.5f);
        rect.anchoredPosition = position;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (style != null)
        {
            text.font = style.font;
            text.fontSharedMaterial = style.fontSharedMaterial;
            text.color = style.color;
        }
        text.fontSize = size;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    /// <summary>
    /// Right (true) or left: the next size, waiting for A
    /// </summary>
    public void Step(bool right)
    {
        shown = ResolutionRules.Step(shown, choices.Count, right);
        Show();
    }

    /// <summary>
    /// A: the shown size becomes the applied one. Returns it, for OptionsMenu to apply and save
    /// </summary>
    public ScreenSize Apply()
    {
        applied = shown;
        Show();
        return Applied;
    }

    /// <summary>
    /// Leaving Options without A: back to the applied size
    /// </summary>
    public void Revert()
    {
        shown = applied;
        Show();
    }

    void Show()
    {
        Value.text = ResolutionRules.Label(Shown, shown != applied);
    }
}

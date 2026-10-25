using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// The Options screen's page names along its top, built by code: the current page bright, the others dimmed, between
/// the shoulder buttons that turn them (the device used last: LB RB, L1 R1, L R, or F R on the keyboard)
/// </summary>
public class OptionsPageHeader : MonoBehaviour
{
    public const string DIMMED = "<alpha=#66>";
    const string BRIGHT = "<alpha=#FF>";
    const float SIZE = 40f;

    string[] names;
    int current;
    TMP_Text text;

    /// <summary>The header's text: the shoulders and the page names</summary>
    public static string Line(string[] names, int current, GlyphStyle device)
    {
        string left = device == GlyphStyle.PlayStation ? "L1" : device == GlyphStyle.Nintendo ? "L" : device == GlyphStyle.Keyboard ? "F" : "LB";
        string right = device == GlyphStyle.PlayStation ? "R1" : device == GlyphStyle.Nintendo || device == GlyphStyle.Keyboard ? "R" : "RB";

        StringBuilder line = new StringBuilder(BRIGHT).Append(left).Append(' ');
        for (int i = 0; i < names.Length; i++)
            line.Append("   ").Append(i == current ? BRIGHT : DIMMED).Append(names[i]);
        return line.Append("   ").Append(BRIGHT).Append(' ').Append(right).ToString();
    }

    /// <summary>
    /// Builds the header across the top of the screen, this far below its top edge, in the screen's look. It hangs from
    /// the edge: the canvas matches the screen's width, so a wide screen has less height to show
    /// </summary>
    public static OptionsPageHeader Create(RectTransform screen, TMP_Text style, string[] names, float belowTop)
    {
        GameObject holder = new GameObject("Page Header", typeof(RectTransform));
        RectTransform rect = holder.GetComponent<RectTransform>();
        rect.SetParent(screen, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -belowTop);

        OptionsPageHeader header = holder.AddComponent<OptionsPageHeader>();
        header.names = names;
        header.text = ResolutionRow.Text(rect, "Pages", style, SIZE, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero, 1600f);
        header.text.richText = true;
        header.Show(0);
        LastDevice.Changed += header.OnDeviceChanged;
        return header;
    }

    /// <summary>The page names again (the language changed)</summary>
    public void Rename(string[] pageNames)
    {
        names = pageNames;
        Show(current);
    }

    /// <summary>Shows a page as the current one</summary>
    public void Show(int page)
    {
        current = page;
        text.text = Line(names, current, GlyphRules.StyleFor(LastDevice.Current));
    }

    void OnDeviceChanged(UnityEngine.InputSystem.InputDevice device)
    {
        if (text != null)
            Show(current);
    }

    void OnDestroy()
    {
        LastDevice.Changed -= OnDeviceChanged;
    }
}

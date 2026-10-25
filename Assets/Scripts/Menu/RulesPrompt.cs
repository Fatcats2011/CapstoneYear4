using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The bottom prompt that says Start (M on a keyboard) opens MATCH RULES: in Dispatch's prompt row for the online host,
/// and along the bottom of player select offline. Its button follows the device in use (ButtonGlyph)
/// </summary>
public static class RulesPrompt
{
    /// <summary>
    /// Builds "[Start] MATCH RULES" under parent: the button height tall, the word in font at size
    /// </summary>
    public static GameObject Build(Transform parent, TMP_FontAsset font, float height, float size)
    {
        GameObject prompt = new GameObject("Match Rules Prompt", typeof(RectTransform));
        prompt.transform.SetParent(parent, false);
        ((RectTransform)prompt.transform).sizeDelta = new Vector2(0, height);
        HorizontalLayoutGroup line = prompt.AddComponent<HorizontalLayoutGroup>();
        line.spacing = 12;
        line.childAlignment = TextAnchor.MiddleCenter;
        line.childControlWidth = line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;
        ContentSizeFitter fit = prompt.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        ButtonArtSet art = ButtonArtSet.Results();
        if (art != null && art.startXbox != null)
        {
            Image button = new GameObject("Button", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            button.transform.SetParent(prompt.transform, false);
            button.sprite = art.startXbox;
            button.preserveAspect = true;
            button.raycastTarget = false;
            LayoutElement layout = button.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = height;
            layout.preferredWidth = height;
            ButtonGlyph glyph = button.gameObject.AddComponent<ButtonGlyph>();
            glyph.xbox = art.startXbox;
            glyph.playStation = art.startPlayStation;
            glyph.keyboard = art.startKeyboard;
            glyph.Show(LastDevice.Current);
        }

        TextMeshProUGUI label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.transform.SetParent(prompt.transform, false);
        if (font != null)
            label.font = font;
        label.fontSize = size;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Left;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        LocalizedText.On(label, "rules.title");
        return prompt;
    }
}

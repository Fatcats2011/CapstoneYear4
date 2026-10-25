using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One drawn word's text (DrawnWords): a child stretched over the art, in Jack's Candlestick, shown instead of the art
/// in a language other than English. It follows the language, and the image's sprite if code swaps it (the countdown's
/// numbers and GO!)
/// </summary>
public class DrawnWord : MonoBehaviour
{
    Image image;
    TMP_Text text;
    Sprite shownFor;
    bool listening;
    bool waiting; // to be shown when its menu is (a hidden text can't lay out)

    public TMP_Text Text => text;

    internal TMP_Text Begin(Image art, Color colour)
    {
        image = art;
        if (text == null)
        {
            GameObject holder = new GameObject("Word", typeof(RectTransform));
            RectTransform rect = (RectTransform)holder.transform;
            rect.SetParent(art.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI word = holder.AddComponent<TextMeshProUGUI>();
            word.fontSize = 36; // set: a hidden text's first Awake would otherwise load TMP's defaults over this (wrapping on)
            GameFonts fonts = GameFonts.Game;
            if (fonts != null && fonts.drawnWords != null)
                word.font = fonts.drawnWords;
            word.color = colour;
            word.alignment = TextAlignmentOptions.Center;
            word.enableWordWrapping = false;
            word.raycastTarget = false;
            word.enableAutoSizing = true;
            text = word;
        }
        if (!listening)
        {
            listening = true;
            Loc.Changed += Show;
        }
        Show();
        return text;
    }

    // The art in English; the word in the language spoken otherwise
    void Show()
    {
        if (image == null || text == null)
            return;
        shownFor = image.sprite;
        bool known = shownFor != null && DrawnWords.KeyOfSprite.TryGetValue(shownFor.name, out string key);
        bool asText = known && Loc.Language != LocaleCodes.ENGLISH;
        image.enabled = !asText;
        text.gameObject.SetActive(asText);
        waiting = asText && !text.isActiveAndEnabled;
        if (!asText || waiting)
            return;

        DrawnWords.KeyOfSprite.TryGetValue(shownFor.name, out key);
        FitTo(Loc.Csv.Get(key, LocaleCodes.ENGLISH) ?? Loc.Get(key));
        text.text = Loc.Get(key);
    }

    // Sizes for the English word: that's the most it may be, and SMALLEST of it the least
    void FitTo(string english)
    {
        RectTransform rect = text.rectTransform;
        float height = Mathf.Max(1f, rect.rect.height);
        text.enableAutoSizing = true;
        text.fontSizeMin = 1f;
        text.fontSizeMax = height;
        text.text = english;
        text.ForceMeshUpdate();
        float fits = text.fontSize;
        text.fontSizeMax = fits;
        text.fontSizeMin = fits * DrawnWords.SMALLEST;
    }

    void LateUpdate()
    {
        // Code changed the art (a countdown number, then GO!), or its menu shows now
        if (image != null && (waiting || image.sprite != shownFor))
            Show();
    }

    void OnDestroy()
    {
        if (listening)
            Loc.Changed -= Show;
    }
}

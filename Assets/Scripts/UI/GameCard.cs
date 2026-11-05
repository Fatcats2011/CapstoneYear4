using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The game's own box, built in code: the customising panel's look (a purple panel in a thick dark rounded frame) with
/// words in Bangers. The match's info box (ModeInfoBox) and the lobby's card (ModeLobbyCard) are made of it
/// </summary>
public static class GameCard
{
    internal static readonly Color FRAME = new Color(0.13f, 0.12f, 0.15f, 1f);
    internal static readonly Color PANEL = new Color(0.52f, 0.33f, 0.8f, 1f);
    internal static readonly Color GOLD = new Color(1f, 0.82f, 0.25f, 1f);
    const string BANGERS = "Fonts & Materials/Bangers SDF";
    const int SPRITE = 64;
    const int CORNER = 18;
    const float EDGE = 8f;

    static Sprite rounded;
    static TMP_FontAsset font;

    /// <summary>A white rounded box that stretches without bending its corners</summary>
    public static Sprite Rounded()
    {
        if (rounded == null)
        {
            Color[] pixels = new Color[SPRITE * SPRITE];
            for (int y = 0; y < SPRITE; y++)
            {
                for (int x = 0; x < SPRITE; x++)
                {
                    float dx = Mathf.Max(CORNER - (x + 0.5f), x + 0.5f - (SPRITE - CORNER), 0f);
                    float dy = Mathf.Max(CORNER - (y + 0.5f), y + 0.5f - (SPRITE - CORNER), 0f);
                    pixels[y * SPRITE + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(CORNER - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f));
                }
            }
            Texture2D texture = new Texture2D(SPRITE, SPRITE, TextureFormat.RGBA32, false) { name = "Game Card", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply();
            rounded = Sprite.Create(texture, new Rect(0, 0, SPRITE, SPRITE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * CORNER);
        }
        return rounded;
    }

    /// <summary>A canvas of its own over the screen, in the 1920 x 1080 reference</summary>
    public static Canvas Canvas(GameObject holder, int sortingOrder)
    {
        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = holder.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>The framed panel, sized to what's put in the returned content (top to bottom, centred)</summary>
    public static RectTransform Box(Transform parent, string name, out RectTransform content)
    {
        Image frame = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        RectTransform box = frame.rectTransform;
        box.SetParent(parent, false);
        frame.sprite = Rounded();
        frame.type = Image.Type.Sliced;
        frame.color = FRAME;
        frame.raycastTarget = false;
        VerticalLayoutGroup outer = box.gameObject.AddComponent<VerticalLayoutGroup>();
        int edge = Mathf.RoundToInt(EDGE);
        outer.padding = new RectOffset(edge, edge, edge, edge);
        outer.childControlWidth = outer.childControlHeight = true;
        outer.childForceExpandWidth = outer.childForceExpandHeight = false;
        ContentSizeFitter fit = box.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        Image panel = new GameObject("Panel", typeof(RectTransform)).AddComponent<Image>();
        content = panel.rectTransform;
        content.SetParent(box, false);
        panel.sprite = Rounded();
        panel.type = Image.Type.Sliced;
        panel.color = PANEL;
        panel.raycastTarget = false;
        VerticalLayoutGroup inner = content.gameObject.AddComponent<VerticalLayoutGroup>();
        inner.padding = new RectOffset(28, 28, 12, 14);
        inner.spacing = 4f;
        inner.childAlignment = TextAnchor.UpperCenter;
        inner.childControlWidth = inner.childControlHeight = true;
        inner.childForceExpandWidth = inner.childForceExpandHeight = false;
        return box;
    }

    /// <summary>Words in Bangers under a parent; with a width they wrap to it</summary>
    public static TMP_Text Words(Transform parent, string name, float size, Color colour, float wrapWidth = 0f)
    {
        TextMeshProUGUI text = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(parent, false);
        if (font == null)
            font = Resources.Load<TMP_FontAsset>(BANGERS);
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.color = colour;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.enableWordWrapping = wrapWidth > 0f;
        if (wrapWidth > 0f)
            text.gameObject.AddComponent<LayoutElement>().preferredWidth = wrapWidth;
        return text;
    }
}

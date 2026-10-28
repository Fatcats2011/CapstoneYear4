using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The highlight reel's look (style C, hidden-camera footage), built by code over the reel's camera: a light grey tint
/// and scanlines, a faint frame, a blinking REC dot, the camera's number, the match clock's time and the clip's caption.
/// Only the look lives here, so another style can replace this layer alone. The cutscene skip prompt is built on its
/// canvas too (CutsceneSkip.PromptParent). See docs/superpowers/specs/2026-10-28-highlight-reel-design.md
/// </summary>
public class ReelOverlay : MonoBehaviour
{
    public const int SORTING_ORDER = 500;
    public const string REC_KEY = "reel.rec";
    public const string CAM_KEY = "reel.cam";

    /// <summary>The caption shows from this long before the clip's key moment...</summary>
    public const float CAPTION_BEFORE = 0.2f;

    /// <summary>...to this long after it</summary>
    public const float CAPTION_AFTER = 1.8f;

    static readonly Color TINT = new Color(0.5f, 0.5f, 0.5f, 0.12f);
    static readonly Color SCANLINE = new Color(0f, 0f, 0f, 0.12f);
    static readonly Color FRAME = new Color(1f, 1f, 1f, 0.4f);
    static readonly Color REC_RED = new Color(0.9f, 0.1f, 0.1f, 1f);
    const float INSET = 0.06f; // the frame's inset, a fraction of the screen
    const float FRAME_WIDTH = 3f;
    const float TEXT_SIZE = 44f;
    const float CAPTION_SIZE = 64f;
    const float MARGIN = 24f; // the texts from the frame's corners
    const float DOT_SIZE = 28f;

    Image dot;
    RawImage scanlines;
    TMP_Text cam;
    TMP_Text clock;
    TMP_Text caption;
    Texture2D scanlineTexture;
    Texture2D dotTexture;
    Sprite dotSprite;
    int shownCam = -1;
    int shownSeconds = -1;
    Caption? shownCaption;
    int scanlinesFor; // the screen height the scanlines are tiled for

    /// <summary>The match clock's time as mm:ss, never below 00:00</summary>
    public static string Timestamp(float timeLeft)
    {
        int seconds = Mathf.FloorToInt(Mathf.Max(0f, timeLeft));
        return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");
    }

    /// <summary>A camera's number, 1-24: the same for every place in the same metre of ground</summary>
    public static int CameraNumber(Vector3 p)
    {
        return (int)Mathf.Abs(Mathf.Floor(p.x) * 7 + Mathf.Floor(p.z) * 13) % 24 + 1;
    }

    /// <summary>The strings key of a caption</summary>
    public static string CaptionKey(Caption c)
    {
        switch (c)
        {
            case Caption.Theft: return "reel.caption.theft";
            case Caption.Collision: return "reel.caption.collision";
            case Caption.Water: return "reel.caption.water";
            default: return "reel.caption.delivery";
        }
    }

    /// <summary>Builds the overlay as a child of the reel: a screen overlay canvas (sort order SORTING_ORDER)</summary>
    internal static ReelOverlay Build(Transform parent)
    {
        GameObject holder = new GameObject("Reel Overlay", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;
        CanvasScaler scaler = holder.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        ReelOverlay overlay = holder.AddComponent<ReelOverlay>();
        overlay.BuildLook((RectTransform)holder.transform);
        return overlay;
    }

    void BuildLook(RectTransform root)
    {
        Image tint = Stretch<Image>(root, "Tint", Vector2.zero, Vector2.one);
        tint.color = TINT;

        // Two clear rows, two dark: one dark band every 4 pixels down the screen
        scanlineTexture = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
        scanlineTexture.SetPixels(new[] { Color.clear, Color.clear, SCANLINE, SCANLINE });
        scanlineTexture.Apply();
        scanlines = Stretch<RawImage>(root, "Scanlines", Vector2.zero, Vector2.one);
        scanlines.texture = scanlineTexture;
        TileScanlines();

        Vector2 low = new Vector2(INSET, INSET);
        Vector2 high = new Vector2(1f - INSET, 1f - INSET);
        FrameEdge(root, "Top", new Vector2(low.x, high.y), high, new Vector2(FRAME_WIDTH, FRAME_WIDTH));
        FrameEdge(root, "Bottom", low, new Vector2(high.x, low.y), new Vector2(FRAME_WIDTH, FRAME_WIDTH));
        FrameEdge(root, "Left", low, new Vector2(low.x, high.y), new Vector2(FRAME_WIDTH, FRAME_WIDTH));
        FrameEdge(root, "Right", new Vector2(high.x, low.y), high, new Vector2(FRAME_WIDTH, FRAME_WIDTH));

        // Top left: the red dot, then REC
        dot = Stretch<Image>(root, "Rec Dot", new Vector2(low.x, high.y), new Vector2(low.x, high.y));
        RectTransform dotRect = dot.rectTransform;
        dotRect.pivot = new Vector2(0f, 1f);
        dotRect.sizeDelta = new Vector2(DOT_SIZE, DOT_SIZE);
        dotRect.anchoredPosition = new Vector2(MARGIN, -MARGIN - (TEXT_SIZE * 1.5f - DOT_SIZE) / 2f);
        dotTexture = Disc(64);
        dotSprite = Sprite.Create(dotTexture, new Rect(0, 0, dotTexture.width, dotTexture.height), new Vector2(0.5f, 0.5f));
        dot.sprite = dotSprite;
        dot.color = REC_RED;

        TMP_Text rec = Text(root, "Rec", TEXT_SIZE, TextAlignmentOptions.Left, new Vector2(low.x, high.y), new Vector2(0f, 1f),
            new Vector2(MARGIN + DOT_SIZE + 12f, -MARGIN));
        LocalizedText.On(rec, REC_KEY);
        cam = Text(root, "Cam", TEXT_SIZE, TextAlignmentOptions.Right, high, new Vector2(1f, 1f), new Vector2(-MARGIN, -MARGIN));
        clock = Text(root, "Timestamp", TEXT_SIZE, TextAlignmentOptions.Left, low, new Vector2(0f, 0f), new Vector2(MARGIN, MARGIN));
        caption = Text(root, "Caption", CAPTION_SIZE, TextAlignmentOptions.Center, new Vector2(0.5f, 0.75f), new Vector2(0.5f, 0.5f), Vector2.zero);
        caption.gameObject.SetActive(false);
    }

    /// <summary>What the overlay shows this frame: the camera's number, the clock's time, and the caption (or none)</summary>
    internal void Show(int cameraNumber, float timeLeft, Caption? shownNow)
    {
        if (cameraNumber != shownCam)
        {
            shownCam = cameraNumber;
            LocalizedText.On(cam, CAM_KEY, cameraNumber.ToString("00"));
        }

        int seconds = Mathf.FloorToInt(Mathf.Max(0f, timeLeft));
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            clock.text = Timestamp(timeLeft);
        }

        if (shownNow != shownCaption)
        {
            shownCaption = shownNow;
            caption.gameObject.SetActive(shownNow.HasValue);
            if (shownNow.HasValue)
                LocalizedText.On(caption, CaptionKey(shownNow.Value));
        }
    }

    void Update()
    {
        dot.enabled = Time.unscaledTime % 1f < 0.5f; // blinks once a second
        if (Screen.height != scanlinesFor)
            TileScanlines();
    }

    // The texture repeats once every 4 screen pixels, whatever the canvas's scale
    void TileScanlines()
    {
        scanlinesFor = Screen.height;
        scanlines.uvRect = new Rect(0f, 0f, 1f, Mathf.Max(1, Screen.height) / 4f);
    }

    void OnDestroy()
    {
        Destroy(scanlineTexture);
        Destroy(dotSprite);
        Destroy(dotTexture);
    }

    static T Stretch<T>(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax) where T : Graphic
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        T graphic = go.AddComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }

    // One side of the frame: a thin line between two anchors, grown by the line's width so the corners meet
    static void FrameEdge(RectTransform parent, string name, Vector2 from, Vector2 to, Vector2 width)
    {
        Image line = Stretch<Image>(parent, "Frame " + name, from, to);
        line.rectTransform.sizeDelta = width;
        line.color = FRAME;
    }

    // A text in TextMesh Pro's default font (as ResolutionRow builds one without a style), placed from an anchor
    static TMP_Text Text(RectTransform parent, string name, float size, TextAlignmentOptions alignment, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        TMP_Text text = ResolutionRow.Text(parent, name, null, size, alignment, pivot, position, 1600f);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = anchor;
        rect.anchoredPosition = position;
        text.color = Color.white;
        return text;
    }

    // A white disc on clear, for the REC dot
    static Texture2D Disc(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float radius = size / 2f;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - distance));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }
}

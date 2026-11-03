using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Options → Accessibility → Text size: every UI text in the game (TextMeshProUGUI), menus included, drawn bigger.
/// Each text is scaled from its own size when it's first drawn (TMP's text-changed event, so code-built UI needs
/// nothing of its own), and again when the code sets its size or its words, or the setting changes. A text that fits
/// its box at 100% grows only as far as it still fits (a title's words broke mid-word otherwise); one its layout sizes
/// grows freely. An auto-sized text has its size range scaled instead. Words drawn over art (DrawnWord) keep the art's
/// size, and the scoreboard's grow with the whole board (Scoreboard). Saved in settings.cfg
/// </summary>
public static class TextSize
{
    /// <summary>The sizes the setting offers, smallest first</summary>
    public static readonly float[] SCALES = { 1f, 1.25f, 1.5f };

    /// <summary>How much bigger text is drawn</summary>
    public static float Scale { get; private set; } = 1f;

    // A text's own sizes (the code's) and the ones shown for them
    class Sizes
    {
        public bool Auto;
        public float Size, Min, Max;
        public float ShownSize, ShownMin, ShownMax;
        public float ShownAt = 1f; // the scale they were shown at
        public string ShownText;   // the words they were fitted to
    }

    /// <summary>The biggest any text grows to, in canvas units (1080 high)</summary>
    internal const float LARGEST = 72f;

    // How finely a text too big for its box steps back towards its own size
    const float FIT_STEP = 0.05f;

    static readonly Dictionary<TMP_Text, Sizes> known = new Dictionary<TMP_Text, Sizes>();
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        known.Clear();
        Scale = 1f;
        if (hooked)
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= Forget;
        }
        hooked = false;
    }

#if UNITY_EDITOR
    // Back in the editor after playing: nothing in an edited scene or prefab is ever drawn bigger, or saved so
    [UnityEditor.InitializeOnLoadMethod]
    static void HookEditor()
    {
        UnityEditor.EditorApplication.playModeStateChanged += state =>
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                ResetForPlay();
        };
    }
#endif

    /// <summary>The setting's label for a size: "125%"</summary>
    public static string Label(int index)
    {
        return Mathf.RoundToInt(SCALES[Mathf.Clamp(index, 0, SCALES.Length - 1)] * 100f) + "%";
    }

    /// <summary>The setting changed (or was loaded): every text drawn from now, and every one up now, at its size</summary>
    public static void Set(int index)
    {
        Scale = SCALES[Mathf.Clamp(index, 0, SCALES.Length - 1)];
        if (!hooked)
        {
            hooked = true;
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += Forget;
        }
        foreach (TextMeshProUGUI text in Object.FindObjectsOfType<TextMeshProUGUI>(true))
            Fit(text);
    }

    static void OnTextChanged(Object changed)
    {
        if (changed is TextMeshProUGUI text)
            Fit(text);
    }

    /// <summary>Scales a text to the setting, if it isn't already</summary>
    internal static void Fit(TMP_Text text)
    {
        if (text == null)
            return;
        if (!known.TryGetValue(text, out Sizes sizes))
        {
            // Never drawn at another size: nothing to do at 100%
            if (Scale == 1f || Exempt(text))
                return;
            sizes = new Sizes();
            Capture(text, sizes);
            known[text] = sizes;
        }
        else if (Changed(text, sizes))
        {
            Capture(text, sizes); // the code set its own size since: that's its size now
        }
        else if (sizes.ShownAt == Scale && (sizes.Auto || Scale == 1f || text.text == sizes.ShownText))
        {
            return;
        }
        Show(text, sizes);
    }

    // Words drawn over art are fitted to it
    static bool Exempt(TMP_Text text)
    {
        return text.GetComponentInParent<DrawnWord>() != null || text.GetComponentInParent<Scoreboard>() != null;
    }

    static void Capture(TMP_Text text, Sizes sizes)
    {
        sizes.Auto = text.enableAutoSizing;
        sizes.Size = text.fontSize;
        sizes.Min = text.fontSizeMin;
        sizes.Max = text.fontSizeMax;
    }

    // Whether the code set the text's size since it was shown. An auto-sized text's own size moves with its layout
    static bool Changed(TMP_Text text, Sizes sizes)
    {
        if (text.enableAutoSizing != sizes.Auto)
            return true;
        if (sizes.Auto)
            return text.fontSizeMax != sizes.ShownMax || text.fontSizeMin != sizes.ShownMin;
        return text.fontSize != sizes.ShownSize;
    }

    static void Show(TMP_Text text, Sizes sizes)
    {
        sizes.ShownAt = Scale;
        if (sizes.Auto)
        {
            sizes.ShownMin = Grown(sizes.Min, Scale);
            sizes.ShownMax = Grown(sizes.Max, Scale);
            text.fontSizeMin = sizes.ShownMin;
            text.fontSizeMax = sizes.ShownMax;
        }
        else
        {
            float scale = Scale;
            sizes.ShownText = text.text;
            if (scale > 1f && !LaidOut(text))
            {
                // Only what held at its own size: its box's height, and no word wider than a line
                bool box = Fits(text, sizes.Size, true, false);
                bool words = Fits(text, sizes.Size, false, true);
                while (scale > 1f && !Fits(text, Grown(sizes.Size, scale), box, words))
                    scale = Mathf.Max(1f, scale - FIT_STEP);
            }
            sizes.ShownSize = Grown(sizes.Size, scale);
            text.fontSize = sizes.ShownSize;
        }
    }

    /// <summary>
    /// A size grown by a scale: never past LARGEST unless it already was (titles and menu rows are big enough; the
    /// setting is for the small text)
    /// </summary>
    internal static float Grown(float size, float scale)
    {
        return Mathf.Max(size, Mathf.Min(size * scale, LARGEST));
    }

    // A text whose box its layout sizes to it
    static bool LaidOut(TMP_Text text)
    {
        return text.GetComponent<UnityEngine.UI.ContentSizeFitter>() != null
            || (text.transform.parent != null && text.transform.parent.GetComponent<UnityEngine.UI.LayoutGroup>() != null);
    }

    // Whether a text fits its box at a size: its box (its height when it wraps, its width when it doesn't), and its
    // words (a wrapping text's longest word on one line; a longer one breaks mid-word)
    static bool Fits(TMP_Text text, float size, bool box, bool words)
    {
        Rect rect = text.rectTransform.rect;
        Vector4 margin = text.margin;
        float width = rect.width - margin.x - margin.z;
        float height = rect.height - margin.y - margin.w;
        if (width <= 0f || height <= 0f || string.IsNullOrEmpty(text.text))
            return true;
        bool wraps = text.enableWordWrapping;
        float was = text.fontSize;
        text.fontSize = size;
        bool fits = true;
        if (box)
        {
            Vector2 needs = wraps ? text.GetPreferredValues(text.text, width, Mathf.Infinity) : text.GetPreferredValues(text.text);
            // TMP's preferred sizes count the margins in
            fits = wraps ? needs.y - margin.y - margin.w <= height + 0.5f : needs.x - margin.x - margin.z <= width + 0.5f;
        }
        if (fits && words && wraps)
        {
            foreach (string word in text.text.Split(' ', '\n'))
            {
                if (word.Length > 0 && text.GetPreferredValues(word).x - margin.x - margin.z > width + 0.5f)
                {
                    fits = false;
                    break;
                }
            }
        }
        text.fontSize = was;
        return fits;
    }

    /// <summary>
    /// Sets a text's own size, shown at the setting's at once: for code that measures a text to fit it (OptionRow), so
    /// it measures what will show
    /// </summary>
    public static void SetSize(TMP_Text text, float size)
    {
        if (!known.TryGetValue(text, out Sizes sizes))
        {
            sizes = new Sizes();
            known[text] = sizes;
        }
        text.enableAutoSizing = false;
        sizes.Auto = false;
        sizes.Size = size;
        sizes.Min = text.fontSizeMin;
        sizes.Max = text.fontSizeMax;
        Show(text, sizes);
    }

    // A scene's texts went with it
    static void Forget(Scene scene)
    {
        List<TMP_Text> gone = new List<TMP_Text>();
        foreach (TMP_Text text in known.Keys)
        {
            if (text == null)
                gone.Add(text);
        }
        foreach (TMP_Text text in gone)
            known.Remove(text);
    }
}

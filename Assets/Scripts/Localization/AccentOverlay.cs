using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine.TextCore.LowLevel;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The accents the game's fonts lack (AccentRules), drawn over the letters: when any UI text is laid out holding a
/// character its font lacks, the text is shown with the base letter, and a drawn mark (AccentMarkSet) is placed on that
/// letter where TextMesh Pro laid it out, so wrapping, auto-size and a language change all follow. It listens to every
/// text by itself (TextMesh Pro's text-changed event): nothing in a scene carries it. Fonts stay untouched.
/// See docs/superpowers/specs/2026-10-24-localization-design.md
/// </summary>
public class AccentOverlay : MonoBehaviour
{
    // Where a mark sits on its letter, and how big, as a share of the letter's capital height
    const float ABOVE_GAP = 0.08f;
    const float LEAN = 0.12f; // the fonts lean right: marks above shift right a little

    struct Placed
    {
        public int Index;       // the character's index in the shown text
        public AccentMark Mark;
    }

    static bool listening;

    readonly List<Placed> placed = new List<Placed>();
    readonly List<Image> images = new List<Image>();
    string shown; // the text as rewritten here (the base letters): seeing it again only re-places the marks
    TMP_Text text;
    bool placing; // laid out inside the canvas's rebuild, where a new or changed image isn't drawn: placed after it

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Listen()
    {
        if (listening)
            return;
        listening = true;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
    }

    /// <summary>Tests: stops listening</summary>
    public static void StopListening()
    {
        listening = false;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
    }

    static void OnTextChanged(Object changed)
    {
        if (!(changed is TextMeshProUGUI ui) || ui.font == null)
            return;

        AccentOverlay overlay = ui.GetComponent<AccentOverlay>();
        if (overlay != null && ui.text == overlay.shown)
        {
            overlay.PlaceSoon();
            return;
        }

        List<Placed> marks = new List<Placed>();
        string rewritten = Rewrite(ui.text, ui.font, marks);
        if (marks.Count == 0 && ReferenceEquals(rewritten, ui.text))
        {
            if (overlay != null && overlay.shown != null)
            {
                overlay.placed.Clear();
                overlay.shown = null;
                overlay.PlaceSoon();
            }
            return;
        }

        if (overlay == null)
        {
            overlay = ui.gameObject.AddComponent<AccentOverlay>();
            overlay.text = ui;
        }
        overlay.placed.Clear();
        overlay.placed.AddRange(marks);
        overlay.shown = rewritten;
        ui.text = rewritten;
        ui.ForceMeshUpdate(); // lays it out now: this event comes again for the rewritten text, and places the marks
    }

    // The text with each character the font lacks swapped for its base (and ẞ for SS), noting where the marks go
    static string Rewrite(string source, TMP_FontAsset font, List<Placed> marks)
    {
        if (string.IsNullOrEmpty(source))
            return source;
        StringBuilder rewritten = null;
        for (int i = 0; i < source.Length; i++)
        {
            char c = source[i];
            bool lacks = (c == 'ẞ' || AccentRules.TryGet(c, out _)) && !FontHas(font, c);
            if (lacks && rewritten == null)
                rewritten = new StringBuilder(source, 0, i, source.Length + 4);
            if (rewritten == null)
                continue;
            if (!lacks)
                rewritten.Append(c);
            else if (c == 'ẞ')
                rewritten.Append("SS");
            else
            {
                AccentRules.TryGet(c, out Accented accented);
                marks.Add(new Placed { Index = rewritten.Length, Mark = accented.Mark });
                rewritten.Append(accented.Base);
            }
        }
        return rewritten != null ? rewritten.ToString() : source;
    }

    static readonly Dictionary<(int, char), bool> sourceHas = new Dictionary<(int, char), bool>();

    /// <summary>
    /// Whether a font itself has a character (not a fallback font: Liberation Sans has these letters, but not in the game's
    /// style): its atlas, or its source font file, which a dynamic atlas adds from when the character is first shown.
    /// The file is asked through TextMesh Pro's font engine: Unity's own Font.HasCharacter also counts the PC's fonts
    /// </summary>
    public static bool FontHas(TMP_FontAsset font, char c)
    {
        if (font.HasCharacter(c, false, false))
            return true;
        if (font.sourceFontFile == null)
            return false;
        (int, char) key = (font.GetInstanceID(), c);
        if (!sourceHas.TryGetValue(key, out bool has))
        {
            has = FontEngine.LoadFontFace(font.sourceFontFile, 90) == FontEngineError.Success
                && FontEngine.TryGetGlyphIndex(c, out uint glyph) && glyph != 0;
            sourceHas[key] = has;
        }
        return has;
    }

    void PlaceSoon()
    {
        if (CanvasUpdateRegistry.IsRebuildingGraphics())
            placing = true;
        else
            Place();
    }

    void LateUpdate()
    {
        if (placing)
            Place();
    }

    // Every mark on its laid-out letter
    void Place()
    {
        placing = false;
        AccentMarkSet set = AccentMarkSet.Game;
        TMP_TextInfo info = text.textInfo;
        int used = 0;
        foreach (Placed mark in placed)
        {
            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo letter = info.characterInfo[i];
                if (letter.index != mark.Index || !letter.isVisible)
                    continue;
                Image image = ImageAt(used++);
                image.sprite = set != null ? set.Of(mark.Mark) : null;
                image.color = letter.color;
                Lay(image.rectTransform, letter, mark.Mark, set != null ? set.capUnits : 180f);
                break;
            }
        }
        for (int i = used; i < images.Count; i++)
            images[i].gameObject.SetActive(false);
    }

    // A mark's square on a letter: above it (most), under it (ogonek, comma below), or across its stem (Ł)
    static void Lay(RectTransform square, TMP_CharacterInfo letter, AccentMark mark, float capUnits)
    {
        float cap = letter.ascender - letter.baseLine;
        float size = cap * 128f / capUnits;
        float centre = (letter.bottomLeft.x + letter.bottomRight.x) / 2f;
        Vector2 at;
        switch (mark)
        {
            case AccentMark.Ogonek:
                at = new Vector2(letter.bottomRight.x - size * 0.25f, letter.baseLine - size * 0.1f);
                break;
            case AccentMark.CommaBelow:
                at = new Vector2(centre - cap * LEAN * 0.5f, letter.baseLine - size * 0.2f);
                break;
            case AccentMark.LStroke:
                at = new Vector2(letter.bottomLeft.x + (letter.bottomRight.x - letter.bottomLeft.x) * 0.35f, letter.baseLine + cap * 0.45f);
                break;
            default:
                at = new Vector2(centre + cap * LEAN, letter.ascender + cap * ABOVE_GAP + size * 0.2f);
                break;
        }
        square.sizeDelta = new Vector2(size, size);
        square.localPosition = at;
    }

    Image ImageAt(int index)
    {
        while (images.Count <= index)
        {
            GameObject mark = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)mark.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            Image image = mark.GetComponent<Image>();
            image.raycastTarget = false;
            images.Add(image);
        }
        images[index].gameObject.SetActive(true);
        return images[index];
    }
}

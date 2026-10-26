using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The rematch vote on the results screen, built by code under the results canvas (no prefab edits): the A REMATCH and
/// B BACK TO LOBBY prompts (ButtonGlyph art from ButtonArtSet.Results, words only without it), a READY or OUT mark under
/// each player's score, and the countdown. In the scores' font
/// </summary>
public class ResultsVoteView : MonoBehaviour
{
    const float PROMPT_HEIGHT = 56f;
    const float CORNER_X = 60f; // in from the left edge
    const float LABEL_SIZE = 48f;
    const float LINE_SIZE = 44f;
    static readonly Color DIMMED = new Color(1f, 1f, 1f, 0.35f);

    Canvas canvas; // the view covers it all, whatever box it's under
    TMP_Text line;
    TMP_Text[] scores;
    TMP_Text[] marks;
    Color[] scoreColours;
    int[] seatOfScore; // the seat each score shows: the scores are in finishing order

    /// <summary>Builds the view under parent, in the font of the scores (one per seat)</summary>
    public static ResultsVoteView Build(Transform parent, TMP_Text[] scores)
    {
        GameObject root = new GameObject("Rematch Vote", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        ResultsVoteView view = root.AddComponent<ResultsVoteView>();
        view.canvas = parent.GetComponentInParent<Canvas>();
        view.CoverCanvas();
        TMP_FontAsset font = scores.Length > 0 && scores[0] != null ? scores[0].font : null;

        // The top-left corner: the title takes the top middle, the scores and their award cards the bottom
        view.line = Text(root.transform, "Countdown", font, LINE_SIZE);
        view.line.alignment = TextAlignmentOptions.Left;
        Place(view.line.rectTransform, new Vector2(CORNER_X, -60), new Vector2(700, 60));

        GameObject row = new GameObject("Prompts", typeof(RectTransform));
        row.transform.SetParent(root.transform, false);
        RectTransform rowRect = (RectTransform)row.transform;
        Place(rowRect, new Vector2(CORNER_X, -110), new Vector2(700, PROMPT_HEIGHT * 2 + 16));
        VerticalLayoutGroup rowLayout = row.AddComponent<VerticalLayoutGroup>();
        rowLayout.spacing = 16;
        rowLayout.childAlignment = TextAnchor.UpperLeft;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;

        ButtonArtSet art = ButtonArtSet.Results();
        Prompt(row.transform, "results.rematch", font, art != null ? art.confirmXbox : null, art != null ? art.confirmPlayStation : null, art != null ? art.confirmKeyboard : null);
        Prompt(row.transform, "results.backtolobby", font, art != null ? art.backXbox : null, art != null ? art.backPlayStation : null, art != null ? art.backKeyboard : null);

        view.scores = scores;
        view.marks = new TMP_Text[scores.Length];
        view.scoreColours = new Color[scores.Length];
        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] == null)
                continue;
            view.scoreColours[i] = scores[i].color;
            TMP_Text mark = Text(scores[i].transform, "Rematch Mark", font, LINE_SIZE);
            RectTransform rect = mark.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0, -6);
            rect.sizeDelta = new Vector2(300, 60);
            view.marks[i] = mark;
        }
        return view;
    }

    // The results' elements sit in a 100 x 100 box mid-screen: stretched over that, "the bottom of the screen" was under
    // the title and the prompts covered EMPLOYEE OF THE MONTH (playtest, 2026-10-24). The view covers the whole canvas
    // instead, again each frame as the window's size can change
    void CoverCanvas()
    {
        RectTransform rect = (RectTransform)transform;
        RectTransform whole = canvas != null ? (RectTransform)canvas.rootCanvas.transform : null;
        if (whole == null || rect.parent == null)
        {
            Stretch(rect);
            return;
        }
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = whole.rect.size;
        // In local units: the canvas is at scale 0 until it first draws. The boxes between are at scale 1
        Vector3 offset = whole.rect.center;
        for (Transform box = rect.parent; box != null && box != whole; box = box.parent)
            offset -= box.localPosition;
        rect.localPosition = offset;
    }

    void LateUpdate()
    {
        CoverCanvas();
    }

    /// <summary>Which seat each score shows (the scores are in finishing order)</summary>
    public void ShowSeats(int[] seatOfScore)
    {
        this.seatOfScore = seatOfScore;
    }

    /// <summary>Shows a board: the marks and the countdown, or the outcome once decided</summary>
    public void Show(RematchBoard board)
    {
        if (line != null)
            line.text = board.Outcome == RematchOutcome.Rematch ? Loc.Get("results.rematching")
                : board.Outcome == RematchOutcome.Lobby ? Loc.Get("results.tolobby")
                : Loc.Get("results.waiting", board.SecondsLeft);

        for (int i = 0; i < marks.Length; i++)
        {
            if (marks[i] == null)
                continue;
            int seat = seatOfScore != null ? (i < seatOfScore.Length ? seatOfScore[i] : -1) : i;
            bool shown = seat >= 0 && scores[i].gameObject.activeInHierarchy;
            marks[i].text = !shown ? "" : board.HasVoted(seat) ? Loc.Get("results.ready") : board.IsIn(seat) ? "" : Loc.Get("results.out");
            scores[i].color = shown && !board.IsIn(seat) ? scoreColours[i] * DIMMED : scoreColours[i];
        }
    }

    static void Prompt(Transform row, string label, TMP_FontAsset font, Sprite xbox, Sprite playStation, Sprite keyboard)
    {
        GameObject prompt = new GameObject(label, typeof(RectTransform));
        prompt.transform.SetParent(row, false);
        HorizontalLayoutGroup layout = prompt.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;

        if (xbox != null)
        {
            GameObject button = new GameObject("Button", typeof(RectTransform), typeof(Image));
            button.transform.SetParent(prompt.transform, false);
            Image image = button.GetComponent<Image>();
            image.sprite = xbox;
            image.preserveAspect = true;
            LayoutElement size = button.AddComponent<LayoutElement>();
            size.preferredHeight = PROMPT_HEIGHT;
            size.preferredWidth = PROMPT_HEIGHT;
            ButtonGlyph glyph = button.AddComponent<ButtonGlyph>();
            glyph.xbox = xbox;
            glyph.playStation = playStation;
            glyph.keyboard = keyboard;
            glyph.Show(LastDevice.Current);
        }

        TMP_Text text = Text(prompt.transform, "Label", font, LABEL_SIZE);
        LocalizedText.On(text, label);
        text.enableWordWrapping = false;
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // From the canvas's top-left corner, its own top-left there
    static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The end-of-match award cards on the results screen, built by code (no prefab edits): one above each player's score,
/// the player it's for, then the award's joke name over its plain count (AwardRules), in the scores' font. Each card sits
/// beside its score, not under it, so it's sized in the canvas: the scores are drawn scaled up, and cards that grew with
/// them overlapped their neighbours. It sits over the score's text, which is left-aligned in a far wider box: a card
/// centred on the box sat over the next player's score (playtest, 2026-10-24). The scores are in finishing order, so each card is told which seat its score shows. Awards that
/// arrive after the scores are up still show (Follow). See docs/superpowers/specs/2026-10-24-awards-design.md
/// </summary>
public class ResultsAwardsView : MonoBehaviour
{
    // A card fits between neighbouring scores (about 325 canvas units apart): BUTTERFINGERS at 34 is about 250 wide
    const float NAME_SIZE = 34f;
    const float LINE_SIZE = 24f;
    const float ABOVE_SCORE = 12f;
    static readonly Color CARD = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color GOLD = new Color(1f, 0.82f, 0.25f);

    GameObject[] cards;
    TMP_Text[] scores;
    TMP_Text[] players; // whose award: P1 ... in their score's colour
    TMP_Text[] names;
    TMP_Text[] lines;
    int[] following; // the seats Follow shows, while it listens
    bool listening;

    /// <summary>Builds a card over each score (hidden until Show), in the scores' font</summary>
    public static ResultsAwardsView Build(Transform parent, TMP_Text[] scores)
    {
        GameObject root = new GameObject("Award Cards", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        ResultsAwardsView view = root.AddComponent<ResultsAwardsView>();
        TMP_FontAsset font = scores.Length > 0 && scores[0] != null ? scores[0].font : null;

        view.cards = new GameObject[scores.Length];
        view.scores = scores;
        view.players = new TMP_Text[scores.Length];
        view.names = new TMP_Text[scores.Length];
        view.lines = new TMP_Text[scores.Length];
        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] == null)
                continue;

            RectTransform score = scores[i].rectTransform;
            GameObject card = new GameObject("Award Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(score.parent, false);
            card.transform.SetSiblingIndex(score.GetSiblingIndex() + 1);
            RectTransform rect = (RectTransform)card.transform;
            rect.anchorMin = score.anchorMin;
            rect.anchorMax = score.anchorMax;
            rect.pivot = new Vector2(0.5f, 0f);

            Image back = card.GetComponent<Image>();
            back.color = CARD;
            back.raycastTarget = false;
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            ContentSizeFitter fit = card.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            view.players[i] = Text(card.transform, "Player", font, LINE_SIZE, scores[i].color);
            view.names[i] = Text(card.transform, "Name", font, NAME_SIZE, GOLD);
            view.lines[i] = Text(card.transform, "Line", font, LINE_SIZE, Color.white);
            card.SetActive(false);
            view.cards[i] = card;
            view.PlaceOver(i);
        }
        return view;
    }

    // Just above the score's text, in the parent's units. The scores' text changes after the cards are made, so they
    // follow it (LateUpdate)
    void PlaceOver(int i)
    {
        RectTransform score = scores[i].rectTransform;
        // The text lays out inside its margins: the scene's scores keep a wide right margin
        Rect box = score.rect;
        Vector4 margin = scores[i].margin;
        box.xMin += margin.x;
        box.xMax -= margin.z;
        box.yMax -= margin.y;
        float width = Mathf.Min(scores[i].GetPreferredValues(scores[i].text).x, box.width);
        float middle = scores[i].horizontalAlignment == HorizontalAlignmentOptions.Left ? box.xMin + width / 2f
            : scores[i].horizontalAlignment == HorizontalAlignmentOptions.Right ? box.xMax - width / 2f
            : box.center.x;
        Vector2 top = new Vector2(middle * score.localScale.x, box.yMax * score.localScale.y);
        ((RectTransform)cards[i].transform).anchoredPosition = score.anchoredPosition + top + new Vector2(0f, ABOVE_SCORE);
    }

    void LateUpdate()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] != null && cards[i].activeSelf)
                PlaceOver(i);
        }
    }

    /// <summary>The card over a score (finishing order); tests</summary>
    internal GameObject CardOf(int score)
    {
        return cards[score];
    }

    /// <summary>Shows the awards: each score's card is its seat's (seatOfScore[i]; -1, or no award: no card)</summary>
    public void Show(AwardBoard board, int[] seatOfScore)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;
            int seat = seatOfScore != null && i < seatOfScore.Length ? seatOfScore[i] : -1;
            Award award = board.AwardOf(seat);
            cards[i].SetActive(award != Award.None);
            players[i].text = seat >= 0 ? "P" + (seat + 1) : "";
            PlaceOver(i);
            names[i].text = AwardRules.Name(award);
            lines[i].text = AwardRules.Line(award, board.CountOf(seat));
        }
    }

    /// <summary>
    /// The scores are up: shows the awards picked or heard this match, and any that arrive while the scores stay up (a
    /// client whose results opened before the host's awards reached it). Hide stops it
    /// </summary>
    public void Follow(int[] seatOfScore)
    {
        following = seatOfScore;
        if (!listening)
        {
            listening = true;
            AwardSync.Arrived += OnArrived;
        }
        if (AwardSync.HasLast)
            Show(AwardSync.Last, following);
    }

    /// <summary>Hides every card (the results are done with) and stops following</summary>
    public void Hide()
    {
        StopFollowing();
        foreach (GameObject card in cards)
        {
            if (card != null)
                card.SetActive(false);
        }
    }

    void OnArrived(AwardBoard board)
    {
        Show(board, following);
    }

    void StopFollowing()
    {
        if (listening)
            AwardSync.Arrived -= OnArrived;
        listening = false;
    }

    // AwardSync's events are static: a view left behind with its scene never keeps listening
    void OnDestroy()
    {
        StopFollowing();
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Color colour)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = colour;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }
}

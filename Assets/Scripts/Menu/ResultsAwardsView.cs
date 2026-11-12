using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>What a results card says about its player's rematch vote</summary>
public enum VoteMark { None, Ready, Out }

/// <summary>
/// The players' cards on the results screen, built by code (no prefab edits): one under each ghost on the podium, along
/// the bottom of the screen, so the podium itself stays clear. A card has
/// - a medal with the finishing place, the player's name and their score;
/// - their award, filling the rest of the card: its joke name over its plain count (AwardRules);
/// - a READY or OUT tag on its top edge once the rematch vote opens (ResultsVoteView).
/// The winner's card is a little bigger, edged in gold. The cards slide up one after another, the winner's last.
/// The scene's own scores (a number in a purple box per player) say where each ghost stands: each card is centred on
/// its score, and the scores themselves, their boxes and the big title are restyled (the title goes to the top of the
/// screen, on one line). The scores are in finishing order, so each card is told which seat its score shows. Awards
/// that arrive after the cards are up still show (Follow). See docs/superpowers/specs/2026-10-24-awards-design.md
/// </summary>
public class ResultsAwardsView : MonoBehaviour
{
    // Sizes in the canvas's 1920 x 1080
    internal const float WIDTH = 300f;
    internal const float HEIGHT = 250f;
    const float BOTTOM = 20f;          // the cards' gap from the bottom of the screen
    const float EDGE = 12f;            // ...and at least this from its sides
    const float PAD = 12f;
    const float MEDAL = 58f;
    const float HEADER = 84f;
    const float AWARD = 128f;          // the award's block: the rest of the card under the header
    const float WINNER_SCALE = 1.07f;
    const float SLIDE = 46f;           // how far below its place a card starts
    const float APPEAR = 0.35f;        // seconds a card takes to arrive
    const float STAGGER = 0.16f;       // ...and how long after the card before it
    const float TITLE_TOP = 26f;
    static readonly Vector2 TITLE_SIZE = new Vector2(820f, 72f); // clear of the rematch prompts in the top-left corner

    static readonly Color PANEL = new Color(0.07f, 0.05f, 0.11f, 0.86f);
    static readonly Color WINNER_PANEL = new Color(0.1f, 0.07f, 0.16f, 1f);
    static readonly Color GOLD = new Color(1f, 0.82f, 0.25f);
    static readonly Color MONEY = new Color(0.85f, 0.78f, 1f);
    static readonly Color RULE = new Color(1f, 1f, 1f, 0.16f);
    static readonly Color INK = new Color(0.06f, 0.05f, 0.08f, 1f);
    static readonly Color READY = new Color(0.25f, 0.82f, 0.42f);
    static readonly Color OUT = new Color(0.9f, 0.3f, 0.3f);
    static readonly Color[] MEDALS =
    {
        GOLD, new Color(0.82f, 0.85f, 0.9f), new Color(0.83f, 0.55f, 0.32f), new Color(0.5f, 0.52f, 0.6f),
    };
    const float GONE = 0.5f;    // the card of a player who left the vote

    Card[] cards;
    TMP_Text[] scores;
    TMP_Text title;     // the scene's big title, moved to the top (null without one)
    int[] following;    // the seats Follow shows, while it listens
    bool listening;
    float clock;        // seconds since the cards were first shown

    class Card
    {
        public GameObject Root;
        public RectTransform Rect;
        public CanvasGroup Fade;
        public Image Medal;
        public TMP_Text Place, Player, Money, Award, Line, Vote;
        public GameObject VoteTag;
        public Image VoteBack;
        public GameObject AwardBlock;
        public Vector2 Home;      // where it rests, in the cards' space
        public float Delay;       // seconds after the first card before it slides in
        public bool Named;        // whether ShowPlayer has filled its header
        public bool Left;
    }

    /// <summary>Builds a card for each score (hidden until shown), in the scores' font</summary>
    public static ResultsAwardsView Build(Transform parent, TMP_Text[] scores)
    {
        GameObject root = new GameObject("Results Cards", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        ResultsAwardsView view = root.AddComponent<ResultsAwardsView>();
        TMP_FontAsset font = scores.Length > 0 && scores[0] != null ? scores[0].font : null;

        view.scores = scores;
        view.cards = new Card[scores.Length];
        view.title = TitleBeside(parent, scores);
        for (int i = 0; i < scores.Length; i++)
        {
            if (scores[i] == null)
                continue;
            HideOldScore(scores[i]);
            view.cards[i] = MakeCard(root.transform, i, font);
        }
        SetLayer(root.transform, scores.Length > 0 && scores[0] != null ? scores[0].gameObject.layer : parent.gameObject.layer);
        view.Place();
        return view;
    }

    // The scene's score is only a marker now: its number and the box behind it don't draw
    static void HideOldScore(TMP_Text score)
    {
        score.enabled = false;
        foreach (Transform child in score.transform)
        {
            if (child.GetComponent<Image>() != null)
                child.gameObject.SetActive(false);
        }
    }

    // The big title: the one text beside the scores, or beside their box, that isn't a score
    static TMP_Text TitleBeside(Transform parent, TMP_Text[] scores)
    {
        Transform box = scores.Length > 0 && scores[0] != null ? scores[0].transform.parent : null;
        foreach (Transform among in new[] { box, parent })
        {
            if (among == null)
                continue;
            foreach (Transform child in among)
            {
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text != null && System.Array.IndexOf(scores, text) < 0)
                    return text;
            }
        }
        return null;
    }

    // Everything the view makes is drawn with the results' own elements
    static void SetLayer(Transform box, int layer)
    {
        box.gameObject.layer = layer;
        foreach (Transform child in box)
            SetLayer(child, layer);
    }

    static Card MakeCard(Transform parent, int place, TMP_FontAsset font)
    {
        Card card = new Card();
        card.Root = new GameObject("Card " + (place + 1), typeof(RectTransform));
        card.Rect = (RectTransform)card.Root.transform;
        card.Rect.SetParent(parent, false);
        card.Rect.anchorMin = card.Rect.anchorMax = new Vector2(0.5f, 0.5f);
        card.Rect.pivot = new Vector2(0.5f, 0f);
        card.Rect.sizeDelta = new Vector2(WIDTH, HEIGHT);
        card.Rect.localScale = Vector3.one * (place == 0 ? WINNER_SCALE : 1f);
        card.Fade = card.Root.AddComponent<CanvasGroup>();
        card.Fade.interactable = card.Fade.blocksRaycasts = false;

        // The winner's gold edge, then the panel
        if (place == 0)
            Stretch(Box(card.Rect, "Gold Edge", StatIcons.Panel(), GOLD, true), -4f);
        // (solid on the winner's, or the gold behind would tint it)
        Stretch(Box(card.Rect, "Panel", StatIcons.Panel(), place == 0 ? WINNER_PANEL : PANEL, true), 0f);

        // The header: the medal, then the name over the score
        card.Medal = Box(card.Rect, "Medal", StatIcons.Disc(), MEDALS[Mathf.Clamp(place, 0, MEDALS.Length - 1)], false);
        At(card.Medal.rectTransform, PAD, -PAD - (HEADER - MEDAL) / 2f, MEDAL, MEDAL);
        card.Place = Text(card.Medal.transform, "Place", font, 40f, INK, TextAlignmentOptions.Center);
        Stretch(card.Place.rectTransform, 4f);
        float textLeft = PAD + MEDAL + 12f;
        card.Player = Text(card.Rect, "Player", font, 28f, Color.white, TextAlignmentOptions.BottomLeft);
        At(card.Player.rectTransform, textLeft, -PAD, WIDTH - textLeft - PAD, 36f);
        card.Money = Text(card.Rect, "Money", font, 46f, MONEY, TextAlignmentOptions.TopLeft);
        At(card.Money.rectTransform, textLeft, -PAD - 34f, WIDTH - textLeft - PAD, HEADER - 34f);

        At(Box(card.Rect, "Rule", null, RULE, false).rectTransform, PAD, -PAD - HEADER - 2f, WIDTH - 2f * PAD, 2f);

        // The award
        card.AwardBlock = new GameObject("Award", typeof(RectTransform));
        RectTransform award = (RectTransform)card.AwardBlock.transform;
        award.SetParent(card.Rect, false);
        At(award, PAD, -PAD - HEADER - 8f, WIDTH - 2f * PAD, AWARD);
        card.Award = Text(award, "Name", font, 50f, GOLD, TextAlignmentOptions.Bottom);
        At(card.Award.rectTransform, 0f, -6f, WIDTH - 2f * PAD, 66f);
        card.Line = Text(award, "Line", font, 30f, Color.white, TextAlignmentOptions.Top);
        At(card.Line.rectTransform, 0f, -74f, WIDTH - 2f * PAD, 44f);

        // The vote's tag, half over the top edge
        card.VoteBack = Box(card.Rect, "Vote", StatIcons.Panel(), READY, true);
        card.VoteTag = card.VoteBack.gameObject;
        RectTransform tag = card.VoteBack.rectTransform;
        tag.anchorMin = tag.anchorMax = new Vector2(1f, 1f);
        tag.pivot = new Vector2(1f, 0.5f);
        tag.anchoredPosition = new Vector2(-10f, 0f);
        tag.sizeDelta = new Vector2(116f, 36f);
        card.Vote = Text(tag, "Label", font, 24f, INK, TextAlignmentOptions.Center);
        Stretch(card.Vote.rectTransform, 5f);
        card.VoteTag.SetActive(false);

        card.AwardBlock.SetActive(false);
        card.Root.SetActive(false);
        return card;
    }

    /// <summary>The card of a score (finishing order); tests</summary>
    internal GameObject CardOf(int score) { return cards[score]?.Root; }
    internal string PlayerName(int score) { return cards[score].Player.text; }
    internal string Money(int score) { return cards[score].Money.text; }
    internal string PlaceText(int score) { return cards[score].Place.text; }
    internal string AwardName(int score) { return cards[score].Award.text; }
    internal string AwardLine(int score) { return cards[score].Line.text; }
    internal bool AwardShown(int score) { return cards[score].AwardBlock.activeSelf; }
    internal string VoteText(int score) { return cards[score].VoteTag.activeSelf ? cards[score].Vote.text : ""; }
    internal TMP_Text Title => title;

    /// <summary>
    /// A score's player: the card comes up with their finishing place (1 = first; ties share one), name and score. A
    /// seat of -1 is nobody: no card
    /// </summary>
    public void ShowPlayer(int score, int seat, int place, string player, string money)
    {
        Card card = score >= 0 && score < cards.Length ? cards[score] : null;
        if (card == null)
            return;
        if (seat < 0)
        {
            card.Root.SetActive(false);
            return;
        }
        card.Place.text = Mathf.Max(1, place).ToString();
        card.Medal.color = MEDALS[Mathf.Clamp(place - 1, 0, MEDALS.Length - 1)];
        card.Player.text = player;
        card.Money.text = money;
        card.Named = true;
        Raise(card);
    }

    /// <summary>
    /// Shows the awards: each score's card is its seat's (seatOfScore[i]; -1: no card). A seat
    /// without an award keeps its card, with no award on it
    /// </summary>
    public void Show(AwardBoard board, int[] seatOfScore)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            Card card = cards[i];
            if (card == null)
                continue;
            int seat = seatOfScore != null && i < seatOfScore.Length ? seatOfScore[i] : -1;
            if (seat < 0)
            {
                card.Root.SetActive(false);
                continue;
            }
            if (!card.Named)
            {
                card.Place.text = (i + 1).ToString();
                card.Player.text = "P" + (seat + 1);
                card.Money.text = scores[i] != null ? scores[i].text : "";
            }
            Raise(card);

            Award award = board.AwardOf(seat);
            card.AwardBlock.SetActive(award != Award.None);
            card.Award.text = AwardRules.Name(award);
            card.Line.text = AwardRules.Line(award, board.CountOf(seat));
        }
    }

    /// <summary>A score's player's rematch vote: READY, OUT (they left: the card fades), or nothing yet</summary>
    public void ShowVote(int score, VoteMark mark)
    {
        Card card = score >= 0 && score < cards.Length ? cards[score] : null;
        if (card == null)
            return;
        card.VoteTag.SetActive(mark != VoteMark.None);
        card.Left = mark == VoteMark.Out;
        if (mark == VoteMark.None)
            return;
        card.Vote.text = Loc.Get(mark == VoteMark.Ready ? "results.ready" : "results.out");
        card.VoteBack.color = mark == VoteMark.Ready ? READY : OUT;
    }

    /// <summary>
    /// The cards are up: shows the awards picked or heard this match, and any that arrive while they stay up (a client
    /// whose results opened before the host's awards reached it). Hide stops it
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

    /// <summary>Hides every card and the title (the results are done with) and stops following</summary>
    public void Hide()
    {
        StopFollowing();
        foreach (Card card in cards)
        {
            if (card != null)
                card.Root.SetActive(false);
        }
        if (title != null)
            title.enabled = false;
    }

    /// <summary>Every card at rest, as if it had finished arriving (tests)</summary>
    internal void Settle()
    {
        clock = 1000f;
        Place();
        Move();
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

    // A card comes up: the first time, it waits its turn (the last place first, the winner last)
    void Raise(Card card)
    {
        if (card.Root.activeSelf)
            return;
        card.Root.SetActive(true);
        int place = System.Array.IndexOf(cards, card);
        card.Delay = clock + (cards.Length - 1 - place) * STAGGER;
        card.Fade.alpha = 0f;
    }

    void LateUpdate()
    {
        clock += Time.unscaledDeltaTime;
        Place();
        Move();
    }

    // Each card under its ghost: centred on its score, along the bottom of the screen, never off its sides. The scores
    // and the canvas can move (the window's size), so again each frame
    void Place()
    {
        RectTransform whole = Whole();
        Flatten(whole);
        Vector2 origin = CanvasPoint(transform, Vector2.zero, whole);
        Rect screen = whole != null ? whole.rect : new Rect(-960f, -540f, 1920f, 1080f);
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;
            float half = WIDTH * cards[i].Rect.localScale.x / 2f;
            float x = CanvasPoint(scores[i].rectTransform, MarkOf(scores[i]), whole).x;
            if (screen.width > 2f * (half + EDGE))
                x = Mathf.Clamp(x, screen.xMin + half + EDGE, screen.xMax - half - EDGE);
            cards[i].Home = new Vector2(x, screen.yMin + BOTTOM) - origin;
        }

        if (title != null && title.enabled)
        {
            RectTransform rect = title.rectTransform;
            float scale = Mathf.Max(0.01f, rect.localScale.x);
            rect.sizeDelta = TITLE_SIZE / scale;
            title.margin = Vector4.zero;
            title.alignment = TextAlignmentOptions.Center;
            title.enableWordWrapping = false;
            title.enableAutoSizing = true;
            title.fontSizeMin = 12f;
            Vector2 at = new Vector2(screen.center.x, screen.yMax - TITLE_TOP - TITLE_SIZE.y / 2f);
            Vector2 parentAt = rect.parent != null ? CanvasPoint(rect.parent, Vector2.zero, whole) : Vector2.zero;
            Vector2 pivot = (rect.pivot - new Vector2(0.5f, 0.5f)) * TITLE_SIZE;
            Vector2 flat = at - parentAt + pivot;
            rect.localPosition = new Vector3(flat.x, flat.y, rect.localPosition.z);
        }
    }

    // The cards slide up into place and fade in, each after its delay; a player who left the vote fades
    void Move()
    {
        foreach (Card card in cards)
        {
            if (card == null || !card.Root.activeSelf)
                continue;
            float arrived = PodiumActs.Smooth((clock - card.Delay) / APPEAR);
            card.Rect.localPosition = card.Home + Vector2.down * SLIDE * (1f - arrived);
            card.Fade.alpha = arrived * (card.Left ? GONE : 1f);
        }
    }

    // The point of a score its card centres on: the middle of the box behind it when the scene has one, else the
    // middle of its text (the scene's scores are left-aligned in boxes far wider than their text)
    static Vector2 MarkOf(TMP_Text score)
    {
        foreach (Transform child in score.transform)
        {
            if (child.GetComponent<Image>() != null)
                return child.localPosition;
        }
        Rect box = score.rectTransform.rect;
        Vector4 margin = score.margin;
        box.xMin += margin.x;
        box.xMax -= margin.z;
        float width = Mathf.Min(score.GetPreferredValues(score.text).x, box.width);
        float middle = score.horizontalAlignment == HorizontalAlignmentOptions.Left ? box.xMin + width / 2f
            : score.horizontalAlignment == HorizontalAlignmentOptions.Right ? box.xMax - width / 2f
            : box.center.x;
        return new Vector2(middle, box.center.y);
    }

    // The results' boxes sit in front of and behind the canvas (their z): the cards lie flat on it
    void Flatten(RectTransform whole)
    {
        float depth = 0f;
        for (Transform box = transform.parent; box != null && box != whole; box = box.parent)
            depth += box.localPosition.z;
        Vector3 at = transform.localPosition;
        if (at.z != -depth)
            transform.localPosition = new Vector3(at.x, at.y, -depth);
    }

    // The canvas everything sits in: the root canvas, or without one (tests) the topmost box
    RectTransform Whole()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return (RectTransform)canvas.rootCanvas.transform;
        Transform top = transform;
        while (top.parent != null)
            top = top.parent;
        return top as RectTransform;
    }

    // A point of a box (some of the results' boxes are plain transforms), in the canvas's own units. Worked out from the boxes' places and scales, not their world
    // positions: the canvas is at scale 0 until it first draws
    internal static Vector2 CanvasPoint(Transform rect, Vector2 point, Transform whole)
    {
        for (Transform box = rect; box != null && box != whole; box = box.parent)
            point = Vector2.Scale(point, box.localScale) + (Vector2)box.localPosition;
        return point;
    }

    static Image Box(Transform parent, string name, Sprite sprite, Color colour, bool sliced)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.transform.SetParent(parent, false);
        image.sprite = sprite;
        image.color = colour;
        image.raycastTarget = false;
        if (sliced)
        {
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
        }
        return image;
    }

    // From its parent's top-left corner, its own top-left there
    static void At(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    static void Stretch(Image image, float inset) { Stretch(image.rectTransform, inset); }

    static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, Color colour, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.alignment = alignment;
        text.color = colour;
        text.enableWordWrapping = false;
        text.enableAutoSizing = true; // never past its box: long names and long languages shrink
        text.fontSizeMin = Mathf.Min(12f, size);
        text.fontSizeMax = size;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }
}

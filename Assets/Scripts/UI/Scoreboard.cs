using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The live scoreboard on the middle of the screen's left edge, through the match (playtest, 2026-11-02; it replaces
/// each player's own score and placement HUD). One row per player, stacked by money: rows slide up and down as the
/// money changes, and first place is drawn bigger. Each row: the player's picture (their Steam picture: one per machine,
/// on that machine's first player. Anyone else, bots included, gets their own ghost in its colour and hat:
/// GhostPortrait), their name, their money, and the packages they hold (a stolen one, worth more, burns purple). Built
/// in code on a screen overlay canvas, so no scene or prefab holds it. Everything it shows, every machine already has:
/// scores and held orders follow the host online (OnlineOrders, OrderSync)
/// </summary>
public class Scoreboard : MonoBehaviour
{
    /// <summary>Over the players' views and HUDs, under the menus' overlays (prompts, rules, the reel)</summary>
    internal const int SORTING_ORDER = 50;

    // Sizes in the 1920 × 1080 reference
    internal const float ROW_HEIGHT = 64f;
    internal const float ROW_GAP = 8f;
    internal const float FIRST_SCALE = 1.3f;
    const float LEFT = 16f;
    const float BORDER = 4f;
    const float PAD = 6f;
    const float NAME_WIDTH = 190f;
    const float MONEY_WIDTH = 96f;
    internal const float PACKAGE = 52f;
    // Apart enough that two burning packages' flames don't touch: PurpleFlame.SIDE
    internal const float PACKAGE_SPACING = PACKAGE * PurpleFlame.SIDE + 2f;
    internal const float TAPER = 0.3f; // how much shorter the row's right end is than its left
    internal const float TIP = 28f;    // how far the right end's slanted cut leans
    const int BANNER_DETAIL = 2;       // the row's shape is drawn at this many pixels per reference pixel
    const float SLIDE_SPEED = 10f;   // how quickly a row reaches its place (1 / seconds, roughly)
    const float FLAME_FPS = 14f;

    static readonly Color INK = new Color(0.06f, 0.05f, 0.08f, 1f);
    static readonly Color PAPER = new Color(1f, 1f, 1f, 0.97f);

    /// <summary>Each row's width: picture, name, money, two packages, and the slanted end</summary>
    internal const float ROW_WIDTH = ROW_HEIGHT + PAD + NAME_WIDTH + MONEY_WIDTH + PACKAGE_SPACING + PACKAGE + 2 * PAD + TIP;

    static Scoreboard instance;
    static Sprite banner;

    Canvas canvas;
    RectTransform board;
    readonly List<Row> rows = new List<Row>();
    Transform portraits; // the ghost copies the portraits are taken of, far from the city

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject("Scoreboard", typeof(RectTransform));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<Scoreboard>();
    }

    void Awake()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;
        canvas.enabled = false;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        board = new GameObject("Board", typeof(RectTransform)).GetComponent<RectTransform>();
        board.SetParent(transform, false);
        board.anchorMin = board.anchorMax = new Vector2(0f, 0.5f);
        board.pivot = new Vector2(0f, 0.5f);
        board.anchoredPosition = new Vector2(LEFT, 0f);
        board.sizeDelta = Vector2.zero;

        portraits = new GameObject("Scoreboard Portraits").transform;
        portraits.SetParent(transform, false);
    }

    void OnDestroy()
    {
        Clear();
        if (instance == this)
            instance = null;
    }

    /// <summary>The states the board shows in: the match's driving</summary>
    internal static bool ShowsIn(GameState state)
    {
        return state == GameState.Tutorial || state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
    }

    /// <summary>
    /// The states that end a match's board: its rows (and their portraits) go, and the next match builds its own, as its
    /// players may have changed their colour or hat
    /// </summary>
    internal static bool EndsMatch(GameState state)
    {
        return state == GameState.Menu || state == GameState.PlayerSelect || state == GameState.Loading || state == GameState.Results;
    }

    /// <summary>The seats from first place down: most money first, a tie to the lower seat</summary>
    internal static List<int> Ranking(IList<int> seats, System.Func<int, int> money)
    {
        List<int> ranked = new List<int>(seats);
        ranked.Sort((a, b) =>
        {
            int byMoney = money(b).CompareTo(money(a));
            return byMoney != 0 ? byMoney : a.CompareTo(b);
        });
        return ranked;
    }

    /// <summary>
    /// Each rank's top edge, from the board's middle (up is +): first place is FIRST_SCALE tall, the rest ROW_HEIGHT,
    /// ROW_GAP apart, the whole stack centred
    /// </summary>
    internal static float[] RowTops(int count)
    {
        float[] tops = new float[count];
        if (count == 0)
            return tops;
        float total = ROW_HEIGHT * FIRST_SCALE + (count - 1) * ROW_HEIGHT + (count - 1) * ROW_GAP;
        float top = total / 2f;
        for (int rank = 0; rank < count; rank++)
        {
            tops[rank] = top;
            top -= (rank == 0 ? ROW_HEIGHT * FIRST_SCALE : ROW_HEIGHT) + ROW_GAP;
        }
        return tops;
    }

    /// <summary>
    /// Whether a seat shows its Steam picture: it has a Steam ID, and no lower seat has the same one (players sharing a
    /// machine share its Steam account: its first player shows the picture, the others their ghost)
    /// </summary>
    internal static bool FirstWithId(int seat, ulong id, IDictionary<int, ulong> idBySeat)
    {
        if (id == 0)
            return false;
        foreach (KeyValuePair<int, ulong> other in idBySeat)
        {
            if (other.Key < seat && other.Value == id)
                return false;
        }
        return true;
    }

    /// <summary>
    /// The row's shape at a point (reference pixels, from its top left, down is +y): white, edged in black, tapering
    /// from full height at the picture to TAPER shorter at the right, where it ends in a slanted cut
    /// </summary>
    internal static Color BannerAt(float x, float y)
    {
        float half = ROW_HEIGHT / 2f * (1f - TAPER * Mathf.Clamp01(x / ROW_WIDTH));
        float top = ROW_HEIGHT / 2f - half;
        float right = ROW_WIDTH - TIP * Mathf.Clamp01((y - top) / (2f * half));
        float inside = Mathf.Min(half - Mathf.Abs(y - ROW_HEIGHT / 2f), Mathf.Min(x, right - x));
        float alpha = Mathf.Clamp01(inside * BANNER_DETAIL + 0.5f);
        if (alpha <= 0f)
            return Color.clear;
        Color colour = Color.Lerp(INK, PAPER, Mathf.Clamp01((inside - BORDER) * BANNER_DETAIL + 0.5f));
        colour.a *= alpha;
        return colour;
    }

    // The row's shape, drawn the first time it's needed
    static Sprite Banner()
    {
        if (banner == null)
        {
            int width = Mathf.CeilToInt(ROW_WIDTH * BANNER_DETAIL);
            int height = Mathf.CeilToInt(ROW_HEIGHT * BANNER_DETAIL);
            Color[] pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                    pixels[py * width + px] = BannerAt((px + 0.5f) / BANNER_DETAIL, ROW_HEIGHT - (py + 0.5f) / BANNER_DETAIL);
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Scoreboard Row", wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels(pixels);
            texture.Apply();
            banner = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0f, 1f));
        }
        return banner;
    }

    void LateUpdate()
    {
        GameManager game = GameManager.Instance;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        GameState state = game != null ? game.MainState : GameState.Default;
        if (EndsMatch(state) || players == null)
        {
            if (rows.Count > 0)
                Clear();
            canvas.enabled = false;
            return;
        }

        bool showing = ShowsIn(state);
        if (showing)
            KeepRowsFor(players.Roster);
        canvas.enabled = showing && rows.Count > 0;
        if (!canvas.enabled)
            return;

        Dictionary<int, ulong> ids = new Dictionary<int, ulong>();
        foreach (Row row in rows)
            ids[row.Seat] = SteamIdIn(row.Seat, players.Roster);

        List<int> seats = new List<int>();
        foreach (Row row in rows)
            seats.Add(row.Seat);
        List<int> ranked = Ranking(seats, MoneyIn);
        float[] tops = RowTops(ranked.Count);
        float step = 1f - Mathf.Exp(-SLIDE_SPEED * Time.unscaledDeltaTime);
        int flame = Mathf.FloorToInt(Time.unscaledTime * FLAME_FPS);

        foreach (Row row in rows)
        {
            int rank = ranked.IndexOf(row.Seat);
            ulong id = ids[row.Seat];
            bool steam = FirstWithId(row.Seat, id, ids);
            row.Show(NameIn(row.Seat, players.Roster, steam), MoneyIn(row.Seat), OrderSync.HandlerIn(row.Seat),
                steam ? SteamAvatars.Get(id) : null, flame);
            row.MoveTowards(tops[rank], rank == 0 ? FIRST_SCALE : 1f, step);
        }
    }

    static int MoneyIn(int seat)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        return handler != null ? handler.Score : 0;
    }

    // The online player in a seat, if this is an online match
    static OnlinePlayer OnlineIn(int seat)
    {
        OnlineGame online = OnlineGame.Playing;
        if (online == null)
            return null;
        foreach (OnlinePlayer player in online.Players)
        {
            if (player != null && player.Seat == seat)
                return player;
        }
        return null;
    }

    // This machine's first player's seat (offline, the one that shows its Steam account), or -1
    static int FirstLocalSeat(PlayerRoster roster)
    {
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            PlayerSlot slot = roster[seat];
            if (slot != null && slot.IsLocal)
                return seat;
        }
        return -1;
    }

    static ulong SteamIdIn(int seat, PlayerRoster roster)
    {
        if (OnlineGame.Playing != null)
        {
            OnlinePlayer online = OnlineIn(seat);
            return online != null && !online.IsBot ? online.SteamId : 0;
        }
        return seat == FirstLocalSeat(roster) ? SteamNames.MyId() : 0;
    }

    // Their name: online, the name they share (a bot's fits its personality: BotNames); offline, the Steam name on this
    // machine's first player. Someone sharing another's Steam account, or without a name, is "Player n"
    static string NameIn(int seat, PlayerRoster roster, bool ownsSteamPicture)
    {
        OnlinePlayer online = OnlineIn(seat);
        if (online != null)
        {
            if (online.IsBot || online.SteamId == 0 || ownsSteamPicture)
                return PlayerNames.Shown(online.Name, seat);
            return PlayerNames.Fallback(seat);
        }
        if (OnlineGame.Playing == null && seat == FirstLocalSeat(roster))
        {
            string mine = PlayerNames.Clean(SteamNames.Mine());
            if (mine.Length > 0)
                return mine;
        }
        return PlayerNames.Fallback(seat);
    }

    // A row for each seated player (rebuilt when a seat's player changes); a row whose player has gone goes too
    void KeepRowsFor(PlayerRoster roster)
    {
        for (int i = rows.Count - 1; i >= 0; i--)
        {
            PlayerSlot slot = roster[rows[i].Seat];
            if (slot == null || slot.Player != rows[i].Player)
            {
                rows[i].Destroy();
                rows.RemoveAt(i);
            }
        }
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            PlayerSlot slot = roster[seat];
            if (slot == null || slot.Player == null || rows.Exists(r => r.Seat == seat))
                continue;
            float[] tops = RowTops(rows.Count + 1);
            rows.Add(new Row(board, seat, slot.Player, GhostPortrait.Of(HighlightReel.Avatar(slot), seat, portraits), tops[tops.Length - 1]));
        }
    }

    void Clear()
    {
        foreach (Row row in rows)
            row.Destroy();
        rows.Clear();
    }

    // One player's row
    class Row
    {
        public readonly int Seat;
        public readonly GameObject Player;
        readonly RectTransform rect;
        readonly RawImage picture;
        readonly TMP_Text nameText;
        readonly TMP_Text moneyText;
        readonly Image[] icons = new Image[2];
        readonly Image[] flames = new Image[2];
        readonly GhostPortrait ghost;
        string shownName;
        int shownMoney = -1;

        public Row(RectTransform board, int seat, GameObject player, GhostPortrait ghost, float top)
        {
            Seat = seat;
            Player = player;
            this.ghost = ghost;

            float x = 0f;
            rect = Box(board, "Row " + (seat + 1), new Vector2(ROW_WIDTH, ROW_HEIGHT), Color.white);
            rect.GetComponent<Image>().sprite = Banner();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, top);

            // The picture, in a thick black frame filling the row's height
            RectTransform frame = Box(rect, "Picture Frame", new Vector2(ROW_HEIGHT, ROW_HEIGHT), INK);
            frame.anchoredPosition = new Vector2(x, 0f);
            picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            Fill(picture.rectTransform, frame, BORDER);
            picture.raycastTarget = false;
            x += ROW_HEIGHT + PAD;

            nameText = Label(rect, "Name", x, NAME_WIDTH, 28f, FontStyles.Bold);
            x += NAME_WIDTH;
            moneyText = Label(rect, "Money", x, MONEY_WIDTH, 26f, FontStyles.Normal);
            x += MONEY_WIDTH;

            for (int i = 0; i < 2; i++)
            {
                float y = -(ROW_HEIGHT - PACKAGE) / 2f;
                flames[i] = new GameObject("Flame " + (i + 1), typeof(RectTransform)).AddComponent<Image>();
                RectTransform flameRect = flames[i].rectTransform;
                flameRect.SetParent(rect, false);
                flameRect.anchorMin = flameRect.anchorMax = new Vector2(0f, 1f);
                flameRect.pivot = new Vector2(0.5f, 0.5f);
                flameRect.sizeDelta = Vector2.one * PACKAGE * PurpleFlame.SPREAD;
                flameRect.anchoredPosition = new Vector2(x + PACKAGE / 2f, y - PACKAGE / 2f);
                flames[i].raycastTarget = false;
                flames[i].enabled = false;

                RectTransform icon = Box(rect, "Package " + (i + 1), new Vector2(PACKAGE, PACKAGE), Color.white);
                icon.anchoredPosition = new Vector2(x, y);
                icons[i] = icon.GetComponent<Image>();
                icons[i].preserveAspect = true;
                icons[i].enabled = false;
                x += PACKAGE_SPACING;
            }
        }

        public void Show(string name, int money, OrderHandler handler, Texture steamPicture, int flameFrame)
        {
            if (name != shownName)
            {
                shownName = name;
                nameText.text = name;
            }
            if (money != shownMoney)
            {
                shownMoney = money;
                moneyText.text = "$" + money;
            }

            Texture shown = steamPicture != null ? steamPicture : ghost != null ? ghost.Texture : null;
            if (picture.texture != shown)
                picture.texture = shown;
            if (ghost != null)
                ghost.Tick();

            // What they hold, from the left
            Order first = handler != null ? handler.order1 : null;
            Order second = handler != null ? handler.order2 : null;
            if (first == null)
            {
                first = second;
                second = null;
            }
            ShowPackage(0, first, flameFrame);
            ShowPackage(1, second, flameFrame);
        }

        void ShowPackage(int slot, Order order, int flameFrame)
        {
            Sprite icon = order != null ? order.Icon : null;
            if (icons[slot].sprite != icon)
                icons[slot].sprite = icon;
            icons[slot].enabled = icon != null;

            bool burning = order != null && order.WorthMoreForSteals;
            flames[slot].enabled = burning;
            if (burning)
            {
                Sprite[] frames = PurpleFlame.Frames;
                flames[slot].sprite = frames[(flameFrame + slot * 5) % frames.Length];
            }
        }

        public void MoveTowards(float top, float scale, float step)
        {
            Vector2 at = rect.anchoredPosition;
            rect.anchoredPosition = new Vector2(at.x, Mathf.Lerp(at.y, top, step));
            float size = Mathf.Lerp(rect.localScale.x, scale, step);
            rect.localScale = new Vector3(size, size, 1f);
        }

        public void Destroy()
        {
            if (rect != null)
                Object.Destroy(rect.gameObject);
            if (ghost != null)
                ghost.Destroy();
        }

        static RectTransform Box(RectTransform parent, string name, Vector2 size, Color colour)
        {
            Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
            RectTransform box = image.rectTransform;
            box.SetParent(parent, false);
            box.anchorMin = box.anchorMax = new Vector2(0f, 1f);
            box.pivot = new Vector2(0f, 1f);
            box.sizeDelta = size;
            image.color = colour;
            image.raycastTarget = false;
            return box;
        }

        static void Fill(RectTransform child, RectTransform parent, float inset)
        {
            child.SetParent(parent, false);
            child.anchorMin = Vector2.zero;
            child.anchorMax = Vector2.one;
            child.offsetMin = new Vector2(inset, inset);
            child.offsetMax = new Vector2(-inset, -inset);
        }

        static TMP_Text Label(RectTransform row, string name, float x, float width, float size, FontStyles style)
        {
            TMP_Text text = ResolutionRow.Text(row, name, null, size, TextAlignmentOptions.MidlineLeft, new Vector2(0f, 0.5f), Vector2.zero, width);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = new Vector2(width - PAD, ROW_HEIGHT);
            text.color = INK;
            text.fontStyle = style;
            text.enableAutoSizing = true;
            text.fontSizeMin = size * 0.55f;
            text.fontSizeMax = size;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
    }
}

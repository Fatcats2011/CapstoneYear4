using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The live scoreboard on the middle of the screen's left edge, through the match (playtest, 2026-11-02; it replaces
/// each player's own score and placement HUD). One row per player, stacked by money: rows slide up and down as the
/// money changes, and first place is drawn bigger. Each row: the player's picture (their Steam picture: one per machine,
/// on that machine's first player. Anyone else, bots included, gets their own ghost in its colour and hat:
/// GhostPortrait) with a small crown on it if they won the match this one is a rematch of (RematchCrown), their name,
/// their money, and the packages they hold (a stolen one, worth more, burns purple and wears its "×2"). Built
/// in code on a screen overlay canvas, so no scene or prefab holds it. Everything it shows, every machine already has:
/// scores and held orders follow the host online (OnlineOrders, OrderSync).
/// A rise in a player's money also shows a small "+$" over their number for a moment while the number counts up to its
/// new total (MoneyShow), and the board watches the ranking so a change of first place is announced (WatchLead)
/// </summary>
public class Scoreboard : MonoBehaviour
{
    /// <summary>Over the players' views and HUDs, under the menus' overlays (prompts, rules, the reel)</summary>
    internal const int SORTING_ORDER = 50;

    // The whole board drawn smaller than the sizes below (and bigger with the text size setting)
    internal const float BOARD_SCALE = 0.7f;

    // Sizes in the 1920 × 1080 reference
    internal const float ROW_HEIGHT = 64f;
    internal const float ROW_GAP = 8f;
    internal const float FIRST_SCALE = 1.3f;
    internal const float LEFT = 16f;
    const float BORDER = 4f;
    internal const float PAD = 6f;
    const float NAME_WIDTH = 150f;
    internal const float MONEY_WIDTH = 84f;
    internal const float PACKAGE = 52f;
    // Apart enough that two burning packages' flames don't touch: PurpleFlame.SIDE
    internal const float PACKAGE_SPACING = PACKAGE * PurpleFlame.SIDE + 2f;
    internal const float TAPER = 0.3f; // how much shorter the row's right end is than its left
    internal const float TIP = 22f;    // how far the right end's slanted cut leans
    const int BANNER_DETAIL = 2;       // the row's shape is drawn at this many pixels per reference pixel
    const float SLIDE_SPEED = 10f;   // how quickly a row reaches its place (1 / seconds, roughly)
    const float FLAME_FPS = 14f;
    const float SEAT_BADGE = 30f;   // colourblind mode's badges
    const float STOLEN_BADGE = 34f;
    const float STAMP_SIZE = 20f;    // a stolen package's "×2"
    const float STAMP_WIDTH = 44f;
    internal const float CROWN = 40f;      // the rematch winner's crown, on the picture's top-left corner
    const float CROWN_TILT = 22f;
    const float GAIN_SIZE = 16f;     // the "+$" over a row's money
    const float GAIN_HEIGHT = 20f;
    const float GAIN_TOP = 2f;       // its gap from the row's top edge

    static readonly Color INK = new Color(0.06f, 0.05f, 0.08f, 1f);
    static readonly Color PAPER = new Color(1f, 1f, 1f, 0.2f); // mostly see through
    static readonly Color INFECTED_TINT = new Color(1f, 0.35f, 0.35f, 1f); // over an infected player's picture
    const float OUT_FADE = 0.55f;                                          // how solid an eliminated player's row is

    /// <summary>Each row's width: picture, name, money, two packages, and the slanted end</summary>
    internal const float ROW_WIDTH = ROW_HEIGHT + PAD + NAME_WIDTH + MONEY_WIDTH + PACKAGE_SPACING + PACKAGE + 2 * PAD + TIP;

    /// <summary>A compact row's width: picture, money and two packages, with no name and no slanted tip</summary>
    internal const float COMPACT_ROW_WIDTH = ROW_HEIGHT + PAD + MONEY_WIDTH + PACKAGE_SPACING + PACKAGE + 2 * PAD;

    /// <summary>How far apart the cells of a strip or a grid sit</summary>
    internal const float CELL_GAP = ROW_GAP;

    /// <summary>The bottom view's compass, across a 1920-wide screen, which a stacked strip leaves clear</summary>
    internal const float COMPASS_LEFT = 715f;
    internal const float COMPASS_RIGHT = 1205f;
    /// <summary>The space a strip keeps from the compass each side</summary>
    internal const float STRIP_MARGIN = 16f;

    static Scoreboard instance;
    static Sprite banner;
    static Sprite compactBanner;

    Canvas canvas;
    RectTransform board;
    readonly List<Row> rows = new List<Row>();
    Transform portraits; // the ghost copies the portraits are taken of, far from the city
    int leader = -1;     // the seat the board last saw leading
    BoardLayout shownLayout = LayoutFor(1, SplitLayout.Boxed); // the layout the rows were built for

    /// <summary>The seat the board last saw leading: -1 for none (tests)</summary>
    internal int Leader => leader;

    /// <summary>
    /// What one row's money column shows over time: the last total seen, the "+$" of a recent rise (rises while it is up
    /// add to it) and the number counting up to the new total. Plain numbers, so it is tested directly. A fall, a reset
    /// or the first total shows no gain
    /// </summary>
    internal struct MoneyShow
    {
        /// <summary>The last total seen: -1 before any</summary>
        public int Total;
        /// <summary>What the number counts up from</summary>
        public int From;
        /// <summary>The "+$" showing: 0 for none</summary>
        public int Gain;
        /// <summary>When the last gain landed</summary>
        public float GainedAt;

        /// <summary>
        /// A new total at a time: a rise adds to the gain (or starts one, if the last has faded), counts up from what was
        /// showing, and times from now; a fall or the first total clears the gain. With showGains false there is never one
        /// </summary>
        public void See(int total, float now, bool showGains)
        {
            if (total == Total)
                return;
            if (Total >= 0 && total > Total && showGains)
            {
                int shown = Shown(now);
                bool showing = Gain > 0 && GainFeedbackRules.PopAlpha(now - GainedAt) > 0f;
                Gain = (showing ? Gain : 0) + (total - Total);
                From = shown;
                GainedAt = now;
            }
            else
            {
                Gain = 0;
                From = total;
            }
            Total = total;
        }

        /// <summary>The number to show at a time: counting up to the total while a gain is showing, else the total</summary>
        public int Shown(float now)
        {
            return Gain > 0 ? GainFeedbackRules.CountUp(From, Total, now - GainedAt) : Total;
        }

        /// <summary>The "+$" text at a time: the gain while it is up (it fades out), else nothing</summary>
        public string GainText(float now)
        {
            return Gain > 0 && GainFeedbackRules.PopAlpha(now - GainedAt) > 0f ? "+$" + Gain : "";
        }
    }

    /// <summary>
    /// Each frame's ranking: announces a change of first place (ScoreFeedback.AnnounceLead). Not the first leader of a
    /// match, and nothing in a mode that doesn't call leads, which also starts it over
    /// </summary>
    /// <param name="ranking">Seats, best first</param>
    /// <param name="points">What a seat has, in the unit the ranking is in</param>
    internal void WatchLead(IReadOnlyList<int> ranking, System.Func<int, int> points, GameMode mode)
    {
        if (!GainFeedbackRules.LeadCalled(mode))
        {
            leader = -1;
            return;
        }
        int now = GainFeedbackRules.NewLeader(leader, ranking, points);
        if (GainFeedbackRules.LeadChanged(leader, now))
            ScoreFeedback.AnnounceLead(now, leader);
        leader = now;
    }

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
        board.pivot = new Vector2(0f, 0.5f);
        board.sizeDelta = Vector2.zero; // its anchor and place follow the layout (LateUpdate)

        // Not under the canvas: its scale follows the screen's size, and a copy built under it came out that much bigger
        // or smaller than its camera's framing (from the second match on, once the canvas had been drawn)
        portraits = new GameObject("Scoreboard Portraits").transform;
        DontDestroyOnLoad(portraits.gameObject);
        Colourblind.Changed += Clear; // the ghost portraits are taken again, in the colours worn now
    }

    void OnDestroy()
    {
        Colourblind.Changed -= Clear;
        Clear();
        if (portraits != null)
            Destroy(portraits.gameObject);
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

    /// <summary>How the rows are arranged: one above the next, side by side, or two by two</summary>
    internal enum BoardShape { Column, Strip, Grid }

    /// <summary>
    /// Where the board sits and what it looks like. Anchor is a point on the screen (0 to 1 each way). LeftEdge means
    /// the board hangs off the screen's left edge, as with one local player; otherwise it is centred on Anchor
    /// </summary>
    internal struct BoardLayout : System.IEquatable<BoardLayout>
    {
        public Vector2 Anchor;
        public bool LeftEdge;
        public BoardShape Shape;
        public bool Compact;

        public bool Equals(BoardLayout other)
        {
            return Anchor == other.Anchor && LeftEdge == other.LeftEdge && Shape == other.Shape && Compact == other.Compact;
        }

        public override bool Equals(object obj)
        {
            return obj is BoardLayout other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Anchor.GetHashCode() ^ (LeftEdge ? 1 : 0) ^ ((int)Shape << 1) ^ (Compact ? 8 : 0);
        }
    }

    /// <summary>
    /// The board's layout for this many local players: the one-player board for none or one, and for two, whichever
    /// way the screen is split. Three and four always get quarters
    /// </summary>
    internal static BoardLayout LayoutFor(int localPlayers, SplitLayout twoPlayers)
    {
        switch (localPlayers)
        {
            case 2:
                switch (twoPlayers)
                {
                    case SplitLayout.SideBySide:
                        return new BoardLayout { Anchor = new Vector2(0.5f, 0.5f), LeftEdge = false, Shape = BoardShape.Column, Compact = true };
                    case SplitLayout.Stacked:
                        return new BoardLayout { Anchor = new Vector2(0.5f, 0.5f), LeftEdge = false, Shape = BoardShape.Strip, Compact = true };
                    default:
                        return new BoardLayout { Anchor = new Vector2(0.125f, 0.5f), LeftEdge = false, Shape = BoardShape.Column, Compact = false };
                }
            case 3:
                return new BoardLayout { Anchor = new Vector2(0.125f, 0.25f), LeftEdge = false, Shape = BoardShape.Column, Compact = false };
            case 4:
                return new BoardLayout { Anchor = new Vector2(0.5f, 0.5f), LeftEdge = false, Shape = BoardShape.Grid, Compact = true };
            default:
                return new BoardLayout { Anchor = new Vector2(0f, 0.5f), LeftEdge = true, Shape = BoardShape.Column, Compact = false };
        }
    }

    /// <summary>
    /// Each rank's row top-left, in board space (up is +); first place is FIRST_SCALE bigger. A column is stacked as
    /// RowTops says, a strip runs left to right round a stripGap-wide gap in the board's middle (StripGap), and a grid
    /// gives each rank a quarter round the middle, first place top-left
    /// </summary>
    internal static Vector2[] CellSpots(BoardLayout layout, int count, float stripGap = 0f)
    {
        Vector2[] spots = new Vector2[count];
        if (count == 0)
            return spots;
        float width = layout.Compact ? COMPACT_ROW_WIDTH : ROW_WIDTH;
        float half = CELL_GAP / 2f;
        if (layout.Shape == BoardShape.Column)
        {
            float[] tops = RowTops(count);
            for (int rank = 0; rank < count; rank++)
            {
                float scale = rank == 0 ? FIRST_SCALE : 1f;
                spots[rank] = new Vector2(layout.LeftEdge ? 0f : -width * scale / 2f, tops[rank]);
            }
        }
        else if (layout.Shape == BoardShape.Strip)
        {
            // The first half of the ranks left of the gap, the rest right of it. With an even count the narrower half
            // moves out by the difference, so the strip is as wide each side of the middle; an odd count keeps the gap
            // tight, with its extra rank on the left
            int left = (count + 1) / 2;
            float leftWidth = width * FIRST_SCALE + (left - 1) * (width + CELL_GAP);
            float rightWidth = count > left ? (count - left) * (width + CELL_GAP) - CELL_GAP : 0f;
            float leftEnd = -stripGap / 2f, rightStart = stripGap / 2f;
            if (count % 2 == 0)
            {
                leftEnd -= Mathf.Max(0f, rightWidth - leftWidth);
                rightStart += Mathf.Max(0f, leftWidth - rightWidth);
            }
            float x = leftEnd - leftWidth;
            for (int rank = 0; rank < count; rank++)
            {
                if (rank == left)
                    x = rightStart;
                float scale = rank == 0 ? FIRST_SCALE : 1f;
                spots[rank] = new Vector2(x, ROW_HEIGHT * scale / 2f);
                x += width * scale + CELL_GAP;
            }
        }
        else
        {
            for (int rank = 0; rank < count; rank++)
            {
                float scale = rank == 0 ? FIRST_SCALE : 1f;
                float x = rank % 2 == 1 ? half : -half - width * scale;
                float y = rank / 2 == 1 ? -half : half + ROW_HEIGHT * scale;
                spots[rank] = new Vector2(x, y);
            }
        }
        return spots;
    }

    /// <summary>
    /// The board's scale for a layout, text size and canvas width (reference units). The one-player board grows with
    /// the text size. A board in a quarter's empty bar grows with it only as far as first place's full row still fits
    /// the bar (a quarter of the canvas, less LEFT each side). A board in the middle of the screen never grows past its
    /// normal size: bigger, it would reach the views' compasses, horns and labels
    /// </summary>
    internal static float BoardScaleFor(BoardLayout layout, float textScale, float canvasWidth)
    {
        if (layout.LeftEdge)
            return BOARD_SCALE * textScale;
        if (layout.Compact)
            return BOARD_SCALE * Mathf.Min(textScale, 1f);
        return Mathf.Min(BOARD_SCALE * textScale, (0.25f * canvasWidth - 2f * LEFT) / (ROW_WIDTH * FIRST_SCALE));
    }

    /// <summary>
    /// The gap a strip leaves in its middle, in board space at a board scale: wide enough that the bottom view's
    /// compass (COMPASS_LEFT to COMPASS_RIGHT across a 1920 screen) and STRIP_MARGIN each side of it stay clear
    /// </summary>
    internal static float StripGap(float boardScale)
    {
        return (COMPASS_RIGHT - COMPASS_LEFT + 2f * STRIP_MARGIN) / boardScale;
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
    /// Whether the board's rows must be built again: the layout they were built for is not the one wanted now (a local
    /// player joined or left, or the two-player split changed)
    /// </summary>
    internal static bool Relays(BoardLayout shown, BoardLayout now)
    {
        return !shown.Equals(now);
    }

    /// <summary>The full row's shape at a point (BannerAt with compact false)</summary>
    internal static Color BannerAt(float x, float y)
    {
        return BannerAt(x, y, false);
    }

    /// <summary>
    /// The row's shape at a point (reference pixels, from its top left, down is +y): white, edged in black. The full
    /// row tapers from full height at the picture to TAPER shorter at the right, where it ends in a slanted cut; the
    /// compact row is a plain box COMPACT_ROW_WIDTH wide
    /// </summary>
    internal static Color BannerAt(float x, float y, bool compact)
    {
        float inside;
        if (compact)
        {
            inside = Mathf.Min(Mathf.Min(y, ROW_HEIGHT - y), Mathf.Min(x, COMPACT_ROW_WIDTH - x));
        }
        else
        {
            float half = ROW_HEIGHT / 2f * (1f - TAPER * Mathf.Clamp01(x / ROW_WIDTH));
            float top = ROW_HEIGHT / 2f - half;
            float right = ROW_WIDTH - TIP * Mathf.Clamp01((y - top) / (2f * half));
            inside = Mathf.Min(half - Mathf.Abs(y - ROW_HEIGHT / 2f), Mathf.Min(x, right - x));
        }
        float alpha = Mathf.Clamp01(inside * BANNER_DETAIL + 0.5f);
        if (alpha <= 0f)
            return Color.clear;
        Color colour = Color.Lerp(INK, PAPER, Mathf.Clamp01((inside - BORDER) * BANNER_DETAIL + 0.5f));
        colour.a *= alpha;
        return colour;
    }

    // The row's shape, full or compact, each drawn the first time it's needed
    static Sprite Banner(bool compact)
    {
        Sprite cached = compact ? compactBanner : banner;
        if (cached == null)
        {
            int width = Mathf.CeilToInt((compact ? COMPACT_ROW_WIDTH : ROW_WIDTH) * BANNER_DETAIL);
            int height = Mathf.CeilToInt(ROW_HEIGHT * BANNER_DETAIL);
            Color[] pixels = new Color[width * height];
            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                    pixels[py * width + px] = BannerAt((px + 0.5f) / BANNER_DETAIL, ROW_HEIGHT - (py + 0.5f) / BANNER_DETAIL, compact);
            }
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = compact ? "Scoreboard Compact Row" : "Scoreboard Row",
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(pixels);
            texture.Apply();
            cached = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0f, 1f));
            if (compact)
                compactBanner = cached;
            else
                banner = cached;
        }
        return cached;
    }

    void LateUpdate()
    {
        GameManager game = GameManager.Instance;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        GameState state = game != null ? game.MainState : GameState.Default;
        if (EndsMatch(state) || players == null)
        {
            leader = -1; // a rematch starts with nobody leading
            if (rows.Count > 0)
                Clear();
            canvas.enabled = false;
            return;
        }

        // Where the board sits and how its rows are laid out follow the local players' split of the screen
        BoardLayout layout = LayoutFor(players.Roster.LocalCount, SplitScreenLayout.TwoPlayers);
        if (Relays(shownLayout, layout))
        {
            Clear();
            shownLayout = layout;
        }
        // Every frame, not just on a relayout: Awake doesn't place the board and Relays never fires for one player
        board.anchorMin = board.anchorMax = layout.Anchor;
        board.anchoredPosition = layout.LeftEdge ? new Vector2(LEFT, 0f) : Vector2.zero;
        float canvasWidth = ((RectTransform)transform).rect.width; // reference units; 0 until the canvas is first laid out
        float boardScale = BoardScaleFor(layout, TextSize.Scale, canvasWidth > 0f ? canvasWidth : 1920f);

        bool showing = ShowsIn(state);
        if (showing)
            KeepRowsFor(players.Roster, layout, StripGap(boardScale));
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
        Vector2[] spots = CellSpots(layout, ranked.Count, StripGap(boardScale));
        float step = 1f - Mathf.Exp(-SLIDE_SPEED * Time.unscaledDeltaTime);
        board.localScale = Vector3.one * boardScale;
        float clock = Time.unscaledTime; // the slide's and the flame's clock: the "+$" and count-up use it too
        int flame = Mathf.FloorToInt(clock * FLAME_FPS);
        WatchLead(ranked, MoneyIn, HouseRules.Playing.Mode);

        foreach (Row row in rows)
        {
            int rank = ranked.IndexOf(row.Seat);
            ulong id = ids[row.Seat];
            bool steam = FirstWithId(row.Seat, id, ids);
            row.Show(NameIn(row.Seat, players.Roster, steam), MoneyIn(row.Seat), OrderSync.HandlerIn(row.Seat),
                steam ? SteamAvatars.Get(id) : null, flame, clock);
            row.MoveTowards(spots[rank], rank == 0 ? FIRST_SCALE : 1f, step);
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

    /// <summary>
    /// What a row shows in its money column: dollars where money's the point, the time held in Kings Crown (its points are
    /// time), nothing in Infection and Hot Potato, where staying clean or in is what counts
    /// </summary>
    internal static string ScoreText(GameMode mode, int points)
    {
        if (mode == GameMode.KingsCrown)
            return DynamicNumberUI.TimerText(points / TimedModeRules.CROWN_POINTS_PER_SECOND);
        return TimedModeRules.IsTimed(mode) ? "" : "$" + points;
    }

    /// <summary>A seated player's name as the board shows it (the results' podium names it the same way)</summary>
    internal static string ShownName(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return PlayerNames.Fallback(seat);
        return NameIn(seat, players.Roster, PictureIdOf(seat) != 0);
    }

    /// <summary>
    /// The Steam ID whose picture a seated player shows, as the board shows it (the results' cards too): 0 for a bot, a
    /// player without Steam, or one sharing another seat's account (they show their ghost)
    /// </summary>
    internal static ulong PictureIdOf(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return 0;
        Dictionary<int, ulong> ids = new Dictionary<int, ulong>();
        for (int other = 0; other < Constants.MAX_PLAYERS; other++)
        {
            PlayerSlot slot = players.Roster[other];
            if (slot != null && slot.Player != null)
                ids[other] = SteamIdIn(other, players.Roster);
        }
        ulong id = ids.TryGetValue(seat, out ulong found) ? found : 0;
        return FirstWithId(seat, id, ids) ? id : 0;
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

    // A row for each seated player in the layout's form (rebuilt when a seat's player changes); a row whose player has
    // gone goes too
    void KeepRowsFor(PlayerRoster roster, BoardLayout layout, float stripGap)
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
            Vector2[] spots = CellSpots(layout, rows.Count + 1, stripGap);
            rows.Add(new Row(board, seat, slot.Player, GhostPortrait.Of(HighlightReel.Avatar(slot), seat, portraits),
                spots[spots.Length - 1], layout.Compact));
        }
    }

    /// <summary>Whether a seat's row shows the winner's crown (tests); false without the row</summary>
    internal static bool CrownShownFor(int seat)
    {
        Row row = instance != null ? instance.rows.Find(r => r.Seat == seat) : null;
        return row != null && row.CrownShown;
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
        readonly TMP_Text nameText;   // null on a compact row, which shows no name
        readonly TMP_Text moneyText;
        readonly Image[] icons = new Image[2];
        readonly Image[] flames = new Image[2];
        readonly GameObject seatBadge;               // colourblind mode: the seat's shape and number on the picture
        readonly GameObject[] stolenBadges = new GameObject[2]; // ...and a "!" on a stolen package
        readonly TMP_Text[] stamps = new TMP_Text[2];            // a stolen package's "×2" (StealFeedbackRules.StampText)
        readonly string[] shownStamps = { "", "" };
        readonly GhostPortrait ghost;
        readonly CanvasGroup fade;
        readonly Image pictureFrame;
        readonly GameObject outCross;
        readonly GameObject crown;
        readonly TMP_Text gainText;   // the "+$" at the top of the money column, over the number
        MoneyShow moneyShow = new MoneyShow { Total = -1 };
        string shownName;
        int shownMoney = -1;
        string shownGain = "";
        float shownGainAlpha = 1f;
        GameMode shownMode;

        public bool CrownShown => crown.activeSelf;

        /// <summary>
        /// A row for a seat, its top left at a spot on the board. A compact row has no name: its money follows the picture
        /// </summary>
        public Row(RectTransform board, int seat, GameObject player, GhostPortrait ghost, Vector2 spot, bool compact)
        {
            Seat = seat;
            Player = player;
            this.ghost = ghost;

            float x = 0f;
            rect = Box(board, "Row " + (seat + 1), new Vector2(compact ? COMPACT_ROW_WIDTH : ROW_WIDTH, ROW_HEIGHT), Color.white);
            rect.GetComponent<Image>().sprite = Banner(compact);
            fade = rect.gameObject.AddComponent<CanvasGroup>();
            fade.interactable = fade.blocksRaycasts = false;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = spot;

            // The picture, in a thick black frame filling the row's height
            RectTransform frame = Box(rect, "Picture Frame", new Vector2(ROW_HEIGHT, ROW_HEIGHT), INK);
            frame.anchoredPosition = new Vector2(x, 0f);
            pictureFrame = frame.GetComponent<Image>();
            picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            Fill(picture.rectTransform, frame, BORDER);
            picture.raycastTarget = false;
            seatBadge = Badge(frame, "Seat Badge", Colourblind.BadgeOf(seat), (seat + 1).ToString(), SEAT_BADGE,
                new Vector2(ROW_HEIGHT - SEAT_BADGE * 0.75f, -(ROW_HEIGHT - SEAT_BADGE * 0.75f)),
                Colourblind.ShapeOf(seat) == Colourblind.Shape.Triangle ? -SEAT_BADGE * 0.08f : 0f);
            // Eliminated (Hot Potato): a red X over the picture, as solid as ever while the row fades
            RectTransform cross = Box(frame, "Out Cross", new Vector2(ROW_HEIGHT, ROW_HEIGHT) * 1.1f, Color.white);
            cross.pivot = new Vector2(0.5f, 0.5f);
            cross.anchoredPosition = new Vector2(ROW_HEIGHT / 2f, -ROW_HEIGHT / 2f);
            cross.GetComponent<Image>().sprite = ModeIcons.Cross();
            cross.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            outCross = cross.gameObject;
            outCross.SetActive(false);
            // The rematch winner's crown, tilted on the picture's top-left corner, as solid as ever too
            RectTransform crownRect = Box(frame, "Winner Crown", new Vector2(CROWN, CROWN), Color.white);
            crownRect.pivot = new Vector2(0.5f, 0.5f);
            crownRect.anchoredPosition = new Vector2(CROWN * 0.22f, CROWN * 0.12f);
            crownRect.localRotation = Quaternion.Euler(0f, 0f, CROWN_TILT);
            Image crownImage = crownRect.GetComponent<Image>();
            crownImage.sprite = RematchCrown.Art();
            crownImage.preserveAspect = true;
            crownRect.gameObject.AddComponent<CanvasGroup>().ignoreParentGroups = true;
            crown = crownRect.gameObject;
            crown.SetActive(false);
            x += ROW_HEIGHT + PAD;

            if (!compact)
            {
                nameText = Label(rect, "Name", x, NAME_WIDTH, 28f, FontStyles.Bold);
                x += NAME_WIDTH;
            }
            moneyText = Label(rect, "Money", x, MONEY_WIDTH, 26f, FontStyles.Normal);
            // The "+$" of a recent gain: small, in the top of the money column, over the number, so the row doesn't grow
            gainText = Label(rect, "Money Gain", x, MONEY_WIDTH, GAIN_SIZE, FontStyles.Bold);
            gainText.rectTransform.sizeDelta = new Vector2(MONEY_WIDTH - PAD, GAIN_HEIGHT);
            gainText.rectTransform.anchoredPosition = new Vector2(x, ROW_HEIGHT / 2f - GAIN_HEIGHT / 2f - GAIN_TOP);
            gainText.enableWordWrapping = false;
            gainText.color = GameCard.GOLD;
            gainText.outlineWidth = 0.3f;
            gainText.outlineColor = INK;
            gainText.text = "";
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
                stolenBadges[i] = Badge(rect, "Stolen Badge " + (i + 1), Colourblind.AlarmBadge(), "!", STOLEN_BADGE,
                    new Vector2(x + PACKAGE - STOLEN_BADGE * 0.5f, y + STOLEN_BADGE * 0.3f), 0f);
                // What a stolen package is worth against its price, on its bottom right corner
                stamps[i] = Label(rect, "Worth Stamp " + (i + 1), x + PACKAGE - STAMP_WIDTH * 0.7f, STAMP_WIDTH + PAD, STAMP_SIZE, FontStyles.Bold);
                stamps[i].rectTransform.anchoredPosition += new Vector2(0f, -PACKAGE * 0.3f);
                stamps[i].alignment = TextAlignmentOptions.Midline;
                stamps[i].color = Color.white;
                stamps[i].outlineWidth = 0.3f;
                stamps[i].outlineColor = INK;
                stamps[i].text = "";
                x += PACKAGE_SPACING;
            }
        }

        /// <summary>One frame of the row: its texts, picture and packages. The clock times the "+$" and the count-up</summary>
        public void Show(string name, int money, OrderHandler handler, Texture steamPicture, int flameFrame, float now)
        {
            if (nameText != null && name != shownName)
            {
                shownName = name;
                nameText.text = name;
            }
            // A row can come up a moment before the match's rules do: the mode counts as well as the money
            GameMode mode = HouseRules.Playing.Mode;
            moneyShow.See(money, now, GainFeedbackRules.BoardShowsGains(mode));
            int moneyNow = moneyShow.Shown(now);
            if (moneyNow != shownMoney || mode != shownMode)
            {
                shownMoney = moneyNow;
                shownMode = mode;
                moneyText.text = ScoreText(mode, moneyNow);
            }
            ShowGain(moneyShow.GainText(now), GainFeedbackRules.PopAlpha(now - moneyShow.GainedAt));
            ShowModeState(ModeMatch.Current);

            if (seatBadge.activeSelf != Colourblind.On)
                seatBadge.SetActive(Colourblind.On);
            if (crown.activeSelf != RematchCrown.Wears(Seat))
                crown.SetActive(RematchCrown.Wears(Seat));

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

            // What a timed mode has them holding goes where the packages would
            ModeMatch match = ModeMatch.Current;
            if (match != null && match.CrownSeat == Seat)
                ShowHeld(ModeIcons.Crown(), null);
            else if (match != null && match.PotatoSeat == Seat)
                ShowHeld(CursedIcon(), PurpleFlame.PinkFrames[flameFrame % PurpleFlame.PinkFrames.Length]);
        }

        /// <summary>The "+$" over the number: its text and how solid it is, each written only when it changes</summary>
        void ShowGain(string text, float alpha)
        {
            if (text != shownGain)
            {
                shownGain = text;
                gainText.text = text;
            }
            if (text.Length > 0 && !Mathf.Approximately(alpha, shownGainAlpha))
            {
                shownGainAlpha = alpha;
                gainText.alpha = alpha;
            }
        }

        // The large package's icon, from any order in the scene
        static Sprite cursedIcon;
        static Sprite CursedIcon()
        {
            if (cursedIcon == null)
            {
                Order order = FindObjectOfType<Order>(true);
                cursedIcon = order != null ? order.IconOf(2) : null;
            }
            return cursedIcon;
        }

        void ShowHeld(Sprite icon, Sprite flame)
        {
            icons[0].sprite = icon;
            icons[0].enabled = icon != null;
            flames[0].enabled = flame != null;
            if (flame != null)
                flames[0].sprite = flame;
        }

        // Who's infected or out, on this row
        void ShowModeState(ModeMatch match)
        {
            bool infected = match != null && match.IsInfected(Seat);
            bool eliminated = match != null && match.IsOut(Seat);
            bool infection = match != null && match.Mode == GameMode.Infection;

            FontStyles style = infection && !infected ? FontStyles.Normal : FontStyles.Bold;
            if (eliminated)
                style |= FontStyles.Strikethrough;
            if (nameText != null)
            {
                if (nameText.fontStyle != style)
                    nameText.fontStyle = style;
                Color nameColour = infected ? ModeIcons.RED : INK;
                if (nameText.color != nameColour)
                    nameText.color = nameColour;
            }
            Color frameColour = infected ? ModeIcons.RED : INK;
            if (pictureFrame.color != frameColour)
                pictureFrame.color = frameColour;
            Color tint = infected ? INFECTED_TINT : Color.white;
            if (picture.color != tint)
                picture.color = tint;
            if (outCross.activeSelf != eliminated)
                outCross.SetActive(eliminated);
            float alpha = eliminated ? OUT_FADE : 1f;
            if (fade.alpha != alpha)
                fade.alpha = alpha;
        }

        void ShowPackage(int slot, Order order, int flameFrame)
        {
            Sprite icon = order != null ? order.Icon : null;
            if (icons[slot].sprite != icon)
                icons[slot].sprite = icon;
            icons[slot].enabled = icon != null;

            bool burning = order != null && order.WorthMoreForSteals;
            flames[slot].enabled = burning;
            if (stolenBadges[slot].activeSelf != (burning && Colourblind.On))
                stolenBadges[slot].SetActive(burning && Colourblind.On);
            string stamp = order != null ? StealFeedbackRules.StampText(order.Value, order.IsLode, order.Steals) : "";
            if (stamp != shownStamps[slot])
            {
                shownStamps[slot] = stamp;
                stamps[slot].text = stamp;
            }
            if (burning)
            {
                Sprite[] frames = PurpleFlame.Frames;
                flames[slot].sprite = frames[(flameFrame + slot * 5) % frames.Length];
            }
        }

        /// <summary>A step of the row's slide and grow towards its spot (its top left) and size</summary>
        public void MoveTowards(Vector2 spot, float scale, float step)
        {
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, spot, step);
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

        // A colourblind badge: a shape with its mark on, centred at a point from its parent's top left, hidden until shown
        static GameObject Badge(RectTransform parent, string name, Sprite shape, string mark, float size, Vector2 centre, float markDrop)
        {
            RectTransform badge = Box(parent, name, Vector2.one * size, Color.white);
            badge.GetComponent<Image>().sprite = shape;
            badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = centre;
            TMP_Text text = ResolutionRow.Text(badge, "Mark", null, size * 0.6f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0f, markDrop), size);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = Vector2.one * size;
            text.text = mark;
            text.color = INK;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.enableAutoSizing = true; // never past its shape, whatever the text size setting
            text.fontSizeMin = 6f;
            text.fontSizeMax = size * 0.6f;
            badge.gameObject.SetActive(false);
            return badge.gameObject;
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

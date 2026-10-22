using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The Dispatch board: online player select shown as four tickets (one per seat), a ready counter and, once everyone
/// is ready, a banner over the countdown. Shows only while online in player select, and through a lobby switch's moment
/// offline (Matchmaker.Switching); Local Play's player select is untouched. The scene wires the fields. See
/// docs/online.md
/// </summary>
public class DispatchBoard : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] GameObject backdrop; // optional: covers what's behind the board until the room's scene is up
    [SerializeField] GameObject roomCamera; // optional: the camera on the Dispatch room, on only while the board is up
    [SerializeField] TicketView[] tickets; // by seat
    [SerializeField] TMP_Text readyLine;
    [SerializeField] GameObject banner;
    [SerializeField] RectTransform[] avatarFrames; // by seat: each player's Steam profile picture, under their podium
    [SerializeField] RawImage[] avatarPictures;    // by seat: the picture inside each frame
    [SerializeField] GameObject customizePanel;    // X's pop-up: the player's colour and hat sliders, over the board
    [SerializeField] RawImage customizePicture;    // inside it, the player's own preview (Player N Cam's picture)
    [SerializeField] TMP_Text matchmakingLine; // optional: searching, open to anyone (with the seconds to a start), friends only
    [SerializeField] TMP_Text fillPrompt; // optional: the right shoulder's label, what pressing it does (host only)
    [Tooltip("Each ticket sits over its seat's anchor on the room's board (DispatchRoom, its own scene) while the room is up")]
    [SerializeField] bool ticketsFollowRoom = true;

    /// <summary>How much of the gap between two seats' anchors a ticket's width takes</summary>
    public const float TICKET_FILL = 0.8f;

    /// <summary>How far below its anchor (canvas units) a profile picture's top edge hangs</summary>
    public const float AVATAR_DROP = 14f;

    /// <summary>Whether the board is up (OnlinePlay hides its hint bar then)</summary>
    public static bool Showing { get; private set; }

    /// <summary>How many times the matchmaking line has been rebuilt (tests: an unchanged second builds nothing)</summary>
    internal int StatusBuilds { get; private set; }

    bool showing;
    PlayerSelectCanvas hooked;
    readonly TicketState[] buffer = new TicketState[Constants.MAX_PLAYERS]; // the tickets as they are now, rebuilt each frame
    readonly TicketState[] shown = new TicketState[Constants.MAX_PLAYERS];  // the tickets as last applied
    bool applied;
    string statusShown; // the matchmaking line as last set
    bool statusKeyed; // the inputs below have been applied since the board came up
    bool statusFilling, statusSearching; // the inputs the matchmaking line was last built from
    int statusSeconds;
    string fillPromptShown; // the Fill prompt as last set (null = hidden)
    bool fillPromptSet; // fillPromptShown has been applied since the board came up
    DispatchRoomLoader roomLoader;
    RectTransform[] ticketRects;

    /// <summary>How the room's scene loads (Unity's, additive; tests set a fake before the board first shows)</summary>
    internal IRoomScenes RoomScenes { get; set; }

    DispatchRoomLoader RoomLoader
    {
        get
        {
            if (roomLoader == null)
                roomLoader = new DispatchRoomLoader(RoomScenes ?? new UnityRoomScenes(), UnityRoomScenes.Leaving);
            return roomLoader;
        }
    }

    void Start()
    {
        Hook();
    }

    void OnDestroy()
    {
        if (hooked != null)
        {
            hooked.CountdownBegan -= OnCountdownBegan;
            hooked.CountdownStopped -= OnCountdownStopped;
        }
        if (showing)
            Showing = false;
    }

    void Update()
    {
        if (hooked == null)
            Hook();

        CheckShowing();
        if (!showing)
            return;

        OnlinePlay play = OnlinePlay.Instance;
        bool switching = play != null && play.Match != null && play.Match.Switching;
        DispatchRoom room = DispatchRoom.Current;
        ShowBackdrop(room != null && room.RoomCamera != null && room.RoomCamera.enabled);

        Gather();
        Apply(buffer);
        ShowAvatars(buffer, SteamAvatars.Get);
        FollowRoom();
        ShowCustomizingView();

        // Between sessions in a lobby switch the prompt and line stay as they were: the player's Find still shows it's
        // searching, and a quiet search again is never shown
        if (!switching)
        {
            bool host = play != null && play.Lobby != null && play.Lobby.IsHost;
            ShowFillPrompt(host, host && play.Lobby.Filling);
            ShowMatchmaking();
        }
        else if (play.Match.Searching)
            ShowMatchmaking();
    }

    // Again after every Update: Multiplayer picked this frame shows the board before the frame is drawn
    void LateUpdate()
    {
        CheckShowing();
    }

    void CheckShowing()
    {
        GameManager game = GameManager.Instance;
        OnlinePlay play = OnlinePlay.Instance;
        bool switching = play != null && play.Match != null && play.Match.Switching;
        bool getting = play != null && play.GettingOnline;
        bool show = ShowsFor(GameAuthority.IsOnline, switching, getting, game == null ? GameState.Default : game.MainState);
        if (show != showing)
            SetShowing(show);
    }

    /// <summary>
    /// Whether the board is up: in player select, online, while Multiplayer's lobby is being made (OnlinePlay.GettingOnline),
    /// or between sessions in a lobby switch (Matchmaker.Switching, which lasts until B's lobby hosts again), so Local
    /// Play's player select never shows through for those moments
    /// </summary>
    public static bool ShowsFor(bool online, bool switching, bool gettingOnline, GameState state)
    {
        return (online || switching || gettingOnline) && state == GameState.PlayerSelect;
    }

    /// <summary>The backdrop covers what's behind the board until the room's scene (and its camera) is up</summary>
    public void ShowBackdrop(bool roomUp)
    {
        if (backdrop != null && backdrop.activeSelf == roomUp)
            backdrop.SetActive(!roomUp);
    }

    /// <summary>
    /// The Fill prompt (fillPrompt, optional): on the host, what pressing the right shoulder does (MatchRules.FillPrompt);
    /// hidden on a client, whose press does nothing. Set only when it changes
    /// </summary>
    internal void ShowFillPrompt(bool host, bool filling)
    {
        if (fillPrompt == null)
            return;

        string label = host ? MatchRules.FillPrompt(filling) : null;
        if (fillPromptSet && label == fillPromptShown)
            return;

        fillPromptSet = true;
        fillPromptShown = label;
        if (label != null)
            fillPrompt.text = label;
        fillPrompt.gameObject.SetActive(label != null);
    }

    // The matchmaking line, set only when it changes. A client can't see whether the host's lobby is open, so it shows the
    // host's countdown when there is one
    void ShowMatchmaking()
    {
        if (matchmakingLine == null)
            return;

        OnlinePlay play = OnlinePlay.Instance;
        OnlineGame game = OnlineGame.Playing;
        bool host = play != null && play.Lobby != null && play.Lobby.IsHost;
        bool filling = host ? play.Lobby.Filling : game != null && game.AutoStartSet;
        bool searching = play != null && play.Match != null && play.Match.Searching;
        float remaining = game == null ? 0 : game.AutoStartRemaining;
        ShowMatchmaking(filling, searching, remaining);
    }

    /// <summary>
    /// Sets the matchmaking line from its inputs. The line is rebuilt (MatchRules.StatusLine allocates) only when filling,
    /// searching or the whole seconds shown (the ceiling of the time left) change, not every frame of a countdown
    /// </summary>
    internal void ShowMatchmaking(bool filling, bool searching, float remaining)
    {
        if (matchmakingLine == null)
            return;

        int seconds = remaining > 0 ? (int)System.Math.Ceiling(remaining) : 0;
        if (statusKeyed && filling == statusFilling && searching == statusSearching && seconds == statusSeconds)
            return;

        statusKeyed = true;
        statusFilling = filling;
        statusSearching = searching;
        statusSeconds = seconds;
        StatusBuilds++;
        string line = MatchRules.StatusLine(filling, searching, remaining);
        if (line == statusShown)
            return;

        statusShown = line;
        matchmakingLine.text = line;
    }

    // Player select's canvas comes and goes with its scene
    void Hook()
    {
        PlayerSelectCanvas canvas = PlayerSelectCanvas.Instance;
        if (canvas == null)
            return;

        hooked = canvas;
        canvas.CountdownBegan += OnCountdownBegan;
        canvas.CountdownStopped += OnCountdownStopped;
    }

    void SetShowing(bool on)
    {
        showing = on;
        Showing = on;
        applied = false;
        if (on)
            System.Array.Clear(shown, 0, shown.Length); // a re-show flashes like the first
        if (root != null)
            root.SetActive(on);
        if (roomCamera != null)
            roomCamera.SetActive(on);

        // The room's scene comes and goes with the board, and its camera with it when the scene was already open
        RoomLoader.SetWanted(on);
        if (DispatchRoom.Current != null)
            DispatchRoom.Current.ShowCamera(on);

        if (PlayerSelectCanvas.Instance != null)
        {
            PlayerSelectCanvas.Instance.SetJoinPromptsHidden(on);
            PlayerSelectCanvas.Instance.SetPlayerViews(on, -1);
            PlayerSelectCanvas.Instance.SetDispatchHidden(on);
        }

        if (!on)
        {
            ShowCustomize(null, -1);
            SetBanner(false);
            statusShown = null;
            statusKeyed = false;
            // Hidden with the board even when the scene wires it outside root; set again once the board is up
            fillPromptSet = false;
            if (fillPrompt != null)
                fillPrompt.gameObject.SetActive(false);
        }

        // The sliders follow Dispatch's rule while it is up, and come back when it is down
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        foreach (PlayerSlot slot in players.Roster.Players)
        {
            PlayerUIHandler handler = slot.Input == null ? null : slot.Input.GetComponent<PlayerUIHandler>();
            if (handler != null && handler.menuInteractions != null)
                handler.menuInteractions.RefreshDispatchSliders();
        }
    }

    // The online players' tickets into the buffer (the rules of DispatchRules.Tickets, without its allocations)
    void Gather()
    {
        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = new TicketState { Seat = i };

        OnlineGame game = OnlineGame.Playing;
        if (game == null)
            return;

        IReadOnlyList<OnlinePlayer> players = game.Players;
        for (int i = 0; i < players.Count; i++)
        {
            OnlinePlayer p = players[i];
            if (p.Seat < 0 || p.Seat >= buffer.Length)
                continue;

            buffer[p.Seat] = new TicketState
            {
                Seat = p.Seat,
                Taken = true,
                Name = PlayerNames.Shown(p.Name, p.Seat),
                Colour = p.Colour,
                Hat = p.Hat,
                Ready = p.Ready,
                Mine = p.IsOwner,
                SteamId = p.SteamId,
            };
        }
    }

    /// <summary>
    /// Shows the tickets and the ready counter. A ticket that changed since the last call flashes (a seat emptying
    /// doesn't). Nothing changed: nothing is touched
    /// </summary>
    public void Apply(TicketState[] states)
    {
        CustomizationSelector choices = OnlineGame.Playing == null ? null : OnlineGame.Playing.Choices;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        bool changed = !applied;

        for (int i = 0; i < tickets.Length && i < states.Length; i++)
        {
            TicketState s = states[i];
            if (applied && s == shown[i])
                continue;

            changed = true;
            if (tickets[i] == null)
                continue;

            CompanyInformation company = players == null ? null : players.CompanyForSlot(i);
            PlayerColorInformationSO colour = choices == null ? null : OnlineGame.Pick(choices.Colours, s.Colour);
            PlayerHatInformationSO hat = choices == null ? null : OnlineGame.Pick(choices.Hats, s.Hat);
            tickets[i].Show(s, company, colour, hat);

            if (DispatchRules.Flashes(shown[i], s))
                tickets[i].Flash();
        }

        if (!changed)
            return;

        for (int i = 0; i < shown.Length && i < states.Length; i++)
            shown[i] = states[i];
        applied = true;

        if (readyLine != null)
            readyLine.text = DispatchRules.ReadyLine(states);
    }

    // Each ticket over its seat's anchor on the room's board, once the room is loaded
    void FollowRoom()
    {
        DispatchRoom room = DispatchRoom.Current;
        if (!ticketsFollowRoom || room == null || room.RoomCamera == null || tickets == null)
            return;

        if (ticketRects == null)
        {
            ticketRects = new RectTransform[tickets.Length];
            for (int i = 0; i < tickets.Length; i++)
                ticketRects[i] = tickets[i] == null ? null : tickets[i].transform as RectTransform;
        }
        PlaceTickets(room.RoomCamera, room.TicketAnchors, ticketRects);
        if (avatarFrames != null)
            PlaceAvatars(room.RoomCamera, room.AvatarAnchors, avatarFrames);
    }

    /// <summary>
    /// Each seated player's profile picture, once Steam has it (a free seat, a player without Steam, or a picture still
    /// loading shows none)
    /// </summary>
    internal void ShowAvatars(TicketState[] states, System.Func<ulong, Texture> picture)
    {
        if (avatarFrames == null || avatarPictures == null)
            return;

        for (int i = 0; i < avatarFrames.Length && i < avatarPictures.Length && i < states.Length; i++)
        {
            if (avatarFrames[i] == null || avatarPictures[i] == null)
                continue;

            Texture shown = states[i].Taken && states[i].SteamId != 0 ? picture(states[i].SteamId) : null;
            if (avatarPictures[i].texture != shown)
                avatarPictures[i].texture = shown;
            if (avatarFrames[i].gameObject.activeSelf != (shown != null))
                avatarFrames[i].gameObject.SetActive(shown != null);
        }
    }

    /// <summary>
    /// Hangs each picture frame (by its top edge) just below its anchor as the camera sees it, centred under it. An anchor
    /// that's missing or behind the camera leaves its frame where it is
    /// </summary>
    internal void PlaceAvatars(Camera cam, Transform[] anchors, RectTransform[] frames)
    {
        if (anchors == null)
            return;

        for (int i = 0; i < frames.Length && i < anchors.Length; i++)
        {
            RectTransform frame = frames[i];
            RectTransform area = frame == null ? null : frame.parent as RectTransform;
            if (area == null || anchors[i] == null)
                continue;

            Vector3 viewport = cam.WorldToViewportPoint(anchors[i].position);
            if (!DispatchRules.InView(viewport))
                continue;

            frame.pivot = new Vector2(0.5f, 1);
            frame.anchoredPosition = DispatchRules.ViewportToCanvas(viewport, area.rect.size) - new Vector2(0, AVATAR_DROP);
        }
    }

    /// <summary>
    /// Puts each ticket (centre-anchored in its parent) over its anchor as the camera sees it, sized to TICKET_FILL of the
    /// gap between neighbouring anchors. An anchor that's missing or behind the camera leaves its ticket where it is
    /// </summary>
    internal void PlaceTickets(Camera cam, Transform[] anchors, RectTransform[] rects)
    {
        if (anchors == null)
            return;

        float gap = float.MaxValue;
        bool hasLast = false;
        float lastX = 0;
        for (int i = 0; i < rects.Length && i < anchors.Length; i++)
        {
            RectTransform rect = rects[i];
            RectTransform area = rect == null ? null : rect.parent as RectTransform;
            if (area == null || anchors[i] == null)
                continue;

            Vector3 viewport = cam.WorldToViewportPoint(anchors[i].position);
            if (!DispatchRules.InView(viewport))
                continue;

            rect.anchoredPosition = DispatchRules.ViewportToCanvas(viewport, area.rect.size);
            if (hasLast)
                gap = Mathf.Min(gap, Mathf.Abs(rect.anchoredPosition.x - lastX));
            lastX = rect.anchoredPosition.x;
            hasLast = true;
        }

        if (gap == float.MaxValue || gap <= 0)
            return;
        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.rect.width > 0)
                rect.localScale = Vector3.one * (gap * TICKET_FILL / rect.rect.width);
        }
    }

    /// <summary>X's pop-up: open on a seat's preview, cropped to its ghost and sliders (null: closed)</summary>
    public void ShowCustomize(Texture preview, int seat)
    {
        if (customizePicture != null && customizePicture.texture != preview)
            customizePicture.texture = preview;
        if (customizePicture != null && preview != null)
            customizePicture.uvRect = DispatchRules.CustomizeCrop(seat);
        if (customizePanel != null && customizePanel.activeSelf != (preview != null))
            customizePanel.SetActive(preview != null);
    }

    // A player here with the sliders open (X): their preview pops up over the board (the quarter-screen panels stay hidden,
    // the board would cover them)
    void ShowCustomizingView()
    {
        PlayerSelectCanvas canvas = PlayerSelectCanvas.Instance;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (canvas == null || players == null)
            return;

        int seat = -1;
        foreach (PlayerSlot slot in players.Roster.LocalPlayers)
        {
            PlayerUIHandler handler = slot.Input == null ? null : slot.Input.GetComponent<PlayerUIHandler>();
            if (handler != null && handler.menuInteractions != null && handler.menuInteractions.DispatchCustomizing)
            {
                seat = slot.Index;
                break;
            }
        }
        canvas.SetPlayerViews(true, -1);
        ShowCustomize(seat < 0 ? null : canvas.ViewPicture(seat), seat);
    }

    /// <summary>The "all orders dispatched" banner over the countdown: on or off (on twice leaves one)</summary>
    public void SetBanner(bool on)
    {
        if (banner != null && banner.activeSelf != on)
            banner.SetActive(on);
    }

    /// <summary>The countdown to the match began</summary>
    public void OnCountdownBegan()
    {
        SetBanner(true);
    }

    /// <summary>The countdown was called off (someone unreadied, or joined)</summary>
    public void OnCountdownStopped()
    {
        SetBanner(false);
    }
}

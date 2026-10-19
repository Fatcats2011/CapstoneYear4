using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The Dispatch board: online player select shown as four tickets (one per seat), a ready counter and, once everyone
/// is ready, a banner over the countdown. Shows only while online in player select; Local Play's player select is
/// untouched. The scene wires the fields. See docs/online.md
/// </summary>
public class DispatchBoard : MonoBehaviour
{
    [SerializeField] GameObject root;
    [SerializeField] TicketView[] tickets; // by seat
    [SerializeField] TMP_Text readyLine;
    [SerializeField] GameObject banner;
    [SerializeField] TMP_Text matchmakingLine; // optional: searching, open to anyone (with the seconds to a start), friends only

    /// <summary>Whether the board is up (OnlinePlay hides its hint bar then)</summary>
    public static bool Showing { get; private set; }

    bool showing;
    PlayerSelectCanvas hooked;
    readonly TicketState[] buffer = new TicketState[Constants.MAX_PLAYERS]; // the tickets as they are now, rebuilt each frame
    readonly TicketState[] shown = new TicketState[Constants.MAX_PLAYERS];  // the tickets as last applied
    bool applied;
    string statusShown; // the matchmaking line as last set

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

        GameManager game = GameManager.Instance;
        bool show = GameAuthority.IsOnline && game != null && game.MainState == GameState.PlayerSelect;
        if (show != showing)
            SetShowing(show);

        if (!showing)
            return;

        Gather();
        Apply(buffer);
        ShowMatchmaking();
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

        if (PlayerSelectCanvas.Instance != null)
            PlayerSelectCanvas.Instance.SetJoinPromptsHidden(on);

        if (!on)
        {
            SetBanner(false);
            statusShown = null;
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

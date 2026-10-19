using System;

/// <summary>
/// Playing with friends through lobbies (Steam's in the game):
/// - Host: makes a friends-only lobby for four, tags it with the game and build, hosts the session in it, and opens the
///   invite dialog.
/// - Answer: enters a friend's lobby, checks its tags, and joins its owner's session.
/// - The lobby follows the session: it's left whenever the session stops, and a lobby that answers after the player
///   moved on is left at once. The host's lobby is joinable only while the game is in its menus.
/// Plain C# over ILobbyService and ISessionControl, so it's tested without Steam. OnlinePlay runs it. See docs/online.md
/// </summary>
public class OnlineLobby
{
    public const string NO_STEAM = "Start Steam to play online.";
    public const string LOBBY_FAILED = "Steam couldn't open a lobby. Try again.";
    public const string HOST_FAILED = "Couldn't start the online match. Try again.";
    public const string JOIN_FAILED = "Couldn't join that match. It may have ended.";
    public const string INVITE_FROM_STEAM = "Invite friends from your Steam friends list.";

    readonly ILobbyService lobbies;
    readonly ISessionControl session;
    readonly string build;
    readonly Func<double> clock;

    JoinChurn churn;   // this hosting session's reconnect counts (null while not hosting)

    bool creating;   // a lobby is being made to host in
    bool inviteOnCreate = true;   // the lobby being made opens the invite dialog
    bool fillOnCreate;   // the lobby being made opens to anyone
    ulong joining;   // the lobby being entered (0 = none)
    int openSeats = -1;   // the free seats last shown (-1 = none yet)

    /// <summary>The lobby this machine is in (0 = none)</summary>
    public ulong Current { get; private set; }

    public bool IsHost { get; private set; }

    /// <summary>Whether the host's lobby is open to anyone (Fill). Friends only otherwise</summary>
    public bool Filling { get; private set; }

    /// <summary>Fill turned on or off, with the new state</summary>
    public event Action<bool> FillChanged;

    /// <summary>A message for the player. Session ends aren't told here: the online game shows those</summary>
    public event Action<string> Notice;

    /// <summary>
    /// Hosting failed, with the reason (also told through Notice): no Steam, no lobby, or the session wouldn't start. Not
    /// raised when the player moved on before the lobby was made (OnlinePlay takes the player back to the title screen)
    /// </summary>
    public event Action<string> HostFailed;

    /// <param name="build">This build's tag (LobbyRules.BuildTag): the host's lobby carries it, and a friend's lobby must match it</param>
    /// <param name="clock">Seconds, for JoinChurn (null = realtime since startup); tests pass a fake</param>
    public OnlineLobby(ILobbyService lobbies, ISessionControl session, string build, Func<double> clock = null)
    {
        this.lobbies = lobbies;
        this.session = session;
        this.build = build;
        this.clock = clock ?? (() => UnityEngine.Time.realtimeSinceStartupAsDouble);

        lobbies.Created += OnCreated;
        lobbies.Entered += OnEntered;
        session.RoleChanged += OnRoleChanged;
        session.Ended += OnEnded;
    }

    /// <summary>
    /// Stops listening to the lobby service and the session (OnlinePlay replaced this flow with another)
    /// </summary>
    public void Detach()
    {
        lobbies.Created -= OnCreated;
        lobbies.Entered -= OnEntered;
        session.RoleChanged -= OnRoleChanged;
        session.Ended -= OnEnded;
    }

    /// <summary>
    /// Makes a lobby and hosts the session in it, then opens the invite dialog. Nothing while one is being made, or while
    /// this machine is in a lobby or a session already
    /// </summary>
    /// <param name="invite">Whether to open the invite dialog once hosting</param>
    /// <param name="fill">Whether to open the lobby to anyone once hosting (SetFill)</param>
    public void Host(bool invite = true, bool fill = false)
    {
        if (creating || Current != 0 || session.IsRunning)
            return;

        if (!lobbies.Available)
        {
            FailHost(NO_STEAM);
            return;
        }

        // Set first: the lobby may be made before Create returns
        creating = true;
        inviteOnCreate = invite;
        fillOnCreate = fill;
        lobbies.Create(Constants.MAX_PLAYERS);
    }

    /// <summary>
    /// Opens the host's lobby to anyone (on) or back to friends only (off). The only place a lobby is made public.
    /// Nothing on a client or without a lobby
    /// </summary>
    public void SetFill(bool on)
    {
        if (!IsHost || Current == 0)
            return;

        if (on)
        {
            lobbies.SetPublic(Current, true);
            lobbies.SetData(Current, MatchRules.MM_KEY, MatchRules.MM_ON);
            if (openSeats < 0)
                openSeats = Constants.MAX_PLAYERS - 1;
            lobbies.SetData(Current, MatchRules.OPEN_KEY, openSeats.ToString());
        }
        else
        {
            lobbies.SetPublic(Current, false);
            lobbies.SetData(Current, MatchRules.MM_KEY, "");
        }

        if (Filling != on)
        {
            Filling = on;
            FillChanged?.Invoke(on);
        }
    }

    /// <summary>
    /// The free seats (0 to 4) a searching stranger sees on the host's lobby. Host only; written only when it changed
    /// </summary>
    public void ShowOpenSeats(int free)
    {
        if (!IsHost || Current == 0)
            return;

        free = Math.Max(0, Math.Min(Constants.MAX_PLAYERS, free));
        if (free == openSeats)
            return;

        openSeats = free;
        lobbies.SetData(Current, MatchRules.OPEN_KEY, free.ToString());
    }

    /// <summary>
    /// A lobby to join (a friend's invite). busy is why this machine can't join now (LobbyRules.Busy), or null. The
    /// latest request wins
    /// </summary>
    public void Answer(ulong lobby, string busy)
    {
        // The lobby it's in, or already entering (a double "Join Game": a second answer would leave it)
        if (lobby == 0 || lobby == Current || lobby == joining)
            return;

        if (busy != null)
        {
            Tell(busy);
            return;
        }

        // Set first: the lobby may answer before Join returns
        joining = lobby;
        lobbies.Join(lobby);
    }

    /// <summary>
    /// The invite dialog for the lobby this machine is in (nothing without one). Without Steam's overlay the player is
    /// pointed to Steam's friends list
    /// </summary>
    public void Invite()
    {
        if (Current == 0)
            return;

        if (!lobbies.Invite(Current))
            Tell(INVITE_FROM_STEAM);
    }

    /// <summary>
    /// The game's state, every time it changes. A lobby still being made is for player select, and a join is for the
    /// menus: once the player moves on they're dropped. The host's lobby takes friends only in the menus
    /// </summary>
    public void ShowState(GameState state)
    {
        if (state != GameState.PlayerSelect)
            creating = false;
        if (!LobbyRules.InTheMenus(state))
            joining = 0;

        if (IsHost && Current != 0)
            lobbies.SetJoinable(Current, LobbyRules.InTheMenus(state));
    }

    void OnCreated(ulong lobby)
    {
        bool wanted = creating;
        creating = false;

        if (lobby == 0)
        {
            if (wanted)
                FailHost(LOBBY_FAILED);
            return;
        }

        // The player moved on while it was being made
        if (!wanted)
        {
            lobbies.Leave(lobby);
            return;
        }

        lobbies.SetData(lobby, LobbyRules.GAME_KEY, LobbyRules.GAME);
        lobbies.SetData(lobby, LobbyRules.BUILD_KEY, build);
        Current = lobby;
        IsHost = true;
        Filling = false;
        openSeats = -1;
        churn = new JoinChurn(clock);

        if (!session.Host())
        {
            LeaveLobby();
            FailHost(HOST_FAILED);
            return;
        }

        if (fillOnCreate)
            SetFill(true);
        if (inviteOnCreate)
            Invite();
    }

    void OnEntered(ulong lobby, bool entered)
    {
        bool wanted = lobby == joining;
        if (wanted)
            joining = 0;

        if (!entered)
        {
            if (wanted)
                Tell(JOIN_FAILED);
            return;
        }

        // Another request came after it, or the player moved on
        if (!wanted)
        {
            lobbies.Leave(lobby);
            return;
        }

        string refusal = LobbyRules.Refusal(lobbies.GetData(lobby, LobbyRules.GAME_KEY),
            lobbies.GetData(lobby, LobbyRules.BUILD_KEY), build);
        if (refusal != null)
        {
            lobbies.Leave(lobby);
            Tell(refusal);
            return;
        }

        Current = lobby;
        IsHost = false;

        if (!session.Join(lobbies.Owner(lobby)))
        {
            LeaveLobby();
            Tell(JOIN_FAILED);
        }
    }

    void OnRoleChanged(NetworkRole role)
    {
        if (role == NetworkRole.Offline)
            LeaveLobby();
    }

    // A joiner who was never let in gets no Offline, only the reason
    void OnEnded(string reason)
    {
        LeaveLobby();
    }

    void LeaveLobby()
    {
        if (Current == 0)
            return;

        lobbies.Leave(Current);
        Current = 0;
        IsHost = false;
        Filling = false;
    }

    /// <summary>
    /// Whether a Steam user may connect to this machine's session: only while it hosts a lobby, and only that lobby's
    /// members (SteamPeerRules). A stranger who knows the host's Steam ID is closed before Netcode sees them. Called once
    /// per accepted connection, and a Steam ID that keeps reconnecting is refused for the session (JoinChurn)
    /// </summary>
    public bool LetsIn(ulong steamId)
    {
        return IsHost && SteamPeerRules.Accepts(steamId, Current, lobbies) && churn.Allow(steamId);
    }

    void Tell(string message)
    {
        Notice?.Invoke(message);
    }

    void FailHost(string reason)
    {
        Tell(reason);
        HostFailed?.Invoke(reason);
    }
}

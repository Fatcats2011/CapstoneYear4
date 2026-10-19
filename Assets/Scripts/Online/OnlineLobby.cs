using System;

/// <summary>
/// Playing with friends through lobbies (Steam's in the game):
/// - Host: makes a friends-only lobby for four, tags it with the game and build, hosts the session in it, and opens the
///   invite dialog.
/// - Answer: enters a friend's lobby, checks its tags, and joins its owner's session.
/// - Switch (Find, Matchmaker): a host alone in its lobby moves into another open lobby, keeping its own until the new
///   one is entered and its tags pass.
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
    readonly Func<int> otherMachines;

    /// <summary>Why a lobby switch failed</summary>
    public enum SwitchFailure
    {
        /// <summary>The lobby couldn't be entered</summary>
        CouldNotEnter,
        /// <summary>Its tags aren't this game and build, or someone joined this host first</summary>
        Refused,
        /// <summary>Its host turned this machine away: another took the last seat first</summary>
        NoSeat,
        /// <summary>Its host's session couldn't be joined</summary>
        JoinFailed
    }

    JoinChurn churn;   // this hosting session's reconnect counts (null while not hosting)

    bool creating;   // a lobby is being made to host in
    bool inviteOnCreate = true;   // the lobby being made opens the invite dialog
    bool fillOnCreate;   // the lobby being made opens to anyone
    ulong joining;   // the lobby being entered (0 = none)
    int openSeats = -1;   // the free seats last shown (-1 = none yet)
    ulong switchEntering;   // the lobby being entered to switch to (0 = none)
    ulong switchingTo;   // the lobby switched to, until its host lets this machine in (0 = none)

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

    /// <summary>A switch failed, with the lobby and why. Before the old lobby was left it's kept, still hosted</summary>
    public event Action<ulong, SwitchFailure> SwitchFailed;

    /// <summary>A switch is done: the new lobby's host let this machine in</summary>
    public event Action<ulong> Switched;

    // A switch is under way: entering the new lobby, or joining its host
    bool Switching { get { return switchEntering != 0 || switchingTo != 0; } }

    /// <param name="build">This build's tag (LobbyRules.BuildTag): the host's lobby carries it, and a friend's lobby must match it</param>
    /// <param name="clock">Seconds, for JoinChurn (null = realtime since startup); tests pass a fake</param>
    /// <param name="otherMachines">How many machines besides the host's are in the hosted session (null = none)</param>
    public OnlineLobby(ILobbyService lobbies, ISessionControl session, string build, Func<double> clock = null,
        Func<int> otherMachines = null)
    {
        this.lobbies = lobbies;
        this.session = session;
        this.build = build;
        this.clock = clock ?? (() => UnityEngine.Time.realtimeSinceStartupAsDouble);
        this.otherMachines = otherMachines ?? (() => 0);

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
    /// Moves into another lobby (Find): enters it while keeping this host's own, and once its tags pass, leaves the own
    /// lobby and session and joins its owner's. Only for a host alone in its session, or a player with no lobby and no
    /// session (a switch that failed after leaving, trying the next lobby). SwitchFailed says why one failed, Switched
    /// when it's done. False (and nothing happens) when it isn't allowed now
    /// </summary>
    public bool Switch(ulong lobby)
    {
        if (lobby == 0 || lobby == Current || Switching || creating || joining != 0)
            return false;

        bool aloneHosting = IsHost && Current != 0 && otherMachines() == 0;
        bool nowhere = Current == 0 && !session.IsRunning;
        if (!aloneHosting && !nowhere)
            return false;

        // Set first: the lobby may answer before Join returns
        switchEntering = lobby;
        lobbies.Join(lobby);
        return true;
    }

    /// <summary>
    /// Drops a switch whose lobby hasn't answered yet (Find cancelled): it's left when it answers. A switch that has left
    /// the old lobby carries on
    /// </summary>
    public void CancelSwitch()
    {
        switchEntering = 0;
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
        {
            joining = 0;
            switchEntering = 0;
        }

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
        if (lobby == switchEntering)
        {
            switchEntering = 0;
            SwitchEntered(lobby, entered);
            return;
        }

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

    // A switch's lobby answered: the old lobby and session are left only once the new lobby is in and its tags pass
    void SwitchEntered(ulong lobby, bool entered)
    {
        if (!entered)
        {
            lobbies.Leave(lobby);
            FailSwitch(lobby, SwitchFailure.CouldNotEnter);
            return;
        }

        // Another build or game, or a friend joined this host meanwhile (nobody is dragged out)
        string refusal = LobbyRules.Refusal(lobbies.GetData(lobby, LobbyRules.GAME_KEY),
            lobbies.GetData(lobby, LobbyRules.BUILD_KEY), build);
        if (refusal != null || (IsHost && otherMachines() > 0))
        {
            lobbies.Leave(lobby);
            FailSwitch(lobby, SwitchFailure.Refused);
            return;
        }

        // Set first: the old session going offline leaves only the old lobby
        switchingTo = lobby;
        LeaveLobby();
        session.Leave();
        Current = lobby;
        IsHost = false;

        if (!session.Join(lobbies.Owner(lobby)))
            EndSwitch(SwitchFailure.JoinFailed);
    }

    // The switched-to lobby's session didn't work out: its lobby is left too
    void EndSwitch(SwitchFailure why)
    {
        ulong lobby = switchingTo;
        switchingTo = 0;
        LeaveLobby();
        FailSwitch(lobby, why);
    }

    void FailSwitch(ulong lobby, SwitchFailure why)
    {
        SwitchFailed?.Invoke(lobby, why);
    }

    void OnRoleChanged(NetworkRole role)
    {
        if (switchingTo != 0 && Current == switchingTo)
        {
            if (role == NetworkRole.Client)
            {
                ulong lobby = switchingTo;
                switchingTo = 0;
                Switched?.Invoke(lobby);
            }
            else if (role == NetworkRole.Offline)
                EndSwitch(SwitchFailure.JoinFailed);
            return;
        }

        if (role == NetworkRole.Offline)
            LeaveLobby();
    }

    // A joiner who was never let in gets no Offline, only the reason. The host's "full" while switching means another
    // took the last seat first
    void OnEnded(string reason)
    {
        if (switchingTo != 0 && Current == switchingTo)
        {
            EndSwitch(reason == JoinRules.FULL ? SwitchFailure.NoSeat : SwitchFailure.JoinFailed);
            return;
        }

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

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

    bool creating;   // a lobby is being made to host in
    ulong joining;   // the lobby being entered (0 = none)

    /// <summary>The lobby this machine is in (0 = none)</summary>
    public ulong Current { get; private set; }

    public bool IsHost { get; private set; }

    /// <summary>A message for the player. Session ends aren't told here: the online game shows those</summary>
    public event Action<string> Notice;

    /// <param name="build">This build's tag (LobbyRules.BuildTag): the host's lobby carries it, and a friend's lobby must match it</param>
    public OnlineLobby(ILobbyService lobbies, ISessionControl session, string build)
    {
        this.lobbies = lobbies;
        this.session = session;
        this.build = build;

        lobbies.Created += OnCreated;
        lobbies.Entered += OnEntered;
        session.RoleChanged += OnRoleChanged;
        session.Ended += OnEnded;
    }

    /// <summary>
    /// Makes a lobby and hosts the session in it, then opens the invite dialog. Nothing while one is being made, or while
    /// this machine is in a lobby or a session already
    /// </summary>
    public void Host()
    {
        if (creating || Current != 0 || session.IsRunning)
            return;

        if (!lobbies.Available)
        {
            Tell(NO_STEAM);
            return;
        }

        // Set first: the lobby may be made before Create returns
        creating = true;
        lobbies.Create(Constants.MAX_PLAYERS);
    }

    /// <summary>
    /// A lobby to join (a friend's invite). busy is why this machine can't join now (LobbyRules.Busy), or null. The
    /// latest request wins
    /// </summary>
    public void Answer(ulong lobby, string busy)
    {
        if (lobby == 0 || lobby == Current)
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
                Tell(LOBBY_FAILED);
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

        if (!session.Host())
        {
            LeaveLobby();
            Tell(HOST_FAILED);
            return;
        }

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
    }

    /// <summary>
    /// Whether a Steam user may connect to this machine's session: only while it hosts a lobby, and only that lobby's
    /// members (SteamPeerRules). A stranger who knows the host's Steam ID is closed before Netcode sees them
    /// </summary>
    public bool LetsIn(ulong steamId)
    {
        return IsHost && SteamPeerRules.Accepts(steamId, Current, lobbies);
    }

    void Tell(string message)
    {
        Notice?.Invoke(message);
    }
}

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playing online from the game's menus, with Steam friends:
/// - Online Play on the title screen goes to player select and hosts a lobby, opening Steam's invite window
///   (OnlineLobby). If it can't get online it says why and goes back to the title screen.
/// - Y in player select, online, reopens the invite window; in Local Play it does nothing. A title screen without the
///   Online Play entry (before its art is in the scene) keeps the old way: Y in player select goes online.
/// - A friend's invite (Steam's join request, or the lobby Steam starts the game with) is answered once the title
///   screen is up.
/// - The lobby follows the game's state, and a line along the top of player select says what Y and B do (LobbyPrompt).
/// - Messages show in the controller hint bar (ControllerPrompts).
/// Owns the Steam session, which OnlineGame plays; the editor's Online menu keeps its own. Made at launch.
/// See docs/online.md
/// </summary>
public class OnlinePlay : MonoBehaviour, ISessionControl
{
    public static string HINT_PLAY_ONLINE => Loc.Get("online.hint.playonline");
    public static string HINT_IN_LOBBY => Loc.Get("online.hint.inlobby");
    public static string HINT_LEAVE => Loc.Get("online.hint.leave");

    public static OnlinePlay Instance { get; internal set; }

    /// <summary>What Y in player select does</summary>
    public enum YPress { Nothing, Host, Invite }

    ILobbyService lobbies;
    OnlineSession session;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    string directAddress; // tests and editor tools: sessions by IP address instead of over Steam
    ushort directPort;
    ushort directJoinPort; // the port joined (0 = directPort): tests with two hosts on this computer
#endif
    ulong requested;      // a lobby to join once the title is up (0 = none)
    bool joinWaiting;     // a join asked for as the last session stopped (a lobby switch): it starts once it has
    ulong joinWaitingFor; // that join's host
    bool leavingForLobby; // the lobby flow is leaving the session itself (ISessionControl.Leave)
    bool startingOnline;  // the host attempt came from Online Play: a failure goes back to the title screen

    /// <summary>
    /// Multiplayer was picked and the lobby is still being made: Dispatch shows already, so Local Play's player select
    /// never flashes up first. Ends once online, on a failure, or when the player leaves player select
    /// </summary>
    public bool GettingOnline { get { return startingOnline; } }
    bool stateShown;
    GameState shownState;
    string shownHint = "";

    Action<NetworkRole> roleChanged;
    Action<string> ended;

    /// <summary>The lobby flow: Steam's lobbies, or the ones UseDirect gave</summary>
    public OnlineLobby Lobby { get; private set; }

    /// <summary>
    /// Find in Dispatch: moving into another open lobby (over the same lobbies as Lobby). Tests may set their own
    /// </summary>
    public Matchmaker Match { get; internal set; }

    /// <summary>
    /// The session this machine plays online (made on first use; OnlineGame plays it)
    /// </summary>
    public OnlineSession Session
    {
        get
        {
            if (session == null)
            {
                session = OnlineSession.Create(Application.version);
                OnlineGame.Attach(session);
                session.RoleChanged += ForwardRole;
                session.Ended += ForwardEnd;
                session.Left += OnLeft;
                session.AcceptsPeer = LetsIn;
            }
            return session;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    ///<summary>
    /// Made once the first scene has loaded (Steam started before it), so no scene needs to contain it
    ///</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (Instance != null)
            return;

        GameObject holder = new GameObject(nameof(OnlinePlay));
        DontDestroyOnLoad(holder);
        holder.AddComponent<OnlinePlay>();
    }

    void Awake()
    {
        Instance = this;
        Use(new SteamLobbyService());

        // Accepting an invite can start the game: Steam passes the lobby
        RequestJoin(LobbyRules.LobbyToJoin(Environment.GetCommandLineArgs()));
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    /// <summary>
    /// Tests and editor tools: sessions by IP address (a Steam account can't join itself), with these lobbies
    /// </summary>
    /// <param name="joinPort">The port joins go to (0 = port): a test hosting another session on this computer</param>
    public void UseDirect(ILobbyService lobbyService, string address, ushort port, ushort joinPort = 0)
    {
        directAddress = address;
        directPort = port;
        directJoinPort = joinPort;
        Use(lobbyService);
    }
#endif

    // Steam host: only the lobby's members may connect (OnlineLobby.LetsIn)
    bool LetsIn(ulong steamId)
    {
        return Lobby != null && Lobby.LetsIn(steamId);
    }

    void Use(ILobbyService lobbyService)
    {
        if (lobbies != null)
            lobbies.JoinRequested -= RequestJoin;
        if (Lobby != null)
        {
            // The replaced lobby flow stops hearing the lobby service and the session
            Lobby.Notice -= Tell;
            Lobby.HostFailed -= OnHostFailed;
            Lobby.Detach();
        }
        if (Match != null)
        {
            Match.Notice -= Tell;
            Match.RehostFailed -= BackToTitleAfterFailing;
            Match.Detach();
        }

        lobbies = lobbyService;
        lobbies.JoinRequested += RequestJoin;
        string build = BuildTag();
        Lobby = new OnlineLobby(lobbies, this, build, Clock, OtherMachines);
        Lobby.Notice += Tell;
        Lobby.HostFailed += OnHostFailed;
        Match = new Matchmaker(lobbies, Lobby, build, Clock, OtherMachines, InMenus);
        Match.Notice += Tell;
        Match.RehostFailed += BackToTitleAfterFailing;
        startingOnline = false;
        stateShown = false;
    }

    static double Clock()
    {
        return Time.realtimeSinceStartupAsDouble;
    }

    static bool InMenus()
    {
        GameManager game = GameManager.Instance;
        return game != null && LobbyRules.InTheMenus(game.MainState);
    }

    // Machines besides this host's in the session it hosts (none when it isn't hosting)
    int OtherMachines()
    {
        if (session == null || !session.IsRunning || !session.Network.IsServer)
            return 0;

        return Math.Max(0, session.Network.ConnectedClientsIds.Count - 1);
    }

    /// <summary>
    /// This build's lobby tag: its version and Netcode setup. Friends on another build are told before they join
    /// </summary>
    public static string BuildTag()
    {
        return LobbyRules.BuildTag(Application.version, OnlineSession.NetcodeSetup(OnlinePrefabs.Load()));
    }

    /// <summary>
    /// Online Play on the title screen (MainMenu switches to player select first): hosts a lobby, without opening Steam's
    /// invite window (Y on Dispatch opens it). Every player here plays. A failure goes back to the title screen
    /// </summary>
    public void StartOnline()
    {
        startingOnline = true;
        Lobby.Host(invite: false);
    }

    /// <summary>
    /// Y in player select (YAction)
    /// </summary>
    /// <param name="hasOnlineEntry">Whether the title screen has Online Play (MainMenu.HasOnlineEntry)</param>
    public void PressY(bool hasOnlineEntry)
    {
        // Between sessions in a lobby switch (offline for a moment): Y waits, as Dispatch's other buttons do
        if (Match != null && Match.Switching)
            return;

        switch (YAction(hasOnlineEntry, GameAuthority.IsOnline))
        {
            case YPress.Host:
                // The old title screen's way online: a failure only says why, as it always did
                startingOnline = false;
                Lobby.Host();
                break;
            case YPress.Invite:
                Lobby.Invite();
                break;
        }
    }

    /// <summary>
    /// What Y in player select does: online, it reopens the invite window. Offline it does nothing in Local Play; a title
    /// screen without Online Play keeps the old way, where Y goes online
    /// </summary>
    public static YPress YAction(bool hasOnlineEntry, bool online)
    {
        if (online)
            return YPress.Invite;

        return hasOnlineEntry ? YPress.Nothing : YPress.Host;
    }

    /// <summary>
    /// Whether a failure to get online takes the player back to the title screen: only from player select while still
    /// offline. A player who moved on (Options, say) is left where they are
    /// </summary>
    public static bool BackToTitle(GameState state, bool online)
    {
        return state == GameState.PlayerSelect && !online;
    }

    // Online Play couldn't get online: the reason is in the hint bar (Notice), and player select would be a local one
    void OnHostFailed(string reason)
    {
        bool fromOnlinePlay = startingOnline;
        startingOnline = false;

        if (fromOnlinePlay)
            BackToTitleAfterFailing();
    }

    // Back to the title screen after failing to get online: Online Play, or the player's lobby hosted again after B
    // (Matchmaker.RehostFailed). A player who readied up while the lobby was being made stops being ready first, as B
    // would have them, so no countdown is left to start a match from the title screen
    void BackToTitleAfterFailing()
    {
        GameManager game = GameManager.Instance;
        if (game == null || !BackToTitle(game.MainState, GameAuthority.IsOnline))
            return;

        if (PlayerInstantiate.Instance != null)
            PlayerInstantiate.Instance.UnreadyLocalPlayers();
        game.SetGameState(GameState.Menu);
    }

    /// <summary>
    /// A lobby to join: Steam's request when the player accepts an invite, or the one the game was started with. It's
    /// answered once the title screen is up; the latest request wins
    /// </summary>
    public void RequestJoin(ulong lobby)
    {
        if (lobby != 0)
            requested = lobby;
    }

    /// <summary>
    /// What the line along the top says: in player select, what Y and B do online; elsewhere nothing
    /// </summary>
    /// <param name="online">Whether this machine is online</param>
    /// <param name="inLobby">Whether it's in a lobby (the editor's Online menu plays without one)</param>
    /// <param name="steam">Whether lobbies work here (Steam is running)</param>
    /// <param name="hasOnlineEntry">Whether the title screen has Online Play: then Local Play's player select offers nothing</param>
    /// <param name="dispatch">Whether the Dispatch board is up (online player select, or a lobby switch between sessions):
    /// its tickets carry the prompts</param>
    public static string HintFor(GameState state, bool online, bool inLobby, bool steam, bool hasOnlineEntry, bool dispatch)
    {
        if (state != GameState.PlayerSelect)
            return "";

        if (dispatch)
            return "";

        if (online)
            return inLobby ? HINT_IN_LOBBY : HINT_LEAVE;

        return steam && !hasOnlineEntry ? HINT_PLAY_ONLINE : "";
    }

    void Update()
    {
        // A switch's join, once the session it left has stopped
        if (joinWaiting && !session.IsRunning)
        {
            joinWaiting = false;
            if (!StartJoin(joinWaitingFor))
                ForwardEnd(OnlineLobby.JOIN_FAILED);
        }

        // The title screen is up once the game has a state
        GameManager game = GameManager.Instance;
        if (game == null || game.MainState == GameState.Default)
            return;

        // Left player select while the lobby was being made: no Dispatch for a later Local Play
        if (startingOnline && game.MainState != GameState.PlayerSelect)
            startingOnline = false;

        if (requested != 0)
            AnswerRequest(game);

        if (!stateShown || game.MainState != shownState)
        {
            stateShown = true;
            shownState = game.MainState;
            Lobby.ShowState(shownState);
            // Find is for Dispatch: going anywhere else (B to the title screen) drops it
            if (shownState != GameState.PlayerSelect)
                Match.Cancel();
        }

        // Not before the host's own player is seated: a new lobby would show every seat free
        int seated = Lobby.IsHost ? SeatedCount() : 0;
        if (seated > 0)
            Lobby.ShowOpenSeats(Constants.MAX_PLAYERS - seated);
        Lobby.ShowLocation();

        Match.Tick();

        bool hasOnlineEntry = MainMenu.Instance != null && MainMenu.Instance.HasOnlineEntry;
        string hint = HintFor(game.MainState, GameAuthority.IsOnline, Lobby.Current != 0, lobbies.Available, hasOnlineEntry, DispatchBoard.Showing);
        if (hint != shownHint)
        {
            shownHint = hint;
            // Built only once there's something to say
            if (hint != "" || LobbyPrompt.Exists)
                LobbyPrompt.Instance.Show(hint);
        }
    }

    // Seated players in the online game (the host included), as the Dispatch board counts them
    static int SeatedCount()
    {
        OnlineGame game = OnlineGame.Playing;
        if (game == null)
            return 0;

        int seated = 0;
        IReadOnlyList<OnlinePlayer> players = game.Players;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i].Seat >= 0)
                seated++;
        }
        return seated;
    }

    // A friend's invite: from options or credits it goes through the title screen
    void AnswerRequest(GameManager game)
    {
        if ((game.MainState == GameState.Options || game.MainState == GameState.Credits) && MainMenu.Instance != null)
            MainMenu.Instance.SwapToMainMenu();

        ulong lobby = requested;
        requested = 0;
        Lobby.Answer(lobby, LobbyRules.Busy(game.MainState, GameAuthority.IsOnline));
    }

    void Tell(string message)
    {
        ControllerPrompts.Instance.ShowHint(message, OnlineGame.NOTICE_SECONDS);
    }

    // The lobby flow starts the session through here, so nothing else hosts or joins around it
    bool ISessionControl.IsRunning { get { return joinWaiting || (session != null && session.IsRunning); } }

    bool ISessionControl.Host()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (directAddress != null)
            return Session.HostDirect(directAddress, directPort);
#endif
#if !DISABLESTEAMWORKS
        return Session.HostSteam();
#else
        return false;
#endif
    }

    bool ISessionControl.Join(ulong hostId)
    {
        // The session just left (a lobby switch) stops at the end of the frame: the join starts once it has (Update)
        if (session != null && session.Stopping)
        {
            joinWaiting = true;
            joinWaitingFor = hostId;
            return true;
        }

        return StartJoin(hostId);
    }

    void ISessionControl.Leave()
    {
        joinWaiting = false;
        if (session == null)
            return;

        leavingForLobby = true;
        try
        {
            session.Leave();
        }
        finally
        {
            leavingForLobby = false;
        }
    }

    // The session was left: a join held for it never starts, and a Find under way stops (the player left, B). The lobby
    // flow's own leave (a switch) is part of the Find
    void OnLeft()
    {
        joinWaiting = false;
        if (!leavingForLobby)
            Match.Cancel();
    }

    bool StartJoin(ulong hostId)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (directAddress != null)
            return Session.JoinDirect(directAddress, directJoinPort != 0 ? directJoinPort : directPort);
#endif
#if !DISABLESTEAMWORKS
        return Session.JoinSteam(hostId);
#else
        return false;
#endif
    }

    event Action<NetworkRole> ISessionControl.RoleChanged
    {
        add { roleChanged += value; }
        remove { roleChanged -= value; }
    }

    event Action<string> ISessionControl.Ended
    {
        add { ended += value; }
        remove { ended -= value; }
    }

    void ForwardRole(NetworkRole role)
    {
        if (role != NetworkRole.Offline)
            startingOnline = false;
        roleChanged?.Invoke(role);
    }

    void ForwardEnd(string reason)
    {
        ended?.Invoke(reason);
    }
}

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using UnityEngine;

/// <summary>
/// Playing online from the game's menus, with Steam friends:
/// - Y in player select plays online: it hosts a lobby and invites friends (OnlineLobby), or invites more once online.
/// - A friend's invite (Steam's join request, or the lobby Steam starts the game with) is answered once the title
///   screen is up.
/// - The lobby follows the game's state, and a line along the top of player select says what Y and B do (LobbyPrompt).
/// - Messages show in the controller hint bar (ControllerPrompts).
/// Owns the Steam session, which OnlineGame plays; the editor's Online menu keeps its own. Made at launch.
/// See docs/online.md
/// </summary>
public class OnlinePlay : MonoBehaviour, ISessionControl
{
    public const string HINT_PLAY_ONLINE = "Y / Triangle: play online with Steam friends";
    public const string HINT_IN_LOBBY = "Y / Triangle: invite friends      B / Circle: leave";
    public const string HINT_LEAVE = "B / Circle: leave online";

    public static OnlinePlay Instance { get; private set; }

    ILobbyService lobbies;
    OnlineSession session;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    string directAddress; // tests and editor tools: sessions by IP address instead of over Steam
    ushort directPort;
#endif
    ulong requested;      // a lobby to join once the title is up (0 = none)
    bool stateShown;
    GameState shownState;
    string shownHint = "";

    Action<NetworkRole> roleChanged;
    Action<string> ended;

    /// <summary>The lobby flow: Steam's lobbies, or the ones UseDirect gave</summary>
    public OnlineLobby Lobby { get; private set; }

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
    public void UseDirect(ILobbyService lobbyService, string address, ushort port)
    {
        directAddress = address;
        directPort = port;
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
            Lobby.Detach();
        }

        lobbies = lobbyService;
        lobbies.JoinRequested += RequestJoin;
        Lobby = new OnlineLobby(lobbies, this, BuildTag());
        Lobby.Notice += Tell;
        stateShown = false;
    }

    /// <summary>
    /// This build's lobby tag: its version and Netcode setup. Friends on another build are told before they join
    /// </summary>
    public static string BuildTag()
    {
        return LobbyRules.BuildTag(Application.version, OnlineSession.NetcodeSetup(OnlinePrefabs.Load()));
    }

    /// <summary>
    /// Y in player select: hosts a lobby and invites friends, or invites more once online. Every player here plays
    /// </summary>
    public void PlayOnline()
    {
        if (GameAuthority.IsOnline)
            Lobby.Invite();
        else
            Lobby.Host();
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
    public static string HintFor(GameState state, bool online, bool inLobby, bool steam)
    {
        if (state != GameState.PlayerSelect)
            return "";

        if (online)
            return inLobby ? HINT_IN_LOBBY : HINT_LEAVE;

        return steam ? HINT_PLAY_ONLINE : "";
    }

    void Update()
    {
        // The title screen is up once the game has a state
        GameManager game = GameManager.Instance;
        if (game == null || game.MainState == GameState.Default)
            return;

        if (requested != 0)
            AnswerRequest(game);

        if (!stateShown || game.MainState != shownState)
        {
            stateShown = true;
            shownState = game.MainState;
            Lobby.ShowState(shownState);
        }

        string hint = HintFor(game.MainState, GameAuthority.IsOnline, Lobby.Current != 0, lobbies.Available);
        if (hint != shownHint)
        {
            shownHint = hint;
            // Built only once there's something to say
            if (hint != "" || LobbyPrompt.Exists)
                LobbyPrompt.Instance.Show(hint);
        }
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
    bool ISessionControl.IsRunning { get { return session != null && session.IsRunning; } }

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (directAddress != null)
            return Session.JoinDirect(directAddress, directPort);
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
        roleChanged?.Invoke(role);
    }

    void ForwardEnd(string reason)
    {
        ended?.Invoke(reason);
    }
}

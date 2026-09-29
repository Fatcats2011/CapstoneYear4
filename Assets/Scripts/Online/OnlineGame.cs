using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's side of an online session:
/// - this machine's role (GameAuthority) and the scene flow (loads with the whole session: OnlineSceneFlow);
/// - this machine's player in the seat the host gave them;
/// - other machines' players as scooters in their seats;
/// - every scooter's pose and what it's doing (OnlineDriving);
/// - every order and score (OnlineOrders);
/// - every machine's player finishing the tutorial (OnlineTutorial);
/// - everyone's colour, hat and readiness;
/// - the host's game states;
/// - the host's match clock (MatchClock);
/// - why a session ended, in the controller hint bar.
/// Whatever starts a session for the game adds it: OnlinePlay (Steam) or the editor's Online menu. See docs/online.md
/// </summary>
public class OnlineGame : MonoBehaviour
{
    /// <summary>Why a machine with more than one player can't go online</summary>
    public const string ONE_PLAYER = "Online: online play is one player per machine (until roadmap Task 3.9). Let the other players leave player select first.";

    /// <summary>How long a message about online play shows (ControllerPrompts)</summary>
    public const float NOTICE_SECONDS = 5f;

    static OnlineGame playing; // the game whose session this machine plays online (none offline)

    // What this machine shows of another machine's player
    class RemotePlayer
    {
        public RemoteAvatar Avatar;
        public int Seat;
        public int Colour = -1;
        public int Hat = -1;
        public bool Ready;
    }

    OnlineSession session;
    OnlinePrefabs prefabs;
    OnlineSceneFlow sceneFlow;
    CustomizationSelector choices; // player select's colours and hats, from the local player's prefab
    OnlinePlayer localPlayer;     // this machine's player
    readonly List<OnlinePlayer> waiting = new List<OnlinePlayer>(); // other machines' players, until their seat is free here
    readonly Dictionary<OnlinePlayer, RemotePlayer> remotes = new Dictionary<OnlinePlayer, RemotePlayer>();
    OnlineOrders orders;     // the host's orders and scores, on this machine
    HostQueue hostMessages;  // client: the host's states and order changes, which wait while this machine's new scene comes up

    /// <summary>
    /// Plays the game over a session (adds an OnlineGame to its object)
    /// </summary>
    public static OnlineGame Attach(OnlineSession session)
    {
        OnlineGame game = session.gameObject.AddComponent<OnlineGame>();
        game.Begin(session);
        return game;
    }

    /// <summary>
    /// Leaves the session this machine plays online (B in player select), whichever started it. Nothing offline. The
    /// host leaving ends the match for everyone
    /// </summary>
    public static void LeaveOnline()
    {
        if (playing != null)
            playing.session.Leave();
    }

    /// <summary>
    /// Whether this machine can go online: at most one player (online play is one player per machine until Task 3.9)
    /// </summary>
    public static bool CanGoOnline()
    {
        return PlayerInstantiate.Instance == null || PlayerInstantiate.Instance.Roster.LocalCount <= 1;
    }

    /// <summary>
    /// The state a client shows for the host's: the host's own menus (options, credits) show as the title screen
    /// </summary>
    public static GameState ForClient(GameState hostState)
    {
        return hostState == GameState.Options || hostState == GameState.Credits ? GameState.Menu : hostState;
    }

    /// <summary>
    /// An item of player select's list by the index another machine sent, or null when this build's list has no such
    /// item
    /// </summary>
    public static T Pick<T>(IReadOnlyList<T> list, int index) where T : class
    {
        return list != null && index >= 0 && index < list.Count ? list[index] : null;
    }

    void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        prefabs = OnlinePrefabs.Load();
        sceneFlow = new OnlineSceneFlow(SceneFlow.Current, SceneFlow.Loader);
        hostMessages = new HostQueue(IsChangingScene);
        sceneFlow.SceneChanged += hostMessages.Release;
        choices = prefabs.LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);

        // Every frame, this machine's scooter goes out and the other machines' come in
        gameObject.AddComponent<OnlineDriving>().Begin(session);

        // The host's orders and scores, on every machine
        orders = gameObject.AddComponent<OnlineOrders>();
        orders.Begin(session);

        // The tutorial ends once every machine's player has finished it
        gameObject.AddComponent<OnlineTutorial>().Begin(session);

        session.RoleChanged += OnRoleChanged;
        session.Ended += ShowEnd;
        session.PlayerSpawned += OnPlayerSpawned;
        session.PlayerDespawned += OnPlayerDespawned;
        session.MatchSpawned += OnMatchSpawned;
        if (GameManager.Instance != null)
            GameManager.Instance.StateApplied += OnStateApplied;

        // A session that's running already
        foreach (OnlinePlayer player in session.Players)
            OnPlayerSpawned(player);
        if (session.Match != null)
            OnMatchSpawned(session.Match);
        if (session.Role != NetworkRole.Offline)
            OnRoleChanged(session.Role);
    }

    void OnDestroy()
    {
        if (session != null)
        {
            session.RoleChanged -= OnRoleChanged;
            session.Ended -= ShowEnd;
            session.PlayerSpawned -= OnPlayerSpawned;
            session.PlayerDespawned -= OnPlayerDespawned;
            session.MatchSpawned -= OnMatchSpawned;
            if (session.Match != null)
            {
                session.Match.StateReceived -= FollowHost;
                session.Match.OrderReceived -= FollowHostOrder;
            }
        }
        if (sceneFlow != null)
            sceneFlow.Unlink();
        if (GameManager.Instance != null)
            GameManager.Instance.StateApplied -= OnStateApplied;
        if (playing == this)
            playing = null;
    }

    // This machine's role and scene flow follow the session's
    void OnRoleChanged(NetworkRole role)
    {
        GameAuthority.Role = role;

        if (role != NetworkRole.Offline)
        {
            playing = this;
            SceneFlow.Current = sceneFlow;
            return;
        }

        if (playing == this)
            playing = null;
        SceneFlow.Current = null;
        sceneFlow.Unlink();
        hostMessages.Clear();
        PlayerInstantiate players = PlayerInstantiate.Instance;
        // Player select doesn't count down to an offline match nobody chose: this machine's players unready first, which
        // also stops a countdown that's running. Removing the others then starts none
        if (players != null && GameManager.Instance != null && GameManager.Instance.MainState == GameState.PlayerSelect)
            players.UnreadyLocalPlayers();
        foreach (RemotePlayer remote in remotes.Values)
        {
            if (players != null)
                players.RemoveRemotePlayer(remote.Seat);
        }
        remotes.Clear();
        RecheckTutorial();
        waiting.Clear();
        localPlayer = null;
        if (players != null)
            players.SetOnlineSeat(-1);
    }

    // The session ended by itself (the host left, turned this player away, or couldn't be reached): the player hears why
    void ShowEnd(string reason)
    {
        ControllerPrompts.Instance.ShowHint(reason, NOTICE_SECONDS);
    }

    void OnPlayerSpawned(OnlinePlayer player)
    {
        if (player.IsOwner)
        {
            localPlayer = player;
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.SetOnlineSeat(player.Seat);
        }
        else
            waiting.Add(player);
    }

    void OnPlayerDespawned(OnlinePlayer player)
    {
        // A machine that leaves during a load isn't waited for
        sceneFlow.MachineLeft(player.OwnerClientId);

        if (player == localPlayer)
            localPlayer = null;
        waiting.Remove(player);

        RemotePlayer remote;
        if (remotes.TryGetValue(player, out remote))
        {
            remotes.Remove(player);
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.RemoveRemotePlayer(remote.Seat);
            RecheckTutorial();
        }
    }

    // A player left (or this machine went offline): everyone left may have finished the tutorial
    static void RecheckTutorial()
    {
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.RecheckAlumni();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        // The scene flow follows the host's loads (client) or the clients' reports (host)
        sceneFlow.Link(match, SessionMachines, session.Network.LocalClientId, LeaveSession);

        if (match.IsServer)
        {
            // Players who join later start from where the host is
            if (GameManager.Instance != null)
                match.SendState(GameManager.Instance.MainState);
            return;
        }

        match.StateReceived += FollowHost;
        match.OrderReceived += FollowHostOrder;
        if (match.State != GameState.Default)
            FollowHost(match.State);
    }

    // Host: the machines in the session now, the host included
    IEnumerable<ulong> SessionMachines()
    {
        return session.Network.ConnectedClientsIds;
    }

    // Client: going back to the menu alone leaves the session
    void LeaveSession()
    {
        session.Leave();
    }

    // Host: each state the game switches to goes to the clients
    void OnStateApplied(GameState state)
    {
        if (session.Match != null && session.Match.IsServer)
            session.Match.SendState(state);
    }

    // Client: the host switched state. While this machine's new scene is still coming up, the state waits for it: its
    // objects (the spawns, the cutscene) must hear it. When the scene is up, what waited goes in order (HostQueue)
    void FollowHost(GameState hostState)
    {
        hostMessages.Add(() => ApplyHostState(hostState));
    }

    void ApplyHostState(GameState hostState)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ApplyGameState(ForClient(hostState));
    }

    // Client: the host changed an order. It shows here in the host's order with its states, so it waits with them for
    // this machine's new scene (the golden order comes with the golden round's)
    void FollowHostOrder(OrderChange change)
    {
        hostMessages.Add(() => orders.Show(change));
    }

    bool IsChangingScene()
    {
        return sceneFlow.Changing;
    }

    void Update()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        SeatWaitingPlayers(players);
        ShareLocalChoices(players);
        ShowRemoteChoices(players);
        ShareOrFollowClock();
    }

    // Other machines' players take their seats here once they're free (this machine's player may be moving out of one)
    void SeatWaitingPlayers(PlayerInstantiate players)
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            OnlinePlayer player = waiting[i];
            int seat = player.Seat;
            if (seat < 0 || seat >= Constants.MAX_PLAYERS || players.Roster[seat] != null)
                continue;

            RemoteAvatar avatar = RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null);
            if (players.AddRemotePlayer(avatar.gameObject, seat, player.OwnerClientId) == null)
            {
                Destroy(avatar.gameObject);
                continue;
            }

            remotes[player] = new RemotePlayer { Avatar = avatar, Seat = seat };
            waiting.RemoveAt(i);
        }
    }

    // This machine's player's colour, hat and readiness go to every machine
    void ShareLocalChoices(PlayerInstantiate players)
    {
        if (localPlayer == null || localPlayer.Seat < 0 || localPlayer.Seat >= Constants.MAX_PLAYERS)
            return;

        PlayerSlot slot = players.Roster[localPlayer.Seat];
        if (slot == null || !slot.IsLocal)
            return;

        CustomizationSelector picked = slot.Input.GetComponent<PlayerUIHandler>().customizationSelector;
        localPlayer.Share(picked.ColourIndex, picked.HatIndex, players.IsReady(slot.Index));
    }

    // Other machines' players' colour, hat and readiness show here as they change
    void ShowRemoteChoices(PlayerInstantiate players)
    {
        foreach (KeyValuePair<OnlinePlayer, RemotePlayer> pair in remotes)
        {
            OnlinePlayer player = pair.Key;
            RemotePlayer shown = pair.Value;

            if (player.Colour != shown.Colour)
            {
                shown.Colour = player.Colour;
                PlayerColorInformationSO colour = Pick(choices.Colours, player.Colour);
                if (colour != null)
                    shown.Avatar.ShowColour(colour);
            }

            if (player.Hat != shown.Hat)
            {
                shown.Hat = player.Hat;
                PlayerHatInformationSO hat = Pick(choices.Hats, player.Hat);
                if (hat != null)
                    shown.Avatar.ShowHat(hat);
            }

            if (player.Ready != shown.Ready)
            {
                shown.Ready = player.Ready;
                players.SetRemoteReady(shown.Seat, player.Ready);
            }
        }
    }

    // The match clock: the host shares it as an end time in server time while its waves run; a client shows it
    void ShareOrFollowClock()
    {
        OnlineMatch match = session.Match;
        OrderManager orders = OrderManager.Instance;
        if (match == null || !session.IsRunning)
            return;

        // No waves (in the menu, after a match): the host shares a stopped clock, so a client never follows an old one
        if (orders == null)
        {
            if (match.IsServer && (match.ClockStarted || match.FinalOrder))
                match.ShareClock(0, false, false);
            return;
        }

        double now = session.Network.ServerTime.Time;
        if (match.IsServer)
        {
            bool moved = orders.GameStarted && MatchClock.NeedsRepublish(match.ClockEnd, now, orders.GameTimer);
            if (moved || match.ClockStarted != orders.GameStarted || match.FinalOrder != orders.FinalOrderActive)
                match.ShareClock(MatchClock.EndTime(now, orders.GameTimer), orders.GameStarted, orders.FinalOrderActive);
        }
        else
            orders.FollowHostClock(MatchClock.Remaining(match.ClockEnd, now), match.ClockStarted, match.FinalOrder);
    }
}

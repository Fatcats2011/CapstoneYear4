using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's side of an online session:
/// - this machine's role (GameAuthority) and the scene flow (loads with the whole session: OnlineSceneFlow), with how
///   long each match load took here, in the log;
/// - this machine's player in the seat the host gave them;
/// - other machines' players as scooters in their seats;
/// - every scooter's pose and what it's doing (OnlineDriving);
/// - every order and score (OnlineOrders);
/// - every machine's player finishing the tutorial (OnlineTutorial);
/// - the results' rematch vote (OnlineRematch);
/// - the vote to skip a cutscene (OnlineCutsceneSkip);
/// - the end-of-match awards (OnlineAwards);
/// - the highlight reel's list (OnlineHighlights);
/// - each machine's round trip (OnlinePings);
/// - steals and clashes between players (OnlineSteals);
/// - where players who fall in the water rise (OnlineRespawns);
/// - every player's one-shots, and the host's clock's (OnlineCues);
/// - everyone's colour, hat and readiness;
/// - the host's game states;
/// - the host's match clock (MatchClock);
/// - an open lobby starting by itself (AutoStartClock);
/// - why a session ended, in the controller hint bar. A match can't go on without its host: a machine in one goes back to
///   the menu.
/// Whatever starts a session for the game adds it: OnlinePlay (Steam) or the editor's Online menu. See docs/online.md
/// </summary>
public class OnlineGame : MonoBehaviour
{
    /// <summary>How long a message about online play shows (ControllerPrompts)</summary>
    public const float NOTICE_SECONDS = 5f;

    static OnlineGame playing; // the game whose session this machine plays online (none offline)

    /// <summary>The game this machine plays online: none offline (Dispatch's board reads its players)</summary>
    public static OnlineGame Playing { get { return playing; } }

    /// <summary>Everyone in the session, whichever machine they are on</summary>
    public IReadOnlyList<OnlinePlayer> Players { get { return session.Players; } }

    /// <summary>
    /// Seconds until the open lobby starts by itself, 0 when no start is set (the host's, shared with every machine)
    /// </summary>
    public float AutoStartRemaining
    {
        get
        {
            OnlineMatch match = session == null ? null : session.Match;
            if (match == null || !session.IsRunning)
                return 0;

            return MatchRules.AutoStartRemaining(match.AutoStartAt, session.Network.ServerTime.Time);
        }
    }

    /// <summary>Whether the host has set an automatic start (a client can't tell whether the lobby is open to anyone otherwise)</summary>
    public bool AutoStartSet
    {
        get
        {
            OnlineMatch match = session == null ? null : session.Match;
            return match != null && session.IsRunning && match.AutoStartAt != 0;
        }
    }

    /// <summary>Player select's colours and hats</summary>
    public CustomizationSelector Choices { get { return choices; } }

    // What this machine shows of another machine's player
    class RemotePlayer
    {
        public RemoteAvatar Avatar;
        public int Seat;
        public int Colour = -1;
        public int Hat = -1;
        public bool Ready;  // the readiness applied here (PlayerInstantiate.SetRemoteReady)
        public bool Forced; // host: made ready for the automatic start, whatever their own machine says
    }

    OnlineSession session;
    OnlinePrefabs prefabs;
    OnlineSceneFlow sceneFlow;
    CustomizationSelector choices; // player select's colours and hats, from the local player's prefab
    string myName; // this machine's Steam name, cleaned (null until first shared, and again after the session ends)
    ulong myId;    // this machine's Steam ID (0 until first shared, or without Steam)
    readonly List<OnlinePlayer> localPlayers = new List<OnlinePlayer>(); // this machine's players (several can share its screen)
    readonly SeatAsker asker = new SeatAsker(); // the seats asked of the host for this machine's players without one
    PlayerInstantiate hooked;     // the player manager whose seats given back this hears
    readonly List<OnlinePlayer> waiting = new List<OnlinePlayer>(); // other machines' players, until their seat is free here
    readonly Dictionary<OnlinePlayer, RemotePlayer> remotes = new Dictionary<OnlinePlayer, RemotePlayer>();
    OnlineOrders orders;     // the host's orders and scores, on this machine
    OnlineSteals steals;     // the host's steals and clashes, on this machine
    OnlineRespawns respawns; // the host's respawn points, on this machine
    HostQueue hostMessages;  // client: the host's states, order changes, hits and respawn points, which wait while this machine's new scene comes up
    string pendingNotice;    // why the session ended, to say again once the menus are up
    bool wasInMenus = true; // the last state applied was in the menus (the host drops unready seats leaving them)
    readonly AutoStartClock autoStartClock = new AutoStartClock(); // host: when an open lobby starts by itself
    double countingTo;       // the automatic start this machine saw counting down (only a start seen counting readies)
    double readiedFor;       // the automatic start this machine's players were readied for
    double graceFor;         // host: the automatic start whose grace ran out (other machines' players made ready, or already counting down)
    double forcedFor;        // host: the automatic start other machines' players were made ready for
    readonly HashSet<ulong> seatedMachines = new HashSet<ulong>();
    readonly List<OnlinePlayer> botsToSeat = new List<OnlinePlayer>(); // host: CPU drivers spawned, until their seat is free here
    readonly HashSet<OnlinePlayer> seatedBots = new HashSet<OnlinePlayer>(); // host: CPU drivers with a scooter here

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
    /// Whether a machine has left the match: none of its players remain (a seat it gave back, with others still here,
    /// isn't the machine leaving)
    /// </summary>
    /// <param name="ownersLeft">The machines of the players still in the session</param>
    /// <summary>
    /// The seats the host still has this machine's players in, though this machine gave them back: a give-back the
    /// host's rate limit dropped. They're given back again
    /// </summary>
    public static List<int> SeatsToGiveBack(IEnumerable<int> ownedSeats, ICollection<int> heldSeats)
    {
        List<int> back = new List<int>();
        foreach (int seat in ownedSeats)
        {
            if (!heldSeats.Contains(seat))
                back.Add(seat);
        }
        return back;
    }

    public static bool MachineGone(IEnumerable<ulong> ownersLeft, ulong machine)
    {
        foreach (ulong owner in ownersLeft)
        {
            if (owner == machine)
                return false;
        }
        return true;
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
        sceneFlow = new OnlineSceneFlow(SceneFlow.Current, SceneFlow.Loader, RealTime);
        sceneFlow.LoadTimed += LogLoad;
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

        // The results' rematch vote: the host holds it, every machine sees it
        gameObject.AddComponent<OnlineRematch>().Begin(session);

        // Skipping a cutscene: everyone's vote, counted on the host
        gameObject.AddComponent<OnlineCutsceneSkip>().Begin(session);

        // The end-of-match awards: picked on the host, shown on every machine
        gameObject.AddComponent<OnlineAwards>().Begin(session);

        // The highlight reel's list: made on the host, played on every machine at the same moment
        gameObject.AddComponent<OnlineHighlights>().Begin(session);

        // Each machine's round trip, measured on the host, for the pause menu
        gameObject.AddComponent<OnlinePings>().Begin(session);

        // Steals and clashes between machines' players: the host decides each one
        steals = gameObject.AddComponent<OnlineSteals>();
        steals.Begin(session);

        // Bumps between machines' scooters: the host passes each one on, and the bumped player's own machine pushes them
        gameObject.AddComponent<OnlineBumps>().Begin(session);

        // Where players who fall in the water rise: the host picks for everyone
        respawns = gameObject.AddComponent<OnlineRespawns>();
        respawns.Begin(session);

        // What a scooter does in a moment (a boost, a full horn…) plays on every machine
        gameObject.AddComponent<OnlineCues>().Begin(session);

        // The host decides achievements; each machine unlocks its own player's
        IAchievementStore achievements = SteamFeatures.Instance != null ? SteamFeatures.Instance.Achievements : new SteamAchievementStore();
        gameObject.AddComponent<OnlineAchievements>().Begin(session, achievements);

        session.RoleChanged += OnRoleChanged;
        session.Ended += ShowEnd;
        session.SeatRefused += OnSeatRefused;
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
            session.SeatRefused -= OnSeatRefused;
            session.PlayerSpawned -= OnPlayerSpawned;
            session.PlayerDespawned -= OnPlayerDespawned;
            session.MatchSpawned -= OnMatchSpawned;
            if (session.Match != null)
            {
                session.Match.StateReceived -= FollowHost;
                session.Match.OrderReceived -= FollowHostOrder;
                session.Match.HitReceived -= FollowHostHit;
                session.Match.RespawnReceived -= FollowHostRespawn;
                session.Match.ReturnRequested -= DropAbandonedMatch;
            }
        }
        if (sceneFlow != null)
            sceneFlow.Unlink();
        if (GameManager.Instance != null)
            GameManager.Instance.StateApplied -= OnStateApplied;
        HookPlayers(null);
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
        localPlayers.Clear();
        myName = null;
        myId = 0;
        asker.Reset();
        countingTo = readiedFor = graceFor = forcedFor = 0; // the next session's server time starts again
        autoStartClock.Reset();
        if (players != null)
            players.GoOffline();
    }

    // The session ended by itself (the host left, turned this player away, or couldn't be reached; on the host, its own
    // connection failed): the player hears why. A match can't go on without its host: a machine in one, or on its way
    // into one, goes back to the menu (the local loader's way, by now), where it hears why again
    void ShowEnd(string reason)
    {
        ControllerPrompts.Instance.ShowHint(reason, NOTICE_SECONDS);

        GameManager game = GameManager.Instance;
        if (game == null || LobbyRules.InTheMenus(game.MainState))
            return;

        pendingNotice = reason;
        ISceneFlow flow = SceneFlow.Current;
        if (flow != null)
            flow.ReturnToMenu();
    }

    void OnPlayerSpawned(OnlinePlayer player)
    {
        // Host: a CPU driver is the host's, but nobody sits at it: its scooter is built here (SeatHostBots)
        if (player.IsBot && player.IsOwner)
        {
            botsToSeat.Add(player);
            return;
        }

        // Host: a machine's first seat in this session restarts an open lobby's automatic start (AutoStartClock)
        if (session.Network != null && session.Network.IsServer)
            autoStartClock.Seated(session.MachineOf(player.OwnerClientId), session.Network.ServerTime.Time);

        if (player.IsOwner)
        {
            // A seat for one of this machine's players: it answers an ask (the machine's first seat answers none). One
            // nobody here will sit in any more goes back
            localPlayers.Add(player);
            asker.Answered();

            // One player per machine: the others here leave before the menu player is seated
            if (OnlineRules.OnePlayerPerMachine && PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.DropExtraLocalPlayers();
            if (PlayerInstantiate.Instance != null && !PlayerInstantiate.Instance.GiveSeat(player.Seat))
                session.FreeSeat(player.Seat);
        }
        else
            waiting.Add(player);
    }

    void OnPlayerDespawned(OnlinePlayer player)
    {
        // A machine that leaves during a load isn't waited for: once none of its players remain
        if (MachineGone(OwnersInSession(), player.OwnerClientId))
            sceneFlow.MachineLeft(player.OwnerClientId);

        if (localPlayers.Remove(player) && PlayerInstantiate.Instance != null)
            PlayerInstantiate.Instance.LoseSeat(player.Seat);
        waiting.Remove(player);
        botsToSeat.Remove(player);
        if (seatedBots.Remove(player) && PlayerInstantiate.Instance != null)
            PlayerInstantiate.Instance.RemoveRemotePlayer(player.Seat);

        RemotePlayer remote;
        if (remotes.TryGetValue(player, out remote))
        {
            remotes.Remove(player);
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.RemoveRemotePlayer(remote.Seat);
            RecheckTutorial();
        }
    }

    IEnumerable<ulong> OwnersInSession()
    {
        foreach (OnlinePlayer player in session.Players)
            yield return player.OwnerClientId;
    }

    // The host refused a seat for one of this machine's players (the match is full, or started): one waiting player is
    // turned away, and hears why
    void OnSeatRefused(string reason)
    {
        asker.Answered();
        if (PlayerInstantiate.Instance != null)
            PlayerInstantiate.Instance.TurnAwayUnseated(reason, 1);
    }

    // A player here gave their seat back (B in player select): the host frees it on every machine
    void OnSeatGivenUp(int seat)
    {
        session.FreeSeat(seat);
    }

    // Hears the player manager's seats given back (a scene's own copy can come and go)
    void HookPlayers(PlayerInstantiate players)
    {
        if (hooked == players)
            return;

        if (hooked != null)
            hooked.SeatGivenUp -= OnSeatGivenUp;
        hooked = players;
        if (hooked != null)
            hooked.SeatGivenUp += OnSeatGivenUp;
    }

    // Asks the host for a seat for each of this machine's players without one (in the menus, once the match exists)
    void AskForSeats(PlayerInstantiate players)
    {
        if (session.Match == null || !players.IsOnline || !session.IsRunning)
            return;

        // One player per machine: the seat the session gave this machine is its only one
        if (!OnlineRules.OnePlayerPerMachine)
        {
            for (int ask = asker.ToAsk(players.UnseatedLocalCount, Time.realtimeSinceStartup); ask > 0; ask--)
                session.AskSeat();
        }

        // Every couple of seconds, a seat given back that the host still holds goes back again
        if (Time.realtimeSinceStartup < nextGiveBackCheck)
            return;
        nextGiveBackCheck = Time.realtimeSinceStartup + GIVE_BACK_CHECK;
        ownedSeats.Clear();
        foreach (OnlinePlayer player in localPlayers)
        {
            if (player != null)
                ownedSeats.Add(player.Seat);
        }
        foreach (int seat in SeatsToGiveBack(ownedSeats, new HashSet<int>(players.OnlineSeats)))
            session.FreeSeat(seat);
    }

    const float GIVE_BACK_CHECK = 2f;
    float nextGiveBackCheck;
    readonly List<int> ownedSeats = new List<int>();

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
        match.HitReceived += FollowHostHit;
        match.RespawnReceived += FollowHostRespawn;
        match.ReturnRequested += DropAbandonedMatch;
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

    // Host: each state the game switches to goes to the clients. Once the menus are up after a session that ended by
    // itself, the player hears why again
    void OnStateApplied(GameState state)
    {
        // Host, as the match starts: a seat granted that moment has a player who never readied. It goes now, before any
        // machine shows it in the match
        bool menus = LobbyRules.InTheMenus(state);
        if (wasInMenus && !menus && session.Match != null && session.Match.IsServer && PlayerInstantiate.Instance != null)
        {
            session.DropUnreadyExtras(PlayerInstantiate.Instance.IsReady);

            // The seats nobody took fill with CPU drivers, unless the match rules say not (a public lobby always fills)
            bool botsOn = session.FillsWithBots && HouseRules.Current.ForMatch(OrderManager.PublicLobby()).Bots;
            session.AddBots(BotFill.Needed(session.PlayersIn, botsOn));
        }
        else if (!wasInMenus && menus && session.Match != null && session.Match.IsServer)
            session.RemoveBots(); // back in the lobby: the seats are people's again
        wasInMenus = menus;

        // The match starts: a player here without a seat can't follow it (the host never counted them)
        if (!menus && PlayerInstantiate.Instance != null && PlayerInstantiate.Instance.IsOnline)
            PlayerInstantiate.Instance.TurnAwayUnseated(JoinRules.STARTED, Constants.MAX_PLAYERS);

        if (pendingNotice != null && LobbyRules.InTheMenus(state))
        {
            ControllerPrompts.Instance.ShowHint(pendingNotice, NOTICE_SECONDS);
            pendingNotice = null;
        }

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

    // Client: the host decided a steal or clash. This machine's player bounces in the host's order, after the steal's
    // order changes
    void FollowHostHit(PlayerHit hit)
    {
        hostMessages.Add(() => steals.Show(hit));
    }

    // Client: the host picked where a player rises. It goes in the host's order, after that player's orders drop
    void FollowHostRespawn(int seat, int point)
    {
        hostMessages.Add(() => respawns.Show(seat, point));
    }

    // Client: the host is taking everyone back to the menu. What still waits for this machine's scene (a slow machine's
    // match states, order changes…) belongs to the match being left: it would show the match starting on the way out.
    // What the host sends after this queues as usual
    void DropAbandonedMatch()
    {
        hostMessages.Clear();
    }

    bool IsChangingScene()
    {
        return sceneFlow.Changing;
    }

    // Match loads are timed in real time, which no pause or slow motion stretches
    static float RealTime()
    {
        return Time.realtimeSinceStartup;
    }

    // How long a match load took here: the log line to read on a slow PC (Player.log in a build)
    static void LogLoad(string report)
    {
        Debug.Log(report);
    }

    void Update()
    {
        sceneFlow.Tick(); // every frame of a load counts, so before anything here can return

        PlayerInstantiate players = PlayerInstantiate.Instance;
        HookPlayers(players);
        if (players == null)
            return;

        AskForSeats(players);
        // Before seating: a machine's first seat moves the automatic start first, so only a machine already seen joins a
        // start that was forced (SeatWaitingPlayers)
        AutoStart(players);
        SeatWaitingPlayers(players);
        SeatHostBots(players);
        ShareLocalChoices(players);
        ShowRemoteChoices(players);
        ShareOrFollowClock();
    }

    // An open lobby (Fill) starts by itself AUTO_START after a machine last sat down for the first time, with at least
    // two machines in it (AutoStartClock). The host shares when (0 = it doesn't); then every machine readies its own
    // players as A does, and FORCE_GRACE later the host makes every other machine's player ready, so Dispatch's all-ready
    // start runs. A seat taken after that sits down ready (SeatWaitingPlayers)
    void AutoStart(PlayerInstantiate players)
    {
        OnlineMatch match = session.Match;
        if (match == null || !session.IsRunning)
            return;

        double now = session.Network.ServerTime.Time;
        bool inPlayerSelect = InPlayerSelect();

        if (match.IsServer)
        {
            // Leaving player select (the match starting) stops it
            double at = autoStartClock.StartAt(inPlayerSelect && Filling(), MachinesSeated(), now);
            match.SetAutoStart(at);

            // Once per start: a countdown already running (everyone ready) is left alone, so a player backing out of it still can
            if (at != 0 && now >= at + MatchRules.FORCE_GRACE && graceFor != at)
            {
                graceFor = at;
                if (players.readyUpCountdown == null)
                {
                    forcedFor = at;
                    foreach (RemotePlayer remote in remotes.Values)
                    {
                        remote.Forced = true;
                        remote.Ready = true;
                        players.SetRemoteReady(remote.Seat, true);
                    }
                }
            }
        }

        // Whatever the host sends goes through AutoStartRemaining, which reads nonsense as no time left. Only a start this
        // machine saw counting down readies its players, so nonsense never does
        double shared = match.AutoStartAt;
        if (MatchRules.AutoStartRemaining(shared, now) > 0)
        {
            countingTo = shared;
            return;
        }
        if (shared == 0 || countingTo != shared || readiedFor == shared || !inPlayerSelect)
            return;

        readiedFor = shared;
        foreach (PlayerSlot slot in players.Roster.LocalPlayers)
            slot.Input.GetComponent<PlayerUIHandler>().menuInteractions.ReadyAsA();
    }

    static bool InPlayerSelect()
    {
        return GameManager.Instance != null && GameManager.Instance.MainState == GameState.PlayerSelect;
    }

    // Host: whether its lobby is open to anyone (Fill in Online Play)
    bool Filling()
    {
        OnlinePlay play = OnlinePlay.Instance;
        return play != null && play.Session == session && play.Lobby != null && play.Lobby.Filling;
    }

    // How many machines have a player in the session
    int MachinesSeated()
    {
        seatedMachines.Clear();
        foreach (OnlinePlayer player in session.Players)
            seatedMachines.Add(player.OwnerClientId);
        return seatedMachines.Count;
    }

    // Host: other machines' players were made ready for the automatic start that's still set. Their own not-ready, sent
    // before they heard of it, mustn't undo that
    bool ForcedReady()
    {
        OnlineMatch match = session.Match;
        return forcedFor != 0 && match != null && match.IsServer && match.AutoStartAt == forcedFor;
    }

    // Other machines' players take their seats here once they're free (this machine's player may be moving out of one).
    // Host: a seat taken once the automatic start has made the others ready (a machine already seen: its seat asked
    // again, or the machine back in) joins that start, ready as it sits, and the countdown runs on. Neither can stop it
    void SeatWaitingPlayers(PlayerInstantiate players)
    {
        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            OnlinePlayer player = waiting[i];
            int seat = player.Seat;
            if (seat < 0 || seat >= Constants.MAX_PLAYERS || players.Roster[seat] != null)
                continue;

            bool forced = ForcedReady();
            RemoteAvatar avatar = RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null);
            if (players.AddRemotePlayer(avatar.gameObject, seat, player.OwnerClientId, forced) == null)
            {
                Destroy(avatar.gameObject);
                continue;
            }

            remotes[player] = new RemotePlayer { Avatar = avatar, Seat = seat, Forced = forced, Ready = forced };
            waiting.RemoveAt(i);
        }
    }

    // Host: each CPU driver gets a scooter here once its seat is free: a full scooter with a BotDriver at the wheel and no
    // view. Its colour and hat are ones nobody uses, and every machine sees it as it sees any other host-owned player
    void SeatHostBots(PlayerInstantiate players)
    {
        for (int i = botsToSeat.Count - 1; i >= 0; i--)
        {
            OnlinePlayer player = botsToSeat[i];
            if (player == null)
            {
                botsToSeat.RemoveAt(i);
                continue;
            }

            int seat = player.Seat;
            if (seat < 0 || seat >= Constants.MAX_PLAYERS || players.Roster[seat] != null)
                continue;

            // Built switched off, so nothing starts before its seat is set (AddBotPlayer switches it on)
            GameObject holder = new GameObject("CPU (being built)");
            holder.SetActive(false);
            GameObject avatar = Instantiate(prefabs.RemoteAvatarPrefab, holder.transform);
            PlayerSlot slot = players.AddBotPlayer(avatar, seat, player.OwnerClientId);
            Destroy(holder);
            if (slot == null)
            {
                Destroy(avatar);
                continue;
            }

            // The horn gauge's sliders live in a view, which a CPU driver has none of
            foreach (PhaseIndicator horns in avatar.GetComponentsInChildren<PhaseIndicator>(true))
                horns.enabled = false;
            BallDriving driving = avatar.GetComponentInChildren<BallDriving>(true);
            BotDriver bot = driving.gameObject.AddComponent<BotDriver>();
            bot.OnlyInRound = true;
            bot.Kind = BotPersonality.ForRank(BotPersonality.RankOf(seat, BotSeats()));
            bot.Drive(driving);

            DressBot(player, avatar);
            seatedBots.Add(player);
            botsToSeat.RemoveAt(i);
        }
    }

    // Every CPU driver's seat, seated or waiting (bots take seats in the order they're added: the lowest is the thief)
    IEnumerable<int> BotSeats()
    {
        foreach (OnlinePlayer bot in botsToSeat)
            if (bot != null)
                yield return bot.Seat;
        foreach (OnlinePlayer bot in seatedBots)
            if (bot != null)
                yield return bot.Seat;
    }

    const string BOT_NAME = "CPU";

    // A CPU driver's colour and hat, from those no one in the session wears, shared like any player's
    void DressBot(OnlinePlayer bot, GameObject avatar)
    {
        int colour = Unused(choices.Colours.Count, bot, p => p.Colour);
        int hat = Unused(choices.Hats.Count, bot, p => p.Hat);
        bot.Share(colour, hat, true);
        bot.ShareName(BOT_NAME);

        PlayerColorInformationSO colourLook = Pick(choices.Colours, colour);
        if (colourLook != null)
            ScooterLook.ShowColour(avatar, colourLook);
        PlayerHatInformationSO hatLook = Pick(choices.Hats, hat);
        if (hatLook != null)
            ScooterLook.ShowHat(avatar, hatLook);
    }

    // The first index of a list that no other player in the session has (the first one if all are used)
    int Unused(int count, OnlinePlayer except, System.Func<OnlinePlayer, int> pick)
    {
        for (int index = 0; index < count; index++)
        {
            bool used = false;
            foreach (OnlinePlayer player in session.Players)
            {
                if (player != except && pick(player) == index)
                    used = true;
            }
            if (!used)
                return index;
        }
        return 0;
    }

    // This machine's players' colour, hat, readiness and name go to every machine
    void ShareLocalChoices(PlayerInstantiate players)
    {
        // Asked of Steam once per session, not every frame
        if (myName == null)
        {
            myName = PlayerNames.Clean(SteamNames.Mine());
            myId = SteamNames.MyId();
        }

        foreach (OnlinePlayer localPlayer in localPlayers)
        {
            if (localPlayer == null || localPlayer.Seat < 0 || localPlayer.Seat >= Constants.MAX_PLAYERS)
                continue;

            PlayerSlot slot = players.Roster[localPlayer.Seat];
            if (slot == null || !slot.IsLocal)
                continue;

            CustomizationSelector picked = slot.Input.GetComponent<PlayerUIHandler>().customizationSelector;
            localPlayer.Share(picked.ColourIndex, picked.HatIndex, players.IsReady(slot.Index));
            localPlayer.ShareName(myName);
            localPlayer.ShareSteamId(myId);
        }
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

            // A player made ready for the automatic start stays ready while it's set, whatever their machine sent before it
            // heard. Once it isn't (a new machine seated, Fill off), their own readiness counts again. From the match
            // starting until player select, readiness stays as it was
            if (shown.Forced && !ForcedReady() && InPlayerSelect())
                shown.Forced = false;
            bool ready = shown.Forced || player.Ready;
            if (ready != shown.Ready)
            {
                shown.Ready = ready;
                players.SetRemoteReady(shown.Seat, ready);
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

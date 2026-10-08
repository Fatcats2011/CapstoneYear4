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
/// - steals and clashes between players (OnlineSteals);
/// - where players who fall in the water rise (OnlineRespawns);
/// - every player's one-shots, and the host's clock's (OnlineCues);
/// - everyone's colour, hat and readiness;
/// - the host's game states;
/// - the host's match clock (MatchClock);
/// - why a session ended, in the controller hint bar. A match can't go on without its host: a machine in one goes back to
///   the menu.
/// Whatever starts a session for the game adds it: OnlinePlay (Steam) or the editor's Online menu. See docs/online.md
/// </summary>
public class OnlineGame : MonoBehaviour
{
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
        asker.Reset();
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
        if (player.IsOwner)
        {
            // A seat for one of this machine's players: it answers an ask (the machine's first seat answers none). One
            // nobody here will sit in any more goes back
            localPlayers.Add(player);
            asker.Answered();
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

        for (int ask = asker.ToAsk(players.UnseatedLocalCount, Time.realtimeSinceStartup); ask > 0; ask--)
            session.AskSeat();

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
        // The match starts: a player here without a seat can't follow it (the host never counted them)
        if (!LobbyRules.InTheMenus(state) && PlayerInstantiate.Instance != null && PlayerInstantiate.Instance.IsOnline)
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

    // This machine's players' colour, hat and readiness go to every machine
    void ShareLocalChoices(PlayerInstantiate players)
    {
        foreach (OnlinePlayer localPlayer in localPlayers)
        {
            if (localPlayer == null || localPlayer.Seat < 0 || localPlayer.Seat >= Constants.MAX_PLAYERS)
                continue;

            PlayerSlot slot = players.Roster[localPlayer.Seat];
            if (slot == null || !slot.IsLocal)
                continue;

            CustomizationSelector picked = slot.Input.GetComponent<PlayerUIHandler>().customizationSelector;
            localPlayer.Share(picked.ColourIndex, picked.HatIndex, players.IsReady(slot.Index));
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

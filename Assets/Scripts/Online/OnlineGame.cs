using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The game's side of an online session:
/// - this machine's role (GameAuthority) and the scene flow;
/// - this machine's player in the seat the host gave them;
/// - other machines' players as scooters in their seats;
/// - everyone's colour, hat and readiness;
/// - the host's game states.
/// Whatever starts a session for the game adds it: the Online menu now, the Steam lobby later (roadmap Task 3.2).
/// See docs/online.md
/// </summary>
public class OnlineGame : MonoBehaviour
{
    /// <summary>Why a machine with more than one player can't go online</summary>
    public const string ONE_PLAYER = "Online: online play is one player per machine (until roadmap Task 3.9). Let the other players leave player select first.";

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
        sceneFlow = new OnlineSceneFlow(SceneFlow.Current);
        choices = prefabs.LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);

        session.RoleChanged += OnRoleChanged;
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
            session.PlayerSpawned -= OnPlayerSpawned;
            session.PlayerDespawned -= OnPlayerDespawned;
            session.MatchSpawned -= OnMatchSpawned;
            if (session.Match != null)
                session.Match.StateReceived -= FollowHost;
        }
        if (GameManager.Instance != null)
            GameManager.Instance.StateApplied -= OnStateApplied;
    }

    // This machine's role and scene flow follow the session's
    void OnRoleChanged(NetworkRole role)
    {
        GameAuthority.Role = role;

        if (role != NetworkRole.Offline)
        {
            SceneFlow.Current = sceneFlow;
            return;
        }

        SceneFlow.Current = null;
        PlayerInstantiate players = PlayerInstantiate.Instance;
        foreach (RemotePlayer remote in remotes.Values)
        {
            if (players != null)
                players.RemoveRemotePlayer(remote.Seat);
        }
        remotes.Clear();
        waiting.Clear();
        localPlayer = null;
        if (players != null)
            players.SetOnlineSeat(-1);
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
        if (player == localPlayer)
            localPlayer = null;
        waiting.Remove(player);

        RemotePlayer remote;
        if (remotes.TryGetValue(player, out remote))
        {
            remotes.Remove(player);
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.RemoveRemotePlayer(remote.Seat);
        }
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        if (match.IsServer)
        {
            // Players who join later start from where the host is
            if (GameManager.Instance != null)
                match.SendState(GameManager.Instance.MainState);
            return;
        }

        match.StateReceived += FollowHost;
        if (match.State != GameState.Default)
            FollowHost(match.State);
    }

    // Host: each state the game switches to goes to the clients
    void OnStateApplied(GameState state)
    {
        if (session.Match != null && session.Match.IsServer)
            session.Match.SendState(state);
    }

    // Client: the host switched state
    void FollowHost(GameState hostState)
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ApplyGameState(ForClient(hostState));
    }

    void Update()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        SeatWaitingPlayers(players);
        ShareLocalChoices(players);
        ShowRemoteChoices(players);
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
}

#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
using System.Text;
#if !DISABLESTEAMWORKS
using Netcode.Transports;
#endif
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// This machine's online match: hosts one or joins one, lets players in by JoinRules, seats them, spawns the match, and
/// every machine's player and scooter, and reports its role (Host or Client while the session runs, Offline once it ends);
/// OnlineGame makes that this machine's GameAuthority.Role. Builds its own Netcode NetworkManager and transport, so no
/// scene holds it. Nothing creates a session in a local match. See docs/online.md
/// </summary>
public class OnlineSession : MonoBehaviour
{
    /// <summary>The port direct (Unity Transport) sessions use</summary>
    public const ushort DIRECT_PORT = 7777;

    /// <summary>A direct join tries to reach the host once a second, this many times</summary>
    public const int DIRECT_CONNECT_ATTEMPTS = 10;

    /// <summary>
    /// A direct session (the editor's and tests') drops a machine it hasn't heard from in 90 s. A cold editor load can
    /// stall longer than Unity Transport's own 30 s. Steam sessions keep Steam's timeouts
    /// </summary>
    public const int DIRECT_DISCONNECT_MS = 90000;

    /// <summary>Why a session ended, when the host gave no reason of its own</summary>
    public const string HOST_LEFT = "The host left the match.";
    public const string HOST_UNREACHABLE = "Couldn't reach the host.";
    public const string CONNECTION_LOST = "The connection was lost.";

    /// <summary>Logged when a Steam session can't start</summary>
    public const string NO_STEAM = "Online: Steam isn't running, so this machine can't host or join over Steam.";

    readonly SeatTable seats = new SeatTable(); // host: who sits where, itself included
    bool reachedHost; // client: the host let this machine in
    bool leaving;     // Leave was called, so the end that follows is expected
    OnlinePrefabs prefabs; // what the host spawns
    readonly List<OnlinePlayer> players = new List<OnlinePlayer>();
    readonly List<OnlineScooter> scooters = new List<OnlineScooter>();
#if !DISABLESTEAMWORKS
    SteamNetworkingSocketsTransport steam; // added the first time a Steam session starts
#endif

    /// <summary>Netcode's manager for this session</summary>
    public NetworkManager Network { get; private set; }

    /// <summary>
    /// Steam host: whether a Steam user may connect (OnlinePlay: only the lobby's members). Asked by the Steam transport
    /// before it accepts a connection; null lets everyone in (direct sessions, tests). See docs/online-safety.md
    /// </summary>
    public Func<ulong, bool> AcceptsPeer { get; set; }

    /// <summary>The Unity Transport that direct (IP address) sessions use</summary>
    public UnityTransport Direct { get; private set; }

    /// <summary>This machine's build: sent when joining, and required of players who join this host</summary>
    public string Version { get; private set; }

    /// <summary>This session's part: Host or Client while it runs (a client once the host lets it in), else Offline</summary>
    public NetworkRole Role { get; private set; }

    /// <summary>Hosting or joining: from a successful start until the session ends</summary>
    public bool IsRunning { get; private set; }

    /// <summary>How many players the host has let in, the host included</summary>
    public int PlayersIn { get { return seats.Count; } }

    /// <summary>Every machine's player in this session, as this machine has them (the host spawns them)</summary>
    public IReadOnlyList<OnlinePlayer> Players { get { return players; } }

    /// <summary>Every machine's scooter in this session, as this machine has them (the host spawns them)</summary>
    public IReadOnlyList<OnlineScooter> Scooters { get { return scooters; } }

    /// <summary>The host's game state in this session (null until the host spawns it)</summary>
    public OnlineMatch Match { get; private set; }

    /// <summary>A player's OnlinePlayer arrived on this machine (this machine's own included)</summary>
    public event Action<OnlinePlayer> PlayerSpawned;

    /// <summary>A player's OnlinePlayer went: they left, or the session ended</summary>
    public event Action<OnlinePlayer> PlayerDespawned;

    /// <summary>The host's OnlineMatch arrived on this machine</summary>
    public event Action<OnlineMatch> MatchSpawned;

    /// <summary>The session's part changed (Offline, Host, Client)</summary>
    public event Action<NetworkRole> RoleChanged;

    /// <summary>
    /// Raised when a session ends without Leave, with the reason to show: the host left, turned this player away or
    /// couldn't be reached; on the host, the connection was lost
    /// </summary>
    public event Action<string> Ended;

    /// <summary>
    /// Builds a session on its own object (Netcode keeps it across scene loads)
    /// </summary>
    /// <param name="version">This build's version: the game passes Application.version</param>
    public static OnlineSession Create(string version)
    {
        GameObject holder = new GameObject("Online Session");
        OnlineSession session = holder.AddComponent<OnlineSession>();
        session.Version = version;
        session.Direct = holder.AddComponent<UnityTransport>();
        session.Direct.MaxConnectAttempts = DIRECT_CONNECT_ATTEMPTS;
        session.Direct.DisconnectTimeoutMS = DIRECT_DISCONNECT_MS; // read when a session starts

        NetworkManager network = holder.AddComponent<NetworkManager>();
        network.NetworkConfig = NewConfig(session.Direct); // a NetworkManager added from code has no config
        network.ConnectionApprovalCallback = session.Approve;
        network.OnClientConnectedCallback += session.OnConnected;
        network.OnClientDisconnectCallback += session.OnDisconnected;
        network.OnClientStopped += session.OnClientStopped;
        network.OnServerStopped += session.OnServerStopped;

        // Every build lists the same network prefabs, or Netcode drops a joiner before the host hears of them
        session.prefabs = OnlinePrefabs.Load();
        foreach (GameObject prefab in NetworkPrefabsOf(session.prefabs))
            network.AddNetworkPrefab(prefab);
        session.Network = network;
        return session;
    }

    /// <summary>
    /// This build's Netcode setup: the hash Netcode compares when a player joins (its protocol, the network prefabs' ids
    /// and the session's settings). A joiner whose setup differs is dropped before the host hears of them, with no
    /// reason, so the Steam lobby carries it (LobbyRules.BuildTag)
    /// </summary>
    public static ulong NetcodeSetup(OnlinePrefabs prefabs)
    {
        NetworkConfig config = NewConfig(null);
        foreach (GameObject prefab in NetworkPrefabsOf(prefabs))
            config.Prefabs.Add(new NetworkPrefab { Prefab = prefab });
        return config.GetConfig(false);
    }

    // Every session's Netcode settings
    static NetworkConfig NewConfig(NetworkTransport transport)
    {
        return new NetworkConfig
        {
            NetworkTransport = transport,
            ConnectionApproval = true,
            EnableSceneManagement = false, // scene loads stay local until the networked scene flow (roadmap Task 3.4)
            // Safety (docs/online-safety.md): a shared value's length is checked as it's read, a connection that isn't let
            // in within 10 s is dropped (joins happen in the menus, so a host hitch of a few seconds doesn't drop a friend),
            // and Netcode's own logs (which another machine's messages can fill) are off in release builds
            EnsureNetworkVariableLengthSafety = true,
            ClientConnectionBufferTimeout = 10,
            EnableNetworkLogs = Debug.isDebugBuild,
        };
    }

    // What every session registers with Netcode
    static GameObject[] NetworkPrefabsOf(OnlinePrefabs prefabs)
    {
        return new[] { prefabs.PlayerPrefab, prefabs.MatchPrefab, prefabs.ScooterPrefab };
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // Direct (IP address) sessions are for the editor and tests only: a release build has no way to start one, so no
    // player's game listens on an address (docs/online-safety.md)

    /// <summary>
    /// Hosts a match that players join by IP address (Unity Transport): the editor and LAN tests
    /// </summary>
    /// <param name="listenAddress">"127.0.0.1" = this computer only; "0.0.0.0" = the local network too (Windows asks once to allow it)</param>
    /// <returns>False when a session is already running, or Netcode can't start (the port is in use)</returns>
    public bool HostDirect(string listenAddress, ushort port)
    {
        if (IsRunning)
            return false;

        Direct.SetConnectionData(listenAddress, port, listenAddress);
        Network.NetworkConfig.NetworkTransport = Direct;
        return StartHost();
    }

    /// <summary>
    /// Joins a match hosted with HostDirect. The session becomes a client once the host lets it in; Ended says why if it doesn't
    /// </summary>
    /// <returns>False when a session is already running, or Netcode can't start</returns>
    public bool JoinDirect(string address, ushort port)
    {
        if (IsRunning)
            return false;

        Direct.SetConnectionData(address, port);
        Network.NetworkConfig.NetworkTransport = Direct;
        return StartClient();
    }
#endif

#if !DISABLESTEAMWORKS
    /// <summary>
    /// Hosts a match that players join over Steam (peer to peer through Steam's relay), with the host's Steam ID
    /// </summary>
    /// <returns>False without Steam, when a session is already running, or when Netcode can't start</returns>
    public bool HostSteam()
    {
        return UseSteam() && StartHost();
    }

    /// <summary>
    /// Joins a match hosted with HostSteam. The session becomes a client once the host lets it in; Ended says why if it doesn't
    /// </summary>
    /// <param name="hostSteamId">The host's Steam ID (the Steam lobby's owner)</param>
    /// <returns>False without Steam, when a session is already running, or when Netcode can't start</returns>
    public bool JoinSteam(ulong hostSteamId)
    {
        if (!UseSteam())
            return false;

        steam.ConnectToSteamID = hostSteamId;
        return StartClient();
    }

    // The Steam transport asks this before it accepts a connection; read each time, so a later AcceptsPeer applies
    bool AcceptsSteamPeer(ulong steamId)
    {
        return AcceptsPeer == null || AcceptsPeer(steamId);
    }

    // Switches Netcode to the Steam transport. Without Steam, Steamworks throws as soon as the transport starts
    bool UseSteam()
    {
        if (IsRunning)
            return false;

        if (!SteamManager.Initialized)
        {
            Debug.LogWarning(NO_STEAM);
            return false;
        }

        if (steam == null)
        {
            steam = gameObject.AddComponent<SteamNetworkingSocketsTransport>();
            steam.AcceptPeer = AcceptsSteamPeer; // a stranger is closed before Netcode sees them
            steam.options = SteamRelay.Options(); // relay only: no player learns another's IP
        }
        Network.NetworkConfig.NetworkTransport = steam;
        return true;
    }
#endif

    /// <summary>
    /// Host: disconnects a client, telling it why (a machine flooding the host, OnlineMatch)
    /// </summary>
    public void Kick(ulong clientId, string reason)
    {
        if (Network.IsServer && clientId != NetworkManager.ServerClientId && Network.ConnectedClients.ContainsKey(clientId))
        {
            Debug.Log("Online: disconnected player " + clientId + ": " + reason);
#if !DISABLESTEAMWORKS
            // Over Steam the kicked player stays out for the session: the lobby has no kick, so they could just reconnect
            if (steam != null && Network.NetworkConfig.NetworkTransport == steam)
                steam.BanOnNextDisconnect = true;
#endif
            Network.DisconnectClient(clientId, reason);
#if !DISABLESTEAMWORKS
            if (steam != null)
                steam.BanOnNextDisconnect = false;
#endif
        }
    }

    /// <summary>
    /// Ends the session on purpose (Ended isn't raised). The host leaving ends the match for everyone
    /// </summary>
    public void Leave()
    {
        if (!IsRunning)
            return;

        leaving = true;
        Network.Shutdown(); // Netcode finishes stopping at the end of the frame, then End tidies up
        SetRole(NetworkRole.Offline);
    }

    /// <summary>
    /// What to tell a client whose session ended: the host left (after letting it in), the host's own reason (it turned
    /// the player away), or that the host couldn't be reached
    /// </summary>
    public static string EndReason(bool reachedHost, string netcodeReason)
    {
        if (reachedHost)
            return HOST_LEFT;

        return string.IsNullOrEmpty(netcodeReason) ? HOST_UNREACHABLE : netcodeReason;
    }

    /// <summary>
    /// Host: whether a machine holds a seat, 0-3 (a player's slot on every machine). A machine can hold several (players
    /// sharing its screen). The host's first player sits in seat 0
    /// </summary>
    public bool Owns(ulong clientId, int seat)
    {
        return seats.Owns(clientId, seat);
    }

    /// <summary>Host: how many seats a machine holds</summary>
    public int SeatsHeldBy(ulong clientId)
    {
        return seats.CountOf(clientId);
    }

    /// <summary>The seat being spawned (OnlinePlayer and OnlineScooter take it as they spawn), -1 otherwise</summary>
    internal int SpawningSeat { get; private set; } = -1;

    /// <summary>
    /// This machine's ask for another seat was refused, with why: JoinRules.FULL or JoinRules.STARTED
    /// </summary>
    public event Action<string> SeatRefused;

    /// <summary>
    /// Another player on this machine wants a seat. The host seats them at once (or raises SeatRefused); a client asks the
    /// host, which answers with the player's OnlinePlayer or a refusal. Nothing before the match exists
    /// </summary>
    public void AskSeat()
    {
        if (Match == null)
            return;

        if (Network.IsServer)
        {
            string refusal = SeatRefusal();
            if (refusal != null)
                SeatRefused?.Invoke(refusal);
            else
                AddSeat(NetworkManager.ServerClientId);
        }
        else
            Match.AskSeat();
    }

    /// <summary>
    /// A player on this machine gives their seat back (they left player select): their player and scooter go on every
    /// machine. A machine keeps its last seat
    /// </summary>
    public void FreeSeat(int seat)
    {
        if (Match == null)
            return;

        if (Network.IsServer)
            FreeSeatOf(NetworkManager.ServerClientId, seat);
        else
            Match.FreeSeat(seat);
    }

    // Host: why another seat can't be had now (the same rules as joining: a full match, or one that started), or null
    string SeatRefusal()
    {
        return JoinRules.Refusal(Version, Version, seats.Count, HostState());
    }

    // Host: a machine asked for another seat
    internal void OnSeatAsked(ulong machine)
    {
        // Asked before its machine left, or by one never let in: a connected machine always holds a seat
        if (seats.CountOf(machine) == 0)
            return;

        string refusal = SeatRefusal();
        if (refusal != null)
            Match.RefuseSeat(machine, refusal == JoinRules.FULL);
        else
            AddSeat(machine);
    }

    /// <summary>
    /// Host, as the match starts: frees each machine's extra seats whose players aren't ready (SeatTable.UnreadyExtras),
    /// so a seat granted that moment doesn't show on every machine until it's given back
    /// </summary>
    public void DropUnreadyExtras(Func<int, bool> ready)
    {
        if (Match == null || !Match.IsServer)
            return;

        foreach ((ulong machine, int seat) extra in seats.UnreadyExtras(ready))
            FreeSeatOf(extra.machine, extra.seat);
    }

    // Host: seats another of a machine's players and spawns them on every machine
    void AddSeat(ulong machine)
    {
        int seat = seats.Take(machine);
        if (seat < 0)
            return;

        Debug.Log("Online: player " + machine + " took another seat (" + seats.Count + " in)");
        Spawn(prefabs.PlayerPrefab, machine, false, seat);
        Spawn(prefabs.ScooterPrefab, machine, false, seat);
    }

    // Host: a machine gives a seat back. Only its own, and never its last (a connected machine always has a player)
    void FreeSeatOf(ulong machine, int seat)
    {
        if (!seats.Owns(machine, seat) || seats.CountOf(machine) <= 1)
            return;

        seats.Free(machine, seat);
        Debug.Log("Online: player " + machine + " gave a seat back (" + seats.Count + " in)");
        foreach (OnlinePlayer player in players.ToArray())
        {
            if (player.Seat == seat && player.OwnerClientId == machine && player.IsSpawned)
                player.NetworkObject.Despawn(true);
        }
        foreach (OnlineScooter scooter in scooters.ToArray())
        {
            if (scooter.Seat == seat && scooter.OwnerClientId == machine && scooter.IsSpawned)
                scooter.NetworkObject.Despawn(true);
        }
    }

    // Client: the host refused this machine's ask for a seat
    void OnSeatRefused(string reason)
    {
        SeatRefused?.Invoke(reason);
    }

    bool StartHost()
    {
        seats.Clear(); // the host takes the first seat while Netcode starts
#if !DISABLESTEAMWORKS
        if (steam != null)
            steam.ClearBans(); // last session's kicks don't carry over
#endif
        if (!Network.StartHost())
            return false;

        IsRunning = true;
        SetRole(NetworkRole.Host);
        Debug.Log("Online: hosting version " + Version);
        Spawn(prefabs.MatchPrefab, NetworkManager.ServerClientId, false, -1);
        Spawn(prefabs.PlayerPrefab, NetworkManager.ServerClientId, true, 0);
        Spawn(prefabs.ScooterPrefab, NetworkManager.ServerClientId, false, 0);
        return true;
    }

    bool StartClient()
    {
        reachedHost = false;
        Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Version);
        if (!Network.StartClient())
            return false;

        IsRunning = true;
        Debug.Log("Online: joining the host");
        return true;
    }

    // Host: Netcode asks about every joining player, and about the host itself as it starts
    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string refusal = request.ClientNetworkId == NetworkManager.ServerClientId ? null
            : request.Payload != null && request.Payload.Length > MAX_PAYLOAD ? JoinRules.NOT_THIS_GAME // never read
            : JoinRules.Refusal(Version, ReadVersion(request.Payload), seats.Count, HostState());

        response.Approved = refusal == null;
        response.Reason = refusal;
        response.CreatePlayerObject = false; // the match spawns the scooters (roadmap Task 3.3)

        if (response.Approved)
            seats.Take(request.ClientNetworkId); // seated now: Netcode lists the player later in the frame. More seats: AskSeat
        else
            Debug.Log("Online: turned a player away: " + refusal);
    }

    /// <summary>The most a joining player may send (their version): anything longer isn't this game, and isn't read</summary>
    public const int MAX_PAYLOAD = 64;

    static string ReadVersion(byte[] payload)
    {
        return payload == null ? "" : Encoding.UTF8.GetString(payload);
    }

    // What the host's game is doing (without a GameManager, as in tests, it isn't in a match)
    static GameState HostState()
    {
        return GameManager.Instance != null ? GameManager.Instance.MainState : GameState.Default;
    }

    // Host: a player is in. Client: the host let this machine in (the id Netcode passes a client isn't its own)
    void OnConnected(ulong clientId)
    {
        if (Network.IsServer)
        {
            if (clientId != NetworkManager.ServerClientId)
            {
                Debug.Log("Online: player " + clientId + " joined (" + seats.Count + " in)");
                bool first = true;
                foreach (int seat in seats.SeatsOf(clientId))
                {
                    Spawn(prefabs.PlayerPrefab, clientId, first, seat); // Netcode's player object: the machine's first
                    Spawn(prefabs.ScooterPrefab, clientId, false, seat);
                    first = false;
                }
            }
            return;
        }

        reachedHost = true;
        SetRole(NetworkRole.Client);
        Debug.Log("Online: joined the host");
    }

    // Host: a machine left, so all its seats are free (the host's own client only goes when the host stops: End clears it)
    void OnDisconnected(ulong clientId)
    {
        if (Network.IsServer && clientId != NetworkManager.ServerClientId && seats.FreeAll(clientId) > 0)
            Debug.Log("Online: player " + clientId + " left (" + seats.Count + " in)");
    }

    // A client's Netcode stops when it leaves, is turned away, loses the host or never reaches it
    void OnClientStopped(bool wasHost)
    {
        if (!wasHost) // the host's own client stops with the host: OnServerStopped covers it
            End(EndReason(reachedHost, Network.DisconnectReason));
    }

    // The host's Netcode stops when it leaves, or on its own (a transport failure)
    void OnServerStopped(bool wasHost)
    {
        End(CONNECTION_LOST);
    }

    // Back to a local machine. Ended says why, unless Leave ended it or the session never started
    void End(string reason)
    {
        bool expected = leaving || !IsRunning;
        IsRunning = false;
        leaving = false;
        reachedHost = false;
        seats.Clear();
        SetRole(NetworkRole.Offline);

        if (expected)
            return;

        Debug.Log("Online: session ended: " + reason);
        RaiseEach(Ended, reason);
    }

    // Calls each listener in turn: one that throws (logged) doesn't keep the rest from hearing it (the game going back to
    // the menu must not skip the Steam lobby's leave)
    internal static void RaiseEach(Action<string> listeners, string reason)
    {
        if (listeners == null)
            return;

        foreach (Delegate listener in listeners.GetInvocationList())
        {
            try
            {
                ((Action<string>)listener)(reason);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    // Host: spawns a network object on every machine (tied to this session's Netcode, which matters when several run in
    // one process, as in tests), for a seat (a player or scooter takes it as it spawns: SpawningSeat). Netcode destroys a
    // machine's objects when it leaves
    void Spawn(GameObject prefab, ulong owner, bool isPlayer, int seat)
    {
        SpawningSeat = seat;
        try
        {
            Network.SpawnManager.InstantiateAndSpawn(prefab.GetComponent<NetworkObject>(), owner, false, isPlayer);
        }
        finally
        {
            SpawningSeat = -1;
        }
    }

    // OnlinePlayer and OnlineMatch report here as they arrive and go
    internal void AddPlayer(OnlinePlayer player)
    {
        // A client takes no more players than the game has seats, and no second player in a seat: another machine's
        // host could spawn any number
        if (!Network.IsServer && (players.Count >= Constants.MAX_PLAYERS || players.Exists(other => other.Seat == player.Seat)))
        {
            WarnTooMany();
            return;
        }

        players.Add(player);
        PlayerSpawned?.Invoke(player);
    }

    internal void RemovePlayer(OnlinePlayer player)
    {
        if (players.Remove(player))
            PlayerDespawned?.Invoke(player);
    }

    internal void SetMatch(OnlineMatch match)
    {
        // One match per session: a second one from another machine's host is ignored
        if (Match != null && Match != match)
        {
            WarnTooMany();
            return;
        }

        Match = match;
        // Seats a machine asks for or gives back (the host), and the host's refusals (a client)
        match.SeatAsked += OnSeatAsked;
        match.SeatFreed += FreeSeatOf;
        match.SeatRefused += OnSeatRefused;
        MatchSpawned?.Invoke(match);
    }

    internal void ClearMatch(OnlineMatch match)
    {
        if (Match != match)
            return;

        match.SeatAsked -= OnSeatAsked;
        match.SeatFreed -= FreeSeatOf;
        match.SeatRefused -= OnSeatRefused;
        Match = null;
    }

    // OnlineScooter reports here as it arrives and goes
    internal void AddScooter(OnlineScooter scooter)
    {
        if (!Network.IsServer && scooters.Count >= Constants.MAX_PLAYERS)
        {
            WarnTooMany();
            return;
        }

        scooters.Add(scooter);
    }

    bool warnedTooMany;

    // Logged once per session: the host sent more than the game allows
    void WarnTooMany()
    {
        if (warnedTooMany)
            return;

        warnedTooMany = true;
        Debug.LogWarning("Online: the host sent more than the game allows; ignored.");
    }

    internal void RemoveScooter(OnlineScooter scooter)
    {
        scooters.Remove(scooter);
    }

    internal void SetRole(NetworkRole role)
    {
        bool changed = role != Role;
        Role = role;

        if (changed)
            RoleChanged?.Invoke(role);
    }
}

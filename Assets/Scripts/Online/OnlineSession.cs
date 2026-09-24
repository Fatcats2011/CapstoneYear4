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
/// This machine's online match: hosts one or joins one, lets players in by JoinRules, and keeps GameAuthority.Role in
/// step (Host or Client while the session runs, Offline once it ends). Builds its own Netcode NetworkManager and
/// transport, so no scene holds it. Nothing creates a session in a local match. See docs/online.md
/// </summary>
public class OnlineSession : MonoBehaviour
{
    /// <summary>The port direct (Unity Transport) sessions use</summary>
    public const ushort DIRECT_PORT = 7777;

    /// <summary>A direct join tries to reach the host once a second, this many times</summary>
    public const int DIRECT_CONNECT_ATTEMPTS = 10;

    /// <summary>Why a session ended, when the host gave no reason of its own</summary>
    public const string HOST_LEFT = "The host left the match.";
    public const string HOST_UNREACHABLE = "Couldn't reach the host.";
    public const string CONNECTION_LOST = "The connection was lost.";

    /// <summary>Logged when a Steam session can't start</summary>
    public const string NO_STEAM = "Online: Steam isn't running, so this machine can't host or join over Steam.";

    readonly HashSet<ulong> seats = new HashSet<ulong>(); // host: the players it let in, itself included
    bool reachedHost; // client: the host let this machine in
    bool leaving;     // Leave was called, so the end that follows is expected
#if !DISABLESTEAMWORKS
    SteamNetworkingSocketsTransport steam; // added the first time a Steam session starts
#endif

    /// <summary>Netcode's manager for this session</summary>
    public NetworkManager Network { get; private set; }

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

        NetworkManager network = holder.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig // a NetworkManager added from code has no config
        {
            NetworkTransport = session.Direct,
            ConnectionApproval = true,
            EnableSceneManagement = false, // scene loads stay local until the networked scene flow (roadmap Task 3.4)
        };
        network.ConnectionApprovalCallback = session.Approve;
        network.OnClientConnectedCallback += session.OnConnected;
        network.OnClientDisconnectCallback += session.OnDisconnected;
        network.OnClientStopped += session.OnClientStopped;
        network.OnServerStopped += session.OnServerStopped;
        session.Network = network;
        return session;
    }

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
            steam = gameObject.AddComponent<SteamNetworkingSocketsTransport>();
        Network.NetworkConfig.NetworkTransport = steam;
        return true;
    }
#endif

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

    bool StartHost()
    {
        seats.Clear(); // the host takes the first seat while Netcode starts
        if (!Network.StartHost())
            return false;

        IsRunning = true;
        SetRole(NetworkRole.Host);
        Debug.Log("Online: hosting version " + Version);
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
            : JoinRules.Refusal(Version, ReadVersion(request.Payload), seats.Count, HostState());

        response.Approved = refusal == null;
        response.Reason = refusal;
        response.CreatePlayerObject = false; // the match spawns the scooters (roadmap Task 3.3)

        if (response.Approved)
            seats.Add(request.ClientNetworkId); // counted now: Netcode lists the player later in the frame
        else
            Debug.Log("Online: turned a player away: " + refusal);
    }

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
                Debug.Log("Online: player " + clientId + " joined (" + seats.Count + " in)");
            return;
        }

        reachedHost = true;
        SetRole(NetworkRole.Client);
        Debug.Log("Online: joined the host");
    }

    // Host: a player left, so their seat is free (the host's own client only goes when the host stops: End clears it)
    void OnDisconnected(ulong clientId)
    {
        if (Network.IsServer && clientId != NetworkManager.ServerClientId && seats.Remove(clientId))
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
        Ended?.Invoke(reason);
    }

    void SetRole(NetworkRole role)
    {
        Role = role;
        GameAuthority.Role = role;
    }
}

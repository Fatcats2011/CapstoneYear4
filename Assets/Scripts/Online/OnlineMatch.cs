using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The host's match, on every machine:
/// - The host's game state. The host sends each state it switches to, in order, and keeps the latest for players who
///   join later. States go one by one because the game sometimes switches twice in a frame (the opening cutscene hands
///   over to the main loop at once), and a shared value would only send the second.
/// - The scene flow's messages (IMatchLink). The host asks every client to load a match scene, show it, or go back to
///   the menu, and each client says when it has a scene loaded (OnlineSceneFlow).
/// - The host's match clock (MatchClock).
/// - The host's order changes, clients' drop requests and the golden order's value (OnlineOrders).
/// - Clients' tutorial reports and cutout requests (OnlineTutorial).
/// - Steals, clashes and respawn points: clients' requests and the host's answers (OnlineSteals, OnlineRespawns).
/// - One-shots: players' (clients report theirs, and the host sends every one to every client) and the host's clock's
///   (OnlineCues).
/// </summary>
public class OnlineMatch : NetworkBehaviour, IMatchLink
{
    readonly NetworkVariable<GameState> state = new NetworkVariable<GameState>(GameState.Default);
    readonly NetworkVariable<double> clockEnd = new NetworkVariable<double>(0);
    readonly NetworkVariable<bool> clockStarted = new NetworkVariable<bool>(false);
    readonly NetworkVariable<bool> finalOrder = new NetworkVariable<bool>(false);
    readonly NetworkVariable<int> goldenValue = new NetworkVariable<int>((int)Constants.OrderValue.Golden);

    OnlineSession session;

    /// <summary>The host's latest state (Default until it sends one)</summary>
    public GameState State { get { return state.Value; } }

    /// <summary>When the host's match clock runs out, in Netcode's server time (MatchClock)</summary>
    public double ClockEnd { get { return clockEnd.Value; } }

    /// <summary>Whether the host's waves have started</summary>
    public bool ClockStarted { get { return clockStarted.Value; } }

    /// <summary>Whether the host is in the golden round</summary>
    public bool FinalOrder { get { return finalOrder.Value; } }

    /// <summary>What the golden order is worth on the host: it grows while the golden round goes on</summary>
    public int GoldenValue { get { return goldenValue.Value; } }

    /// <summary>Clients: each change the host made to an order, in order with its states</summary>
    public event Action<OrderChange> OrderReceived;

    /// <summary>Host: a client asks it to drop what its player holds (the client's id, the player's seat, spot1, spot2, spinOut)</summary>
    public event Action<ulong, int, Vector3, Vector3, bool> DropAsked;

    /// <summary>Host: a client's player finished the tutorial (the client's id, the player's seat)</summary>
    public event Action<ulong, int> MachineLearnt;

    /// <summary>Host: a client's player boosted into a cutout (the client's id, the seat of the cutout's lane)</summary>
    public event Action<ulong, int> CutoutAsked;

    /// <summary>Host: a client's player, boosting, hit another (the client's id, the attacker's seat, the victim's seat)</summary>
    public event Action<ulong, int, int> StealAsked;

    /// <summary>Clients: a steal or clash the host decided</summary>
    public event Action<PlayerHit> HitReceived;

    /// <summary>Host: a client's player fell in the water (the client's id, their seat, where they were last on the ground)</summary>
    public event Action<ulong, int, Vector3> RespawnAsked;

    /// <summary>Clients: where the host says a player rises (their seat, the respawn point's index)</summary>
    public event Action<int, int> RespawnReceived;

    /// <summary>Host: a client's player made a one-shot (the client's id, the player's seat, the cue)</summary>
    public event Action<ulong, int, ScooterCue> CueReported;

    /// <summary>Clients: a player's one-shot, from the host (their seat, the cue)</summary>
    public event Action<int, ScooterCue> CueReceived;

    /// <summary>Clients: the host's match clock rang</summary>
    public event Action<ClockCue> ClockRang;

    /// <summary>Clients: each state the host switches to, in order</summary>
    public event Action<GameState> StateReceived;

    /// <summary>Clients: the host wants a match scene loaded, held behind the loading screen</summary>
    public event Action<MatchScene> LoadRequested;

    /// <summary>Clients: every machine has the scene loaded: show it</summary>
    public event Action<MatchScene> ShowRequested;

    /// <summary>Clients: the host is taking everyone back to the menu</summary>
    public event Action ReturnRequested;

    /// <summary>Host: a client has a scene loaded (its Netcode client id)</summary>
    public event Action<ulong, MatchScene> MachineLoaded;

    public override void OnNetworkSpawn()
    {
        session = NetworkManager.GetComponent<OnlineSession>();
        DontDestroyOnLoad(gameObject);

        if (session != null)
            session.SetMatch(this);
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
            session.ClearMatch(this);
    }

    /// <summary>
    /// Host: tells every client the host switched to a state. Does nothing on a client
    /// </summary>
    public void SendState(GameState newState)
    {
        if (!IsServer)
            return;

        state.Value = newState;
        StateClientRpc(newState);
    }

    /// <summary>
    /// Host: shares its match clock (only what changed goes out). Does nothing on a client
    /// </summary>
    public void ShareClock(double end, bool started, bool final)
    {
        if (!IsServer)
            return;

        if (clockEnd.Value != end)
            clockEnd.Value = end;
        if (clockStarted.Value != started)
            clockStarted.Value = started;
        if (finalOrder.Value != final)
            finalOrder.Value = final;
    }

    /// <summary>Host: asks every client to load a match scene. Does nothing on a client</summary>
    public void RequestLoad(MatchScene scene)
    {
        if (IsServer)
            LoadClientRpc(scene);
    }

    /// <summary>Host: asks every client to show the scene it loaded. Does nothing on a client</summary>
    public void RequestShow(MatchScene scene)
    {
        if (IsServer)
            ShowClientRpc(scene);
    }

    /// <summary>Host: takes every client back to the menu. Does nothing on a client</summary>
    public void RequestReturn()
    {
        if (IsServer)
            ReturnClientRpc();
    }

    /// <summary>Client: tells the host it has a scene loaded. Does nothing on the host, which counts itself</summary>
    public void ReportLoaded(MatchScene scene)
    {
        if (!IsServer)
            LoadedServerRpc(scene);
    }

    /// <summary>Host: tells every client about a change it made to an order. Does nothing on a client</summary>
    public void SendOrder(OrderChange change)
    {
        if (IsServer)
            OrderClientRpc(change);
    }

    /// <summary>
    /// Client: asks the host to drop what its player in a seat holds, onto these spots. Does nothing on the host, which
    /// drops its own at once
    /// </summary>
    public void AskDrop(int seat, Vector3 spot1, Vector3 spot2, bool spinOut)
    {
        if (!IsServer)
            DropServerRpc(seat, spot1, spot2, spinOut);
    }

    /// <summary>Client: tells the host its player in a seat finished the tutorial. Does nothing on the host, which counts its own</summary>
    public void ReportLearnt(int seat)
    {
        if (!IsServer)
            LearntServerRpc(seat);
    }

    /// <summary>
    /// Client: asks the host for the order of the cutout its player boosted into (the seat of the cutout's lane). Does
    /// nothing on the host, whose player steals at once
    /// </summary>
    public void AskCutout(int seat)
    {
        if (!IsServer)
            CutoutServerRpc(seat);
    }

    /// <summary>
    /// Client: asks the host about its player (the attacker's seat) boosting into another (the victim's seat). Does
    /// nothing on the host, which judges its own player's at once
    /// </summary>
    public void AskSteal(int attacker, int victim)
    {
        if (!IsServer)
            StealServerRpc(attacker, victim);
    }

    /// <summary>Host: tells every client about a steal or clash it decided. Does nothing on a client</summary>
    public void SendHit(PlayerHit hit)
    {
        if (IsServer)
            HitClientRpc(hit);
    }

    /// <summary>
    /// Client: asks the host where its player in a seat rises (they fell in the water, last on the ground there). Does
    /// nothing on the host, which picks its own player's point at once
    /// </summary>
    public void AskRespawn(int seat, Vector3 lastGrounded)
    {
        if (!IsServer)
            RespawnServerRpc(seat, lastGrounded);
    }

    /// <summary>Host: tells every client where the player in a seat rises (a respawn point's index). Does nothing on a client</summary>
    public void SendRespawn(int seat, int point)
    {
        if (IsServer)
            RespawnClientRpc(seat, point);
    }

    /// <summary>
    /// Client: tells the host its player in a seat made a one-shot. Does nothing on the host, which sends its own player's
    /// at once
    /// </summary>
    public void ReportCue(int seat, ScooterCue cue)
    {
        if (!IsServer)
            CueServerRpc(seat, cue);
    }

    /// <summary>Host: tells every client the player in a seat made a one-shot. Does nothing on a client</summary>
    public void SendCue(int seat, ScooterCue cue)
    {
        if (IsServer)
            CueClientRpc(seat, cue);
    }

    /// <summary>Host: tells every client its match clock rang. Does nothing on a client</summary>
    public void RingClock(ClockCue cue)
    {
        if (IsServer)
            ClockClientRpc(cue);
    }

    /// <summary>Host: shares what the golden order is worth (only a change goes out). Does nothing on a client</summary>
    public void ShareGoldenValue(int value)
    {
        if (IsServer && goldenValue.Value != value)
            goldenValue.Value = value;
    }

    // The host is a client too: it skips its own messages below, as it acted on them already

    [ClientRpc]
    void StateClientRpc(GameState newState)
    {
        if (IsServer)
            return;

        StateReceived?.Invoke(newState);
    }

    [ClientRpc]
    void LoadClientRpc(MatchScene scene)
    {
        if (!IsServer)
            LoadRequested?.Invoke(scene);
    }

    [ClientRpc]
    void ShowClientRpc(MatchScene scene)
    {
        if (!IsServer)
            ShowRequested?.Invoke(scene);
    }

    [ClientRpc]
    void ReturnClientRpc()
    {
        if (!IsServer)
            ReturnRequested?.Invoke();
    }

    [ClientRpc]
    void OrderClientRpc(OrderChange change)
    {
        if (!IsServer)
            OrderReceived?.Invoke(change);
    }

    [ClientRpc]
    void HitClientRpc(PlayerHit hit)
    {
        if (!IsServer)
            HitReceived?.Invoke(hit);
    }

    [ClientRpc]
    void RespawnClientRpc(int seat, int point)
    {
        if (!IsServer)
            RespawnReceived?.Invoke(seat, point);
    }

    [ClientRpc]
    void CueClientRpc(int seat, ScooterCue cue)
    {
        if (!IsServer)
            CueReceived?.Invoke(seat, cue);
    }

    [ClientRpc]
    void ClockClientRpc(ClockCue cue)
    {
        if (!IsServer)
            ClockRang?.Invoke(cue);
    }

    [ServerRpc(RequireOwnership = false)]
    void LoadedServerRpc(MatchScene scene, ServerRpcParams rpc = default)
    {
        MachineLoaded?.Invoke(rpc.Receive.SenderClientId, scene);
    }

    [ServerRpc(RequireOwnership = false)]
    void DropServerRpc(int seat, Vector3 spot1, Vector3 spot2, bool spinOut, ServerRpcParams rpc = default)
    {
        DropAsked?.Invoke(rpc.Receive.SenderClientId, seat, spot1, spot2, spinOut);
    }

    [ServerRpc(RequireOwnership = false)]
    void LearntServerRpc(int seat, ServerRpcParams rpc = default)
    {
        MachineLearnt?.Invoke(rpc.Receive.SenderClientId, seat);
    }

    [ServerRpc(RequireOwnership = false)]
    void CutoutServerRpc(int seat, ServerRpcParams rpc = default)
    {
        CutoutAsked?.Invoke(rpc.Receive.SenderClientId, seat);
    }

    [ServerRpc(RequireOwnership = false)]
    void StealServerRpc(int attacker, int victim, ServerRpcParams rpc = default)
    {
        StealAsked?.Invoke(rpc.Receive.SenderClientId, attacker, victim);
    }

    [ServerRpc(RequireOwnership = false)]
    void RespawnServerRpc(int seat, Vector3 lastGrounded, ServerRpcParams rpc = default)
    {
        RespawnAsked?.Invoke(rpc.Receive.SenderClientId, seat, lastGrounded);
    }

    [ServerRpc(RequireOwnership = false)]
    void CueServerRpc(int seat, ScooterCue cue, ServerRpcParams rpc = default)
    {
        CueReported?.Invoke(rpc.Receive.SenderClientId, seat, cue);
    }
}

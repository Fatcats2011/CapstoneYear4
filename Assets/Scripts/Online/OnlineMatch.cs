using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// The host's game state, on every machine. The host sends each state it switches to, in order, and keeps the latest
/// for players who join later. States go one by one because the game sometimes switches twice in a frame (the opening
/// cutscene hands over to the main loop at once), and a shared value would only send the second
/// </summary>
public class OnlineMatch : NetworkBehaviour
{
    readonly NetworkVariable<GameState> state = new NetworkVariable<GameState>(GameState.Default);

    OnlineSession session;

    /// <summary>The host's latest state (Default until it sends one)</summary>
    public GameState State { get { return state.Value; } }

    /// <summary>Clients: each state the host switches to, in order</summary>
    public event Action<GameState> StateReceived;

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

    [ClientRpc]
    void StateClientRpc(GameState newState)
    {
        if (IsServer)
            return; // the host is a client too, and switched already

        StateReceived?.Invoke(newState);
    }
}

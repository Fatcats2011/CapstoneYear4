using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Each machine's connection, online. Every PingRules.EVERY seconds:
/// - Host: measures each seated machine's round trip (its own seats are 0) and shares them (OnlineMatch.SharePings).
/// - Client: keeps the host's.
/// Every machine keeps the seats' names beside them (PingSync), for the pause menu's list. OnlineGame adds it next to
/// OnlineAwards. See docs/online.md (Connection info)
/// </summary>
public class OnlinePings : MonoBehaviour
{
    OnlineSession session;
    OnlineMatch listening; // the match whose round trips this machine hears
    float sinceLast = PingRules.EVERY;

    /// <summary>Starts sharing this session's round trips</summary>
    public void Begin(OnlineSession onlineSession)
    {
        session = onlineSession;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // PingSync is static: an ended session leaves no round trips behind
    void OnDestroy()
    {
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
        PingSync.Reset();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.PingsReceived += Keep;
    }

    void StopListening()
    {
        if (listening != null)
            listening.PingsReceived -= Keep;
        listening = null;
    }

    void Update()
    {
        sinceLast += Time.unscaledDeltaTime;
        OnlineMatch match = session != null ? session.Match : null;
        if (match == null || !PingRules.Due(match.IsServer, sinceLast))
            return;
        sinceLast = 0f;

        NetworkManager network = NetworkManager.Singleton;
        PingBoard board = PingBoard.Unknown;
        foreach (OnlinePlayer player in session.Players)
        {
            if (player == null || player.Seat < 0)
                continue;
            ulong machine = player.OwnerClientId;
            if (machine == NetworkManager.ServerClientId)
                board.Set(player.Seat, 0);
            // Only a connected machine (the transport logs an error for any other), asked by its transport id
            else if (network != null && network.ConnectedClients.ContainsKey(machine))
            {
                ulong transportId = network.GetTransportIdFromClientId(machine);
                if (transportId != ulong.MaxValue)
                    board.Set(player.Seat, (long)network.NetworkConfig.NetworkTransport.GetCurrentRtt(transportId));
            }
        }
        Keep(board);
        match.SharePings(board);
    }

    // The round trips, with the seats' names as this machine knows them
    void Keep(PingBoard board)
    {
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
            PingSync.Names[seat] = "";
        foreach (OnlinePlayer player in session.Players)
        {
            if (player != null && player.Seat >= 0 && player.Seat < Constants.MAX_PLAYERS)
                PingSync.Names[player.Seat] = player.Name;
        }
        PingSync.Arrive(board);
    }
}

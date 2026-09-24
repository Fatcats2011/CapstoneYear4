using Unity.Netcode;
using UnityEngine;

/// <summary>
/// One player in an online match, on every machine: their seat, which the host gives, and what they picked in player
/// select (ghost colour, hat, ready), which only their own machine may change. The host spawns one per machine.
/// OnlineGame shows another machine's player as a scooter, and keeps this machine's player's choices up to date.
/// See docs/online.md
/// </summary>
public class OnlinePlayer : NetworkBehaviour
{
    readonly NetworkVariable<int> seat = new NetworkVariable<int>(-1);
    readonly NetworkVariable<int> colour = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<int> hat = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    readonly NetworkVariable<bool> ready = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    OnlineSession session;

    /// <summary>Their seat, 0-3: their slot on every machine (-1 until the host gives one)</summary>
    public int Seat { get { return seat.Value; } }

    /// <summary>Their ghost colour: an index into player select's colours</summary>
    public int Colour { get { return colour.Value; } }

    /// <summary>Their hat: an index into player select's hats</summary>
    public int Hat { get { return hat.Value; } }

    /// <summary>Whether they're ready in player select</summary>
    public bool Ready { get { return ready.Value; } }

    public override void OnNetworkSpawn()
    {
        session = NetworkManager.GetComponent<OnlineSession>();

        // The host seats the player as it spawns them: a value set here goes out with the spawn itself
        if (IsServer && session != null)
            seat.Value = session.SeatOf(OwnerClientId);

        DontDestroyOnLoad(gameObject);

        if (session != null)
            session.AddPlayer(this);
    }

    public override void OnNetworkDespawn()
    {
        if (session != null)
            session.RemovePlayer(this);
    }

    /// <summary>
    /// This machine's player: shares what they picked with every machine. Does nothing for another machine's player
    /// </summary>
    public void Share(int chosenColour, int chosenHat, bool isReady)
    {
        if (!IsOwner)
            return;

        if (colour.Value != chosenColour)
            colour.Value = chosenColour;
        if (hat.Value != chosenHat)
            hat.Value = chosenHat;
        if (ready.Value != isReady)
            ready.Value = isReady;
    }
}

using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// A taken player slot. The slot number decides the player's layers, renderers, company and split-screen order
/// </summary>
public class PlayerSlot
{
    public PlayerSlot(int index, GameObject player, PlayerInput input, ulong ownerClientId)
    {
        Index = index;
        Player = player;
        Input = input;
        OwnerClientId = ownerClientId;
    }

    /// <summary>
    /// Slot number, 0-3 (player 1 is slot 0)
    /// </summary>
    public int Index { get; }

    /// <summary>
    /// The player's top object: their scooter and orders, plus cameras and menus for a player on this machine
    /// </summary>
    public GameObject Player { get; }

    /// <summary>
    /// Controls of a player on this machine (their PlayerInput also holds their camera and menus); null for an online player on another machine
    /// </summary>
    public PlayerInput Input { get; }

    /// <summary>
    /// Plays on this machine: has a controller, a split-screen view and menus here
    /// </summary>
    public bool IsLocal => Input != null;

    /// <summary>
    /// The online client that owns this player (0 when playing offline)
    /// </summary>
    public ulong OwnerClientId { get; }

    /// <summary>
    /// The delivery company this slot plays for
    /// </summary>
    public CompanyInformation Company { get; set; }
}

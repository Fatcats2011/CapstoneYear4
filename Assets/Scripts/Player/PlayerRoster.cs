using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Who is in each of the 4 player slots: the one list of players every system reads. A player who joins takes the
/// lowest free slot, and leaving frees it. Offline every player is local (controller, split-screen view and menus on
/// this machine); online players on other machines are remote
/// </summary>
public class PlayerRoster
{
    readonly PlayerSlot[] slots = new PlayerSlot[Constants.MAX_PLAYERS];

    /// <summary>
    /// How many slots are taken
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// The player in a slot (0-3), or null when the slot is free
    /// </summary>
    public PlayerSlot this[int index] => slots[index];

    /// <summary>
    /// Every player, in slot order
    /// </summary>
    public IEnumerable<PlayerSlot> Players
    {
        get
        {
            foreach (PlayerSlot slot in slots)
            {
                if (slot != null)
                    yield return slot;
            }
        }
    }

    /// <summary>
    /// Players on this machine, in slot order (the order of the split-screen views)
    /// </summary>
    public IEnumerable<PlayerSlot> LocalPlayers
    {
        get
        {
            foreach (PlayerSlot slot in slots)
            {
                if (slot != null && slot.IsLocal)
                    yield return slot;
            }
        }
    }

    /// <summary>
    /// How many players are on this machine
    /// </summary>
    public int LocalCount
    {
        get
        {
            int count = 0;
            foreach (PlayerSlot slot in LocalPlayers)
                count++;
            return count;
        }
    }

    ///<summary>
    /// Puts a player on this machine in the lowest free slot. Returns their slot, or null when all 4 are taken or they already have one
    ///</summary>
    public PlayerSlot JoinLocal(PlayerInput input)
    {
        int index = FirstFreeSlot();
        if (input == null || index < 0 || IndexOf(input) >= 0)
            return null;

        return Take(new PlayerSlot(index, input.gameObject, input, 0));
    }

    ///<summary>
    /// Puts an online player from another machine in the lowest free slot. Returns their slot, or null when all 4 are taken or they already have one
    ///</summary>
    public PlayerSlot JoinRemote(GameObject player, ulong ownerClientId)
    {
        int index = FirstFreeSlot();
        if (player == null || index < 0 || SlotOfPlayer(player) >= 0)
            return null;

        return Take(new PlayerSlot(index, player, null, ownerClientId));
    }

    ///<summary>
    /// Frees the slot of a player on this machine. Returns the freed slot, or -1 if they weren't in the roster
    ///</summary>
    public int Leave(PlayerInput input)
    {
        int index = IndexOf(input);
        if (index >= 0)
        {
            slots[index] = null;
            Count--;
        }
        return index;
    }

    ///<summary>
    /// The slot of a player on this machine, or -1 if they aren't in the roster
    ///</summary>
    public int IndexOf(PlayerInput input)
    {
        if (input == null)
            return -1;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].Input == input)
                return i;
        }
        return -1;
    }

    ///<summary>
    /// Frees every slot
    ///</summary>
    public void Clear()
    {
        for (int i = 0; i < slots.Length; i++)
            slots[i] = null;
        Count = 0;
    }

    int FirstFreeSlot()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                return i;
        }
        return -1;
    }

    int SlotOfPlayer(GameObject player)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].Player == player)
                return i;
        }
        return -1;
    }

    PlayerSlot Take(PlayerSlot slot)
    {
        slots[slot.Index] = slot;
        Count++;
        return slot;
    }
}

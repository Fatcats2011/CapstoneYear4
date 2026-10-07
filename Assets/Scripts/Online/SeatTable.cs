using System.Collections.Generic;

/// <summary>
/// Who sits in each of an online match's 4 seats, by Netcode client id. The host takes seat 0, and each new seat goes to
/// the lowest free one. A machine can hold several seats: players sharing its screen (Phase 3J). A seat is that player's
/// slot on every machine: their company, podium, spawn point and place in the results
/// </summary>
public class SeatTable
{
    readonly ulong[] clients = new ulong[Constants.MAX_PLAYERS];
    readonly bool[] taken = new bool[Constants.MAX_PLAYERS];

    /// <summary>How many seats are taken</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Gives a machine another seat, the lowest free one. Returns the seat, or -1 when every seat is taken
    /// </summary>
    public int Take(ulong clientId)
    {
        for (int i = 0; i < taken.Length; i++)
        {
            if (!taken[i])
            {
                taken[i] = true;
                clients[i] = clientId;
                Count++;
                return i;
            }
        }
        return -1;
    }

    /// <summary>Frees one seat, if that machine holds it. Returns whether it did</summary>
    public bool Free(ulong clientId, int seat)
    {
        if (!Owns(clientId, seat))
            return false;

        taken[seat] = false;
        Count--;
        return true;
    }

    /// <summary>Frees every seat a machine holds (it left). Returns how many</summary>
    public int FreeAll(ulong clientId)
    {
        int freed = 0;
        for (int i = 0; i < taken.Length; i++)
        {
            if (Free(clientId, i))
                freed++;
        }
        return freed;
    }

    /// <summary>Whether a machine holds a seat (false for a number that isn't a seat)</summary>
    public bool Owns(ulong clientId, int seat)
    {
        return seat >= 0 && seat < taken.Length && taken[seat] && clients[seat] == clientId;
    }

    /// <summary>How many seats a machine holds</summary>
    public int CountOf(ulong clientId)
    {
        int count = 0;
        for (int i = 0; i < taken.Length; i++)
        {
            if (Owns(clientId, i))
                count++;
        }
        return count;
    }

    /// <summary>The seats a machine holds, lowest first</summary>
    public List<int> SeatsOf(ulong clientId)
    {
        List<int> seats = new List<int>();
        for (int i = 0; i < taken.Length; i++)
        {
            if (Owns(clientId, i))
                seats.Add(i);
        }
        return seats;
    }

    /// <summary>Frees every seat</summary>
    public void Clear()
    {
        for (int i = 0; i < taken.Length; i++)
            taken[i] = false;
        Count = 0;
    }
}

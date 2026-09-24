/// <summary>
/// Who sits in each of an online match's 4 seats, by Netcode client id. The host takes seat 0 and each joiner the
/// lowest free seat. A seat is that player's slot on every machine: their company, podium, spawn point and place in
/// the results
/// </summary>
public class SeatTable
{
    readonly ulong[] clients = new ulong[Constants.MAX_PLAYERS];
    readonly bool[] taken = new bool[Constants.MAX_PLAYERS];

    /// <summary>How many seats are taken</summary>
    public int Count { get; private set; }

    /// <summary>
    /// Seats a player in the lowest free seat. Returns their seat (the one they have if they're seated already), or -1
    /// when every seat is taken
    /// </summary>
    public int Take(ulong clientId)
    {
        int seat = SeatOf(clientId);
        if (seat >= 0)
            return seat;

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

    /// <summary>Frees a player's seat. Returns the seat, or -1 if they had none</summary>
    public int Free(ulong clientId)
    {
        int seat = SeatOf(clientId);
        if (seat >= 0)
        {
            taken[seat] = false;
            Count--;
        }
        return seat;
    }

    /// <summary>A player's seat, or -1 if they have none</summary>
    public int SeatOf(ulong clientId)
    {
        for (int i = 0; i < taken.Length; i++)
        {
            if (taken[i] && clients[i] == clientId)
                return i;
        }
        return -1;
    }

    /// <summary>Frees every seat</summary>
    public void Clear()
    {
        for (int i = 0; i < taken.Length; i++)
            taken[i] = false;
        Count = 0;
    }
}

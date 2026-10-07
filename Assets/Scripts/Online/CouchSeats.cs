using System.Collections.Generic;

/// <summary>
/// Online with several players on one machine (Phase 3J): which of the seats this machine holds each of its players moves
/// to. A player already in one of them stays. The others take the remaining ones lowest first, the menu player (listed
/// first) before the rest. A player with no seat left waits unseated where they are, while the game asks the host for
/// one (OnlineGame). PlayerInstantiate moves them. See docs/online.md
/// </summary>
public static class CouchSeats
{
    /// <summary>
    /// Each player's seat, in the order given (-1: unseated)
    /// </summary>
    /// <param name="localSlots">This machine's players' slots now, the menu player first</param>
    /// <param name="heldSeats">The seats this machine holds</param>
    public static int[] Plan(IReadOnlyList<int> localSlots, IReadOnlyCollection<int> heldSeats)
    {
        int[] targets = new int[localSlots.Count];
        List<int> free = new List<int>(heldSeats);
        free.Sort();

        // Players already in a held seat keep it
        for (int i = 0; i < localSlots.Count; i++)
        {
            targets[i] = free.Remove(localSlots[i]) ? localSlots[i] : -1;
        }

        // The rest take what's left, in order
        for (int i = 0; i < localSlots.Count; i++)
        {
            if (targets[i] >= 0 || free.Count == 0)
                continue;

            targets[i] = free[0];
            free.RemoveAt(0);
        }
        return targets;
    }
}

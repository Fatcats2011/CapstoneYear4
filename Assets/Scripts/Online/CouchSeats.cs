using System.Collections.Generic;

/// <summary>
/// Online with several players on one machine (Phase 3J): which of the seats this machine holds each of its players moves
/// to. The menu player (listed first) always has one while the machine holds any: their own slot if it's held, else the
/// lowest free held seat, else the lowest one another player stands in (who then waits: a machine's menus need their
/// player). The others keep a held seat they stand in, then take what's left, lowest first. A player with no seat waits
/// unseated while the game asks the host for one (OnlineGame). PlayerInstantiate moves them. See docs/online.md
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
        for (int i = 0; i < targets.Length; i++)
            targets[i] = -1;

        List<int> free = new List<int>(heldSeats);
        free.Sort();
        if (localSlots.Count == 0 || free.Count == 0)
            return targets;

        // The menu player first: their own slot if held, else the lowest held seat nobody else here stands in, else the
        // lowest held seat (whoever stands there waits)
        int menuSeat = free.Contains(localSlots[0]) ? localSlots[0] : -1;
        for (int s = 0; menuSeat < 0 && s < free.Count; s++)
        {
            if (!Stands(localSlots, free[s]))
                menuSeat = free[s];
        }
        if (menuSeat < 0)
            menuSeat = free[0];
        targets[0] = menuSeat;
        free.Remove(menuSeat);

        // Players standing in a held seat keep it
        for (int i = 1; i < localSlots.Count; i++)
        {
            if (free.Remove(localSlots[i]))
                targets[i] = localSlots[i];
        }

        // The rest take what's left, in order
        for (int i = 1; i < localSlots.Count && free.Count > 0; i++)
        {
            if (targets[i] >= 0)
                continue;

            targets[i] = free[0];
            free.RemoveAt(0);
        }
        return targets;
    }

    // Whether a player other than the menu player stands in a slot
    static bool Stands(IReadOnlyList<int> localSlots, int slot)
    {
        for (int i = 1; i < localSlots.Count; i++)
        {
            if (localSlots[i] == slot)
                return true;
        }
        return false;
    }
}

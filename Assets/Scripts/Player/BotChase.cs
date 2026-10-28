using System.Collections.Generic;

/// <summary>
/// A CPU driver's chase of one victim: over its time limit it gives up, and leaves that victim alone for FORGET_VICTIM
/// seconds, so a thief doesn't hound one player forever
/// </summary>
public class BotChase
{
    public const float FORGET_VICTIM = 6f;

    readonly Dictionary<int, float> barred = new Dictionary<int, float>();
    readonly List<int> seats = new List<int>();
    int victim = -1;
    float chased;

    /// <summary>A frame of chasing a victim's seat (-1: no chase) with this limit: whether the chase is over now</summary>
    public bool Tick(int victimSeat, float limitSeconds, float deltaTime)
    {
        if (victimSeat != victim)
        {
            victim = victimSeat;
            chased = 0f;
        }
        if (victimSeat < 0)
            return false;

        chased += deltaTime;
        if (chased < limitSeconds)
            return false;
        barred[victimSeat] = FORGET_VICTIM;
        victim = -1;
        chased = 0f;
        return true;
    }

    /// <summary>Whether this seat was given up on lately</summary>
    public bool Barred(int seat)
    {
        return barred.ContainsKey(seat);
    }

    /// <summary>Time goes by: the given-up victims' cool-offs run down (once a frame)</summary>
    public void Advance(float deltaTime)
    {
        seats.Clear();
        seats.AddRange(barred.Keys);
        foreach (int seat in seats)
        {
            float left = barred[seat] - deltaTime;
            if (left <= 0f)
                barred.Remove(seat);
            else
                barred[seat] = left;
        }
    }
}

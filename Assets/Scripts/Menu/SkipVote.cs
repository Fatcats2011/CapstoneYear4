using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The vote to skip a cutscene: once every player still in has voted, it ends. A vote can't be taken back (the
/// cutscene is short). CutsceneSkip runs it on the machine that decides (the host online)
/// </summary>
public class SkipVote
{
    readonly HashSet<int> seats;
    readonly HashSet<int> voted = new HashSet<int>();

    public SkipVote(IEnumerable<int> seats)
    {
        this.seats = new HashSet<int>(seats);
    }

    /// <summary>A seat votes to skip. False: it isn't in, or voted already</summary>
    public bool Vote(int seat)
    {
        return seats.Contains(seat) && voted.Add(seat);
    }

    /// <summary>A seat leaves (its machine left): nobody waits for it</summary>
    public void Leave(int seat)
    {
        seats.Remove(seat);
        voted.Remove(seat);
    }

    public bool IsIn(int seat) { return seats.Contains(seat); }

    public bool HasVoted(int seat) { return voted.Contains(seat); }

    public int Voted { get { return voted.Count; } }

    public int Total { get { return seats.Count; } }

    /// <summary>Everyone still in has voted (and someone is in)</summary>
    public bool Everyone { get { return seats.Count > 0 && seats.All(voted.Contains); } }
}

/// <summary>
/// One player's hold on A to skip a cutscene: half a second (HOLD) counts once. The hold only starts from a fresh press
/// after the prompt shows (SHOW_AFTER into the cutscene), so an A still held from the loading screen never skips, and a
/// tap lets go before it counts
/// </summary>
public class SkipHold
{
    public const float HOLD = 0.5f;
    public const float SHOW_AFTER = 1f;

    bool armed; // the button was seen up while the prompt showed
    float held;
    bool done;

    /// <summary>One frame of the player's A: true once, the frame the hold completes</summary>
    public bool Tick(bool pressed, float deltaTime, bool promptShown)
    {
        if (done || !promptShown)
        {
            held = 0;
            return false;
        }
        if (!pressed)
        {
            armed = true;
            held = 0;
            return false;
        }
        if (!armed)
            return false;

        held += deltaTime;
        if (held < HOLD - 0.0001f)
            return false;
        done = true;
        return true;
    }
}

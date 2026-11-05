using System.Collections.Generic;

/// <summary>
/// The numbers and pure rules of the modes without deliveries (Kings Crown, Infection, Hot Potato): every number is here,
/// to tune in playtests. ModeMatch runs them on the authority. See docs/game-modes-ideas.md
/// </summary>
public static class TimedModeRules
{
    // Kings Crown
    /// <summary>Points a second for holding the crown</summary>
    public const float CROWN_POINTS_PER_SECOND = 10f;
    /// <summary>How close (m) a ball must come to the crown lying on the road to take it</summary>
    public const float CROWN_PICKUP_REACH = 5f;
    /// <summary>Seconds a new holder can't lose the crown (no instant steal-backs)</summary>
    public const float CROWN_SAFE_SECONDS = 2f;

    // Infection
    /// <summary>Points a second for every thief still free</summary>
    public const float THIEF_POINTS_PER_SECOND = 5f;
    /// <summary>Points to a hunter for each catch</summary>
    public const int CATCH_POINTS = 50;

    // Hot Potato
    /// <summary>The fuse's length in seconds is random in this range; its seconds left count down on the package and the HUD's clock</summary>
    public const float FUSE_MIN = 15f, FUSE_MAX = 25f;
    /// <summary>The round plays until one player is left; this only guards against a round that never ends (four fuses are far shorter)</summary>
    public const float POTATO_ROUND_LIMIT = 600f;
    /// <summary>Seconds before the first potato appears, and before the next one after an explosion</summary>
    public const float POTATO_DELAY = 3f;
    /// <summary>Seconds after a pass the new holder can't pass it straight back</summary>
    public const float NO_TAG_BACKS = 1.5f;
    /// <summary>Points a second for every player still in</summary>
    public const float SURVIVOR_POINTS_PER_SECOND = 10f;

    /// <summary>Whether a mode is played on the round timer, with ModeMatch's objectives instead of deliveries</summary>
    public static bool IsTimed(GameMode mode) => !ModeRules.HasDeliveries(mode);

    /// <summary>The seats in a mask (bit n = seat n)</summary>
    public static IEnumerable<int> Seats(int mask)
    {
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
            if ((mask & 1 << seat) != 0)
                yield return seat;
    }

    public static int Count(int mask)
    {
        int count = 0;
        foreach (int _ in Seats(mask))
            count++;
        return count;
    }

    /// <summary>Hot Potato: how far along the fuse is, 0 lit to 1 exploding, for the glow and the ticking</summary>
    public static float FuseProgress(float now, float lit, float explodes)
    {
        if (explodes <= lit)
            return 1f;
        float t = (now - lit) / (explodes - lit);
        return t < 0f ? 0f : t > 1f ? 1f : t;
    }

    /// <summary>Hot Potato: the fuse's whole seconds left as shown, counting down to 1 and then 0 as it goes off</summary>
    public static int FuseSeconds(float now, float explodes)
    {
        float left = explodes - now;
        return left <= 0f ? 0 : (int)System.Math.Ceiling(left);
    }

    /// <summary>Hot Potato: seconds between ticks, from 1 s when lit down to 0.15 s at the end</summary>
    public static float TickInterval(float progress)
    {
        return 1f - 0.85f * progress;
    }

    /// <summary>
    /// Whether a timed match is over before its timer: Infection when no thief is left, Hot Potato when at most one
    /// player is still in. players, infected and out are seat masks
    /// </summary>
    public static bool EndsEarly(GameMode mode, int players, int infected, int out_)
    {
        if (mode == GameMode.Infection)
            return Count(players) > 1 && (players & ~infected) == 0;
        if (mode == GameMode.HotPotato)
            return Count(players & ~out_) <= 1 && Count(players) > 1;
        return false;
    }
}

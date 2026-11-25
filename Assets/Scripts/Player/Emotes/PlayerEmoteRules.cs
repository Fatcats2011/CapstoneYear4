using UnityEngine;

/// <summary>
/// When a ghost emotes in a match (PlayerEmotes). Every number is here, to tune in playtests. These only ask for an
/// emote: GhostEmoteRules still decides whether it plays (priorities and cooldowns).
/// - ForSteal, ForGain, HardBump: emotes from what happened (a steal, a delivery, a hard bump).
/// - PadIndex, ForPad: the player's own d-pad emotes.
/// - WallStrength: how hard a wall hit sways the ghost. LAND_EASE: a Wheee easing out on landing.
/// - AirFor/Wheee, StillFor/Bored, TreasureHold: emotes from what a ghost has been doing for a while.
/// - Behind, ClosingSpeed, Closing, CarrierLook, Scheme: emotes from where the others are and where they're going.
/// - Pick, Readied, Restyled, ImpatientDue: the lobby. REV_AGAIN: the countdown. ForPodium, PodiumGap: the results.
/// Pure: plain values in, a decision out
/// </summary>
public static class PlayerEmoteRules
{
    /// <summary>Seconds off the ground before Wheee (once per time in the air)</summary>
    public const float AIR_SECONDS = 0.4f;

    /// <summary>
    /// Metres the ball must rise above where it left the ground for a Wheee: a launch off a ramp or a bump, not a drop
    /// off a bank into the river (half of all air time in bot matches ended in the water)
    /// </summary>
    public const float LAUNCH_RISE = 0.25f;

    /// <summary>A bump at least this hard (0 to 1, FeatSync.Bumped) leaves the victim SeeingStars</summary>
    public const float HARD_BUMP = 0.6f;

    /// <summary>Seconds between TreasureHolds while the golden order is carried (one plays as it's picked up)</summary>
    public const float GOLDEN_EVERY = 15f;

    /// <summary>A carrier with a rival this close behind, closing in, JumpScares, in metres</summary>
    public const float SCARE_METRES = 4f;

    /// <summary>A carrier with a rival this close behind SideEyes, at most once every SIDE_EYE_EVERY seconds</summary>
    public const float SIDE_EYE_METRES = 12f;
    public const float SIDE_EYE_EVERY = 8f;

    /// <summary>A ghost holding nothing, closing in on a carrier this close ahead, Schemes, in metres</summary>
    public const float SCHEME_METRES = 10f;

    /// <summary>Slower than this (m/s, flat) counts as standing still; this long standing still in a match is Bored</summary>
    public const float STILL_SPEED = 0.5f;
    public const float BORED_SECONDS = 12f;

    /// <summary>Two players are closing when the gap shrinks faster than this, in m/s</summary>
    public const float CLOSING_SPEED = 0.5f;

    /// <summary>Below this length a flattened vector has no direction</summary>
    const float MIN_FLAT = 0.001f;

    /// <summary>
    /// What a bump makes a seat do: Cackle for the thief of a real steal, FistShake for its victim. Nothing for any other
    /// outcome (no room, nothing to take, steals off, a mode's hit), another seat, no seat, or a seat on both sides
    /// </summary>
    public static GhostAction? ForSteal(int seat, int attacker, int victim, BumpOutcome outcome)
    {
        if (seat < 0 || outcome != BumpOutcome.Stolen || attacker == victim)
            return null;
        if (seat == attacker)
            return GhostAction.Cackle;
        if (seat == victim)
            return GhostAction.FistShake;
        return null;
    }

    /// <summary>
    /// What money coming in makes a seat do: every gain ScoreFeedback announces is a delivery (a plain one, the golden
    /// order or the Mother Lode: GainReason), so its own seat's is a DeliveryFlourish
    /// </summary>
    public static GhostAction? ForGain(int seat, int gainSeat, GainReason reason)
    {
        if (seat < 0 || seat != gainSeat)
            return null;
        switch (reason)
        {
            case GainReason.Delivered:
            case GainReason.Golden:
            case GainReason.Lode:
                return GhostAction.DeliveryFlourish;
            default:
                return null;
        }
    }

    /// <summary>How many d-pad emotes there are: one per direction</summary>
    public const int PAD_EMOTES = 4;

    /// <summary>
    /// Which d-pad emote a press is: 0 up, 1 right, 2 down, 3 left (EmoteHandler's sprites and sounds are in that order).
    /// A sideways press wins over up or down; -1 for none
    /// </summary>
    public static int PadIndex(Vector2 dpad)
    {
        if (dpad.x == 1f)
            return 1;
        if (dpad.x == -1f)
            return 3;
        if (dpad.y == 1f)
            return 0;
        if (dpad.y == -1f)
            return 2;
        return -1;
    }

    /// <summary>
    /// What the ghost does for a d-pad emote, matching its bubble's picture: up (the grinning top-hatted bear) BigWave,
    /// right ("NUH UH") Taunt, down (the big laugh) Cackle, left (Yoshi's cheeky "Whaaat?!") Raspberry.
    /// Nothing for an index out of range
    /// </summary>
    public static GhostAction? ForPad(int index)
    {
        switch (index)
        {
            case 0: return GhostAction.BigWave;
            case 1: return GhostAction.Taunt;
            case 2: return GhostAction.Cackle;
            case 3: return GhostAction.Raspberry;
            default: return null;
        }
    }

    /// <summary>Whether a bump this hard leaves its victim SeeingStars</summary>
    public static bool HardBump(float strength)
    {
        return strength >= HARD_BUMP;
    }

    /// <summary>Seconds in the air, after dt more: back to 0 on the ground</summary>
    public static float AirFor(float airFor, bool grounded, float dt)
    {
        return grounded ? 0f : airFor + dt;
    }

    /// <summary>
    /// Wheee: AIR_SECONDS or more in the air, having risen LAUNCH_RISE or more above where it left the ground; once per
    /// time in the air (already: it played this time)
    /// </summary>
    /// <param name="rise">How far the ball has risen above where it left the ground, at most, this time in the air</param>
    public static bool Wheee(float airFor, float rise, bool already)
    {
        return !already && airFor >= AIR_SECONDS && rise >= LAUNCH_RISE;
    }

    /// <summary>Seconds a Wheee still playing as the ghost lands takes to ease back to rest (not a snap, not its full length)</summary>
    public const float LAND_EASE = 0.25f;

    /// <summary>
    /// A wall hit's speeds, m/s: BallCollision counts a hit from WALL_SOFT (a gentler touch is nothing); from WALL_HARD
    /// the sway is at its fullest
    /// </summary>
    public const float WALL_SOFT = 10f, WALL_HARD = 30f;

    /// <summary>The share of its full size a WallBump sways at, from the impact speed: WALL_WEAKEST at WALL_SOFT, 1 from WALL_HARD</summary>
    public const float WALL_WEAKEST = 0.5f;

    /// <summary>How big a wall hit's sway is (GhostEmotes.Play's strength), from its speed in m/s</summary>
    public static float WallStrength(float speed)
    {
        return Mathf.Lerp(WALL_WEAKEST, 1f, Mathf.InverseLerp(WALL_SOFT, WALL_HARD, speed));
    }

    /// <summary>The highest rise this time in the air, after a frame at height y (left: the height it left the ground at)</summary>
    public static float Rise(float rise, float y, float left)
    {
        return Mathf.Max(rise, y - left);
    }

    /// <summary>Seconds standing still, after dt more at this speed: back to 0 once it moves</summary>
    public static float StillFor(float stillFor, float speed, float dt)
    {
        return speed < STILL_SPEED ? stillFor + dt : 0f;
    }

    /// <summary>Bored, as the time standing still reaches BORED_SECONDS: once, then not again until it has moved</summary>
    public static bool Bored(float stillBefore, float stillNow)
    {
        return stillBefore < BORED_SECONDS && stillNow >= BORED_SECONDS;
    }

    /// <summary>
    /// TreasureHold: as the golden order is picked up, then every GOLDEN_EVERY seconds while it's carried
    /// </summary>
    /// <param name="wasCarrying">Carried it last time</param>
    /// <param name="sinceLast">Seconds since the last TreasureHold asked for</param>
    public static bool TreasureHold(bool wasCarrying, bool carrying, float sinceLast)
    {
        return carrying && (!wasCarrying || sinceLast >= GOLDEN_EVERY);
    }

    /// <summary>Whether the other player is behind: in the back half, flat (height is ignored). Not when either has no flat direction</summary>
    /// <param name="forward">Where this player faces</param>
    /// <param name="toOther">From this player to the other</param>
    public static bool Behind(Vector3 forward, Vector3 toOther)
    {
        forward.y = 0f;
        toOther.y = 0f;
        if (forward.magnitude < MIN_FLAT || toOther.magnitude < MIN_FLAT)
            return false;
        return Vector3.Dot(forward, toOther) < 0f;
    }

    /// <summary>Whether the other player is ahead: in the front half, flat. Not when either has no flat direction</summary>
    public static bool Ahead(Vector3 forward, Vector3 toOther)
    {
        forward.y = 0f;
        toOther.y = 0f;
        if (forward.magnitude < MIN_FLAT || toOther.magnitude < MIN_FLAT)
            return false;
        return Vector3.Dot(forward, toOther) > 0f;
    }

    /// <summary>
    /// How fast the flat gap between two players shrinks, in m/s (negative: it grows). 0 when they're on top of each other
    /// </summary>
    /// <param name="toOther">From this player to the other</param>
    public static float ClosingSpeed(Vector3 toOther, Vector3 myVelocity, Vector3 otherVelocity)
    {
        toOther.y = 0f;
        float gap = toOther.magnitude;
        if (gap < MIN_FLAT)
            return 0f;
        Vector3 relative = myVelocity - otherVelocity;
        relative.y = 0f;
        return Vector3.Dot(relative, toOther / gap);
    }

    /// <summary>Whether a gap shrinking this fast counts as closing in</summary>
    public static bool Closing(float closingSpeed)
    {
        return closingSpeed > CLOSING_SPEED;
    }

    /// <summary>
    /// What a carrier does about one rival: JumpScare with them close behind and closing in; SideEye with them behind and
    /// within SIDE_EYE_METRES, once SIDE_EYE_EVERY seconds have passed since the last; otherwise nothing
    /// </summary>
    /// <param name="sinceSideEye">Seconds since the last SideEye asked for</param>
    public static GhostAction? CarrierLook(float distance, bool behind, bool closing, float sinceSideEye)
    {
        if (!behind)
            return null;
        if (distance <= SCARE_METRES && closing)
            return GhostAction.JumpScare;
        if (distance <= SIDE_EYE_METRES && sinceSideEye >= SIDE_EYE_EVERY)
            return GhostAction.SideEye;
        return null;
    }

    /// <summary>The worse of two carrier looks: JumpScare over SideEye over nothing</summary>
    public static GhostAction? Worse(GhostAction? a, GhostAction? b)
    {
        if (a == GhostAction.JumpScare || b == GhostAction.JumpScare)
            return GhostAction.JumpScare;
        return a ?? b;
    }

    /// <summary>Scheme: holding nothing, closing in on a rival who carries, ahead and within SCHEME_METRES</summary>
    public static bool Scheme(bool carrying, bool rivalCarries, float distance, bool ahead, bool closing)
    {
        return !carrying && rivalCarries && ahead && closing && distance <= SCHEME_METRES;
    }
    // ---- The lobby, the countdown and the results

    /// <summary>Ready while someone else isn't: Impatient after this many seconds, then again every IMPATIENT_EVERY</summary>
    public const float IMPATIENT_FIRST = 5f;
    public const float IMPATIENT_EVERY = 8f;

    /// <summary>The countdown's second RevBounce, this many seconds after the first (which plays as it starts)</summary>
    public const float REV_AGAIN = 1.5f;

    /// <summary>At the results, the winner's and the last place's first emote after this many seconds, then one every
    /// PODIUM_MIN to PODIUM_MAX seconds while the results show</summary>
    public const float PODIUM_FIRST = 1f;
    public const float PODIUM_MIN = 4f, PODIUM_MAX = 6f;

    /// <summary>
    /// One of two actions ("X or Y"): never the one picked last time (so after the first, random, pick they take turns);
    /// either at random when the last was neither
    /// </summary>
    public static GhostAction Pick(GhostAction a, GhostAction b, GhostAction? last, System.Random random)
    {
        if (last == a)
            return b;
        if (last == b)
            return a;
        return random != null && random.Next(2) == 1 ? b : a;
    }

    /// <summary>Readying up: WarmUp or KnuckleCrack</summary>
    public static GhostAction Readied(GhostAction? last, System.Random random)
    {
        return Pick(GhostAction.WarmUp, GhostAction.KnuckleCrack, last, random);
    }

    /// <summary>
    /// A new hat or colour: HatShowOff or MirrorCheck. With no hat showing after it (the NoHat look) always MirrorCheck:
    /// there is no hat to show off
    /// </summary>
    public static GhostAction Restyled(GhostAction? last, System.Random random, bool hatShows = true)
    {
        if (!hatShows)
            return GhostAction.MirrorCheck;
        return Pick(GhostAction.HatShowOff, GhostAction.MirrorCheck, last, random);
    }

    /// <summary>
    /// Whether another machine's player's colour or hat (an index) going from shown to now is a new look to show off: not
    /// the first look shown as they appear (shown is -1 until then), nor no change
    /// </summary>
    public static bool NewLook(int shown, int now)
    {
        return shown >= 0 && now != shown;
    }

    /// <summary>
    /// Impatient, as the time spent ready while someone else isn't goes from waitBefore to waitNow: at IMPATIENT_FIRST,
    /// then every IMPATIENT_EVERY after it
    /// </summary>
    public static bool ImpatientDue(float waitBefore, float waitNow)
    {
        return ImpatientCount(waitNow) > ImpatientCount(waitBefore);
    }

    static int ImpatientCount(float wait)
    {
        if (wait < IMPATIENT_FIRST)
            return 0;
        return Mathf.FloorToInt((wait - IMPATIENT_FIRST) / IMPATIENT_EVERY) + 1;
    }

    /// <summary>
    /// Which emotes a finishing place (0 = first) among this many players plays at the results: the winner's, the last
    /// place's (the last finishing place, whenever there are two or more players), or none (Runner). Not the podium acts'
    /// mood (PodiumActs.MoodOf), which gives the last place's acts to 4th only
    /// </summary>
    public static PodiumMood PodiumEmoteMood(int place, int players)
    {
        if (place <= 0)
            return PodiumMood.Winner;
        return players >= 2 && place == players - 1 ? PodiumMood.Last : PodiumMood.Runner;
    }

    /// <summary>
    /// The results' emotes of a podium mood: the winner Victory or CrownPolish, the last place Tantrum or FadeAway. False
    /// for the places between (their podium acts carry them)
    /// </summary>
    public static bool ForPodium(PodiumMood mood, out GhostAction a, out GhostAction b)
    {
        switch (mood)
        {
            case PodiumMood.Winner:
                a = GhostAction.Victory;
                b = GhostAction.CrownPolish;
                return true;
            case PodiumMood.Last:
                a = GhostAction.Tantrum;
                b = GhostAction.FadeAway;
                return true;
            default:
                a = b = default;
                return false;
        }
    }

    /// <summary>Seconds to the next podium emote, from a random 0 to 1: PODIUM_MIN to PODIUM_MAX</summary>
    public static float PodiumGap(double random01)
    {
        return PODIUM_MIN + (float)random01 * (PODIUM_MAX - PODIUM_MIN);
    }
}

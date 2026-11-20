/// <summary>
/// A ghost's emotes on the results' podium, on every machine: the winner Victory or CrownPolish, the last finishing place
/// (with two or more players: PlayerEmoteRules.PodiumEmoteMood) Tantrum or FadeAway (PlayerEmoteRules.ForPodium), the first PODIUM_FIRST seconds in, then one every PODIUM_MIN to PODIUM_MAX
/// seconds while the results show. The places between have only their podium acts. Its PodiumPerformer ticks it and
/// stops it with itself: an emote waits for the act playing to end, and acts wait while an emote plays. Cosmetic only
/// </summary>
public class PodiumEmotes
{
    readonly GhostEmotes emotes;
    readonly GhostAction a, b;
    readonly System.Random random;
    float next = PlayerEmoteRules.PODIUM_FIRST;
    GhostAction? last;

    /// <summary>The emote last asked for (tests)</summary>
    internal GhostAction? Asked => last;

    /// <summary>How many have been asked for (tests)</summary>
    internal int AskCount { get; private set; }

    PodiumEmotes(GhostEmotes emotes, GhostAction a, GhostAction b, System.Random random)
    {
        this.emotes = emotes;
        this.a = a;
        this.b = b;
        this.random = random ?? new System.Random();
    }

    /// <summary>
    /// The emotes of a ghost standing in this mood: null when its mood has none (a runner) or it has no emotes
    /// </summary>
    public static PodiumEmotes For(PodiumMood mood, GhostEmotes emotes, System.Random random = null)
    {
        if (emotes == null || !PlayerEmoteRules.ForPodium(mood, out GhostAction a, out GhostAction b))
            return null;
        return new PodiumEmotes(emotes, a, b, random);
    }

    /// <summary>The emotes of a ghost in a finishing place (0 = first) among this many players (PlayerEmoteRules.PodiumEmoteMood)</summary>
    public static PodiumEmotes For(int place, int players, GhostEmotes emotes, System.Random random = null)
    {
        return For(PlayerEmoteRules.PodiumEmoteMood(place, players), emotes, random);
    }

    /// <summary>Whether one of the ghost's emotes is playing (its podium acts wait)</summary>
    public bool Playing => emotes != null && emotes.Playing != null;

    /// <summary>That much time goes by; with no act playing (actPlaying false), an emote that is due starts</summary>
    public void Tick(float dt, bool actPlaying)
    {
        next -= dt;
        if (next > 0f || actPlaying)
            return;
        last = PlayerEmoteRules.Pick(a, b, last, random);
        AskCount++;
        emotes.Play(last.Value);
        next = PlayerEmoteRules.PodiumGap(random.NextDouble());
    }

    /// <summary>The results are over: one of its emotes still playing stops</summary>
    public void Stop()
    {
        if (emotes == null || emotes.Playing == null)
            return;
        GhostAction playing = emotes.Playing.Action;
        if (playing == a || playing == b)
            emotes.Stop();
    }
}

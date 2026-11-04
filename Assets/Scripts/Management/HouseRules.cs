using System;
using System.Collections.Generic;

/// <summary>How a match's waves last (MATCH RULES)</summary>
public enum MatchLength { Short, Normal, Long }

/// <summary>
/// The match rules the host sets in player select or Dispatch (Start opens MATCH RULES): the mode, the waves' length
/// and count, whether boosting into a rival steals, whether the match ends with the golden round, whether empty online
/// seats fill with bots, and the mutators (speed, low gravity, round timer). Normal is today's game. Which rules a
/// mode uses is ModeRules' table; the game reads the effective values (StealsOn, GoldenOn, WavesPlayed...). Online,
/// the host shares the packed rules with clients (OnlineMatch.rules) so every machine plays the same mode and physics.
/// A public lobby always plays normal. See docs/superpowers/specs/2026-10-24-match-rules-design.md and
/// docs/game-modes-ideas.md
/// </summary>
public readonly struct HouseRules : IEquatable<HouseRules>
{
    public static readonly int[] SpeedChoices = { 50, 75, 100, 125, 150, 200 };
    public static readonly int[] WaveChoices = { 1, 2, 3, 4, 5, 6 };
    public static readonly int[] RoundChoices = { 2, 3, 5 };
    public const int DEFAULT_WAVES = 3;
    public const int DEFAULT_SPEED = 100;
    public const int DEFAULT_ROUND = 3;

    public readonly MatchLength Length;
    public readonly bool Steals;
    public readonly bool GoldenOrder;
    /// <summary>Whether an online match with empty seats fills them with CPU bots (BotFill)</summary>
    public readonly bool Bots;
    public readonly GameMode Mode;
    /// <summary>How many waves a match with deliveries has</summary>
    public readonly int Waves;
    /// <summary>Everyone's driving force, in percent</summary>
    public readonly int SpeedPercent;
    public readonly bool LowGravity;
    /// <summary>How long a match without waves lasts, in minutes</summary>
    public readonly int RoundMinutes;

    public static readonly HouseRules Normal = new HouseRules(MatchLength.Normal, true, true, true);

    /// <summary>The rules the host picked (from settings.cfg and the panel)</summary>
    public static HouseRules Current = Normal;

    /// <summary>The rules this match plays by: Current, or normal in a public lobby. Set as the match starts (OrderManager; a client: OnlineGame)</summary>
    public static HouseRules Playing = Normal;

    public HouseRules(MatchLength length, bool steals, bool goldenOrder, bool bots = true, GameMode mode = GameMode.Standard,
        int waves = DEFAULT_WAVES, int speedPercent = DEFAULT_SPEED, bool lowGravity = false, int roundMinutes = DEFAULT_ROUND)
    {
        Length = length;
        Steals = steals;
        GoldenOrder = goldenOrder;
        Bots = bots;
        Mode = mode;
        Waves = waves;
        SpeedPercent = speedPercent;
        LowGravity = lowGravity;
        RoundMinutes = roundMinutes;
    }

    /// <summary>How much longer than normal each wave lasts</summary>
    public float WaveFactor => Length == MatchLength.Short ? 0.75f : Length == MatchLength.Long ? 1.5f : 1f;

    /// <summary>Whether boosting into a rival steals: the mode may lock it on or have none</summary>
    public bool StealsOn => Effective(Rule.Steals, Steals);
    /// <summary>Whether the match ends with the golden round: only modes with deliveries have one</summary>
    public bool GoldenOn => Effective(Rule.GoldenOrder, GoldenOrder);
    /// <summary>How many waves are played (the default where the mode has none)</summary>
    public int WavesPlayed => ModeRules.For(Mode, Rule.Waves) == RuleShow.Hidden ? DEFAULT_WAVES : Waves;
    public float SpeedScale => SpeedPercent / 100f;
    public float GravityScale => LowGravity ? 0.5f : 1f;

    /// <summary>Not the game as designed: another mode, or a mutator changed (the panel says so)</summary>
    public bool IsExperimental => Mode != GameMode.Standard || Waves != DEFAULT_WAVES || SpeedPercent != DEFAULT_SPEED || LowGravity;

    bool Effective(Rule rule, bool picked)
    {
        RuleShow show = ModeRules.For(Mode, rule);
        return show == RuleShow.LockedOn || (show == RuleShow.Shown && picked);
    }

    /// <summary>The rules a match plays by: a public lobby's are always normal</summary>
    public HouseRules ForMatch(bool publicLobby)
    {
        return publicLobby ? Normal : this;
    }

    /// <summary>What differs from normal, in a line ("GOLD RUSH · SHORT · NO STEALS"); "" for normal</summary>
    public string Summary
    {
        get
        {
            List<string> parts = new List<string>();
            if (Mode != GameMode.Standard)
                parts.Add(Loc.Get(GameModes.Key(Mode)));
            if (Shows(Rule.WaveLength) && Length != MatchLength.Normal)
                parts.Add(Loc.Get(Length == MatchLength.Short ? "rules.length.short" : "rules.length.long"));
            if (Shows(Rule.Steals) && !Steals)
                parts.Add(Loc.Get("rules.nosteals"));
            if (Shows(Rule.GoldenOrder) && !GoldenOrder)
                parts.Add(Loc.Get("rules.nogolden"));
            if (!Bots)
                parts.Add(Loc.Get("rules.nobots"));
            if (SpeedPercent != DEFAULT_SPEED)
                parts.Add(Loc.Get("rules.summary.speed", SpeedPercent));
            if (LowGravity)
                parts.Add(Loc.Get("rules.lowgravity"));
            if (Shows(Rule.Waves) && Waves != DEFAULT_WAVES)
                parts.Add(Loc.Get("rules.summary.waves", Waves));
            if (Shows(Rule.RoundTimer) && RoundMinutes != DEFAULT_ROUND)
                parts.Add(Loc.Get("rules.summary.round", RoundMinutes));
            return string.Join(" · ", parts);
        }
    }

    bool Shows(Rule rule) => ModeRules.For(Mode, rule) == RuleShow.Shown;

    /// <summary>The rules saved in settings.cfg</summary>
    public static HouseRules From(GameSettings settings)
    {
        return new HouseRules((MatchLength)settings.matchLength, settings.steals == 1, settings.goldenOrder == 1, settings.bots == 1,
            (GameMode)settings.mode, settings.waves, settings.speed, settings.lowGravity == 1, settings.roundMinutes);
    }

    /// <summary>Puts the rules in the settings, to be saved</summary>
    public void Into(GameSettings settings)
    {
        settings.matchLength = (int)Length;
        settings.steals = Steals ? 1 : 0;
        settings.goldenOrder = GoldenOrder ? 1 : 0;
        settings.bots = Bots ? 1 : 0;
        settings.mode = (int)Mode;
        settings.waves = Waves;
        settings.speed = SpeedPercent;
        settings.lowGravity = LowGravity ? 1 : 0;
        settings.roundMinutes = RoundMinutes;
    }

    /// <summary>The rules in one int, for the host to share (OnlineMatch.rules)</summary>
    public int Pack()
    {
        return (int)Mode | (int)Length << 3 | (Steals ? 1 : 0) << 5 | (GoldenOrder ? 1 : 0) << 6 | (Bots ? 1 : 0) << 7
            | (Waves & 7) << 8 | (SpeedPercent & 0xFF) << 11 | (LowGravity ? 1 : 0) << 19 | (RoundMinutes & 0xF) << 20;
    }

    /// <summary>The rules from Pack. Another machine could send anything, so values outside the choices become the nearest one</summary>
    public static HouseRules Unpack(int packed)
    {
        int mode = packed & 7;
        int length = packed >> 3 & 3;
        return new HouseRules(length > (int)MatchLength.Long ? MatchLength.Normal : (MatchLength)length,
            (packed >> 5 & 1) == 1, (packed >> 6 & 1) == 1, (packed >> 7 & 1) == 1,
            mode > (int)GameMode.HotPotato ? GameMode.Standard : (GameMode)mode,
            Nearest(WaveChoices, packed >> 8 & 7), Nearest(SpeedChoices, packed >> 11 & 0xFF), (packed >> 19 & 1) == 1,
            Nearest(RoundChoices, packed >> 20 & 0xF));
    }

    /// <summary>An online client, as the host's match begins: plays the host's rules, physics included</summary>
    public static void FollowHost(int packed)
    {
        Playing = Unpack(packed);
        MatchPhysics.Apply(Playing);
    }

    /// <summary>The choice nearest a value (ties go to the smaller one), for clamping saved values</summary>
    public static int Nearest(int[] choices, int value)
    {
        int best = choices[0];
        foreach (int choice in choices)
        {
            if (Math.Abs(choice - value) < Math.Abs(best - value))
                best = choice;
        }
        return best;
    }

    public bool Equals(HouseRules other)
    {
        return Length == other.Length && Steals == other.Steals && GoldenOrder == other.GoldenOrder && Bots == other.Bots
            && Mode == other.Mode && Waves == other.Waves && SpeedPercent == other.SpeedPercent && LowGravity == other.LowGravity
            && RoundMinutes == other.RoundMinutes;
    }

    public override bool Equals(object obj) { return obj is HouseRules other && Equals(other); }
    public override int GetHashCode() { return Pack(); }
    public override string ToString() { return Summary == "" ? Loc.Get("rules.length.normal") : Summary; }
}

using System;
using System.Collections.Generic;

/// <summary>How long a match's waves last (MATCH RULES)</summary>
public enum MatchLength { Short, Normal, Long }

/// <summary>
/// The match rules the host sets in player select or Dispatch (Start opens MATCH RULES): the waves' length, whether
/// boosting into a rival steals, and whether the match ends with the golden round, and whether empty online seats fill with bots. Normal is today's game. They apply
/// where the rules run (this PC offline, the host online: its waves, its steals, its scene loads), so clients need
/// nothing sent. A public lobby always plays normal. See docs/superpowers/specs/2026-10-24-match-rules-design.md
/// </summary>
public readonly struct HouseRules : IEquatable<HouseRules>
{
    public readonly MatchLength Length;
    public readonly bool Steals;
    public readonly bool GoldenOrder;
    /// <summary>Whether an online match with empty seats fills them with CPU bots (BotFill)</summary>
    public readonly bool Bots;

    public static readonly HouseRules Normal = new HouseRules(MatchLength.Normal, true, true, true);

    /// <summary>The rules the host picked (from settings.cfg and the panel)</summary>
    public static HouseRules Current = Normal;

    /// <summary>The rules this match plays by: Current, or normal in a public lobby. Set as the match starts (OrderManager)</summary>
    public static HouseRules Playing = Normal;

    public HouseRules(MatchLength length, bool steals, bool goldenOrder, bool bots = true)
    {
        Length = length;
        Steals = steals;
        GoldenOrder = goldenOrder;
        Bots = bots;
    }

    /// <summary>How much longer than normal each wave lasts</summary>
    public float WaveFactor => Length == MatchLength.Short ? 0.75f : Length == MatchLength.Long ? 1.5f : 1f;

    /// <summary>The rules a match plays by: a public lobby's are always normal</summary>
    public HouseRules ForMatch(bool publicLobby)
    {
        return publicLobby ? Normal : this;
    }

    /// <summary>What differs from normal, in a line ("SHORT · NO STEALS"); "" for normal</summary>
    public string Summary
    {
        get
        {
            List<string> parts = new List<string>();
            if (Length != MatchLength.Normal)
                parts.Add(Loc.Get(Length == MatchLength.Short ? "rules.length.short" : "rules.length.long"));
            if (!Steals)
                parts.Add(Loc.Get("rules.nosteals"));
            if (!GoldenOrder)
                parts.Add(Loc.Get("rules.nogolden"));
            if (!Bots)
                parts.Add(Loc.Get("rules.nobots"));
            return string.Join(" · ", parts);
        }
    }

    /// <summary>The rules saved in settings.cfg</summary>
    public static HouseRules From(GameSettings settings)
    {
        return new HouseRules((MatchLength)settings.matchLength, settings.steals == 1, settings.goldenOrder == 1, settings.bots == 1);
    }

    /// <summary>Puts the rules in the settings, to be saved</summary>
    public void Into(GameSettings settings)
    {
        settings.matchLength = (int)Length;
        settings.steals = Steals ? 1 : 0;
        settings.goldenOrder = GoldenOrder ? 1 : 0;
        settings.bots = Bots ? 1 : 0;
    }

    public bool Equals(HouseRules other) { return Length == other.Length && Steals == other.Steals && GoldenOrder == other.GoldenOrder && Bots == other.Bots; }
    public override bool Equals(object obj) { return obj is HouseRules other && Equals(other); }
    public override int GetHashCode() { return (int)Length * 4 + (Steals ? 2 : 0) + (GoldenOrder ? 1 : 0) + (Bots ? 8 : 0); }
    public override string ToString() { return Summary == "" ? Loc.Get("rules.length.normal") : Summary; }
}

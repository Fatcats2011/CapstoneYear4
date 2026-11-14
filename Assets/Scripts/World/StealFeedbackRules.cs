using System;

/// <summary>What one player boosting into another did (StealFeedbackRules.Outcome). It travels online as a byte (PlayerHit)</summary>
public enum BumpOutcome : byte { Stolen, SlotsFull, NothingToSteal, StealsOff, ModeHit }

/// <summary>Whose line a callout is, which sets its colour (StealCallout)</summary>
public enum CalloutTone { None, Thief, Victim, Plain }

/// <summary>
/// What players are told about a bump. Every number and word choice is here, to tune in playtests:
/// - Outcome: what a bump did: a mode took the hit (crown, potato, infection), steals are off, the victim held nothing,
///   the thief had no room, or an order changed hands.
/// - AttackerKey, VictimKey: the line each of the two reads (a strings.csv key), and its tone. No key, no line.
/// - Short: a rival's name, cut to fit the line.
/// - StampText: the "×2" on a stolen package: its worth against its price. Never on the golden order or the Mother Lode
///   (they don't double).
/// - CalloutAlpha: how a line fades in and out.
/// OrderHandler decides the outcome; StealCallout, WorthStamp and Scoreboard show these.
/// </summary>
public static class StealFeedbackRules
{
    /// <summary>The most of a rival's name a line shows, before "…"</summary>
    public const int NAME_LENGTH = 12;

    /// <summary>The most steals the stamp counts (×64): past it the stamp reads "×64+", so it never grows wide</summary>
    public const int MAX_STAMPED_STEALS = 6;

    /// <summary>How long a line is up, its fades included</summary>
    public const float CALLOUT_SECONDS = 1.5f;
    public const float CALLOUT_FADE = 0.15f;

    /// <summary>What a bump did. A mode's hit comes first, then the Steals rule, then what the victim held, then the thief's room</summary>
    /// <param name="modeTookIt">The mode used the hit itself (ModeReferee.Strike)</param>
    public static BumpOutcome Outcome(bool modeTookIt, bool stealsOn, bool victimHolds, bool thiefHasRoom)
    {
        if (modeTookIt)
            return BumpOutcome.ModeHit;
        if (!stealsOn)
            return BumpOutcome.StealsOff;
        if (!victimHolds)
            return BumpOutcome.NothingToSteal;
        return thiefHasRoom ? BumpOutcome.Stolen : BumpOutcome.SlotsFull;
    }

    /// <summary>Whether a byte off the network is an outcome this build knows</summary>
    public static bool Known(byte outcome)
    {
        return outcome <= (byte)BumpOutcome.ModeHit;
    }

    /// <summary>The line the player who boosted in reads: null for none. A steal's line names the money only when some was paid</summary>
    public static string AttackerKey(BumpOutcome outcome, int paid, out CalloutTone tone)
    {
        tone = outcome == BumpOutcome.Stolen ? CalloutTone.Thief : CalloutTone.Plain;
        switch (outcome)
        {
            case BumpOutcome.Stolen: return paid > 0 ? "steal.stole_paid" : "steal.stole";
            case BumpOutcome.SlotsFull: return "steal.full";
            case BumpOutcome.NothingToSteal: return "steal.nothing";
            case BumpOutcome.StealsOff: return "steal.off";
            default:
                tone = CalloutTone.None;
                return null;
        }
    }

    /// <summary>The line the player who was hit reads: only when they lost an order</summary>
    public static string VictimKey(BumpOutcome outcome, out CalloutTone tone)
    {
        tone = outcome == BumpOutcome.Stolen ? CalloutTone.Victim : CalloutTone.None;
        return outcome == BumpOutcome.Stolen ? "steal.robbed" : null;
    }

    /// <summary>A name as a line shows it: its first NAME_LENGTH characters, then "…" if there were more</summary>
    public static string Short(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        return name.Length <= NAME_LENGTH ? name : name.Substring(0, NAME_LENGTH) + "…";
    }

    /// <summary>The stamp on an order stolen this many times: "×2", "×4"... "×64", then "×64+". "" for none</summary>
    public static string StampText(Constants.OrderValue tier, bool isLode, int steals)
    {
        if (steals <= 0 || isLode || tier == Constants.OrderValue.Golden)
            return "";

        string stamp = "×" + (1 << Math.Min(steals, MAX_STAMPED_STEALS));
        return steals > MAX_STAMPED_STEALS ? stamp + "+" : stamp;
    }

    /// <summary>How solid a line is at a time since it came up: fades in, holds, fades out, gone by CALLOUT_SECONDS</summary>
    public static float CalloutAlpha(float age)
    {
        if (age < 0f || age >= CALLOUT_SECONDS)
            return 0f;
        return Math.Min(1f, Math.Min(age, CALLOUT_SECONDS - age) / CALLOUT_FADE);
    }
}

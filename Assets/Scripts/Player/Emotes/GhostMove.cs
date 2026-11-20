using System;
using UnityEngine;

/// <summary>
/// Which extra the move adds on top of the body (nothing, or floating z letters)
/// </summary>
public enum GhostExtra { None, Zzz }

/// <summary>
/// What the ghost holds in its hands during the move (GhostProps): nothing; the parcel it carries, between its hands;
/// that parcel spinning over its right hand; or a crown, between its hands or in its left hand
/// </summary>
public enum GhostProp { None, Parcel, ParcelSpin, Crown, CrownLeft }

/// <summary>
/// One version of an emote: a name, a length and a pose for every moment of it
/// </summary>
public sealed class GhostMove
{
    public GhostAction Action;
    public string Name;
    /// <summary>Seconds</summary>
    public float Length;
    /// <summary>True when the move starts hidden instead of at rest</summary>
    public bool Entrance;
    public GhostExtra Extra;
    /// <summary>What it holds (GhostProps); nothing by default</summary>
    public GhostProp Prop;
    /// <summary>A crown's time in hand, as shares of the move: it comes from the head before, goes back to it after</summary>
    public float PropIn, PropOut = 1f;
    /// <summary>The hand clip it plays on the Animator's Emotes layer: the state's name (null when none)</summary>
    public readonly string Clip;
    /// <summary>Animator.StringToHash(Clip), worked out once (0 when none)</summary>
    public readonly int ClipHash;

    readonly Func<float, float, GhostPose> at;

    public GhostMove(GhostAction action, string name, float length, Func<float, float, GhostPose> at,
                     bool entrance = false, GhostExtra extra = GhostExtra.None, string clip = null)
    {
        Action = action;
        Name = name;
        Length = length;
        this.at = at;
        Entrance = entrance;
        Extra = extra;
        Clip = string.IsNullOrEmpty(clip) ? null : clip;
        ClipHash = Clip != null ? Animator.StringToHash(Clip) : 0;
    }

    /// <summary>The same move, holding a prop (a crown in hand from share "from" to "to")</summary>
    internal GhostMove Holding(GhostProp prop, float from = 0f, float to = 1f)
    {
        Prop = prop;
        PropIn = from;
        PropOut = to;
        return this;
    }

    /// <summary>Whether it holds the ghost's parcel</summary>
    public bool HoldsParcel => Prop == GhostProp.Parcel || Prop == GhostProp.ParcelSpin;

    /// <summary>Whether it holds a crown</summary>
    public bool HoldsCrown => Prop == GhostProp.Crown || Prop == GhostProp.CrownLeft;

    /// <summary>
    /// The pose at a share (0 to 1) of the move. Side is +1 or -1: which way it turns
    /// </summary>
    public GhostPose At(float share, float side)
    {
        return at(Mathf.Clamp01(share), side);
    }
}

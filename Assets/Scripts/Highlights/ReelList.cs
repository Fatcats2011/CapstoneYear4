using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>One clip of the highlight reel as the host shares it: when it happened, what it shows and the cameras that film it</summary>
public struct ReelClip : INetworkSerializable
{
    public double Start, End, KeyTime;

    /// <summary>Bit per seat in the clip</summary>
    public byte SeatsMask;

    public Caption Caption;

    /// <summary>The round clock when the clip starts, for the caption's time</summary>
    public float TimeLeftAtStart;

    public ReelShot[] Shots;

    /// <summary>
    /// How long the clip takes to play, in real seconds: its length, plus what each slowed stretch (clamped to the
    /// clip's end) adds
    /// </summary>
    public float RealLength
    {
        get
        {
            double length = End - Start;
            if (Shots != null)
            {
                foreach (ReelShot shot in Shots)
                {
                    if (shot.Speed <= 0f || shot.Speed >= 1f)
                        continue;
                    double stretch = Math.Max(0.0, Math.Min(ShotDirector.PEAK_LENGTH, End - shot.Cut));
                    length += stretch * (1.0 / shot.Speed - 1.0);
                }
            }
            return (float)length;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Start);
        serializer.SerializeValue(ref End);
        serializer.SerializeValue(ref KeyTime);
        serializer.SerializeValue(ref SeatsMask);
        byte caption = (byte)Caption;
        serializer.SerializeValue(ref caption);
        Caption = (Caption)caption;
        serializer.SerializeValue(ref TimeLeftAtStart);

        // An explicit length, capped on read so a hostile one can't make a huge array. Over the cap the shots stay null
        // (NetChecks.Sane refuses that) and the rest of this clip is not read
        byte count = (byte)(Shots != null ? Math.Min(Shots.Length, byte.MaxValue) : 0);
        serializer.SerializeValue(ref count);
        if (serializer.IsReader)
        {
            if (count > ReelList.MAX_SHOTS)
            {
                Shots = null;
                return;
            }
            Shots = new ReelShot[count];
        }
        for (int i = 0; i < count; i++)
            serializer.SerializeValue(ref Shots[i]);
    }
}

/// <summary>
/// The highlight reel as the host shares it with every machine: the clips chosen and the camera shots for each. Clients
/// check it on arrival (NetChecks.Sane)
/// </summary>
public struct ReelList : INetworkSerializable, IEquatable<ReelList>
{
    public const int MAX_CLIPS = 8;
    public const int MAX_SHOTS = 3;

    public ReelClip[] Clips;

    /// <summary>The round time to begin playing, the same on every machine</summary>
    public double StartAt;

    /// <summary>The reel's length in real seconds, counting slow shots</summary>
    public float Length;

    /// <summary>The reel for these clips (at most MAX_CLIPS), the director's shots for each, chained so no clip opens on the kind the last one ended on</summary>
    public static ReelList From(List<Clip> clips, ShotDirector director, double startAt)
    {
        int count = Math.Min(clips.Count, MAX_CLIPS);
        var reel = new ReelList { Clips = new ReelClip[count], StartAt = startAt };
        ShotKind? previous = null;
        for (int i = 0; i < count; i++)
        {
            Clip clip = clips[i];
            List<ReelShot> shots = director.Shots(clip, previous);
            if (shots.Count > 0)
                previous = shots[shots.Count - 1].Kind;

            ReelClip entry = new ReelClip
            {
                Start = clip.Start,
                End = clip.End,
                KeyTime = clip.KeyTime,
                SeatsMask = (byte)clip.SeatsMask,
                Caption = clip.Caption,
                TimeLeftAtStart = clip.TimeLeftAtStart,
                Shots = shots.ToArray(),
            };
            reel.Clips[i] = entry;
            reel.Length += entry.RealLength;
        }
        return reel;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref StartAt);
        serializer.SerializeValue(ref Length);

        // Same as the shots: an explicit, capped length; over the cap the clips stay null and nothing more is read
        byte count = (byte)(Clips != null ? Math.Min(Clips.Length, byte.MaxValue) : 0);
        serializer.SerializeValue(ref count);
        if (serializer.IsReader)
        {
            if (count > MAX_CLIPS)
            {
                Clips = null;
                return;
            }
            Clips = new ReelClip[count];
        }
        for (int i = 0; i < count; i++)
            serializer.SerializeValue(ref Clips[i]);
    }

    public bool Equals(ReelList other)
    {
        if (StartAt != other.StartAt || Length != other.Length)
            return false;
        int mine = Clips != null ? Clips.Length : 0;
        int theirs = other.Clips != null ? other.Clips.Length : 0;
        if (mine != theirs)
            return false;
        for (int i = 0; i < mine; i++)
        {
            if (!SameClip(Clips[i], other.Clips[i]))
                return false;
        }
        return true;
    }

    static bool SameClip(ReelClip a, ReelClip b)
    {
        if (a.Start != b.Start || a.End != b.End || a.KeyTime != b.KeyTime || a.SeatsMask != b.SeatsMask
            || a.Caption != b.Caption || a.TimeLeftAtStart != b.TimeLeftAtStart)
            return false;
        int mine = a.Shots != null ? a.Shots.Length : 0;
        int theirs = b.Shots != null ? b.Shots.Length : 0;
        if (mine != theirs)
            return false;
        for (int i = 0; i < mine; i++)
        {
            ReelShot x = a.Shots[i], y = b.Shots[i];
            if (x.Kind != y.Kind || x.Cut != y.Cut || x.Position != y.Position || x.Subject != y.Subject
                || x.Offset != y.Offset || x.FramedMask != y.FramedMask || x.Speed != y.Speed)
                return false;
        }
        return true;
    }

    public override bool Equals(object obj) { return obj is ReelList other && Equals(other); }

    public override int GetHashCode() { return HashCode.Combine(StartAt, Length, Clips != null ? Clips.Length : 0); }
}

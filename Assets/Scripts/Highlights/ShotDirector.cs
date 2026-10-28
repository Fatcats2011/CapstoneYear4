using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>The kinds of invisible camera the highlight reel can place for a clip</summary>
public enum ShotKind : byte { Low, HighWide, SideTrack, Behind, FixedPan }

/// <summary>One camera placement in a clip</summary>
public struct ReelShot : INetworkSerializable
{
    public ShotKind Kind;

    /// <summary>The round time this shot starts</summary>
    public double Cut;

    /// <summary>Where the camera sits, for the static kinds (Low, HighWide, FixedPan)</summary>
    public Vector3 Position;

    /// <summary>The seat a moving kind (SideTrack, Behind) follows</summary>
    public int Subject;

    /// <summary>Where a moving kind sits from its subject's ball, in the subject's heading frame (side, up, forward)</summary>
    public Vector3 Offset;

    /// <summary>Bit per seat the camera was framed on</summary>
    public int FramedMask;

    /// <summary>Playback speed: 1, or ShotDirector.PEAK_SPEED. A peak shot plays slowed for PEAK_LENGTH seconds of clip time, then goes back to 1</summary>
    public float Speed;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        byte kind = (byte)Kind;
        serializer.SerializeValue(ref kind);
        Kind = (ShotKind)kind;
        serializer.SerializeValue(ref Cut);
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref Subject);
        serializer.SerializeValue(ref Offset);
        serializer.SerializeValue(ref FramedMask);
        serializer.SerializeValue(ref Speed);
    }
}

/// <summary>
/// Places the invisible cameras for a highlight clip. Each kind is tried against the recording and is kept only if
/// the framed players stay in view and in the open and the camera stays a fair distance from them; the first kind
/// that does, after the previous clip's kind, is used. Pure logic: line of sight comes in through a delegate, so there is no physics in here.
/// </summary>
public class ShotDirector
{
    /// <summary>How many times across a clip a candidate is tested</summary>
    public const int SAMPLES = 5;

    /// <summary>The camera's field of view, in degrees</summary>
    public const float FOV = 60f;

    /// <summary>A clip scoring this much or more also gets a slow peak shot</summary>
    public const float PEAK_SCORE = 12f;

    /// <summary>Seconds before the clip's key moment that the peak shot cuts in</summary>
    public const float PEAK_LEAD = 0.3f;

    public const float PEAK_SPEED = 0.5f;

    /// <summary>Seconds of clip time the peak shot plays slowed</summary>
    public const float PEAK_LENGTH = 1.5f;

    // Where each kind sits (side, up, forward), from the action's centre along the direction of travel. Side track
    // and behind sit from their subject's ball in its heading frame instead
    static readonly Vector3 LOW = new Vector3(4f, 0.8f, 6f);
    static readonly Vector3 HIGH_WIDE = new Vector3(0f, 18f, -25f);
    static readonly Vector3 SIDE_TRACK = new Vector3(8f, 2f, 0f);
    static readonly Vector3 BEHIND = new Vector3(0f, 3f, -7f);
    static readonly Vector3 FIXED_PAN = new Vector3(0f, 3f, 15f);

    // The framed seats moving less than this (metres) over TRAVEL_TIME aren't going anywhere: their heading is the direction instead
    const float MIN_TRAVEL = 0.5f;
    const float TRAVEL_TIME = 0.5f;

    /// <summary>A kind is only valid with its mean distance from the action at least this many times its ideal</summary>
    public const float BAND_MIN = 0.4f;

    /// <summary>...and at most this many times its ideal</summary>
    public const float BAND_MAX = 2.5f;

    // The kinds in enum order: the order the cycle goes round
    static readonly ShotKind[] ALL_KINDS = { ShotKind.Low, ShotKind.HighWide, ShotKind.SideTrack, ShotKind.Behind, ShotKind.FixedPan };

    readonly ReelRecording recording;
    readonly Func<Vector3, Vector3, bool> clear;

    /// <param name="clear">True when nothing solid lies between two points</param>
    public ShotDirector(ReelRecording recording, Func<Vector3, Vector3, bool> clear)
    {
        this.recording = recording;
        this.clear = clear;
    }

    /// <summary>
    /// The shots for a clip: the first, plus a slow peak shot for a big clip (if Behind or Low is valid for it). The
    /// first is the first valid kind after the previous one, going round the kinds in order; if none is valid it is the
    /// most clear, and never the previous kind.
    /// </summary>
    public List<ReelShot> Shots(Clip clip, ShotKind? previous)
    {
        List<int> framed = FramedSeats(clip);
        var shots = new List<ReelShot>();

        var order = new List<ShotKind>();
        int startAt = previous.HasValue ? (int)previous.Value + 1 : 0;
        for (int i = 0; i < ALL_KINDS.Length; i++)
        {
            ShotKind kind = ALL_KINDS[(startAt + i) % ALL_KINDS.Length];
            if (!previous.HasValue || kind != previous.Value)
                order.Add(kind);
        }
        ReelShot first = Choose(clip, framed, clip.Start, order, true, out _);
        shots.Add(first);

        // A peak at the very start would only replace the first shot
        double cut = clip.KeyTime - PEAK_LEAD;
        if (clip.Score >= PEAK_SCORE && cut > clip.Start)
        {
            var peakOrder = new List<ShotKind>();
            foreach (ShotKind kind in new[] { ShotKind.Behind, ShotKind.Low })
            {
                if (kind != first.Kind)
                    peakOrder.Add(kind);
            }

            ReelShot peak = Choose(clip, framed, cut, peakOrder, false, out bool valid);
            if (valid)
            {
                peak.Speed = PEAK_SPEED;
                shots.Add(peak);
            }
        }
        return shots;
    }

    // The first valid kind in the order for a shot cutting in at from and lasting to the clip's end. A kind is valid
    // when every sample is clear and in frame and its mean distance is within the band around its ideal. With none
    // valid and fallback set, the pick is the kind with the most clear samples (ties to the earlier kind).
    ReelShot Choose(Clip clip, List<int> framed, double from, List<ShotKind> order, bool fallback, out bool valid)
    {
        int framedMask = 0;
        foreach (int seat in framed)
            framedMask |= 1 << seat;

        Vector3 centre = Centre(framed, from, clip.KeyPlace);
        Vector3 forward = Travel(framed, from);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        int subject = framed.Count > 0 ? framed[0] : 0;

        valid = false;
        ReelShot best = default;
        int bestClear = -1;
        foreach (ShotKind kind in order)
        {
            ReelShot shot = Place(kind, from, framedMask, subject, centre, forward, right);
            Test(shot, framed, from, clip.End, out int clearSamples, out int tested, out float meanDistance);
            float ideal = Ideal(kind);
            if (tested > 0 && clearSamples == tested && meanDistance >= BAND_MIN * ideal && meanDistance <= BAND_MAX * ideal)
            {
                valid = true;
                return shot;
            }

            // Kinds go through the fallback in their own order, so a tie stays with the earlier one
            if (clearSamples > bestClear || (clearSamples == bestClear && kind < best.Kind))
            {
                best = shot;
                bestClear = clearSamples;
            }
        }
        return best;
    }

    static ReelShot Place(ShotKind kind, double cut, int framedMask, int subject, Vector3 centre, Vector3 forward, Vector3 right)
    {
        var shot = new ReelShot { Kind = kind, Cut = cut, FramedMask = framedMask, Subject = subject, Speed = 1f };
        switch (kind)
        {
            case ShotKind.Low: shot.Position = centre + right * LOW.x + Vector3.up * LOW.y + forward * LOW.z; break;
            case ShotKind.HighWide: shot.Position = centre + Vector3.up * HIGH_WIDE.y + forward * HIGH_WIDE.z; break;
            case ShotKind.FixedPan: shot.Position = centre + Vector3.up * FIXED_PAN.y + forward * FIXED_PAN.z; break;
            case ShotKind.SideTrack: shot.Offset = SIDE_TRACK; break;
            case ShotKind.Behind: shot.Offset = BEHIND; break;
        }
        return shot;
    }

    // How far a kind likes to sit from the action: its placement's distance
    static float Ideal(ShotKind kind)
    {
        switch (kind)
        {
            case ShotKind.Low: return LOW.magnitude;
            case ShotKind.HighWide: return HIGH_WIDE.magnitude;
            case ShotKind.SideTrack: return SIDE_TRACK.magnitude;
            case ShotKind.Behind: return BEHIND.magnitude;
            default: return FIXED_PAN.magnitude;
        }
    }

    // Looks through the shot at SAMPLES times from..to: how many have every framed seat in view and in the open, how
    // many had anyone recorded to look at, and the camera's mean distance to their centroid
    void Test(ReelShot shot, List<int> framed, double from, double to, out int clearSamples, out int tested, out float meanDistance)
    {
        clearSamples = 0;
        tested = 0;
        float distance = 0f;
        var balls = new List<Vector3>();
        for (int i = 0; i < SAMPLES; i++)
        {
            double time = from + (to - from) * i / (SAMPLES - 1);
            balls.Clear();
            Vector3 centroid = Vector3.zero;
            foreach (int seat in framed)
            {
                if (!recording.TryGet(seat, time, out ReelSample sample))
                    continue;
                balls.Add(sample.Pose.Ball);
                centroid += sample.Pose.Ball;
            }
            if (balls.Count == 0)
                continue;
            centroid /= balls.Count;

            Vector3 camera = CameraAt(shot, time);
            tested++;
            distance += Vector3.Distance(camera, centroid);

            Vector3 look = centroid - camera;
            bool ok = true;
            foreach (Vector3 ball in balls)
            {
                bool inView = look.sqrMagnitude < 1e-6f || Vector3.Angle(look, ball - camera) <= FOV / 2f;
                if (!inView || !clear(camera, ball + Vector3.up))
                {
                    ok = false;
                    break;
                }
            }
            if (ok)
                clearSamples++;
        }
        meanDistance = tested > 0 ? distance / tested : 0f;
    }

    // Where the shot's camera is at a time. A moving kind rides its subject's ball, and stays on its last known
    // place if the subject isn't recorded then.
    Vector3 CameraAt(ReelShot shot, double time)
    {
        if (shot.Kind != ShotKind.SideTrack && shot.Kind != ShotKind.Behind)
            return shot.Position;

        double clamped = Math.Min(Math.Max(time, recording.First(shot.Subject)), recording.Last(shot.Subject));
        if (!recording.TryGet(shot.Subject, clamped, out ReelSample sample))
            return shot.Offset;
        return sample.Pose.Ball + Quaternion.Euler(0f, sample.Pose.Heading, 0f) * shot.Offset;
    }

    // The seats in the clip's mask that were recorded at some point in the clip
    List<int> FramedSeats(Clip clip)
    {
        var seats = new List<int>();
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            if ((clip.SeatsMask & (1 << seat)) == 0)
                continue;
            if (recording.TryGet(seat, clip.Start, out _) || recording.TryGet(seat, clip.End, out _) || recording.TryGet(seat, clip.KeyTime, out _))
                seats.Add(seat);
        }
        return seats;
    }

    // The framed seats' mean ball at a time (those recorded then), or the fallback if none were
    Vector3 Centre(List<int> framed, double time, Vector3 fallback)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        foreach (int seat in framed)
        {
            if (!recording.TryGet(seat, time, out ReelSample sample))
                continue;
            sum += sample.Pose.Ball;
            count++;
        }
        return count > 0 ? sum / count : fallback;
    }

    // The flat direction the framed seats travel from a time, or the first one's heading if they barely move
    Vector3 Travel(List<int> framed, double time)
    {
        Vector3 move = Vector3.zero;
        foreach (int seat in framed)
        {
            if (recording.TryGet(seat, time, out ReelSample a) && recording.TryGet(seat, time + TRAVEL_TIME, out ReelSample b))
                move += b.Pose.Ball - a.Pose.Ball;
        }
        move.y = 0f;
        if (framed.Count > 0 && move.magnitude / framed.Count >= MIN_TRAVEL)
            return move.normalized;

        if (framed.Count > 0 && recording.TryGet(framed[0], time, out ReelSample first))
            return Quaternion.Euler(0f, first.Pose.Heading, 0f) * Vector3.forward;
        return Vector3.forward;
    }
}

/// <summary>The game's line-of-sight check for the shot director, kept out of ShotDirector so that stays pure</summary>
public static class ShotLineOfSight
{
    /// <summary>
    /// True when nothing solid lies between two points. The players' own ball and scooter layers are left out (read
    /// from a roster player, not hard-coded), since the camera is meant to see them.
    /// </summary>
    internal static Func<Vector3, Vector3, bool> WorldClear()
    {
        int mask = Physics.DefaultRaycastLayers;
        PlayerInstantiate instantiate = PlayerInstantiate.Instance;
        if (instantiate != null)
        {
            foreach (PlayerSlot slot in instantiate.Roster.Players)
            {
                BallDriving driving = slot.Player != null ? slot.Player.GetComponentInChildren<BallDriving>(true) : null;
                if (driving == null)
                    continue;
                mask &= ~(1 << driving.Sphere.layer);
                mask &= ~(1 << driving.gameObject.layer);
                break;
            }
        }
        return (a, b) => !Physics.Linecast(a, b, mask, QueryTriggerInteraction.Ignore);
    }
}

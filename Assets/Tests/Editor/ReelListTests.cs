using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The highlight reel as the host shares it with every machine (ReelList): it arrives as it was sent, its length
    /// counts slow shots, and the checks a client makes on it (NetChecks)
    /// </summary>
    public class ReelListTests
    {
        delegate void Change(ref ReelList list);

        static ReelShot AShot(ShotKind kind, double cut, float speed)
        {
            return new ReelShot
            {
                Kind = kind, Cut = cut, Position = new Vector3(1f, 2f, 3f), Subject = 2,
                Offset = new Vector3(0f, 3f, -7f), FramedMask = 0b0110, Speed = speed,
            };
        }

        static ReelClip AClip(double start)
        {
            return new ReelClip
            {
                Start = start, End = start + 4.0, KeyTime = start + 2.0, SeatsMask = 0b0101, Caption = Caption.Water,
                TimeLeftAtStart = 61.5f,
                Shots = new[] { AShot(ShotKind.Low, start, 1f), AShot(ShotKind.Behind, start + 1.7, ShotDirector.PEAK_SPEED) },
            };
        }

        static ReelList AList()
        {
            var list = new ReelList { Clips = new[] { AClip(10.0), AClip(30.0) }, StartAt = 100.25 };
            list.Length = list.Clips[0].RealLength + list.Clips[1].RealLength;
            return list;
        }

        // A sane list with one thing changed
        static ReelList Broken(Change change)
        {
            ReelList list = AList();
            change(ref list);
            return list;
        }

        static ReelList Sent(ReelList list)
        {
            using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(list);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out ReelList received);
                    return received;
                }
            }
        }

        [Test]
        public void RoundTrip_SerializesEveryField()
        {
            ReelList sent = AList();
            ReelList received = Sent(sent);

            Assert.AreEqual(sent, received);
            Assert.AreEqual(100.25, received.StartAt);
            Assert.AreEqual(sent.Length, received.Length);
            ReelClip clip = received.Clips[1];
            Assert.AreEqual(30.0, clip.Start);
            Assert.AreEqual(34.0, clip.End);
            Assert.AreEqual(32.0, clip.KeyTime);
            Assert.AreEqual(0b0101, clip.SeatsMask);
            Assert.AreEqual(Caption.Water, clip.Caption);
            Assert.AreEqual(61.5f, clip.TimeLeftAtStart);
            ReelShot peak = clip.Shots[1];
            Assert.AreEqual(ShotKind.Behind, peak.Kind);
            Assert.AreEqual(31.7, peak.Cut);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), peak.Position);
            Assert.AreEqual(2, peak.Subject);
            Assert.AreEqual(new Vector3(0f, 3f, -7f), peak.Offset);
            Assert.AreEqual(0b0110, peak.FramedMask);
            Assert.AreEqual(ShotDirector.PEAK_SPEED, peak.Speed);
        }

        [Test]
        public void RoundTrip_NullArraysArriveEmpty()
        {
            Assert.AreEqual(0, Sent(new ReelList()).Clips.Length);

            ReelList noShots = new ReelList { Clips = new[] { new ReelClip { Start = 0, End = 4 } } };
            Assert.AreEqual(0, Sent(noShots).Clips[0].Shots.Length);
        }

        [Test]
        public void Read_AHostileClipCount_ArrivesNullAndSaneRefusesIt()
        {
            using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteValueSafe(5.0);
                writer.WriteValueSafe(5f);
                writer.WriteValueSafe((byte)200);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out ReelList received);
                    Assert.IsNull(received.Clips);
                    Assert.IsFalse(NetChecks.Sane(received));
                }
            }
        }

        [Test]
        public void Read_AHostileShotCount_ArrivesNullAndSaneRefusesIt()
        {
            using (FastBufferWriter writer = new FastBufferWriter(128, Allocator.Temp))
            {
                writer.WriteValueSafe(5.0);
                writer.WriteValueSafe(5f);
                writer.WriteValueSafe((byte)1);
                writer.WriteValueSafe(0.0);
                writer.WriteValueSafe(4.0);
                writer.WriteValueSafe(2.0);
                writer.WriteValueSafe((byte)1);
                writer.WriteValueSafe((byte)0);
                writer.WriteValueSafe(10f);
                writer.WriteValueSafe((byte)255);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out ReelList received);
                    Assert.IsNull(received.Clips[0].Shots);
                    Assert.IsFalse(NetChecks.Sane(received));
                }
            }
        }

        [Test]
        public void Length_CountsSlowShots()
        {
            ReelClip clip = new ReelClip
            {
                Start = 10.0, End = 14.0,
                Shots = new[] { AShot(ShotKind.Low, 10.0, 1f), AShot(ShotKind.Behind, 11.5, 0.5f) },
            };

            Assert.AreEqual(5.5f, clip.RealLength, 1e-4f, "4 s plus 1.5 s at half speed adds 1.5 s");
        }

        [Test]
        public void Length_ASlowShotNearTheEnd_IsClampedToTheClip()
        {
            ReelClip clip = new ReelClip
            {
                Start = 10.0, End = 14.0,
                Shots = new[] { AShot(ShotKind.Behind, 13.0, 0.5f) },
            };

            Assert.AreEqual(5f, clip.RealLength, 1e-4f, "only 1 s of slow stretch is left");
        }

        [Test]
        public void Sane_AcceptsARealList()
        {
            Assert.IsTrue(NetChecks.Sane(AList()));
            Assert.IsTrue(NetChecks.Sane(Sent(AList())));
        }

        [Test]
        public void Sane_RejectsNullArrays()
        {
            Assert.IsFalse(NetChecks.Sane(new ReelList { Length = 5f }), "no clips");
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots = null)), "no shots");
        }

        [Test]
        public void Sane_RejectsTooManyClipsOrShots()
        {
            Assert.IsFalse(NetChecks.Sane(new ReelList { Clips = new ReelClip[ReelList.MAX_CLIPS + 1] }));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots = new ReelShot[ReelList.MAX_SHOTS + 1])));
        }

        [Test]
        public void Sane_RejectsNaNAndInfinityAnywhere()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.StartAt = double.NaN)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Length = float.NaN)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Start = double.NegativeInfinity)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].End = double.PositiveInfinity)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].KeyTime = double.NaN)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].TimeLeftAtStart = float.PositiveInfinity)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Cut = double.NaN)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Position = new Vector3(0f, float.NaN, 0f))));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Offset = new Vector3(float.PositiveInfinity, 0f, 0f))));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Speed = float.NaN)));
        }

        [Test]
        public void Sane_RejectsAClipThatDoesNotRunForwards()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].End = l.Clips[0].Start)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].End = l.Clips[0].Start - 1.0)));
        }

        [Test]
        public void Sane_RejectsSeatsOutsideTheGame()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].SeatsMask = 0b10000)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Subject = Constants.MAX_PLAYERS)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Subject = -1)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].FramedMask = 0b10000)));
        }

        [Test]
        public void Sane_RejectsUndefinedEnums()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Kind = (ShotKind)99)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Caption = (Caption)99)));
        }

        [Test]
        public void Sane_RejectsATooLongReel()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Length = 30.5f)));
            Assert.IsTrue(NetChecks.Sane(Broken((ref ReelList l) => l.Length = 30f)));
        }

        [Test]
        public void Sane_RejectsASpeedOutsideZeroToOne()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Speed = 0f)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Speed = -0.5f)));
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].Shots[0].Speed = 1.5f)));
        }
    }
}

using System;
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
            using (FastBufferWriter writer = new FastBufferWriter(4096, Allocator.Temp))
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
        public void Read_AHostileShotCount_ArrivesWithNoClipsAndSaneRefusesIt()
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
                    Assert.IsNull(received.Clips);
                    Assert.IsFalse(NetChecks.Sane(received));
                }
            }
        }

        [Test]
        public void Read_AHostileShotCountInTheFirstOfTwoClips_RejectsTheWholeListWithoutThrowing()
        {
            ReelList good = AList();
            using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(good);
                byte[] bytes = writer.ToArray();
                // StartAt (8) + Length (4) + clip count (1), then the clip's 3 doubles, 2 bytes and a float, then its shot count
                bytes[8 + 4 + 1 + 24 + 2 + 4] = 255;

                ReelList received = default;
                Assert.DoesNotThrow(() => received = ReadBytes(bytes));
                Assert.IsNull(received.Clips);
                Assert.IsFalse(NetChecks.Sane(received));
            }
        }

        [Test]
        public void Read_ATruncatedBuffer_RejectsWithoutThrowing()
        {
            using (FastBufferWriter writer = new FastBufferWriter(1024, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(AList());
                byte[] bytes = writer.ToArray();
                byte[] cut = new byte[bytes.Length - 20];
                Array.Copy(bytes, cut, cut.Length);

                ReelList received = default;
                Assert.DoesNotThrow(() => received = ReadBytes(cut));
                Assert.IsNull(received.Clips);
                Assert.IsFalse(NetChecks.Sane(received));
            }
        }

        [Test]
        public void RoundTrip_ABiggestSizeList()
        {
            var clips = new ReelClip[ReelList.MAX_CLIPS];
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = AClip(i * 10.0);
                clips[i].Shots = new[] { AShot(ShotKind.Low, i * 10.0, 1f), AShot(ShotKind.Behind, i * 10.0 + 1, 0.5f), AShot(ShotKind.HighWide, i * 10.0 + 3, 1f) };
            }
            ReelList sent = new ReelList { Clips = clips, StartAt = 5.0, Length = 20f };

            ReelList received = Sent(sent);

            Assert.AreEqual(sent, received);
            Assert.AreEqual(ReelList.MAX_SHOTS, received.Clips[ReelList.MAX_CLIPS - 1].Shots.Length);
        }

        static ReelList ReadBytes(byte[] bytes)
        {
            using (FastBufferWriter writer = new FastBufferWriter(bytes.Length + 1, Allocator.Temp))
            {
                writer.WriteBytesSafe(bytes);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out ReelList received);
                    return received;
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
            // Clips of 8 s each (their real length too), so the length can match what they take: 4 x 8 = 32 s
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) =>
            {
                l.Clips = new[] { Longer(10), Longer(30), Longer(50), Longer(70) };
                l.Length = 32f;
            })));
            Assert.IsTrue(NetChecks.Sane(Broken((ref ReelList l) =>
            {
                l.Clips = new[] { Longer(10), Longer(30), Longer(50) };
                l.Length = 24f;
            })));
        }

        static ReelClip Longer(double start)
        {
            ReelClip clip = AClip(start);
            clip.End = start + 8.0;
            clip.Shots = new[] { AShot(ShotKind.Low, start, 1f) };
            return clip;
        }

        [Test]
        public void Sane_RejectsAClipRunningOverTwentySeconds()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Clips[0].End = l.Clips[0].Start + 1e9)), "a billion seconds");
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) =>
            {
                l.Clips[0].End = l.Clips[0].Start + 20.5;
                l.Clips[0].Shots = new[] { AShot(ShotKind.Low, l.Clips[0].Start, 1f) };
                l.Length = l.Clips[0].RealLength + l.Clips[1].RealLength;
            })), "20.5 s, even with a matching length");
            Assert.IsTrue(NetChecks.Sane(Broken((ref ReelList l) =>
            {
                l.Clips[0].End = l.Clips[0].Start + 20.0;
                l.Clips[0].Shots = new[] { AShot(ShotKind.Low, l.Clips[0].Start, 1f) };
                l.Length = l.Clips[0].RealLength + l.Clips[1].RealLength;
            })), "20 s (the reel's whole length) is allowed");
        }

        [Test]
        public void Sane_AcceptsTheHostsOwnLongMergedClip()
        {
            // Steals by two pairs of seats take turns, each clip (up to 6 s) overlapping the next: they merge into one of about 15 s
            var moments = new System.Collections.Generic.List<Moment>();
            int[][] steals = { new[] { 10, 0, 1 }, new[] { 12, 0, 1 }, new[] { 12, 2, 3 }, new[] { 15, 2, 3 }, new[] { 17, 0, 1 }, new[] { 19, 2, 3 }, new[] { 21, 0, 1 } };
            foreach (int[] steal in steals)
                moments.Add(new Moment { Kind = MomentKind.Steal, Time = steal[0], Seat = steal[1], Other = steal[2], TimeLeft = 100f });
            var recording = new ReelRecording(1000);
            for (int i = 0; i <= 600; i++)
            {
                for (int seat = 0; seat < 4; seat++)
                {
                    Vector3 ball = new Vector3(i / 20f * 0.5f, 0f, seat * 3f);
                    recording.Add(seat, new ReelSample { Time = i / 20.0, Pose = new ScooterPose { Ball = ball, Heading = 90f, ModelPosition = ball, ModelRotation = Quaternion.identity } });
                }
            }

            var picked = HighlightRules.Pick(HighlightRules.Clips(moments), new[] { 0, 1, 2, 3 });
            Assert.AreEqual(1, picked.Count, "one merged clip: " + string.Join(" ", picked.ConvertAll(c => c.Start + "-" + c.End)));
            Assert.GreaterOrEqual(picked[0].Length, 14.0, "a long one");

            ReelList list = ReelList.From(picked, new ShotDirector(recording, (a, b) => true), 50);

            Assert.IsTrue(NetChecks.Sane(list), "the host's own list passes what every machine checks");
            Assert.IsTrue(NetChecks.Sane(HighlightDirector.Playable(list)));
        }

        [Test]
        public void Sane_RejectsALengthThatIsNotWhatTheClipsTake()
        {
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Length -= 0.1f)), "shorter than the clips");
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Length += 0.1f)), "longer than the clips");
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) => l.Length = 0f)), "none claimed");
            Assert.IsTrue(NetChecks.Sane(Broken((ref ReelList l) => l.Length += 0.04f)), "within the tolerance");
        }

        [Test]
        public void Sane_RejectsAClipsSlowShotsTakingTooLong()
        {
            // 12 s at a hundredth of the speed would take 1200 s, however short the claimed length
            Assert.IsFalse(NetChecks.Sane(Broken((ref ReelList l) =>
            {
                l.Clips[0].End = l.Clips[0].Start + 12.0;
                l.Clips[0].Shots[1].Cut = l.Clips[0].Start;
                l.Clips[0].Shots[1].Speed = 0.01f;
                l.Length = 8f;
            })));
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

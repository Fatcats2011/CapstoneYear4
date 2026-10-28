using System;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The highlight reel's shot director (ShotDirector): which invisible camera frames a clip, given two scooters
    /// driving straight along +X and a line-of-sight check the tests control
    /// </summary>
    public class ShotDirectorTests
    {
        const float SPEED = 10f; // metres a second along +X
        const float SLOW = 2f;

        // Two seats driving along +X, one 3 m to the side of the other, recorded 0 to 10 s at 20 Hz
        static ReelRecording Recording(float speed = SPEED)
        {
            var recording = new ReelRecording(400);
            for (int i = 0; i <= 200; i++)
            {
                double time = i / ReelRecording.SAMPLE_RATE;
                for (int seat = 0; seat < 2; seat++)
                {
                    Vector3 ball = new Vector3((float)time * speed, 0f, seat * 3f);
                    recording.Add(seat, new ReelSample
                    {
                        Time = time,
                        Pose = new ScooterPose { Ball = ball, Heading = 90f, ModelPosition = ball, ModelRotation = Quaternion.identity },
                    });
                }
            }
            return recording;
        }

        static Clip Clip(float score)
        {
            return new Clip { Start = 2, End = 6, KeyTime = 4, Score = score, SeatsMask = 0b11, KeyPlace = new Vector3(40f, 0f, 1.5f) };
        }

        static bool Open(Vector3 a, Vector3 b) => true;

        // Rays from the FixedPan camera start 3 m up, from the HighWide camera 18 m up, from the Low camera 0.8 m up
        static bool Near(float y, float height) => Mathf.Abs(y - height) < 0.01f;

        [Test]
        public void Shots_AllClear_SlowAction_AStaticKindWins()
        {
            var director = new ShotDirector(Recording(SLOW), Open);

            var shots = director.Shots(Clip(5f), null);

            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(ShotKind.Low, shots[0].Kind, "the first valid kind from the start of the cycle");
            Assert.AreEqual(2.0, shots[0].Cut, 1e-6, "the first shot starts with the clip");
            Assert.AreEqual(0b11, shots[0].FramedMask);
            Assert.AreEqual(1f, shots[0].Speed);
        }

        [Test]
        public void Shots_FastAction_ConsecutiveClips_UseAStaticKindToo()
        {
            var director = new ShotDirector(Recording(), Open);
            ShotKind? previous = null;
            bool sawStatic = false;
            for (int i = 0; i < 5; i++)
            {
                ShotKind kind = director.Shots(Clip(5f), previous)[0].Kind;
                Assert.AreNotEqual(previous, kind, "never the previous kind");
                sawStatic |= kind != ShotKind.Behind && kind != ShotKind.SideTrack;
                previous = kind;
            }
            Assert.IsTrue(sawStatic, "static cameras are not starved by the moving ones");
        }

        static bool Moving(ShotKind kind) => kind == ShotKind.SideTrack || kind == ShotKind.Behind;

        [Test]
        public void Shots_NeverAMovingKind_FirstOrPeak()
        {
            // A camera riding a scooter shakes with it: only the fixed kinds are chosen, through a run of big clips,
            // after any previous kind, slow or fast, all clear or only the low cameras clear
            foreach (float speed in new[] { SLOW, SPEED, 30f })
            {
                foreach (Func<Vector3, Vector3, bool> clear in new Func<Vector3, Vector3, bool>[] { Open, (a, b) => a.y < 5f, (a, b) => false })
                {
                    var director = new ShotDirector(Recording(speed), clear);
                    foreach (ShotKind? start in new ShotKind?[] { null, ShotKind.Low, ShotKind.HighWide, ShotKind.SideTrack, ShotKind.Behind, ShotKind.FixedPan })
                    {
                        ShotKind? previous = start;
                        for (int i = 0; i < 6; i++)
                        {
                            var shots = director.Shots(Clip(20f), previous);
                            foreach (ReelShot shot in shots)
                                Assert.IsFalse(Moving(shot.Kind), shot.Kind + " at speed " + speed + " after " + previous);
                            previous = shots[0].Kind;
                        }
                    }
                }
            }
        }

        [Test]
        public void Shots_TooFarOrTooCloseToTheAction_IsNotValid()
        {
            // Fast action drives right past a Low camera: far from it for most of the clip, so Low is out of band (at
            // 20 m/s FixedPan and HighWide are still in theirs; much faster, nothing fixed is, and the fallback is Low)
            var director = new ShotDirector(Recording(20f), Open);

            Assert.AreNotEqual(ShotKind.Low, director.Shots(Clip(5f), null)[0].Kind);
        }

        [Test]
        public void Shots_NeverRepeatsThePreviousKind()
        {
            var director = new ShotDirector(Recording(SLOW), Open);

            foreach (ShotKind previous in new[] { ShotKind.Low, ShotKind.HighWide, ShotKind.SideTrack, ShotKind.Behind, ShotKind.FixedPan })
                Assert.AreNotEqual(previous, director.Shots(Clip(5f), previous)[0].Kind);
        }

        [Test]
        public void Shots_BlockedCandidate_IsRejected()
        {
            // Slow action, previous Low: HighWide is next in the cycle. Block every ray from 18 m up and it is not taken.
            var recording = Recording(SLOW);
            Assert.AreEqual(ShotKind.HighWide, new ShotDirector(recording, Open).Shots(Clip(5f), ShotKind.Low)[0].Kind);

            var director = new ShotDirector(recording, (a, b) => !Near(a.y, 18f));
            var shots = director.Shots(Clip(5f), ShotKind.Low);

            Assert.AreNotEqual(ShotKind.HighWide, shots[0].Kind);
        }

        [Test]
        public void Shots_NoSeatsRecorded_NoPeakShot()
        {
            var director = new ShotDirector(new ReelRecording(10), Open);

            Assert.AreEqual(1, director.Shots(Clip(20f), null).Count);
        }

        [Test]
        public void Shots_EverythingBlocked_StillReturnsAShot()
        {
            var director = new ShotDirector(Recording(), (a, b) => false);

            var shots = director.Shots(Clip(5f), null);

            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(ShotKind.Low, shots[0].Kind, "nothing is clear anywhere: a tie goes to the first kind");
            Assert.AreEqual(ShotKind.HighWide, director.Shots(Clip(5f), ShotKind.Low)[0].Kind, "and never the previous");
        }

        [Test]
        public void Shots_NothingFullyClear_TakesTheMostClearCandidate()
        {
            // Only the FixedPan camera (3 m up) has any view, and only of the scooters early in the clip (before x = 40)
            var director = new ShotDirector(Recording(), (a, b) => Near(a.y, 3f) && b.x < 40f);

            var shots = director.Shots(Clip(5f), null);

            Assert.AreEqual(ShotKind.FixedPan, shots[0].Kind, "the most clear, though the last kind tried");
        }

        [Test]
        public void Shots_BigClip_AddsASlowPeakShot()
        {
            var director = new ShotDirector(Recording(SLOW), Open);

            var shots = director.Shots(Clip(20f), null);

            Assert.AreEqual(2, shots.Count);
            Assert.AreEqual(4.0 - ShotDirector.PEAK_LEAD, shots[1].Cut, 1e-6);
            Assert.AreEqual(ShotDirector.PEAK_SPEED, shots[1].Speed);
            Assert.AreEqual(ShotKind.FixedPan, shots[1].Kind, "Low is tried first, but the first shot was Low");
            Assert.AreEqual(1f, shots[0].Speed);
        }

        [Test]
        public void Shots_BigClip_PeakShotIsNeverTheFirstShotsKind()
        {
            var director = new ShotDirector(Recording(SLOW), Open);

            var shots = director.Shots(Clip(20f), ShotKind.FixedPan);
            var after = director.Shots(Clip(20f), ShotKind.Low);

            Assert.AreEqual(2, shots.Count);
            Assert.AreNotEqual(shots[0].Kind, shots[1].Kind);
            Assert.AreEqual(2, after.Count);
            Assert.AreEqual(ShotKind.HighWide, after[0].Kind);
            Assert.AreEqual(ShotKind.Low, after[1].Kind, "the closest peak kind");
        }

        [Test]
        public void Shots_TheCycle_GoesRoundTheFixedKinds()
        {
            var director = new ShotDirector(Recording(SLOW), Open);

            Assert.AreEqual(ShotKind.HighWide, director.Shots(Clip(5f), ShotKind.Low)[0].Kind);
            Assert.AreEqual(ShotKind.FixedPan, director.Shots(Clip(5f), ShotKind.HighWide)[0].Kind);
            Assert.AreEqual(ShotKind.Low, director.Shots(Clip(5f), ShotKind.FixedPan)[0].Kind);
            Assert.AreEqual(ShotKind.FixedPan, director.Shots(Clip(5f), ShotKind.Behind)[0].Kind, "after a moving kind: the next fixed one in enum order");
        }

        [Test]
        public void Shots_BigClip_FirstShotFixedPan_PeakShotIsLow()
        {
            // Previous HighWide, so FixedPan is the first shot and Low is the only peak kind left. Fast and slow action.
            foreach (float speed in new[] { SLOW, SPEED })
            {
                var director = new ShotDirector(Recording(speed), Open);

                var shots = director.Shots(Clip(20f), ShotKind.HighWide);

                Assert.AreEqual(ShotKind.FixedPan, shots[0].Kind, "speed " + speed);
                Assert.AreEqual(2, shots.Count, "speed " + speed);
                Assert.AreEqual(ShotKind.Low, shots[1].Kind, "speed " + speed);
            }
        }

        [Test]
        public void Shots_SmallClip_OneShot()
        {
            var director = new ShotDirector(Recording(), Open);

            Assert.AreEqual(1, director.Shots(Clip(ShotDirector.PEAK_SCORE - 0.1f), null).Count);
        }
    }
}

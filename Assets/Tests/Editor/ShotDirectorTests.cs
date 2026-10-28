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

        // Two seats driving along +X, one 3 m to the side of the other, recorded 0 to 10 s at 20 Hz
        static ReelRecording Recording()
        {
            var recording = new ReelRecording(400);
            for (int i = 0; i <= 200; i++)
            {
                double time = i / ReelRecording.SAMPLE_RATE;
                for (int seat = 0; seat < 2; seat++)
                {
                    Vector3 ball = new Vector3((float)time * SPEED, 0f, seat * 3f);
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

        // Rays from the Behind camera sit 3 m up, from the HighWide camera 18 m up, from a Side track camera 2 m up
        static bool Near(float y, float height) => Mathf.Abs(y - height) < 0.01f;

        [Test]
        public void Shots_AllClear_PicksTheBestFramedKind()
        {
            var director = new ShotDirector(Recording(), Open);

            var shots = director.Shots(Clip(5f), null);

            Assert.AreEqual(1, shots.Count);
            Assert.AreEqual(ShotKind.Behind, shots[0].Kind, "a camera riding the lead scooter stays at its ideal distance the whole clip");
            Assert.AreEqual(2.0, shots[0].Cut, 1e-6, "the first shot starts with the clip");
            Assert.AreEqual(0b11, shots[0].FramedMask);
            Assert.AreEqual(1f, shots[0].Speed);
        }

        [Test]
        public void Shots_NeverRepeatsThePreviousKind()
        {
            var director = new ShotDirector(Recording(), Open);
            ShotKind best = director.Shots(Clip(5f), null)[0].Kind;

            var shots = director.Shots(Clip(5f), best);

            Assert.AreNotEqual(best, shots[0].Kind);
        }

        [Test]
        public void Shots_BlockedCandidate_IsRejected()
        {
            // Every ray from the HighWide spot (18 m up) is blocked, and so are the best kind's
            var recording = Recording();
            var open = new ShotDirector(recording, Open);
            ShotKind best = open.Shots(Clip(5f), null)[0].Kind;
            float bestHeight = best == ShotKind.Behind ? 3f : 2f;

            var director = new ShotDirector(recording, (a, b) => !Near(a.y, 18f) && !Near(a.y, bestHeight));
            var shots = director.Shots(Clip(5f), null);

            Assert.AreNotEqual(ShotKind.HighWide, shots[0].Kind);
            Assert.AreNotEqual(best, shots[0].Kind, "the best kind is blocked, so another is taken");
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
            // Only the Behind camera (3 m up) has any view, and only early in the clip (before it passes x = 25)
            var director = new ShotDirector(Recording(), (a, b) => Near(a.y, 3f) && a.x < 25f);

            var shots = director.Shots(Clip(5f), null);

            Assert.AreEqual(ShotKind.Behind, shots[0].Kind);
        }

        [Test]
        public void Shots_BigClip_AddsASlowPeakShot()
        {
            // Behind is taken for the first shot, so the peak shot is the Behind camera
            var director = new ShotDirector(Recording(), Open);

            var shots = director.Shots(Clip(20f), ShotKind.Behind);

            Assert.AreEqual(2, shots.Count);
            Assert.AreNotEqual(ShotKind.Behind, shots[0].Kind);
            Assert.AreEqual(4.0 - ShotDirector.PEAK_LEAD, shots[1].Cut, 1e-6);
            Assert.AreEqual(ShotDirector.PEAK_SPEED, shots[1].Speed);
            Assert.AreEqual(ShotKind.Behind, shots[1].Kind);
            Assert.AreEqual(1f, shots[0].Speed);
        }

        [Test]
        public void Shots_BigClip_PeakShotIsNeverTheFirstShotsKind()
        {
            var director = new ShotDirector(Recording(), Open);

            var shots = director.Shots(Clip(20f), null);

            if (shots.Count == 2)
                Assert.AreNotEqual(shots[0].Kind, shots[1].Kind);
        }

        [Test]
        public void Shots_SmallClip_OneShot()
        {
            var director = new ShotDirector(Recording(), Open);

            Assert.AreEqual(1, director.Shots(Clip(ShotDirector.PEAK_SCORE - 0.1f), null).Count);
        }
    }
}

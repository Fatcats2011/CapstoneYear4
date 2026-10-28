using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The highlight reel's recording buffer (ReelRecording): a ring of timed scooter samples per seat, read back
    /// interpolated
    /// </summary>
    public class ReelRecordingTests
    {
        static ReelSample Sample(double time, Vector3 ball = default, float heading = 0f, bool hidden = false, byte carrying = 0)
        {
            return new ReelSample
            {
                Time = time,
                Pose = new ScooterPose { Ball = ball, Heading = heading, ModelPosition = ball, ModelRotation = Quaternion.identity },
                Flags = new DriveFlags(false, false, false, 0, true, false, hidden),
                Carrying = carrying,
            };
        }

        [Test]
        public void TryGet_BetweenSamples_Interpolates()
        {
            ReelRecording recording = new ReelRecording(10);
            ReelSample a = Sample(0, Vector3.zero, 350f);
            ReelSample b = Sample(1, new Vector3(10, 0, 0), 10f);
            b.Pose.ModelRotation = Quaternion.Euler(0, 90, 0);
            recording.Add(0, a);
            recording.Add(0, b);

            Assert.IsTrue(recording.TryGet(0, 0.5, out ReelSample mid));
            Assert.AreEqual(5f, mid.Pose.Ball.x, 1e-4f);
            Assert.AreEqual(Mathf.LerpAngle(350f, 10f, 0.5f), mid.Pose.Heading, 1e-3f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, mid.Pose.Heading), 1e-3f, "across the wrap, not the long way round");
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(0, 45, 0), mid.Pose.ModelRotation), 1e-2f, "the model rotation is slerped");
        }

        [Test]
        public void TryGet_FlagsAndCarrying_TakeTheEarlierSample()
        {
            ReelRecording recording = new ReelRecording(10);
            recording.Add(1, Sample(0, hidden: true, carrying: 2));
            recording.Add(1, Sample(1, hidden: false, carrying: 0));

            Assert.IsTrue(recording.TryGet(1, 0.9, out ReelSample sample));
            Assert.IsTrue(sample.Flags.Hidden);
            Assert.AreEqual(2, sample.Carrying);
        }

        [Test]
        public void TryGet_OutsideTheSeatsSamples_ReturnsFalse()
        {
            ReelRecording recording = new ReelRecording(10);
            recording.Add(0, Sample(1));
            recording.Add(0, Sample(2));

            Assert.IsFalse(recording.TryGet(0, 0.5, out _), "before First");
            Assert.IsFalse(recording.TryGet(0, 2.5, out _), "after Last");
            Assert.IsFalse(recording.TryGet(1, 1.5, out _), "a seat with no samples");
            Assert.IsTrue(recording.TryGet(0, 1, out _), "exactly First");
            Assert.IsTrue(recording.TryGet(0, 2, out _), "exactly Last");
        }

        [Test]
        public void Add_PastCapacity_DropsTheOldest()
        {
            ReelRecording recording = new ReelRecording(3);
            for (int i = 0; i < 5; i++)
                recording.Add(0, Sample(i));

            Assert.AreEqual(2.0, recording.First(0));
            Assert.AreEqual(4.0, recording.Last(0));
            Assert.IsTrue(recording.TryGet(0, 3.5, out ReelSample sample));
            Assert.AreEqual(3.5, sample.Time, "the wrapped ring still reads in order");
            Assert.IsFalse(recording.TryGet(0, 1.5, out _), "the dropped samples are gone");
        }

        [Test]
        public void Add_OutOfOrderTime_IsIgnored()
        {
            ReelRecording recording = new ReelRecording(10);
            recording.Add(0, Sample(5));
            recording.Add(0, Sample(5));
            recording.Add(0, Sample(4));

            Assert.AreEqual(5.0, recording.First(0));
            Assert.AreEqual(5.0, recording.Last(0));
        }

        [Test]
        public void Clear_EmptiesEverySeat()
        {
            ReelRecording recording = new ReelRecording(10);
            recording.Add(0, Sample(1));
            recording.Add(3, Sample(1));
            recording.Clear();

            Assert.IsFalse(recording.TryGet(0, 1, out _));
            Assert.IsFalse(recording.TryGet(3, 1, out _));
            recording.Add(0, Sample(0.5));
            Assert.AreEqual(0.5, recording.First(0), "a cleared seat takes earlier times again");
        }
    }
}

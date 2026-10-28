using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The highlight reel's hidden-camera overlay (ReelOverlay): its timestamp, camera number and caption text
    /// </summary>
    public class ReelOverlayTests
    {
        [Test]
        public void Timestamp_FormatsMinutesAndSeconds()
        {
            Assert.AreEqual("02:41", ReelOverlay.Timestamp(161.4f));
            Assert.AreEqual("00:00", ReelOverlay.Timestamp(-3f));
            Assert.AreEqual("00:59", ReelOverlay.Timestamp(59.99f));
            Assert.AreEqual("10:00", ReelOverlay.Timestamp(600f));
        }

        [Test]
        public void CameraNumber_StableAndInRange()
        {
            Vector3 place = new Vector3(12.3f, 4f, -56.7f);
            Assert.AreEqual(ReelOverlay.CameraNumber(place), ReelOverlay.CameraNumber(place), "the same place, the same camera");
            Assert.AreEqual(ReelOverlay.CameraNumber(place), ReelOverlay.CameraNumber(place + new Vector3(0.2f, 9f, 0.1f)),
                "the same metre on the ground, the same camera");

            var seen = new HashSet<int>();
            for (int x = -40; x <= 40; x += 3)
            {
                for (int z = -40; z <= 40; z += 5)
                {
                    int number = ReelOverlay.CameraNumber(new Vector3(x * 1.7f, 0f, z * 2.3f));
                    Assert.That(number, Is.InRange(1, 24));
                    seen.Add(number);
                }
            }
            Assert.Greater(seen.Count, 12, "places spread over the cameras");
        }

        [Test]
        public void CaptionKey_EachCaption()
        {
            StringsCsv csv = StringsCsv.Load();
            var keys = new HashSet<string>();
            foreach (Caption caption in Enum.GetValues(typeof(Caption)))
            {
                string key = ReelOverlay.CaptionKey(caption);
                Assert.IsTrue(keys.Add(key), caption + " has its own key");
                Assert.IsFalse(string.IsNullOrEmpty(csv.Get(key, LocaleCodes.ENGLISH)), key + " is in the strings");
            }
            Assert.IsFalse(string.IsNullOrEmpty(csv.Get(ReelOverlay.REC_KEY, LocaleCodes.ENGLISH)));
            Assert.IsFalse(string.IsNullOrEmpty(csv.Get(ReelOverlay.CAM_KEY, LocaleCodes.ENGLISH)));
        }
    }
}

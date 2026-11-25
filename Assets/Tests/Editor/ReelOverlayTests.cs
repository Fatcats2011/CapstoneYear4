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

        // ---- Name tags over the stand-ins (playtest, 2026-11-25)

        [Test]
        public void TagPlace_OnlyOnScreen_InFront_AndNearEnough()
        {
            Assert.IsTrue(ReelOverlay.TagPlace(new Vector3(0.3f, 0.6f, 5f), 5f, out Vector2 place));
            Assert.AreEqual(new Vector2(0.3f, 0.6f), place, "where the camera sees it");
            Assert.IsFalse(ReelOverlay.TagPlace(new Vector3(0.5f, 0.5f, -2f), 2f, out _), "behind the camera");
            Assert.IsFalse(ReelOverlay.TagPlace(new Vector3(1.2f, 0.5f, 5f), 5f, out _), "off the right");
            Assert.IsFalse(ReelOverlay.TagPlace(new Vector3(0.5f, -0.1f, 5f), 5f, out _), "off the bottom");
            Assert.IsFalse(ReelOverlay.TagPlace(new Vector3(0.5f, 0.5f, 50f), ReelOverlay.TAG_RANGE + 1f, out _), "too far to read");
            Assert.IsTrue(ReelOverlay.TagPlace(new Vector3(0.5f, 0.5f, 40f), ReelOverlay.TAG_RANGE, out _), "at the range");
        }

        [Test]
        public void NameTags_FollowTheirStandIns_HiddenWhenTheStandInIs()
        {
            ReelOverlay overlay = ReelOverlay.Build(null);
            Camera cam = new GameObject("Reel Camera").AddComponent<Camera>();
            Transform near = new GameObject("Stand-in P1").transform;
            Transform far = new GameObject("Stand-in P3").transform;
            try
            {
                const string longName = "Someone With A Very Long Name";
                overlay.BuildNameTags(new[] { "Ann", null, longName, null });
                cam.aspect = 16f / 9f;
                cam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                near.position = new Vector3(0f, -ReelOverlay.TAG_HEIGHT, 10f); // its tag's point: straight ahead
                far.position = new Vector3(0f, 0f, ReelOverlay.TAG_RANGE + 20f);

                overlay.ShowNameTags(cam, seat => seat == 0 ? near : seat == 2 ? far : null);

                TMPro.TMP_Text ann = overlay.transform.Find("Name Tag P1").GetComponent<TMPro.TMP_Text>();
                Assert.IsTrue(ann.gameObject.activeSelf, "in view");
                Assert.AreEqual("Ann", ann.text);
                Assert.AreEqual(0.5f, ann.rectTransform.anchorMin.x, 0.01f, "over the stand-in");
                Assert.AreEqual(0.5f, ann.rectTransform.anchorMin.y, 0.01f);
                Assert.IsNull(overlay.transform.Find("Name Tag P2"), "no stand-in, no tag");
                TMPro.TMP_Text other = overlay.transform.Find("Name Tag P3").GetComponent<TMPro.TMP_Text>();
                Assert.IsFalse(other.gameObject.activeSelf, "too far away");
                Assert.AreEqual(StealFeedbackRules.Short(longName), other.text, "a long name is shortened");

                near.gameObject.SetActive(false); // the rider was hidden then (a respawn)
                overlay.ShowNameTags(cam, seat => seat == 0 ? near : seat == 2 ? far : null);
                Assert.IsFalse(ann.gameObject.activeSelf);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(overlay.gameObject);
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
                UnityEngine.Object.DestroyImmediate(near.gameObject);
                UnityEngine.Object.DestroyImmediate(far.gameObject);
            }
        }
    }
}

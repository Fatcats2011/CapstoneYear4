using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Another machine's scooter proxy stops following poses that aren't numbers (PoseHold, in OwnerNetworkTransform): a
    /// modified game's NaN pose would make Unity log an error every frame. It follows again after a second of good poses,
    /// which clears Netcode's interpolation of the bad one. See docs/online-safety.md
    /// </summary>
    public class PoseHoldTests
    {
        static readonly Vector3 Here = new Vector3(10f, 2f, -30f);
        static readonly Vector3 NotANumber = new Vector3(float.NaN, 0f, 0f);

        [Test]
        public void GoodPoses_AreFollowed()
        {
            PoseHold hold = new PoseHold();
            hold.Received(Here, Quaternion.identity, 0f);

            Assert.IsTrue(hold.Following(0.1f));
        }

        [Test]
        public void ParkedFarBelowTheMap_IsStillFollowed()
        {
            PoseHold hold = new PoseHold();
            hold.Received(new Vector3(0f, OnlineScooter.PARKED_Y, 0f), Quaternion.identity, 0f);

            Assert.IsTrue(hold.Following(0.1f), "proxies wait parked before the first pose");
        }

        [Test]
        public void ANanPose_IsHeld_UntilASecondOfGoodOnes()
        {
            PoseHold hold = new PoseHold();
            hold.Received(NotANumber, Quaternion.identity, 1f);
            Assert.IsFalse(hold.Following(1f), "held at the last good pose");

            hold.Received(Here, Quaternion.identity, 1.5f);
            Assert.IsFalse(hold.Following(1.6f), "half a second of good poses isn't enough");

            hold.Received(Here, Quaternion.identity, 2.1f);
            Assert.IsTrue(hold.Following(2f + PoseHold.CLEAR_AFTER), "a second after the bad one: followed again");
        }

        [Test]
        public void ABrokenRotation_IsHeldToo()
        {
            PoseHold hold = new PoseHold();
            hold.Received(Here, new Quaternion(float.PositiveInfinity, 0f, 0f, 1f), 0f);

            Assert.IsFalse(hold.Following(0.1f));
        }

        [Test]
        public void ABadPoseDuringTheWait_StartsItAgain()
        {
            PoseHold hold = new PoseHold();
            hold.Received(NotANumber, Quaternion.identity, 0f);
            hold.Received(NotANumber, Quaternion.identity, 0.9f);

            Assert.IsFalse(hold.Following(1.5f), "the wait counts from the latest bad pose");
            Assert.IsTrue(hold.Following(0.9f + PoseHold.CLEAR_AFTER));
        }
    }
}

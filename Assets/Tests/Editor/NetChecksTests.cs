using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What no honest game sends (NetChecks): numbers that aren't finite, places outside the world, undefined enum values
    /// and a scooter pose with a broken rotation. Every machine refuses them. See docs/online-safety.md
    /// </summary>
    public class NetChecksTests
    {
        [Test]
        public void InWorld_RefusesNaNInfinityAndFarAway()
        {
            Assert.IsTrue(NetChecks.InWorld(new Vector3(120f, -4f, 4999f)));
            Assert.IsFalse(NetChecks.InWorld(new Vector3(float.NaN, 0f, 0f)));
            Assert.IsFalse(NetChecks.InWorld(new Vector3(0f, float.PositiveInfinity, 0f)));
            Assert.IsFalse(NetChecks.InWorld(new Vector3(0f, 0f, 1e30f)));
            Assert.IsFalse(NetChecks.InWorld(new Vector3(-5001f, 0f, 0f)));
        }

        [Test]
        public void Finite_RefusesNaNAndInfinity()
        {
            Assert.IsTrue(NetChecks.Finite(-3.5f));
            Assert.IsFalse(NetChecks.Finite(float.NaN));
            Assert.IsFalse(NetChecks.Finite(float.NegativeInfinity));
        }

        [Test]
        public void Defined_RefusesUndefinedEnums()
        {
            Assert.IsTrue(NetChecks.Defined(GameState.MainLoop));
            Assert.IsFalse(NetChecks.Defined((GameState)99));
            Assert.IsFalse(NetChecks.Defined((Achievement)12345));
            Assert.IsFalse(NetChecks.Defined((MatchScene)(-1)));
        }

        [Test]
        public void Sane_RefusesABadRotationOrPlace()
        {
            ScooterPose good = new ScooterPose { Ball = new Vector3(10, 2, 10), Heading = 90f, ModelPosition = new Vector3(10, 1, 10), ModelRotation = Quaternion.Euler(0, 90, 0) };
            Assert.IsTrue(NetChecks.Sane(good));

            ScooterPose badRotation = good;
            badRotation.ModelRotation = new Quaternion(5f, 0f, 0f, 0f);
            Assert.IsFalse(NetChecks.Sane(badRotation), "not a rotation");

            ScooterPose nanHeading = good;
            nanHeading.Heading = float.NaN;
            Assert.IsFalse(NetChecks.Sane(nanHeading));

            ScooterPose farAway = good;
            farAway.Ball = new Vector3(0f, 0f, 1e9f);
            Assert.IsFalse(NetChecks.Sane(farAway));
        }

        [Test]
        public void Sane_AnOrderChangesPlacesAndHeights()
        {
            OrderChange drop = OrderChange.Drop(1, 3, new Vector3(5, 0, 5), 2f, 4, new Vector3(6, 0, 6), 2f, true);
            Assert.IsTrue(NetChecks.Sane(drop));

            OrderChange nowhere = drop;
            nowhere.Spot2 = new Vector3(float.NaN, 0f, 0f);
            Assert.IsFalse(NetChecks.Sane(nowhere));

            OrderChange tooHigh = drop;
            tooHigh.Height = float.PositiveInfinity;
            Assert.IsFalse(NetChecks.Sane(tooHigh));
        }

        [Test]
        public void Hosting_ADropAtNoPlace_IsRefused()
        {
            Assert.IsTrue(NetChecks.InWorld(new Vector3(1, 0, 1)) && NetChecks.InWorld(new Vector3(2, 0, 2)));
            Assert.IsFalse(OnlineOrders.AcceptsDrop(new Vector3(float.NaN, 0, 0), new Vector3(1e30f, 0, 0)));
            Assert.IsFalse(OnlineOrders.AcceptsDrop(new Vector3(1, 0, 1), new Vector3(1e30f, 0, 0)));
            Assert.IsTrue(OnlineOrders.AcceptsDrop(new Vector3(1, 0, 1), new Vector3(2, 0, 2)));
        }
    }
}

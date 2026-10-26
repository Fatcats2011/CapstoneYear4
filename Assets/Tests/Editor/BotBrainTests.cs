using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A CPU driver's choices (BotBrain, bots phase G1): the nearest order free to take, else where its held order goes;
    /// steering towards the target; easing off the throttle in a sharp turn; boosting only lined up on a far target; and
    /// backing out when it's stuck
    /// </summary>
    public class BotBrainTests
    {
        [Test]
        public void Nearest_TheClosestSpot_OnTheGround()
        {
            List<Vector3> spots = new List<Vector3> { new Vector3(100, 0, 0), new Vector3(0, 50, 20), new Vector3(-30, 0, 0) };

            Assert.AreEqual(1, BotBrain.Nearest(Vector3.zero, spots), "height doesn't count: 20 m away on the ground");
            Assert.AreEqual(-1, BotBrain.Nearest(Vector3.zero, new List<Vector3>()));
        }

        [Test]
        public void Steer_TowardsTheTarget()
        {
            Assert.AreEqual(0f, BotBrain.Steer(Vector3.forward, Vector3.forward * 10f), 1e-4f, "straight ahead");
            Assert.Greater(BotBrain.Steer(Vector3.forward, Vector3.right * 10f), 0.99f, "to the right: full right");
            Assert.Less(BotBrain.Steer(Vector3.forward, new Vector3(-1, 0, 1)), -0.5f, "ahead and left: left");
            Assert.Greater(BotBrain.Steer(Vector3.forward, Vector3.back * 10f + Vector3.right * 0.1f), 0.99f, "behind: turn hard");
        }

        [Test]
        public void Steer_IgnoresHeight()
        {
            Assert.AreEqual(0f, BotBrain.Steer(Vector3.forward, new Vector3(0, 30, 10)), 1e-4f);
        }

        [Test]
        public void Throttle_FullAhead_EasesInASharpTurn()
        {
            Assert.AreEqual(1f, BotBrain.Throttle(5f));
            Assert.Less(BotBrain.Throttle(120f), 1f);
            Assert.Greater(BotBrain.Throttle(120f), 0f, "never stops dead");
        }

        [Test]
        public void Boost_OnlyLinedUp_OnAFarTarget()
        {
            Assert.IsTrue(BotBrain.Boosts(5f, 80f));
            Assert.IsFalse(BotBrain.Boosts(40f, 80f), "not lined up");
            Assert.IsFalse(BotBrain.Boosts(5f, 10f), "too close: it would overshoot");
        }

        [Test]
        public void NextCorner_TheFirstOneNotReachedYet()
        {
            List<Vector3> corners = new List<Vector3> { Vector3.zero, new Vector3(2, 0, 0), new Vector3(20, 0, 0), new Vector3(20, 0, 30) };

            Assert.AreEqual(2, BotBrain.NextCorner(Vector3.zero, corners, 1), "a corner within reach is skipped");
            Assert.AreEqual(2, BotBrain.NextCorner(new Vector3(10, 0, 0), corners, 2), "on its way to the bend");
            Assert.AreEqual(3, BotBrain.NextCorner(new Vector3(19, 0, 1), corners, 2), "at the bend: on to the next");
            Assert.AreEqual(3, BotBrain.NextCorner(new Vector3(19, 0, 10), corners, 3), "never back to a corner it passed");
        }

        [Test]
        public void NextCorner_AllReached_TheLast_NoneIsMinusOne()
        {
            List<Vector3> corners = new List<Vector3> { Vector3.zero, new Vector3(1, 0, 0) };

            Assert.AreEqual(1, BotBrain.NextCorner(Vector3.zero, corners, 1));
            Assert.AreEqual(-1, BotBrain.NextCorner(Vector3.zero, new List<Vector3>(), 1));
        }

        [Test]
        public void NextCorner_IgnoresHeight()
        {
            List<Vector3> corners = new List<Vector3> { Vector3.zero, new Vector3(1, 30, 0), new Vector3(30, 0, 0) };

            Assert.AreEqual(2, BotBrain.NextCorner(Vector3.zero, corners, 1), "a corner 30 m up but 1 m along is reached");
        }

        [Test]
        public void Stuck_NoProgressForAWhile_BacksOut()
        {
            BotStuck stuck = new BotStuck();

            Assert.IsFalse(stuck.Tick(Vector3.zero, 0.1f));
            for (int i = 0; i < 25; i++)
                stuck.Tick(new Vector3(0.01f * i, 0, 0), 0.1f);
            Assert.IsTrue(stuck.Reversing, "barely moved in 2.5 s");

            for (int i = 0; i < 12; i++)
                stuck.Tick(Vector3.zero, 0.1f);
            Assert.IsFalse(stuck.Reversing, "backs out for a moment, then goes again");
        }

        [Test]
        public void Stuck_Moving_NeverBacksOut()
        {
            BotStuck stuck = new BotStuck();

            for (int i = 0; i < 50; i++)
                stuck.Tick(new Vector3(i * 1.5f, 0, 0), 0.1f);

            Assert.IsFalse(stuck.Reversing);
        }
    }
}

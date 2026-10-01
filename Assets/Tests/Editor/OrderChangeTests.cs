using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// One change to an order, as it travels online: every kind arrives as it was sent
    /// </summary>
    public class OrderChangeTests
    {
        static IEnumerable<OrderChange> EveryKind()
        {
            yield return OrderChange.Spawn(11, true);
            yield return OrderChange.Pickup(11, 2);
            yield return OrderChange.Deliver(11, 2);
            yield return OrderChange.Drop(2, 11, new Vector3(1, 2, 3), 4.5f, 12, new Vector3(-1, 0, 7), 9f, true);
            yield return OrderChange.Erase(11);
            yield return OrderChange.EraseGold(13);
            yield return OrderChange.Steal(11, 0, 2);
        }

        [TestCaseSource(nameof(EveryKind))]
        public void AChange_ArrivesAsItWasSent(OrderChange sent)
        {
            using (FastBufferWriter writer = new FastBufferWriter(256, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out OrderChange received);
                    Assert.AreEqual(sent, received);
                }
            }
        }

        [Test]
        public void Drop_CarriesBothOrdersSpotsAndHeights()
        {
            OrderChange drop = OrderChange.Drop(2, 11, new Vector3(1, 2, 3), 4.5f, 12, new Vector3(-1, 0, 7), 9f, true);
            Assert.AreEqual(OrderChangeKind.Drop, drop.Kind);
            Assert.AreEqual(2, drop.Seat);
            Assert.AreEqual(11, drop.Order);
            Assert.AreEqual(12, drop.Order2);
            Assert.AreEqual(new Vector3(1, 2, 3), drop.Spot);
            Assert.AreEqual(4.5f, drop.Height);
            Assert.AreEqual(new Vector3(-1, 0, 7), drop.Spot2);
            Assert.AreEqual(9f, drop.Height2);
            Assert.IsTrue(drop.Flag, "spins out");
        }

        [Test]
        public void Steal_CarriesTheOrder_TheThief_AndTheVictim()
        {
            OrderChange steal = OrderChange.Steal(11, 0, 2);
            Assert.AreEqual(OrderChangeKind.Steal, steal.Kind);
            Assert.AreEqual(11, steal.Order);
            Assert.AreEqual(0, steal.Seat, "the thief");
            Assert.AreEqual(2, steal.Seat2, "the victim");
            Assert.AreEqual(-1, OrderChange.Pickup(11, 0).Seat2, "only a steal has a second seat");
        }

        [Test]
        public void ChangesWithoutAPlayer_HaveNoSeat()
        {
            Assert.AreEqual(-1, OrderChange.Spawn(11, false).Seat);
            Assert.AreEqual(-1, OrderChange.Erase(11).Seat);
            Assert.AreEqual(-1, OrderChange.EraseGold(11).Seat);
        }
    }
}

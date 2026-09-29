using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Who may change orders, and the host's changes going out: orders change offline and on the host as always, and on a
    /// client only while it replays the host's change. On an online host each outermost change goes out once
    /// </summary>
    public class OrderSyncTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<OrderChange> told = new List<OrderChange>();

        [SetUp]
        public void SetUp()
        {
            OrderSync.Changed += told.Add;
        }

        [TearDown]
        public void TearDown()
        {
            OrderSync.Reset();
            GameAuthority.Role = NetworkRole.Offline;
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
            told.Clear();
        }

        [TestCase(NetworkRole.Offline)]
        [TestCase(NetworkRole.Host)]
        public void MayChange_OfflineAndOnTheHost_Always(NetworkRole role)
        {
            GameAuthority.Role = role;
            Assert.IsTrue(OrderSync.MayChange);
        }

        [Test]
        public void MayChange_OnAClient_OnlyWhileShowingTheHostsChange()
        {
            GameAuthority.Role = NetworkRole.Client;
            bool during = false;
            OrderSync.Show(() => during = OrderSync.MayChange);
            Assert.IsTrue(during, "replaying the host's change");
            Assert.IsFalse(OrderSync.MayChange, "on its own");
        }

        [Test]
        public void Change_OnAnOnlineHost_GoesOutOnce_WithWhatItDoesOnTheWay()
        {
            GameAuthority.Role = NetworkRole.Host;
            using (OrderSync.Change(OrderChange.Deliver(7, 1)))
            using (OrderSync.Change(OrderChange.Erase(7))) { }
            CollectionAssert.AreEqual(new[] { OrderChange.Deliver(7, 1) }, told);
        }

        [Test]
        public void Change_OneAfterAnother_EachGoesOut()
        {
            GameAuthority.Role = NetworkRole.Host;
            using (OrderSync.Change(OrderChange.Spawn(7, true))) { }
            using (OrderSync.Change(OrderChange.Pickup(7, 0))) { }
            CollectionAssert.AreEqual(new[] { OrderChange.Spawn(7, true), OrderChange.Pickup(7, 0) }, told);
        }

        [TestCase(NetworkRole.Offline)]
        [TestCase(NetworkRole.Client)]
        public void Change_OfflineOrOnAClient_GoesNowhere(NetworkRole role)
        {
            GameAuthority.Role = role;
            OrderSync.Show(EraseSeven); // a client replaying the host's change doesn't send it back
            EraseSeven();
            CollectionAssert.IsEmpty(told);
        }

        static void EraseSeven()
        {
            using (OrderSync.Change(OrderChange.Erase(7))) { }
        }

        [Test]
        public void Change_ThatThrows_StillEnds_SoTheNextGoesOut()
        {
            GameAuthority.Role = NetworkRole.Host;
            Assert.Throws<InvalidOperationException>(ThrowInsideAChange);
            using (OrderSync.Change(OrderChange.Erase(8))) { }
            CollectionAssert.AreEqual(new[] { OrderChange.Erase(7), OrderChange.Erase(8) }, told);
        }

        static void ThrowInsideAChange()
        {
            using (OrderSync.Change(OrderChange.Erase(7)))
                throw new InvalidOperationException();
        }

        [Test]
        public void AskDrop_TellsTheListener_WhoDropsAndWhere()
        {
            OrderHandler handler = objects.Add<OrderHandler>(), asked = null;
            Vector3 first = default, second = default;
            bool spinOut = true;
            OrderSync.DropAsked += (h, a, b, s) => { asked = h; first = a; second = b; spinOut = s; };

            OrderSync.AskDrop(handler, Vector3.up, Vector3.right, false);

            Assert.AreSame(handler, asked);
            Assert.AreEqual(Vector3.up, first);
            Assert.AreEqual(Vector3.right, second);
            Assert.IsFalse(spinOut);
        }

        [Test]
        public void SeatOf_AndHandlerIn_FollowTheRoster()
        {
            PlayerInstantiate players = objects.Add<PlayerInstantiate>();
            Reflect.SetSingleton(players);
            GameObject avatar = objects.NewGameObject("P3");
            GameObject control = new GameObject("Control");
            control.transform.SetParent(avatar.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            players.Roster.JoinRemoteAt(avatar, 7, 2);

            Assert.AreEqual(2, OrderSync.SeatOf(handler));
            Assert.AreSame(handler, OrderSync.HandlerIn(2));
            Assert.IsNull(OrderSync.HandlerIn(0), "an empty seat");
            Assert.AreEqual(-1, OrderSync.SeatOf(objects.Add<OrderHandler>()), "nobody's");
        }
    }
}

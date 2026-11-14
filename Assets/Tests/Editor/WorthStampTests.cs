using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The stamp over a stolen package (WorthStamp): "×2" after one steal, doubling with each, gone when the order starts
    /// afresh, never on the golden order. Edit mode: nothing starts, so these reach the members directly
    /// </summary>
    public class WorthStampTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        Order NewOrder(Constants.OrderValue tier, int tierIndex)
        {
            Order order = objects.Add<Order>();
            order.value = tier;
            order.orderMeshObject = objects.NewGameObject("Mesh");
            order.meshScale = new float[] { 1f, 1f, 1f, 1f };
            order.meshScale[tierIndex] = 2f;
            return order;
        }

        static WorthStamp StampOf(Order order)
        {
            return order.GetComponentInChildren<WorthStamp>(true);
        }

        [Test]
        public void AnOrderNeverStolen_HasNoStamp()
        {
            Order order = NewOrder(Constants.OrderValue.Easy, 0);

            order.ResetSteals();

            Assert.IsNull(StampOf(order));
        }

        [Test]
        public void AStolenOrder_IsStampedTimesTwo()
        {
            Order order = NewOrder(Constants.OrderValue.Easy, 0);

            order.CountSteal();

            Assert.AreEqual("×2", StampOf(order).Text);
            Assert.IsTrue(StampOf(order).gameObject.activeSelf);
        }

        [Test]
        public void EachSteal_DoublesTheStamp()
        {
            Order order = NewOrder(Constants.OrderValue.Hard, 2);

            order.CountSteal();
            order.CountSteal();

            Assert.AreEqual("×4", StampOf(order).Text);
            Assert.AreEqual(1, order.GetComponentsInChildren<WorthStamp>(true).Length, "one stamp, changed");
        }

        [Test]
        public void AnOrderStartingAfresh_LosesItsStamp()
        {
            Order order = NewOrder(Constants.OrderValue.Easy, 0);
            order.CountSteal();

            order.ResetSteals();

            Assert.IsFalse(StampOf(order).gameObject.activeSelf);
            Assert.AreEqual("", StampOf(order).Text);
        }

        [Test]
        public void TheGoldenOrder_IsNeverStamped()
        {
            Order order = NewOrder(Constants.OrderValue.Golden, 3);

            order.CountSteal();

            Assert.IsNull(StampOf(order));
        }

        [Test]
        public void TheStamp_DoesNotSwellWithThePackage()
        {
            Order order = NewOrder(Constants.OrderValue.Easy, 0);

            order.CountSteal();

            Assert.AreNotSame(order.orderMeshObject.transform, StampOf(order).transform.parent, "it hangs off the order, not its swelling mesh");
        }
    }
}

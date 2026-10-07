using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Order keys (online, an order change names its order by key): every machine gives a scene order the same key, from
    /// its scene, value, pickup point and dropoff point. OrderBook finds the loaded orders by key
    /// </summary>
    public class OrderBookTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            OrderBook.Clear();
            objects.DestroyAll();
        }

        Order OrderWithKey(int key)
        {
            Order order = objects.Add<Order>();
            order.key = key;
            return order;
        }

        [Test]
        public void KeyOf_TheSameOrder_IsTheSameOnEveryMachine()
        {
            // FNV-1a over exact bits: nothing that differs between runs, such as string.GetHashCode
            Assert.AreEqual(742737598, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy,
                new Vector3(3.1f, 4.43f, -11.5f), new Vector3(-89.8f, 4.43f, 41.6f)));
        }

        [Test]
        public void KeyOf_AnyDifference_GivesAnotherKey()
        {
            Vector3 pickup = new Vector3(3.1f, 4.43f, -11.5f), dropoff = new Vector3(-89.8f, 4.43f, 41.6f);
            int key = OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy, pickup, dropoff);
            Assert.AreNotEqual(key, OrderBook.KeyOf("FinalAreaScene", Constants.OrderValue.Easy, pickup, dropoff), "another scene");
            Assert.AreNotEqual(key, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Medium, pickup, dropoff), "another value");
            Assert.AreNotEqual(key, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy, pickup + Vector3.right, dropoff), "another pickup");
            Assert.AreNotEqual(key, OrderBook.KeyOf("Design Scene(Main)", Constants.OrderValue.Easy, pickup, dropoff + Vector3.right), "another dropoff");
        }

        [Test]
        public void Find_GivesTheOrderAddedUnderAKey_AndNothingElse()
        {
            Order order = OrderWithKey(5);
            OrderBook.Add(order);
            Assert.AreSame(order, OrderBook.Find(5));
            Assert.IsNull(OrderBook.Find(6), "another key");
            Assert.IsNull(OrderBook.Find(OrderBook.NONE), "no order");
        }

        [Test]
        public void Remove_TakesOnlyThatOrderOut()
        {
            Order first = OrderWithKey(5), second = OrderWithKey(5);
            OrderBook.Add(first);
            OrderBook.Remove(second);
            Assert.AreSame(first, OrderBook.Find(5), "another order under its key stays");
            OrderBook.Remove(first);
            Assert.IsNull(OrderBook.Find(5));
        }

        [Test]
        public void Add_TwoLiveOrdersUnderOneKey_IsAnError_AndTheFirstStays()
        {
            Order first = OrderWithKey(5), second = OrderWithKey(5);
            OrderBook.Add(first);
            LogAssert.Expect(LogType.Error, new Regex("share the key 5"));
            OrderBook.Add(second);
            Assert.AreSame(first, OrderBook.Find(5));
        }

        [Test]
        public void Add_TheKeyOfAnOrderThatsGone_IsTakenOver()
        {
            Order gone = OrderWithKey(5), next = OrderWithKey(5);
            OrderBook.Add(gone);
            Object.DestroyImmediate(gone.gameObject);
            OrderBook.Add(next); // no error: its scene unloaded
            Assert.AreSame(next, OrderBook.Find(5));
        }
    }
}

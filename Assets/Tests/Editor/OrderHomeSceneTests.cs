using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoA.Tests
{
    /// <summary>
    /// An order the order manager takes back stays in the scene it loaded with. The order manager outlives scenes
    /// (DontDestroyOnLoad): an order under it outlived the final scene, and its Update threw every frame in the menu
    /// (playtest, 2026-10-24: the golden order). Edit mode: nothing starts
    /// </summary>
    public class OrderHomeSceneTests
    {
        readonly TestObjects objects = new TestObjects();
        Scene home;

        [SetUp]
        public void SetUp()
        {
            home = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [TestCase(Constants.OrderValue.Easy)]
        [TestCase(Constants.OrderValue.Golden)]
        public void RemovedOrder_StaysInItsOwnScene(Constants.OrderValue value)
        {
            OrderManager manager = objects.Add<OrderManager>();
            Order order = objects.Add<Order>();
            order.value = value;
            order.homeScene = home; // Awake sets it in Play Mode

            manager.RemoveOrder(order);

            Assert.AreNotSame(manager.transform, order.transform.parent, "under the order manager");
            Assert.AreEqual(home, order.gameObject.scene, "in its own scene");
        }
    }
}

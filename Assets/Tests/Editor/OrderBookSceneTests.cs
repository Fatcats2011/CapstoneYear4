using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoA.Tests
{
    /// <summary>
    /// Every order in a match scene has its own key (scene, value, pickup and dropoff), so online an order change always
    /// names one order. A scene edit that gives two orders the same points fails here. Opens the scenes in the editor,
    /// which takes a while
    /// </summary>
    public class OrderBookSceneTests
    {
        Scene scene;

        [TearDown]
        public void TearDown()
        {
            if (scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }

        [TestCase("Assets/Scenes/Design Scene(Main).unity", 28)] // 19 in the waves, 4 tutorial, 4 cardboard-cutout, 1 unused golden
        [TestCase("Assets/Scenes/FinalAreaScene.unity", 1)]
        public void EveryOrderInAMatchScene_HasItsOwnKey(string path, int orders)
        {
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            List<int> keys = new List<int>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Order order in root.GetComponentsInChildren<Order>(true))
                    keys.Add(OrderBook.KeyOf(order));
            }

            Assert.AreEqual(orders, keys.Count, "orders in the scene");
            CollectionAssert.AllItemsAreUnique(keys);
            CollectionAssert.DoesNotContain(keys, OrderBook.NONE);
        }
    }
}

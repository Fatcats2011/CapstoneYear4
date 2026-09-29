using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DoA.Tests
{
    /// <summary>
    /// Online, a client's orders only change when the host says so: its own spawner, beacons and respawns wait for the
    /// host, and a respawn asks the host to drop its player's orders. Offline, each of these calls goes on to scene
    /// objects these bare orders and players don't have, so each gate must come first. And an order goes back into its
    /// own scene when the player holding it goes back to the menu
    /// </summary>
    public class OrderRulesTests
    {
        const string HOME_SCENE = "Assets/Scenes/SplashScreen.unity";

        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            OrderSync.Reset();
            objects.DestroyAll();
        }

        /// <summary>
        /// A player as the triggers see them: a root with the orders handler (and its scooter) and the ball as children
        /// </summary>
        OrderHandler NewPlayer(string name)
        {
            GameObject player = objects.NewGameObject(name);
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            Reflect.SetField(handler, "ball", control.AddComponent<BallDriving>());
            GameObject sphere = new GameObject("Ball Of Fun");
            sphere.transform.SetParent(player.transform);
            sphere.AddComponent<SphereCollider>();
            return handler;
        }

        [Test]
        public void InitOrder_OnAClient_WaitsForTheHost()
        {
            // e.g. the golden round's FinalOrder.Start: the host's Spawn shows it
            GameAuthority.Role = NetworkRole.Client;
            Order order = objects.Add<Order>();
            order.InitOrder();
            Assert.IsFalse(order.IsActive);
        }

        [Test]
        public void EraseOrder_OnAClient_WaitsForTheHost()
        {
            GameAuthority.Role = NetworkRole.Client;
            Order order = objects.Add<Order>();
            order.IsActive = true;
            order.EraseOrder();
            Assert.IsTrue(order.IsActive);
        }

        [Test]
        public void EraseGoldWithoutDelivering_OnAClient_WaitsForTheHost()
        {
            GameAuthority.Role = NetworkRole.Client;
            Order order = objects.Add<Order>();
            order.IsActive = true;
            order.EraseGoldWithoutDelivering();
            Assert.IsTrue(order.IsActive);
        }

        [Test]
        public void AddOrder_OnAClient_WaitsForTheHost()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler player = NewPlayer("Player 2");
            Order order = objects.Add<Order>();
            player.AddOrder(order);
            Assert.IsFalse(player.HasOrder);
            Assert.IsNull(order.PlayerHolding);
        }

        [Test]
        public void DeliverOrder_OnAClient_WaitsForTheHost()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler player = NewPlayer("Player 2");
            Order order = objects.Add<Order>();
            Reflect.SetField(player, "order1", order);
            player.DeliverOrder(order);
            Assert.AreSame(order, Reflect.GetField(player, "order1"), "still held");
            Assert.AreEqual(0, player.Score);
        }

        [Test]
        public void DropEverything_OnAClient_AsksTheHost_AndKeepsTheOrders()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler player = NewPlayer("Player 2");
            Order order = objects.Add<Order>();
            Reflect.SetField(player, "order1", order);
            OrderHandler asked = null;
            Vector3 first = default;
            OrderSync.DropAsked += (h, a, b, s) => { asked = h; first = a; };

            player.DropEverything(Vector3.up, Vector3.right, false);

            Assert.AreSame(player, asked, "the host is asked");
            Assert.AreEqual(Vector3.up, first);
            Assert.AreSame(order, Reflect.GetField(player, "order1"), "the host drops it");
        }

        [Test]
        public void ReturnHome_TakesTheOrderOffItsScooter_BackIntoItsOwnScene()
        {
            // A second scene for the order to come from: a small saved one (the test runner's own scene is untitled, and
            // the editor makes no new scene beside an untitled one)
            Scene home = EditorSceneManager.OpenScene(HOME_SCENE, OpenSceneMode.Additive);
            try
            {
                Order order = new GameObject("Order").AddComponent<Order>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(order.gameObject, home);
                Reflect.SetField(order, "homeScene", home);
                order.transform.SetParent(NewPlayer("Player 1").transform); // riding on a scooter, in another scene

                order.ReturnHome();

                Assert.IsNull(order.transform.parent, "off the scooter");
                Assert.AreEqual(home, order.gameObject.scene, "back where it loaded");
            }
            finally
            {
                EditorSceneManager.CloseScene(home, true);
            }
        }
    }
}

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
            PlayerInstantiate.instance = null;
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
            handler.ball = control.AddComponent<BallDriving>();
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
            player.order1 = order;
            player.DeliverOrder(order);
            Assert.AreSame(order, player.order1, "still held");
            Assert.AreEqual(0, player.Score);
        }

        [Test]
        public void DropEverything_OnAClient_AsksTheHost_AndKeepsTheOrders()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler player = NewPlayer("Player 2");
            Order order = objects.Add<Order>();
            player.order1 = order;
            OrderHandler asked = null;
            Vector3 first = default;
            OrderSync.DropAsked += (h, a, b, s) => { asked = h; first = a; };

            player.DropEverything(Vector3.up, Vector3.right, false);

            Assert.AreSame(player, asked, "the host is asked");
            Assert.AreEqual(Vector3.up, first);
            Assert.AreSame(order, player.order1, "the host drops it");
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
                order.homeScene = home;
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

        [Test]
        public void TakeOrderFrom_OnAClient_WaitsForTheHost()
        {
            GameAuthority.Role = NetworkRole.Client;
            OrderHandler thief = NewPlayer("Player 1"), victim = NewPlayer("Player 2");
            Order order = objects.Add<Order>();
            victim.order1 = order;

            thief.TakeOrderFrom(victim, order);

            Assert.AreSame(order, victim.order1, "the host's Steal moves it");
            Assert.IsFalse(thief.HasOrder);
        }

        [Test]
        public void RemovePlayerHolding_TheGoldenOrder_NoLongerSlowsItsLastHolder()
        {
            // A steal takes it (2024: the robbed player stayed slowed for the rest of the golden round)
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>(); // the compass markers look for this machine's players
            OrderHandler robbed = NewPlayer("Player 1");
            robbed.HasGoldenOrder = true;
            Order golden = GoldenOrderHeldBy(robbed);

            golden.RemovePlayerHolding();

            Assert.IsFalse(robbed.HasGoldenOrder);
        }

        // The golden order, held by a player, with the parts letting go of it touches (Awake sets these in a real order)
        Order GoldenOrderHeldBy(OrderHandler player)
        {
            Order golden = objects.Add<Order>();
            golden.value = Constants.OrderValue.Golden;
            golden.arrow = objects.NewGameObject("Arrow");
            golden.orderMeshObject = objects.NewGameObject("Mesh");
            golden.ogMeshRot = Quaternion.identity;
            golden.compassMarker = objects.Add<CompassMarker>();
            golden.playerHolding = player;
            return golden;
        }
    }
}

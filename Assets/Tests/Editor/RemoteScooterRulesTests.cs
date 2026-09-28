using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Online, another machine's scooter (a RemoteAvatar) doesn't act on this machine until orders and steals are shared
    /// (roadmap Tasks 3.5-3.6): it doesn't fall in water here, collect orders, steal, clash, or freeze when the main game
    /// ends. Its own machine does all that. EditMode: the scripts' messages are called directly on bare scooters
    /// </summary>
    public class RemoteScooterRulesTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            Reflect.SetSingleton<OrderManager>(null);
        }

        // A bare scooter: the avatar root (with a RemoteAvatar when it's another machine's), its ball and its Control
        GameObject Scooter(bool remote)
        {
            GameObject root = objects.NewGameObject(remote ? "P2" : "P1");
            if (remote)
                root.AddComponent<RemoteAvatar>();

            GameObject ball = new GameObject("Ball Of Fun");
            ball.transform.SetParent(root.transform);
            ball.AddComponent<SphereCollider>();
            ball.AddComponent<Respawn>();

            GameObject control = new GameObject("Control");
            control.transform.SetParent(root.transform);
            control.AddComponent<OrderHandler>();
            return root;
        }

        [Test]
        public void IsRemote_IsTrueForThePartsOfAnotherMachinesScooter()
        {
            GameObject remote = Scooter(true);
            GameObject local = Scooter(false);

            Assert.IsTrue(RemoteAvatar.IsRemote(remote.GetComponentInChildren<OrderHandler>()), "its order handler");
            Assert.IsTrue(RemoteAvatar.IsRemote(remote.GetComponentInChildren<SphereCollider>()), "its ball");
            Assert.IsFalse(RemoteAvatar.IsRemote(local.GetComponentInChildren<OrderHandler>()), "a scooter driven here");
            Assert.IsFalse(RemoteAvatar.IsRemote(null), "nothing");
        }

        [Test]
        public void OrderBeacons_OnlyServeTheScootersThisMachineDrives()
        {
            Assert.IsTrue(OrderBeacon.IsPlayersBall(Scooter(false).GetComponentInChildren<SphereCollider>()), "a scooter driven here");
            Assert.IsFalse(OrderBeacon.IsPlayersBall(Scooter(true).GetComponentInChildren<SphereCollider>()), "another machine's: orders are shared in roadmap Task 3.5");
            Assert.IsFalse(OrderBeacon.IsPlayersBall(objects.NewGameObject("Wall").AddComponent<BoxCollider>()), "not a scooter");
        }

        [Test]
        public void Water_DoesntRespawnAnotherMachinesScooter()
        {
            Respawn respawn = Scooter(true).GetComponentInChildren<Respawn>();
            GameObject water = objects.NewGameObject("Water");
            water.tag = "Water";
            Collider waterCollider = water.AddComponent<BoxCollider>();

            Reflect.Invoke(respawn, "OnTriggerEnter", waterCollider);

            Assert.IsFalse(respawn.IsRespawning, "its own machine respawns it");
        }

        [Test]
        public void AnotherMachinesScooter_AndALocalOne_DontTouchEachOtherHere()
        {
            GameObject remote = Scooter(true);
            GameObject local = Scooter(false);
            OrderHandler remoteHandler = remote.GetComponentInChildren<OrderHandler>();
            OrderHandler localHandler = local.GetComponentInChildren<OrderHandler>();

            Reflect.Invoke(remoteHandler, "OnTriggerEnter", local.GetComponentInChildren<SphereCollider>());
            Reflect.Invoke(localHandler, "OnTriggerEnter", remote.GetComponentInChildren<SphereCollider>());

            Assert.IsNull(localHandler.PlayerTouching, "another machine's scooter can't steal from or clash with it here");
            Assert.IsNull(remoteHandler.PlayerTouching, "nor be stolen from here");
        }

        [Test]
        public void AnotherMachinesBoost_DoesntStealHere()
        {
            OrderHandler remoteHandler = Scooter(true).GetComponentInChildren<OrderHandler>();
            remoteHandler.PlayerTouching = Scooter(false).GetComponentInChildren<OrderHandler>();

            Assert.DoesNotThrow(remoteHandler.AttemptSteal, "its own machine decides its steals: nothing is tried here");
        }

        [Test]
        public void TheEndOfTheMainGame_DoesntFreezeAnotherMachinesScooterHere()
        {
            OrderManager orders = objects.Add<OrderManager>();
            Reflect.SetSingleton(orders);
            OrderHandler remoteHandler = Scooter(true).GetComponentInChildren<OrderHandler>();

            Reflect.Invoke(remoteHandler, "InitHandler");

            Assert.IsNull(Reflect.GetField(orders, "OnMainGameFinishes"), "its own machine freezes it (here its BallDriving never started)");
        }
    }
}

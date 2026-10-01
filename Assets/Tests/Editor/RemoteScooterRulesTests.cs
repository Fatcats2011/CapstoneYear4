using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.VFX;

namespace DoA.Tests
{
    /// <summary>
    /// Online, another machine's scooter (a RemoteAvatar) doesn't act on this machine: it doesn't fall in water here,
    /// steal, clash, or freeze when the main game ends. Its own machine does all that, and asks the host about its hits.
    /// A scooter driven here that boosts into it asks the host too (StealSync): the host decides every hit. Orders are the
    /// host's: its beacons serve every scooter. While its owner's rider is hidden for a respawn, it's hidden here too and
    /// bumps nobody. EditMode: the scripts' messages are called directly on bare scooters
    /// </summary>
    public class RemoteScooterRulesTests
    {
        readonly TestObjects objects = new TestObjects();
        PlayerInstantiate players;
        List<Vector2Int> asks; // the hits this machine asked the host about (attacker's seat, victim's seat)

        [SetUp]
        public void SetUp()
        {
            players = objects.Add<PlayerInstantiate>();
            Reflect.SetSingleton(players); // seats, for OrderSync.SeatOf
            asks = new List<Vector2Int>();
            StealSync.Asked += (attacker, victim) => asks.Add(new Vector2Int(attacker, victim));
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            Reflect.SetSingleton<OrderManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            GameAuthority.Role = NetworkRole.Offline;
            StealSync.Reset();
        }

        // A bare scooter: the avatar root (with a RemoteAvatar when it's another machine's), its ball and its Control, in
        // a seat as a client sees them: another machine's in seat 0, this machine's in seat 1
        GameObject Scooter(bool remote)
        {
            GameObject root = objects.NewGameObject(remote ? "P1" : "P2");
            if (remote)
                root.AddComponent<RemoteAvatar>();

            GameObject ball = new GameObject("Ball Of Fun");
            ball.transform.SetParent(root.transform);
            ball.AddComponent<SphereCollider>();
            ball.AddComponent<Respawn>();

            GameObject control = new GameObject("Control");
            control.transform.SetParent(root.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            Reflect.SetField(handler, "ball", control.AddComponent<BallDriving>());

            int seat = remote ? 0 : 1;
            players.Roster.JoinRemoteAt(root, (ulong)seat, seat);
            return root;
        }

        // The scooter is boosting (another machine's: its owner is, and ShowRemote shows it)
        static void Boost(GameObject scooter)
        {
            Reflect.SetField(scooter.GetComponentInChildren<BallDriving>(), "boosting", true);
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
        public void OrderBeacons_ServeEveryScooter_OnTheHostThatDecides()
        {
            Assert.IsTrue(OrderBeacon.IsPlayersBall(Scooter(false).GetComponentInChildren<SphereCollider>()), "a scooter driven here");
            Assert.IsTrue(OrderBeacon.IsPlayersBall(Scooter(true).GetComponentInChildren<SphereCollider>()), "another machine's: the host decides its pickups too (beacons act only on the host)");
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
        public void AnotherMachinesScooter_HiddenForARespawn_ShowsNoRiderAndBumpsNobody()
        {
            GameObject remote = Scooter(true);
            Respawn respawn = remote.GetComponentInChildren<Respawn>();
            SphereCollider ball = remote.GetComponentInChildren<SphereCollider>();
            GameObject rider = new GameObject("SubBasket");
            rider.transform.SetParent(remote.transform);
            Reflect.SetField(respawn, "modelParent", rider);
            // Its wisp, off with no trail, as Respawn.Awake leaves it (Awake doesn't run in EditMode)
            GameObject wispObject = new GameObject("DeathWisp");
            wispObject.transform.SetParent(respawn.transform);
            VisualEffect wisp = wispObject.AddComponent<VisualEffect>();
            TrailRenderer trail = new GameObject("Trail").AddComponent<TrailRenderer>();
            trail.transform.SetParent(wispObject.transform);
            wisp.enabled = false;
            trail.time = 0f;
            Reflect.SetField(respawn, "deathWisp", wisp);
            Reflect.SetField(respawn, "wispTrail", trail);

            respawn.ShowRemote(true);
            Assert.IsFalse(rider.activeSelf, "no rider while its owner's is hidden");
            Assert.IsFalse(ball.enabled, "its ball bumps nobody");
            Assert.IsTrue(wisp.enabled, "its wisp shows while its owner's does");
            Assert.Greater(trail.time, 0f, "with its trail");

            respawn.ShowRemote(false);
            Assert.IsTrue(rider.activeSelf, "its rider shows again");
            Assert.IsTrue(ball.enabled, "and its ball is solid again");
            Assert.IsFalse(wisp.enabled, "its wisp is gone when the rider shows");
            Assert.AreEqual(0f, trail.time, "and so is its trail");
        }

        [Test]
        public void AnotherMachinesScooter_BumpingSomethingHere_KeepsItsOwnersDrift()
        {
            GameObject remote = Scooter(true);
            BallDriving driving = remote.GetComponentInChildren<BallDriving>();
            Respawn respawn = remote.GetComponentInChildren<Respawn>();
            Reflect.SetField(driving, "currentVelocity", 20f);
            Reflect.SetField(driving, "drifting", true);
            Reflect.SetField(driving, "respawn", respawn);
            BallCollision bump = respawn.gameObject.AddComponent<BallCollision>();
            Reflect.SetField(bump, "control", driving);
            BoxCollider post = objects.NewGameObject("Lamp post").AddComponent<BoxCollider>();
            post.isTrigger = true;

            Reflect.Invoke(bump, "OnTriggerEnter", post);

            Assert.IsTrue(driving.Drifting, "its own machine drops its drift");
        }

        [Test]
        public void AnotherMachinesScooter_TouchingThisMachinesOne_IsNotedHere_ForThisOnesBoost()
        {
            GameObject remote = Scooter(true), local = Scooter(false);
            OrderHandler remoteHandler = remote.GetComponentInChildren<OrderHandler>();

            Reflect.Invoke(remoteHandler, "OnTriggerEnter", local.GetComponentInChildren<SphereCollider>());

            Assert.AreSame(remoteHandler, local.GetComponentInChildren<OrderHandler>().PlayerTouching,
                "its own machine never tells this one: this machine notes it");
        }

        [TestCase(NetworkRole.Host)]
        [TestCase(NetworkRole.Client)]
        public void ThisMachinesScooter_BoostingIntoAnotherMachines_AsksTheHost_WithBothSeats(NetworkRole role)
        {
            GameAuthority.Role = role;
            GameObject remote = Scooter(true), local = Scooter(false);
            Boost(local);

            Reflect.Invoke(local.GetComponentInChildren<OrderHandler>(), "OnTriggerEnter", remote.GetComponentInChildren<SphereCollider>());

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, asks, "this machine's player hit seat 0's: the host decides");
        }

        [Test]
        public void ThisMachinesScooter_StartingABoostWhileTouched_AsksTheHost()
        {
            GameAuthority.Role = NetworkRole.Client;
            GameObject remote = Scooter(true), local = Scooter(false);
            OrderHandler localHandler = local.GetComponentInChildren<OrderHandler>();
            localHandler.PlayerTouching = remote.GetComponentInChildren<OrderHandler>();

            localHandler.AttemptSteal();

            CollectionAssert.AreEqual(new[] { new Vector2Int(1, 0) }, asks);
        }

        [Test]
        public void AnotherMachinesScooter_BoostingIntoThisMachinesOne_AsksNothingHere()
        {
            GameAuthority.Role = NetworkRole.Client;
            GameObject remote = Scooter(true), local = Scooter(false);
            Boost(remote); // its owner boosts (ShowRemote)

            Reflect.Invoke(remote.GetComponentInChildren<OrderHandler>(), "OnTriggerEnter", local.GetComponentInChildren<SphereCollider>());

            Assert.IsEmpty(asks, "its own machine asks");
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

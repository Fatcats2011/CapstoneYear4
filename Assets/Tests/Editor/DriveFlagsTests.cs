using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What a scooter is doing, in one byte: each flag and the drift tier come back out as they went in, and a scooter
    /// driven here reports its own state, including a rider hidden for a respawn. EditMode
    /// </summary>
    public class DriveFlagsTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void EachFlag_ComesBackOutOnItsOwn()
        {
            Assert.IsTrue(new DriveFlags(true, false, false, 0, false, false).Boosting, "boosting");
            Assert.IsTrue(new DriveFlags(false, true, false, 0, false, false).Drifting, "drifting");
            Assert.IsTrue(new DriveFlags(false, false, true, 0, false, false).DriftRight, "drifting right");
            Assert.IsTrue(new DriveFlags(false, false, false, 0, true, false).Grounded, "on the ground");
            Assert.IsTrue(new DriveFlags(false, false, false, 0, false, true).Phasing, "phasing");
            Assert.IsTrue(new DriveFlags(false, false, false, 0, false, false, true).Hidden, "hidden");
            Assert.AreEqual(0, new DriveFlags(false, false, false, 0, false, false).Value, "nothing set");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void TheDriftTier_ComesBackOut_BesideTheOtherFlags(int tier)
        {
            DriveFlags flags = new DriveFlags(true, true, true, tier, true, true, true);

            Assert.AreEqual(tier, flags.DriftTier);
            Assert.IsTrue(flags.Boosting && flags.Drifting && flags.DriftRight && flags.Grounded && flags.Phasing && flags.Hidden);
        }

        [Test]
        public void TheByte_ComesBackTheSame_OnAnotherMachine()
        {
            DriveFlags sent = new DriveFlags(false, true, true, 2, true, false, true);
            DriveFlags received = new DriveFlags(sent.Value);

            Assert.AreEqual(sent.Value, received.Value);
            Assert.AreEqual(2, received.DriftTier);
            Assert.IsTrue(received.Drifting && received.DriftRight && received.Grounded && received.Hidden);
            Assert.IsFalse(received.Boosting || received.Phasing);
        }

        [Test]
        public void ATierOutsideZeroToThree_IsClamped()
        {
            Assert.AreEqual(3, new DriveFlags(false, false, false, 7, false, false).DriftTier);
            Assert.AreEqual(0, new DriveFlags(false, false, false, -1, false, false).DriftTier);
        }

        [Test]
        public void AScooterDrivenHere_ReportsWhatItsDoing()
        {
            BallDriving driving = objects.Add<BallDriving>();
            driving.drifting = true;
            driving.driftDirection = 1;
            driving.driftTier = 2;
            driving.grounded = true;

            DriveFlags flags = driving.Flags;

            Assert.IsTrue(flags.Drifting && flags.DriftRight && flags.Grounded, "a tier-2 drift to the right, on the ground");
            Assert.AreEqual(2, flags.DriftTier);
            Assert.IsFalse(flags.Boosting || flags.Phasing || flags.Hidden);
        }

        [Test]
        public void AScooterRespawningHere_ReportsItsRiderHidden_UntilItShowsAgain()
        {
            BallDriving driving = objects.Add<BallDriving>();
            Respawn respawn = objects.Add<Respawn>();
            GameObject rider = objects.NewGameObject("SubBasket");
            respawn.modelParent = rider;
            driving.respawn = respawn;

            Assert.IsFalse(driving.Flags.Hidden, "its rider shows");

            rider.SetActive(false); // fell in the water: only its wisp shows while it flies to the casket
            Assert.IsTrue(driving.Flags.Hidden, "hidden");

            rider.SetActive(true); // it rises from its grave
            Assert.IsFalse(driving.Flags.Hidden, "shown again");
        }
    }
}

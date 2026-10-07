using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What the tutorial's cardboard cutout counts as a scooter (CutoutHandler.FindScooter): only a ball with both a
    /// BallDriving and an OrderHandler. Edit mode: no component here starts
    /// </summary>
    public class CutoutHandlerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void ABallWithoutOrders_IsNotAScooter()
        {
            GameObject top = objects.NewGameObject("Ballish");
            GameObject driving = objects.NewGameObject("Driving");
            driving.transform.SetParent(top.transform);
            driving.AddComponent<BallDriving>();
            Collider ball = objects.NewGameObject("Ball").AddComponent<SphereCollider>();
            ball.transform.SetParent(top.transform);

            Assert.IsFalse(CutoutHandler.FindScooter(ball, out OrderHandler player, out BallDriving found),
                "no OrderHandler: the cutout ignores it, every physics step");
        }
    }
}

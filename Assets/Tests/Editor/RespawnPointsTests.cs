using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Where a player who fell in the water rises: the nearest respawn point nobody is rising from. Online the host picks
    /// for everyone, and holds another machine's player's point while they rise, so no two players rise on one point. A
    /// point goes by its place among the points, the same on every machine. EditMode: three bare points 50 m apart
    /// </summary>
    public class RespawnPointsTests
    {
        readonly TestObjects objects = new TestObjects();
        RespawnManager manager;
        RespawnPoint[] points;

        [SetUp]
        public void SetUp()
        {
            manager = objects.Add<RespawnManager>();
            GameObject parent = objects.NewGameObject("Respawn Points");
            float[] along = { 0f, 50f, 100f };
            points = new RespawnPoint[along.Length];
            for (int i = 0; i < along.Length; i++)
            {
                GameObject point = new GameObject("Respawn Point " + i);
                point.transform.SetParent(parent.transform);
                point.transform.position = new Vector3(along[i], 0, 0);
                points[i] = point.AddComponent<RespawnPoint>();
                Reflect.SetField(points[i], "order1Spawn", Spot(point, "Order 1"));
                Reflect.SetField(points[i], "order2Spawn", Spot(point, "Order 2"));
            }
            Reflect.Invoke(manager, "InitRespawnPoints", parent);
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        // Where one of a point's two dropped orders lands
        static Transform Spot(GameObject point, string name)
        {
            GameObject spot = new GameObject(name);
            spot.transform.SetParent(point.transform, false);
            return spot.transform;
        }

        [Test]
        public void TheNearestFreePoint_IsPicked()
        {
            Assert.AreSame(points[1], manager.GetRespawnPoint(new Vector3(48, 0, 0)));
        }

        [Test]
        public void APointInUse_IsSkipped_EvenTheFirst()
        {
            // 2024: the first point was handed out even while someone rose from it
            points[0].InUse = true;
            Assert.AreSame(points[1], manager.GetRespawnPoint(Vector3.zero));
        }

        [Test]
        public void EveryPointInUse_TheNearestAnyway()
        {
            foreach (RespawnPoint point in points)
                point.InUse = true;
            Assert.AreSame(points[2], manager.GetRespawnPoint(new Vector3(90, 0, 0)));
        }

        [Test]
        public void APointHeldForAnotherMachinesPlayer_IsInUse_UntilANewRound()
        {
            points[1].HoldFor(5f);
            Assert.IsTrue(points[1].InUse);
            Assert.AreNotSame(points[1], manager.GetRespawnPoint(new Vector3(50, 0, 0)));

            points[1].InitPoint();
            Assert.IsFalse(points[1].InUse);
        }

        [Test]
        public void AGrave_StandsBehindItsPoint_FacingWhereTheRisenPlayerWillFace()
        {
            // the point at (50, 0, 0) faces the middle of its order spots, 10 m ahead along +z
            points[1].transform.Find("Order 1").localPosition = new Vector3(-1, 0, 10);
            points[1].transform.Find("Order 2").localPosition = new Vector3(1, 0, 10);
            points[1].InitPoint();

            Pose grave = Respawn.GraveAt(points[1], 2f);

            Assert.Less(Vector3.Distance(new Vector3(50, 0, -2), grave.position), 0.001f, "2 m behind the point");
            Assert.Less(Quaternion.Angle(Quaternion.identity, grave.rotation), 0.01f, "facing along +z, as the risen player will");
        }

        [Test]
        public void PointsGoByTheirPlace_TheSameOnEveryMachine()
        {
            Assert.AreEqual(2, manager.IndexOf(points[2]));
            Assert.AreSame(points[2], manager.PointAt(2));
            Assert.IsNull(manager.PointAt(-1));
            Assert.IsNull(manager.PointAt(3));
            Assert.AreEqual(-1, manager.IndexOf(null));
        }
    }
}

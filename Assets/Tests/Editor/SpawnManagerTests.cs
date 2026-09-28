using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class SpawnManagerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        GameObject Point(string name, float x)
        {
            GameObject point = objects.NewGameObject(name);
            point.transform.position = new Vector3(x, 0f, 0f);
            return point;
        }

        /// <summary>
        /// A player on this machine (their PlayerInput needs no controller or camera here): a ball and a scooter under
        /// their object. Returns the ball
        /// </summary>
        Rigidbody AddLocalPlayer(PlayerRoster roster, string name)
        {
            PlayerInput input = objects.NewGameObject(name).AddComponent<PlayerInput>();
            Rigidbody ball = AddScooter(input.gameObject);
            roster.JoinLocal(input);
            return ball;
        }

        /// <summary>
        /// Another machine's player (online): a ball and a scooter under one top object. Returns the ball
        /// </summary>
        Rigidbody AddOnlinePlayer(PlayerRoster roster, string name)
        {
            GameObject player = objects.NewGameObject(name);
            Rigidbody ball = AddScooter(player);
            roster.JoinRemote(player, 1);
            return ball;
        }

        static Rigidbody AddScooter(GameObject player)
        {
            GameObject ball = new GameObject("Ball");
            ball.transform.SetParent(player.transform);
            GameObject scooter = new GameObject("Scooter");
            scooter.transform.SetParent(player.transform);
            scooter.AddComponent<BallDriving>();
            return ball.AddComponent<Rigidbody>();
        }

        [Test]
        public void SpawnPoint_SlotWithoutAGoldenPoint_UsesItsNormalSpawnPoint()
        {
            GameObject[] golden = { Point("Golden 1", 1), Point("Golden 2", 2), Point("Golden 3", 3) };
            GameObject[] normal = { Point("Spawn 1", 11), Point("Spawn 2", 12), Point("Spawn 3", 13), Point("Spawn 4", 14) };

            Assert.AreSame(golden[2], SpawnManager.SpawnPoint(2, golden, normal));
            Assert.AreSame(normal[3], SpawnManager.SpawnPoint(3, golden, normal));
        }

        [Test]
        public void SpawnPoint_NoPointAnywhere_IsNull()
        {
            Assert.IsNull(SpawnManager.SpawnPoint(3, new GameObject[3], null));
        }

        [Test]
        public void SpawnPlayersFinalPackage_FourPlayersThreeGoldenPoints_EveryoneStartsOnASpawnPoint()
        {
            SpawnManager spawns = objects.Add<SpawnManager>();
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] golden = { Point("Golden 1", 1), Point("Golden 2", 2), Point("Golden 3", 3) };
            GameObject[] normal = { Point("Spawn 1", 11), Point("Spawn 2", 12), Point("Spawn 3", 13), Point("Spawn 4", 14) };
            Reflect.SetField(spawns, "goldenPackageSpawnPositions", golden);
            Reflect.SetField(spawns, "gameSpawnPositions", normal);
            Reflect.SetField(spawns, "playerInstantiate", instantiate);
            Rigidbody[] balls = new Rigidbody[Constants.MAX_PLAYERS];
            for (int i = 0; i < balls.Length; i++)
                balls[i] = AddLocalPlayer(instantiate.Roster, "P" + (i + 1));

            spawns.SpawnPlayersFinalPackage();

            Assert.AreEqual(golden[0].transform.position, balls[0].transform.position);
            Assert.AreEqual(golden[2].transform.position, balls[2].transform.position);
            Assert.AreEqual(normal[3].transform.position, balls[3].transform.position);
        }

        [Test]
        public void SpawnPlayersFinalPackage_AnotherMachinesPlayer_IsLeftWhereItsOwnerPutsIt()
        {
            SpawnManager spawns = objects.Add<SpawnManager>();
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] golden = { Point("Golden 1", 1), Point("Golden 2", 2), Point("Golden 3", 3) };
            Reflect.SetField(spawns, "goldenPackageSpawnPositions", golden);
            Reflect.SetField(spawns, "gameSpawnPositions", new GameObject[0]);
            Reflect.SetField(spawns, "playerInstantiate", instantiate);
            Rigidbody mine = AddLocalPlayer(instantiate.Roster, "P1");
            Rigidbody theirs = AddOnlinePlayer(instantiate.Roster, "P2");
            theirs.transform.position = new Vector3(50f, 5f, 50f); // where its own machine has it

            spawns.SpawnPlayersFinalPackage();

            Assert.AreEqual(golden[0].transform.position, mine.transform.position, "this machine's player");
            Assert.AreEqual(new Vector3(50f, 5f, 50f), theirs.transform.position, "its own machine places it");
        }
    }
}

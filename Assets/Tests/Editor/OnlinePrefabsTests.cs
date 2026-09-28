using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What online play spawns (Resources/Online/OnlinePrefabs): three network objects that carry data, the scooter shown
    /// for another machine's player, and the local player's prefab, whose player select lists the colours and hats
    /// </summary>
    public class OnlinePrefabsTests
    {
        [Test]
        public void Load_FindsTheOnlinePrefabs()
        {
            Assert.IsNotNull(OnlinePrefabs.Load(), "Resources/" + OnlinePrefabs.RESOURCE);
        }

        [Test]
        public void NetworkPrefabs_AreNetworkObjectsThatCarryData()
        {
            OnlinePrefabs prefabs = OnlinePrefabs.Load();

            AssertNetworkPrefab(prefabs.PlayerPrefab, typeof(OnlinePlayer));
            AssertNetworkPrefab(prefabs.MatchPrefab, typeof(OnlineMatch));
            AssertNetworkPrefab(prefabs.ScooterPrefab, typeof(OnlineScooter));
        }

        [Test]
        public void TheScooterPrefab_CarriesItsPoseInTwoOwnerMovedProxies_ParkedUntilItsFirstPose()
        {
            GameObject scooter = OnlinePrefabs.Load().ScooterPrefab;
            OwnerNetworkTransform ball = scooter.transform.Find("Ball").GetComponent<OwnerNetworkTransform>();
            OwnerNetworkTransform model = scooter.transform.Find("Model").GetComponent<OwnerNetworkTransform>();

            Assert.IsTrue(ball.SyncPositionX && ball.SyncPositionY && ball.SyncPositionZ, "the ball's position");
            Assert.IsTrue(ball.SyncRotAngleY && !ball.SyncRotAngleX && !ball.SyncRotAngleZ, "the scooter's heading only");
            Assert.IsTrue(model.SyncPositionX && model.SyncPositionY && model.SyncPositionZ
                && model.SyncRotAngleX && model.SyncRotAngleY && model.SyncRotAngleZ, "the model's whole pose");
            Assert.IsTrue(model.UseQuaternionSynchronization, "lean, drift, slope and wheelie together");
            foreach (OwnerNetworkTransform proxy in new[] { ball, model })
            {
                Assert.IsFalse(proxy.SyncScaleX || proxy.SyncScaleY || proxy.SyncScaleZ, proxy.name + ": no scale");
                Assert.IsTrue(proxy.Interpolate, proxy.name + ": smoothed");
                Assert.AreEqual(OnlineScooter.PARKED_Y, proxy.transform.localPosition.y, proxy.name + " is parked");
            }
        }

        static void AssertNetworkPrefab(GameObject prefab, System.Type script)
        {
            Assert.IsNotNull(prefab, script.Name);
            NetworkObject networkObject = prefab.GetComponent<NetworkObject>();
            Assert.IsNotNull(networkObject, script.Name + " has a NetworkObject");
            Assert.IsNotNull(prefab.GetComponent(script), script.Name + " script");
            Assert.AreNotEqual(0u, networkObject.PrefabIdHash, script.Name + " has Netcode's prefab id");
            Assert.IsFalse(networkObject.SynchronizeTransform, script.Name + " doesn't move, so its transform isn't sent");
        }

        [Test]
        public void Scooters_AreThePlayerPrefabs()
        {
            OnlinePrefabs prefabs = OnlinePrefabs.Load();

            Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab"), prefabs.RemoteAvatarPrefab);
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab"), prefabs.LocalPlayerPrefab);
        }
    }
}

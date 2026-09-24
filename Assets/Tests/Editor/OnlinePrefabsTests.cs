using NUnit.Framework;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What online play spawns (Resources/Online/OnlinePrefabs): two network objects that carry data, the scooter shown
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

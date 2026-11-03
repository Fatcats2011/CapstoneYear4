using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Another machine's player's Steam picture over their scooter (SteamIcon): where it floats, and which cameras see it
    /// </summary>
    public class SteamIconTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        [Test]
        public void ShowsFor_OnlyAPicture_OfAShownScooter_OffTheMinimap()
        {
            Assert.IsTrue(SteamIcon.ShowsFor(true, true, false), "a player's camera");
            Assert.IsFalse(SteamIcon.ShowsFor(false, true, false), "no picture: no Steam, a CPU driver, still loading");
            Assert.IsFalse(SteamIcon.ShowsFor(true, false, false), "the rider's hidden, respawning");
            Assert.IsFalse(SteamIcon.ShowsFor(true, true, true), "the minimap draws to a texture");
        }

        [Test]
        public void Attach_FloatsOverTheRemoteScooter()
        {
            GameObject scooter = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR));
            try
            {
                RemoteAvatar avatar = scooter.AddComponent<RemoteAvatar>();
                Reflect.Invoke(avatar, "TurnOffOwnerParts");

                SteamIcon icon = SteamIcon.Attach(avatar, () => 0);

                Assert.IsNotNull(icon);
                Assert.AreSame(avatar.Driving.transform, icon.transform.parent, "moves with the scooter");
                Assert.AreEqual(new Vector3(0, SteamIcon.HEIGHT, 0), icon.transform.localPosition, "over it");
            }
            finally
            {
                Object.DestroyImmediate(scooter);
            }
        }

        [Test]
        public void Attach_WithoutAScooter_IsNothing()
        {
            Assert.IsNull(SteamIcon.Attach(null, () => 0));
        }
    }
}

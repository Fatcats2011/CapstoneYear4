using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DoA.Tests
{
    /// <summary>
    /// ScooterLook dresses another machine's scooter by path. The paths must be the very parts the local player's view
    /// dresses (PlayerCameraResizer, CustomizationSelector), so renaming or moving one fails here until ScooterLook follows
    /// </summary>
    public class ScooterLookTests
    {
        const string VIEW = "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab";
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        static Object Link(Component owner, string field, int element = -1)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, owner.GetType().Name + "." + field);
            return element < 0 ? property.objectReferenceValue : property.GetArrayElementAtIndex(element).objectReferenceValue;
        }

        static Transform Part(Transform avatar, string path)
        {
            Transform part = avatar.Find(path);
            Assert.IsNotNull(part, path + " is missing from the scooter");
            return part;
        }

        [Test]
        public void Paths_AreThePartsTheViewDresses()
        {
            GameObject view = AssetDatabase.LoadAssetAtPath<GameObject>(VIEW);
            Transform avatar = view.transform.GetChild(0);
            PlayerCameraResizer resizer = view.GetComponent<PlayerCameraResizer>();
            CustomizationSelector customization = view.GetComponentInChildren<CustomizationSelector>(true);

            Assert.AreSame(Part(avatar, ScooterLook.SCOOTER_BODY).GetComponent<MeshRenderer>(), Link(resizer, "scooterModel"), "company colours");
            Assert.AreSame(Part(avatar, ScooterLook.LOGO).GetComponent<DecalProjector>(), Link(resizer, "logoDecal", 0), "logo");
            Assert.AreSame(Part(avatar, ScooterLook.LOGO_2).GetComponent<DecalProjector>(), Link(resizer, "logoDecal", 1), "second logo");
            Assert.AreSame(Part(avatar, ScooterLook.GHOST).GetComponent<SkinnedMeshRenderer>(), Link(customization, "ghostModel"), "ghost");
            Assert.AreSame(Part(avatar, ScooterLook.EYELID_LEFT).GetComponent<MeshRenderer>(), Link(customization, "ghostEyelid1"), "left eyelid");
            Assert.AreSame(Part(avatar, ScooterLook.EYELID_RIGHT).GetComponent<MeshRenderer>(), Link(customization, "ghostEyelid2"), "right eyelid");
            Assert.AreSame(Part(avatar, ScooterLook.HAT).GetComponent<MeshRenderer>(), Link(customization, "hatModel"), "hat");
        }

        [Test]
        public void Paths_AreOnTheBareScooterToo()
        {
            Transform avatar = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR).transform;

            foreach (string path in new[] { ScooterLook.SCOOTER_BODY, ScooterLook.LOGO, ScooterLook.LOGO_2, ScooterLook.GHOST, ScooterLook.EYELID_LEFT, ScooterLook.EYELID_RIGHT, ScooterLook.HAT })
                Part(avatar, path);
        }

        [Test]
        public void PlayerSelect_ListsColoursAndHats()
        {
            CustomizationSelector customization = AssetDatabase.LoadAssetAtPath<GameObject>(VIEW).GetComponentInChildren<CustomizationSelector>(true);

            Assert.AreEqual(6, customization.Colours.Count, "colours");
            Assert.AreEqual(8, customization.Hats.Count, "hats");
            Assert.IsTrue(customization.Hats.Any(h => h.displayHat), "at least one hat shows");
        }
    }
}

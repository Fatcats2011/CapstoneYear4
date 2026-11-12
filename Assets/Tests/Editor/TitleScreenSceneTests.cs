using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The title screen as the builder (TitleScreenBuilder) leaves the menu scene: everything the old screen had wired
    /// is still wired, the new pictures sit behind the logo and let the mouse through, the old ghost rig is gone, and
    /// the stage has the game's wardrobe. Opens the menu scene in the editor, without Play Mode
    /// </summary>
    public class TitleScreenSceneTests
    {
        Scene scene;
        Transform canvas;

        [OneTimeSetUp]
        public void Open()
        {
            scene = EditorSceneManager.OpenScene(TitleScreenBuilder.MENU_SCENE, OpenSceneMode.Additive);
            canvas = Root(TitleScreenBuilder.CANVAS).transform;
        }

        [OneTimeTearDown]
        public void Close()
        {
            if (scene.IsValid())
                EditorSceneManager.CloseScene(scene, true);
        }

        GameObject Root(string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(r => r.name == name);
        }

        [Test]
        public void MainMenu_StillHasItsFiveEntriesCanvasesAndSelector()
        {
            MainMenu menu = canvas.GetComponent<MainMenu>();
            Assert.IsNotNull(menu, "MainMenu on the title canvas");
            Assert.AreEqual(5, menu.selectorObjects.Length, "entries");
            string[] names = menu.selectorObjects.Select(e => e != null ? e.name : null).ToArray();
            CollectionAssert.AreEqual(new[] { "Multiplayer", "Local", "Options", "Credits", "Quit" }, names);
            foreach (GameObject entry in menu.selectorObjects)
            {
                Assert.IsTrue(entry.activeInHierarchy, entry.name + " shows");
                Assert.IsNotNull(entry.GetComponent<Image>(), entry.name + " has its picture");
            }
            Assert.IsNotNull(menu.selector, "selector");
            Assert.IsTrue(menu.selector.activeInHierarchy, "the selector shows");
            Assert.IsNotNull(menu.menuGhostImage, "the selector's ghost picture");
            Assert.IsNotNull(menu.PlayerSelectCanvas, "player select");
            Assert.IsNotNull(menu.OptionsCanvas, "options");
            Assert.IsNotNull(menu.CreditsCanvas, "credits");
            Assert.IsTrue(menu.HasOnlineEntry, "Online Play is on the title screen");
            Assert.IsNotNull(canvas.Find("Button UI"), "the button prompt");
            Assert.IsNotNull(canvas.Find("P1 Connected Controller"), "the controller's name");
        }

        [Test]
        public void TitleCanvas_DrawOrder_BackdropFogFloorGhostsThenLogoThenEntries()
        {
            for (int i = 0; i < TitleScreenBuilder.Behind.Length; i++)
                Assert.AreEqual(TitleScreenBuilder.Behind[i], canvas.GetChild(i).name, "child " + i);

            int ghosts = canvas.Find(TitleScreenBuilder.GHOSTS).GetSiblingIndex();
            int logo = canvas.Find("Logo").GetSiblingIndex();
            int entries = canvas.Find("Positions").GetSiblingIndex();
            int selector = canvas.Find("Selector").GetSiblingIndex();
            int intro = canvas.Find(TitleScreenBuilder.INTRO_PICTURE).GetSiblingIndex();
            Assert.Less(ghosts, logo, "the ghosts ride behind the logo");
            Assert.Less(logo, entries, "the entries are over the logo");
            Assert.Less(ghosts, selector, "the ghosts ride behind the selector");
            Assert.AreEqual(canvas.childCount - 1, intro, "the phase into the menu is over everything");
        }

        [Test]
        public void NewTitleImages_AreNotRaycastTargets()
        {
            foreach (string name in TitleScreenBuilder.Behind.Append(TitleScreenBuilder.INTRO_PICTURE))
            {
                Graphic picture = canvas.Find(name).GetComponent<Graphic>();
                Assert.IsNotNull(picture, name);
                Assert.IsFalse(picture.raycastTarget, name + " lets the mouse through to the entries");
            }
        }

        [Test]
        public void OldLook_IsGone()
        {
            foreach (string name in TitleScreenBuilder.Old)
                Assert.IsNull(canvas.Find(name), name);
        }

        [Test]
        public void Stage_HasTheGamesWardrobe_AndItsPrefabs()
        {
            GameObject stageObject = Root(TitleScreenBuilder.STAGE);
            Assert.IsNotNull(stageObject, "the stage");
            TitleStage stage = stageObject.GetComponent<TitleStage>();

            Assert.AreEqual(8, stage.hats.Length, "hats (one is no hat)");
            Assert.AreEqual(1, stage.hats.Count(h => !h.displayHat), "a bare head");
            Assert.AreEqual(6, stage.colours.Length, "colours");
            Assert.AreEqual(4, stage.companies.Length, "companies");
            CollectionAssert.AllItemsAreNotNull(stage.hats);
            CollectionAssert.AllItemsAreNotNull(stage.colours);
            CollectionAssert.AllItemsAreNotNull(stage.companies);
            CollectionAssert.AllItemsAreUnique(stage.hats);
            CollectionAssert.AllItemsAreUnique(stage.colours);

            Assert.IsNotNull(stage.avatarPrefab, "the scooter");
            Assert.AreEqual(3, stage.packagePrefabs.Length, "small, medium, large");
            CollectionAssert.AllItemsAreNotNull(stage.packagePrefabs);
            Assert.IsNotNull(stage.goldenMaterial, "gold");
            Assert.IsNotNull(stage.stageCamera, "its camera");
            Assert.AreEqual(0f, stage.stageCamera.backgroundColor.a, "films onto nothing");
            Assert.AreSame(canvas.Find(TitleScreenBuilder.GHOSTS).GetComponent<RawImage>(), stage.ghostImage);
            GameObject rig = Root(TitleScreenBuilder.INTRO_RIG);
            Assert.IsNotNull(rig, "the intro's ghost crew");
            Assert.AreSame(rig.GetComponent<MenuGhostReveal>(), stage.intro, "the show waits for the intro");
            Assert.AreEqual(2, rig.GetComponentsInChildren<Camera>(true).Length, "the intro's two cameras");
            Assert.Greater(Vector3.Distance(rig.transform.position, stageObject.transform.position), 200f, "the stage is out of the intro's sight");
            Assert.AreEqual(0, stageObject.GetComponentsInChildren<AudioListener>(true).Length, "no ears on the stage");
        }

        [Test]
        public void Logo_IsWhereItWas()
        {
            RectTransform logo = (RectTransform)canvas.Find("Logo");
            Assert.AreEqual(-623.1f, logo.anchoredPosition.x, 0.1f);
            Assert.AreEqual(334.53f, logo.anchoredPosition.y, 0.1f);
            Assert.AreEqual(1057.2561f, logo.sizeDelta.x, 0.1f);
            Assert.AreEqual(792.9417f, logo.sizeDelta.y, 0.1f);
            Assert.IsTrue(logo.gameObject.activeInHierarchy);
        }

        [Test]
        public void OptionsAndCredits_KeepTheirBackgrounds()
        {
            foreach (string name in new[] { "Options Canvas", "Credits Canvas" })
            {
                Transform other = Root(name).transform;
                Assert.IsNotNull(other.Find("Background Purple"), name + " purple");
                Assert.IsNotNull(other.Find("Background Pink"), name + " pink");
            }
        }
    }
}

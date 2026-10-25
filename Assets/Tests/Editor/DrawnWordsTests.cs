using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The hand-lettered words (DrawnWords): in English the drawn art shows as it always has; in another language the
    /// art hides and a text in Jack's Candlestick spells the word in its place, shrinking (to 70% at most) to fit a
    /// longer word. Every image in the menus and prefabs that shows a word is one it knows. The same for the plain texts
    /// placed in scenes (SceneTexts)
    /// </summary>
    public class DrawnWordsTests
    {
        readonly TestObjects objects = new TestObjects();
        StringsCsv saved;

        [SetUp]
        public void SetUp()
        {
            saved = Loc.Csv;
            Loc.Csv = StringsCsv.Parse(
                "key,note,en,fr,de,reviewed\n" +
                "word.fullscreen,,FULLSCREEN,PLEIN ÉCRAN,VOLLBILDMODUS AUF DEM GANZEN BILDSCHIRM,\n" +
                "word.resume,,RESUME,REPRENDRE,WEITER,\n" +
                "dispatch.leave,,LEAVE,PARTIR,VERLASSEN,\n");
            Loc.SetLanguage("en");
            SceneTexts.Forget();
        }

        [TearDown]
        public void TearDown()
        {
            Loc.SetLanguage("en");
            Loc.Csv = saved;
            SceneTexts.Forget();
            objects.DestroyAll();
        }

        static Sprite WordSprite(string name)
        {
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(name.StartsWith("MenuNumber") ? "Assets/Sprites/UI/MenuNumber_UI-spritesUPDATED.png" : "Assets/Sprites/UI/MenuText_UI-sprites2.png"))
                if (asset is Sprite sprite && sprite.name == name)
                    return sprite;
            Assert.Fail("no sprite " + name);
            return null;
        }

        Image WordImage(string spriteName, Vector2 size)
        {
            RectTransform canvas = objects.NewGameObject("Canvas").AddComponent<RectTransform>();
            canvas.gameObject.AddComponent<Canvas>();
            Image image = objects.NewGameObject("Word").AddComponent<Image>();
            image.rectTransform.SetParent(canvas, false);
            image.rectTransform.sizeDelta = size;
            image.sprite = WordSprite(spriteName);
            return image;
        }

        [Test]
        public void Swap_English_KeepsTheDrawnArt()
        {
            Image image = WordImage("MenuText_UI-sprites2_7", new Vector2(400, 110)); // RESUME

            TMP_Text text = DrawnWords.Swap(image);

            Assert.IsNotNull(text);
            Assert.IsTrue(image.enabled, "the art shows");
            Assert.IsFalse(text.gameObject.activeSelf);
        }

        [Test]
        public void Swap_AnotherLanguage_TheWordAsText_InTheSameRect()
        {
            Image image = WordImage("MenuText_UI-sprites2_7", new Vector2(400, 110));
            TMP_Text text = DrawnWords.Swap(image);

            Loc.SetLanguage("fr");

            Assert.IsFalse(image.enabled, "the art hides");
            Assert.IsTrue(text.gameObject.activeSelf);
            Assert.AreEqual("REPRENDRE", text.text);
            Assert.AreSame(image.transform, text.transform.parent, "it moves with the art (a selector entry)");
            Assert.AreEqual(Vector2.zero, text.rectTransform.anchorMin);
            Assert.AreEqual(Vector2.one, text.rectTransform.anchorMax);
            Assert.AreEqual("jcandlestick norm", text.font.name);
            Assert.AreEqual(new Color32(0xF2, 0xF2, 0xF2, 0xFF), (Color32)text.color);

            Loc.SetLanguage("en");
            Assert.IsTrue(image.enabled, "back to the art");
            Assert.IsFalse(text.gameObject.activeSelf);
        }

        [Test]
        public void Swap_UnknownSprite_DoesNothing()
        {
            Image image = WordImage("MenuNumber_UI-spritesUPDATED_12", new Vector2(100, 100)); // a number

            Assert.IsNull(DrawnWords.Swap(image));
            Assert.AreEqual(0, image.transform.childCount);
        }

        [Test]
        public void Swap_Twice_MakesOneText()
        {
            Image image = WordImage("MenuText_UI-sprites2_7", new Vector2(400, 110));

            DrawnWords.Swap(image);
            DrawnWords.Swap(image);

            Assert.AreEqual(1, image.transform.childCount);
        }

        [Test]
        public void ALongWord_ShrinksToFitItsRect()
        {
            Image image = WordImage("MenuNumber_UI-spritesUPDATED_1", new Vector2(600, 120)); // FULLSCREEN
            TMP_Text text = DrawnWords.Swap(image);

            Loc.SetLanguage("fr");
            text.ForceMeshUpdate();
            float english = text.fontSizeMax;
            Loc.SetLanguage("de");
            text.ForceMeshUpdate();

            Assert.GreaterOrEqual(text.fontSize, english * DrawnWords.SMALLEST - 0.01f, "never under the smallest");
            Assert.LessOrEqual(text.fontSize, english);
            Assert.IsTrue(text.enableAutoSizing);
        }

        [Test]
        public void SceneText_AnotherLanguage_ShowsItsTranslation_EnglishKeepsTheOriginal()
        {
            TMP_Text text = objects.Add<TextMeshProUGUI>();
            text.text = "Leave";

            SceneTexts.Localize(text);
            Assert.AreEqual("Leave", text.text, "English: as it was placed");
            Loc.SetLanguage("fr");
            Assert.AreEqual("PARTIR", text.text);
            Loc.SetLanguage("en");
            Assert.AreEqual("Leave", text.text);
        }

        [Test]
        public void SceneText_CodeWroteItsOwnWords_TheyStay()
        {
            TMP_Text placed = objects.NewGameObject("Label").AddComponent<TextMeshProUGUI>();
            placed.text = "LEAVE";
            SceneTexts.Localize(placed);

            placed.text = "RED"; // a ticket's colour, written by its view
            Loc.SetLanguage("fr");

            Assert.AreEqual("RED", placed.text);
        }

        [Test]
        public void EveryWordImage_IsKnown()
        {
            List<string> unknown = new List<string>();
            List<GameObject> roots = new List<GameObject>();
            Scene menu = EditorSceneManager.OpenScene("Assets/Scenes/Alex Player Testing.unity", OpenSceneMode.Additive);
            try
            {
                roots.AddRange(menu.GetRootGameObjects());
                foreach (string prefab in new[] { "Assets/Prefabs/Player Prefabs/Menu Canvas.prefab", "Assets/Prefabs/Player Prefabs/Loading Screen World Canvas.prefab", "Assets/Prefabs/Cutscene Manager.prefab" })
                    roots.Add(AssetDatabase.LoadAssetAtPath<GameObject>(prefab));
                foreach (GameObject root in roots)
                    foreach (Image image in root.GetComponentsInChildren<Image>(true))
                        if (image.sprite != null && DrawnWords.LooksLikeAWord(image.sprite) && !DrawnWords.KeyOfSprite.ContainsKey(image.sprite.name))
                            unknown.Add(root.name + "/" + image.name + ": " + image.sprite.name);
            }
            finally
            {
                EditorSceneManager.CloseScene(menu, true);
            }

            Assert.IsEmpty(unknown);
        }
    }
}

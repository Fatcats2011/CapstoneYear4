using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// Localization as the game runs it, frame by frame (each test enters Play Mode, a few seconds): a drawn accent set by
    /// a plain text change (laid out inside the canvas's own rebuild) still gets drawn, and a drawn word swapped while its
    /// menu is hidden is sized and laid out right once the menu shows. No lambda here captures a local
    /// </summary>
    public class LocalizationPlayTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
            {
                Loc.SetLanguage(LocaleCodes.ENGLISH);
                yield return new ExitPlayMode();
            }
            AccentOverlay.StopListening();
        }

        static void NewEmptyScene()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static RectTransform NewCanvas()
        {
            GameObject canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            return (RectTransform)canvas.transform;
        }

        [UnityTest]
        public IEnumerator AnAccent_SetByAPlainTextChange_IsDrawn()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            AccentOverlay.Listen();
            TextMeshProUGUI text = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.rectTransform.SetParent(NewCanvas(), false);
            text.rectTransform.sizeDelta = new Vector2(800, 200);
            text.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
            text.fontSize = 60;
            yield return null;

            text.text = "ČECH Č"; // laid out in the canvas's rebuild, not here
            yield return null;
            yield return null;
            yield return null;

            List<Image> marks = new List<Image>(text.GetComponentsInChildren<Image>(false));
            Assert.AreEqual(2, marks.Count, "both Č");
            foreach (Image mark in marks)
            {
                Assert.IsNotNull(mark.sprite);
                Assert.Greater(mark.canvasRenderer.materialCount, 0, "the mark was rebuilt, so it draws");
            }
        }

        [UnityTest]
        public IEnumerator AWord_SwappedWhileHidden_FitsOnceShown()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            Loc.SetLanguage("fr");
            GameObject menu = new GameObject("Menu", typeof(RectTransform));
            menu.transform.SetParent(NewCanvas(), false);
            menu.SetActive(false);
            Image image = new GameObject("Resume", typeof(RectTransform)).AddComponent<Image>();
            image.rectTransform.SetParent(menu.transform, false);
            image.rectTransform.sizeDelta = new Vector2(400, 110);
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/UI/MenuText_UI-sprites2.png"))
                if (asset is Sprite sprite && sprite.name == "MenuText_UI-sprites2_7") // RESUME
                    image.sprite = sprite;
            TMP_Text text = DrawnWords.Swap(image);

            menu.SetActive(true);
            yield return null;
            yield return null;

            Assert.AreEqual("REPRENDRE", text.text);
            Assert.IsFalse(text.enableWordWrapping, "one line, like the art");
            Assert.IsFalse(text.raycastTarget);
            Assert.Greater(text.fontSizeMax, 10f, "sized for the English word in its rect");
            Assert.AreEqual(text.fontSizeMax * DrawnWords.SMALLEST, text.fontSizeMin, 0.01f);
        }
    }
}

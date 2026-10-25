using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The Options pages in the real menu scene: the shoulders turn from the drawn GENERAL page to DISPLAY, AUDIO and
    /// ACCESSIBILITY and round again, each page shows only its own rows, a row's change applies at once (VSync off
    /// un-dims Frame cap), and leaving goes back to GENERAL. Enters Play Mode (about 10 s)
    /// </summary>
    public class OptionsPagesMenuTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        // Leaving saves the settings file, which the editor shares with the game on this PC: it's put back afterwards
        static string SettingsFile => Path.Combine(Application.persistentDataPath, "settings.cfg");
        static string Backup => Path.GetFullPath("Temp/settings.cfg.pages-test-backup");
        const string NONE = "<none>";

        [SetUp]
        public void SetUp()
        {
            File.WriteAllText(Backup, File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile) : NONE);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = null;
            Comfort.ReduceMotion = false;
            SplitScreenLayout.TwoPlayers = SplitLayout.Boxed;

            if (!File.Exists(Backup))
                yield break;
            string saved = File.ReadAllText(Backup);
            if (saved != NONE)
                File.WriteAllText(SettingsFile, saved);
            else if (File.Exists(SettingsFile))
                File.Delete(SettingsFile);
            File.Delete(Backup);
        }

        [UnityTest]
        public IEnumerator Language_RightOnTheRow_SwitchesTheGame()
        {
            File.WriteAllText(SettingsFile, "language=en\n");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance == null || GameManager.Instance.MainState != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;

            OptionsMenu options = OptionsMenu.Instance;
            Assert.AreEqual("en", Loc.Language);
            Assert.IsNotNull(options.LanguageRow, "the row was built");
            options.ScrollMenuUpDown(false); // up from the top: LANGUAGE, the last row
            Assert.AreEqual(OptionsMenu.OptionSelected.LANGUAGE, options.optionSelected);

            options.ScrollMenuLeftRight(true);

            Assert.AreEqual("fr", Loc.Language);
            Assert.AreEqual("FRANÇAIS", options.LanguageRow.Value.text);
            StringAssert.Contains("language=fr", File.ReadAllText(SettingsFile), "saved at once");
            bool frenchWord = false;
            foreach (DrawnWord word in Object.FindObjectsOfType<DrawnWord>(true))
                frenchWord |= word.Text != null && word.Text.gameObject.activeInHierarchy && word.Text.text == Loc.Get("word.options");
            Assert.IsTrue(frenchWord, "the title screen's drawn OPTIONS reads in French");
            LanguageFlag flag = Object.FindObjectOfType<LanguageFlag>();
            Assert.AreSame(FlagSet.Game.Of("fr"), flag.GetComponent<UnityEngine.UI.Image>().sprite);

            options.ScrollMenuLeftRight(false);
            Assert.AreEqual("en", Loc.Language);
        }

        [UnityTest]
        public IEnumerator Options_ThePagesTurn_AndTheirRowsWork()
        {
            File.WriteAllText(SettingsFile, "vsync=1\nframecap=0\nreducemotion=0\n");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance == null || GameManager.Instance.MainState != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;

            OptionsMenu options = OptionsMenu.Instance;
            Assert.IsNotNull(options.Book, "the pages were built");
            Assert.AreEqual(4, options.Book.Pages.Count);
            GameObject drawnRow = options.ResolutionRow.gameObject;

            // Music and Effects are on AUDIO, not GENERAL (playtest, 2026-10-24): GENERAL starts at Fullscreen
            Assert.AreEqual(OptionsMenu.OptionSelected.FULLSCREEN, options.optionSelected);
            Assert.AreEqual("AUDIO", options.Book.Pages[2].Name);
            CollectionAssert.AreEqual(new[] { "MASTER", "MUSIC", "EFFECTS", "IN THE BACKGROUND" },
                System.Linq.Enumerable.Select(options.RowsOn(2), row => row.LabelText));

            options.TurnPage(true);
            Assert.AreEqual("DISPLAY", options.Book.Current.Name);
            Assert.AreEqual(6, options.RowsOn(1).Count); // ... Split screen, Units
            Assert.IsTrue(options.RowsOn(1)[0].gameObject.activeInHierarchy, "DISPLAY's rows show");
            Assert.IsFalse(drawnRow.activeInHierarchy, "GENERAL's rows hide");

            // VSync is on, so Frame cap is dimmed and doesn't change
            OptionRow frameCap = options.RowsOn(1)[2];
            Assert.AreEqual("FRAME CAP", frameCap.LabelText);
            options.ScrollMenuUpDown(true);
            options.ScrollMenuUpDown(true);
            options.ScrollMenuLeftRight(true);
            Assert.AreEqual("OFF", frameCap.Value.text, "dimmed while VSync is on");

            // VSync off: Frame cap works
            options.ScrollMenuUpDown(false);
            options.ScrollMenuLeftRight(false);
            Assert.AreEqual("OFF", options.RowsOn(1)[1].Value.text, "VSync off");
            options.ScrollMenuUpDown(true);
            options.ScrollMenuLeftRight(true);
            Assert.AreEqual("30", frameCap.Value.text);

            // ACCESSIBILITY, the other way round: Reduce motion applies at once
            options.TurnPage(false);
            options.TurnPage(false);
            Assert.AreEqual("ACCESSIBILITY", options.Book.Current.Name);
            options.ScrollMenuLeftRight(true);
            Assert.IsTrue(Comfort.ReduceMotion);
            StringAssert.Contains("reducemotion=1", File.ReadAllText(SettingsFile), "saved at once: leaving any other way, or quitting, keeps it");

            options.ExitMenu();
            Assert.AreEqual(0, options.Book.Page, "back to GENERAL");
            Assert.IsTrue(drawnRow.activeInHierarchy);
            StringAssert.Contains("framecap=30", File.ReadAllText(SettingsFile));
            StringAssert.Contains("reducemotion=1", File.ReadAllText(SettingsFile));

            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

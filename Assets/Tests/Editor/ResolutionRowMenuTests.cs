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
    /// The Resolution row in the real menu scene: Options builds it under its last row, the selector reaches it, and
    /// left, right and A work there without errors. Enters Play Mode (about 10 s)
    /// </summary>
    public class ResolutionRowMenuTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        // A saves the settings file, which the editor shares with the game on this PC: it's put back afterwards
        // (the backup is a file: entering Play Mode reloads the scripts, which clears fields)
        static string SettingsFile => Path.Combine(Application.persistentDataPath, "settings.cfg");
        static string Backup => Path.GetFullPath("Temp/settings.cfg.test-backup");
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
        public IEnumerator Options_HasAResolutionRow_ThatTheSelectorReaches()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance == null || GameManager.Instance.MainState != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;

            OptionsMenu options = OptionsMenu.Instance;
            Assert.IsNotNull(options.ResolutionRow, "the row was built");
            Assert.AreEqual("RESOLUTION", options.ResolutionRow.LabelText);

            // Up from the first row wraps round to the last two: LANGUAGE, then the Resolution row
            options.ScrollMenuUpDown(false);
            Assert.AreEqual(OptionsMenu.OptionSelected.LANGUAGE, options.optionSelected);
            options.ScrollMenuUpDown(false);
            Assert.AreEqual(OptionsMenu.OptionSelected.RESOLUTION, options.optionSelected);
            options.ScrollMenuLeftRight(false);
            options.ScrollMenuLeftRight(true);
            options.Confirm();
            Assert.AreEqual(options.ResolutionRow.Shown, options.ResolutionRow.Applied);
            options.ScrollMenuUpDown(true);
            options.ScrollMenuUpDown(true);
            Assert.AreEqual(OptionsMenu.OptionSelected.BGM, options.optionSelected, "down past LANGUAGE: back to the first row");

            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

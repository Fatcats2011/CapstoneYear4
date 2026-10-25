using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace DoA.Tests
{
    /// <summary>
    /// The editor's Set Up (LocalizationSetup): Unity Localization's string tables filled from the strings CSV, every key
    /// in every locale, and keys the CSV no longer has removed. In a scratch folder, deleted after
    /// </summary>
    public class LocalizationSetupTests
    {
        const string FOLDER = "Assets/Tests/LocalizationScratch";

        StringTableCollection collection;

        [SetUp]
        public void SetUp()
        {
            AssetDatabase.CreateFolder("Assets/Tests", "LocalizationScratch");
            List<Locale> locales = new List<Locale>();
            foreach (string code in new[] { "en", "fr" })
            {
                Locale locale = Locale.CreateLocale(new LocaleIdentifier(code));
                AssetDatabase.CreateAsset(locale, FOLDER + "/" + code + ".asset");
                locales.Add(locale);
            }
            collection = LocalizationEditorSettings.CreateStringTableCollection("Scratch", FOLDER, locales);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(FOLDER);
        }

        static StringsCsv Csv(string rows)
        {
            return StringsCsv.Parse("key,note,en,fr,reviewed\n" + rows);
        }

        static string Text(StringTableCollection collection, string code, string key)
        {
            foreach (StringTable table in collection.StringTables)
                if (table.LocaleIdentifier.Code == code)
                    return table.GetEntry(key)?.Value;
            return null;
        }

        [Test]
        public void FillTables_EveryKeyAndLocale()
        {
            LocalizationSetup.FillTables(collection, Csv("hello,,HELLO,BONJOUR,\nsteals,,{0} STEALS,{0} VOLS,\n"));

            Assert.AreEqual("HELLO", Text(collection, "en", "hello"));
            Assert.AreEqual("BONJOUR", Text(collection, "fr", "hello"));
            Assert.AreEqual("{0} VOLS", Text(collection, "fr", "steals"));
        }

        [Test]
        public void BeforeBuild_StaleTables_RefreshedFromTheCsv()
        {
            LocalizationSetup.FillTables(collection, Csv("word.play,,OLD PLAY,VIEUX,\n"));

            LocalizationSetup.BeforeBuild(collection);

            Assert.AreEqual(Loc.Csv.Get("word.play", "fr"), Text(collection, "fr", "word.play"));
        }

        [Test]
        public void FillTables_RemovesKeysNoLongerInTheCsv()
        {
            LocalizationSetup.FillTables(collection, Csv("hello,,HELLO,BONJOUR,\nold,,OLD,VIEUX,\n"));

            LocalizationSetup.FillTables(collection, Csv("hello,,HELLO,SALUT,\n"));

            Assert.AreEqual("SALUT", Text(collection, "fr", "hello"), "updated");
            Assert.IsNull(collection.SharedData.GetEntry("old"), "gone");
        }
    }
}

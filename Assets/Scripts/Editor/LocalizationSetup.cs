using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Sets up Unity Localization for the game, from the strings CSV (Resources/Localization/strings.csv, the source of
/// truth; tools/strings.py writes it):
/// - Tools → Dead on Arrival → Localization → Set Up: the settings (Assets/Localization/LocalizationSettings.asset, made
///   the active ones), the 16 locales (Assets/Localization/Locales) and the "Game" string table collection
///   (Assets/Localization/Tables), filled from the CSV. Run once (EDITOR-TODO.md), save, commit Assets/Localization.
/// - Tools → Dead on Arrival → Localization → Reimport Strings: the tables again from the CSV after it changes (every
///   build does it too, so a build never ships old text).
/// Until it's run the game reads the CSV itself (Loc). See docs/superpowers/specs/2026-10-24-localization-design.md
/// </summary>
public static class LocalizationSetup
{
    const string ROOT = "Assets/Localization";
    const string LOCALES = ROOT + "/Locales";
    const string TABLES = ROOT + "/Tables";
    const string SETTINGS = ROOT + "/LocalizationSettings.asset";

    [MenuItem("Tools/Dead on Arrival/Localization/Set Up")]
    public static void SetUp()
    {
        Folder("Assets", "Localization");
        Folder(ROOT, "Locales");
        Folder(ROOT, "Tables");

        LocalizationSettings settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SETTINGS);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SETTINGS);
        }
        LocalizationEditorSettings.ActiveLocalizationSettings = settings;

        List<Locale> locales = new List<Locale>();
        foreach (string code in LocaleCodes.All)
        {
            string path = LOCALES + "/" + code + ".asset";
            Locale locale = AssetDatabase.LoadAssetAtPath<Locale>(path);
            if (locale == null)
            {
                locale = Locale.CreateLocale(new LocaleIdentifier(code));
                AssetDatabase.CreateAsset(locale, path);
            }
            if (LocalizationEditorSettings.GetLocale(locale.Identifier) == null)
                LocalizationEditorSettings.AddLocale(locale);
            locales.Add(locale);
        }

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(Loc.TABLE)
            ?? LocalizationEditorSettings.CreateStringTableCollection(Loc.TABLE, TABLES, locales);
        FillTables(collection, StringsCsv.Load());
        AssetDatabase.SaveAssets();
        Debug.Log($"Localization set up: {locales.Count} locales, {collection.SharedData.Entries.Count} strings in {Loc.TABLE}");
    }

    [MenuItem("Tools/Dead on Arrival/Localization/Reimport Strings")]
    public static void Reimport()
    {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(Loc.TABLE);
        if (collection == null)
        {
            Debug.LogWarning("Run Tools → Dead on Arrival → Localization → Set Up first");
            return;
        }
        FillTables(collection, StringsCsv.Load());
        AssetDatabase.SaveAssets();
    }

    /// <summary>A build ships the CSV as it is now: the tables (which the game reads first) refreshed from it</summary>
    internal static void BeforeBuild(StringTableCollection collection)
    {
        StringsCsv csv = StringsCsv.Load();
        if (collection == null || csv == null)
            return;
        FillTables(collection, csv);
        AssetDatabase.SaveAssets();
    }

    class RefreshBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BeforeBuild(LocalizationEditorSettings.GetStringTableCollection(Loc.TABLE));
        }
    }

    /// <summary>Every key of the CSV in every table of the collection (its locale's column), and no key the CSV lacks</summary>
    internal static void FillTables(StringTableCollection collection, StringsCsv csv)
    {
        HashSet<string> keys = new HashSet<string>(csv.Keys);
        List<string> gone = new List<string>();
        foreach (SharedTableData.SharedTableEntry entry in collection.SharedData.Entries)
            if (!keys.Contains(entry.Key))
                gone.Add(entry.Key);
        foreach (string key in gone)
            collection.RemoveEntry(key);

        foreach (string key in csv.Keys)
        {
            if (collection.SharedData.GetEntry(key) == null)
                collection.SharedData.AddKey(key);
            foreach (StringTable table in collection.StringTables)
            {
                string text = csv.Get(key, table.LocaleIdentifier.Code);
                if (text != null)
                    table.AddEntry(key, text);
                EditorUtility.SetDirty(table);
            }
        }
        EditorUtility.SetDirty(collection.SharedData);
        EditorUtility.SetDirty(collection);
    }

    static void Folder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// The game's text, in the language it speaks: Loc.Get("pause.restart"). The text comes from Unity Localization's
/// "Game" table once its settings exist (Tools → Dead on Arrival → Localization → Set Up builds them from the CSV), and
/// from the CSV itself until then, so the game and its tests work either way. A missing string falls back (Latin
/// American Spanish to Spain's, Brazil's to Portugal's, all to English) and never shows a key to a player.
/// See docs/superpowers/specs/2026-10-24-localization-design.md
/// </summary>
public static class Loc
{
    public const string TABLE = "Game";

    static string language = LocaleCodes.ENGLISH;
    static StringsCsv csv;
    static readonly HashSet<string> warned = new HashSet<string>();

    /// <summary>The locale the game speaks now</summary>
    public static string Language => language;

    /// <summary>The language changed: text already on screen shows it again</summary>
    public static event Action Changed;

    /// <summary>The strings read from the CSV (the fallback source); tests swap it</summary>
    internal static StringsCsv Csv
    {
        get { return csv ?? (csv = StringsCsv.Load() ?? StringsCsv.Parse("")); }
        set { csv = value; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        language = LocaleCodes.ENGLISH;
        csv = null;
        Changed = null;
        warned.Clear();
    }

    /// <summary>Speaks a language from now (an unknown one is English). Text on screen follows (Changed)</summary>
    public static void SetLanguage(string code)
    {
        if (!LocaleCodes.Known(code))
            code = LocaleCodes.ENGLISH;
        if (code == language)
            return;
        language = code;

        if (Application.isPlaying && LocalizationSettings.HasSettings)
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (locale != null)
                LocalizationSettings.SelectedLocale = locale;
        }
        Changed?.Invoke();
    }

    /// <summary>A key's text in the language spoken now, with any arguments filled in ({0}, {1}…)</summary>
    public static string Get(string key, params object[] args)
    {
        string text = Find(key);
        if (text == null)
        {
            if (warned.Add(key))
                Debug.LogWarning("No text for " + key);
            return key;
        }
        if (args == null || args.Length == 0)
            return text;
        try
        {
            return string.Format(CultureInfo.InvariantCulture, text, args);
        }
        catch (FormatException)
        {
            if (warned.Add(key))
                Debug.LogWarning("Too few arguments for " + key);
            return text;
        }
    }

    // The language spoken, then its fallbacks
    static string Find(string key)
    {
        for (string code = language; code != null; code = LocaleCodes.FallbackOf(code))
        {
            string text = FromTables(key, code) ?? Csv.Get(key, code);
            if (text != null)
                return text;
        }
        return null;
    }

    // Unity Localization's table, once it's set up and ready (never while editing, or before Set Up)
    static string FromTables(string key, string code)
    {
        if (!Application.isPlaying || !LocalizationSettings.HasSettings || !LocalizationSettings.InitializationOperation.IsDone
            || LocalizationSettings.InitializationOperation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
            return null;
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
        if (locale == null)
            return null;
        var table = LocalizationSettings.StringDatabase.GetTable(TABLE, locale);
        var entry = table != null ? table.GetEntry(key) : null;
        return entry != null && !string.IsNullOrEmpty(entry.Value) ? entry.Value : null;
    }
}

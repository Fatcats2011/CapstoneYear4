using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// The languages the game speaks, in the order the LANGUAGE row steps through them, and what each falls back to when a
/// string is missing. See docs/superpowers/specs/2026-10-24-localization-design.md
/// </summary>
public static class LocaleCodes
{
    public const string ENGLISH = "en";

    public static readonly string[] All =
    {
        "en", "fr", "de", "it", "nl", "es", "es-419", "pt", "pt-BR", "pl", "tr", "cs", "ro", "hu", "sv", "da",
    };

    /// <summary>The language a missing string comes from: Latin American Spanish from Spain's, Brazil's from Portugal's, the rest from English</summary>
    public static string FallbackOf(string code)
    {
        if (code == ENGLISH)
            return null;
        if (code == "es-419")
            return "es";
        if (code == "pt-BR")
            return "pt";
        return ENGLISH;
    }

    public static bool Known(string code)
    {
        return System.Array.IndexOf(All, code) >= 0;
    }
}

/// <summary>
/// The game's strings as the repo keeps them: Resources/Localization/strings.csv, one row per key ("key,note,en,fr,…,
/// reviewed"), UTF-8 with RFC 4180 quoting. The source of truth: Loc reads it until Unity Localization's tables exist,
/// and the Set Up tool builds those tables from it. "reviewed" lists the locales a native speaker has checked
/// </summary>
public class StringsCsv
{
    public const string RESOURCE = "Localization/strings";

    readonly List<string> header = new List<string>();
    readonly List<string> keys = new List<string>();
    readonly Dictionary<string, string[]> rows = new Dictionary<string, string[]>();

    public IReadOnlyList<string> Header => header;
    public IReadOnlyList<string> Keys => keys;

    /// <summary>The game's file, or null when it's missing</summary>
    public static StringsCsv Load()
    {
        TextAsset file = Resources.Load<TextAsset>(RESOURCE);
        return file != null ? Parse(file.text) : null;
    }

    public static StringsCsv Parse(string text)
    {
        StringsCsv csv = new StringsCsv();
        List<List<string>> records = Records(text ?? "");
        if (records.Count == 0)
            return csv;
        csv.header.AddRange(records[0]);
        for (int r = 1; r < records.Count; r++)
        {
            List<string> record = records[r];
            if (record.Count == 0 || string.IsNullOrEmpty(record[0]))
                continue;
            string[] cells = new string[csv.header.Count];
            for (int c = 0; c < cells.Length && c < record.Count; c++)
                cells[c] = record[c];
            csv.keys.Add(record[0]);
            csv.rows[record[0]] = cells;
        }
        return csv;
    }

    /// <summary>A key's text in a locale, or null when the key, the column or the cell is missing or empty</summary>
    public string Get(string key, string locale)
    {
        string cell = Cell(key, locale);
        return string.IsNullOrEmpty(cell) ? null : cell;
    }

    public string Note(string key)
    {
        return Cell(key, "note") ?? "";
    }

    public bool Reviewed(string key, string locale)
    {
        string reviewed = Cell(key, "reviewed");
        return reviewed != null && System.Array.IndexOf(reviewed.Split(' '), locale) >= 0;
    }

    string Cell(string key, string column)
    {
        int index = header.IndexOf(column);
        if (key == null || index < 0 || !rows.TryGetValue(key, out string[] cells))
            return null;
        return cells[index];
    }

    // RFC 4180: commas split cells, quotes wrap a cell that holds commas, quotes ("" for one) or line breaks
    static List<List<string>> Records(string text)
    {
        List<List<string>> records = new List<List<string>>();
        List<string> record = new List<string>();
        StringBuilder cell = new StringBuilder();
        bool quoted = false, any = false;
        if (text.Length > 0 && text[0] == '﻿')
            text = text.Substring(1);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                    quoted = false;
                else
                    cell.Append(c);
                continue;
            }

            if (c == '"')
            {
                quoted = true;
                any = true;
            }
            else if (c == ',')
            {
                record.Add(cell.ToString());
                cell.Clear();
                any = true;
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                if (any || cell.Length > 0)
                {
                    record.Add(cell.ToString());
                    records.Add(record);
                }
                record = new List<string>();
                cell.Clear();
                any = false;
            }
            else
            {
                cell.Append(c);
                any = true;
            }
        }
        if (any || cell.Length > 0)
        {
            record.Add(cell.ToString());
            records.Add(record);
        }
        return records;
    }
}

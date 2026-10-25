using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Every seat's rebound controls, kept in bindings.cfg next to settings.cfg, so whoever plays as Player N gets Player
/// N's, across launches. One line per seat and set ("seat1.keyboard=BOOST:&lt;Keyboard&gt;/k;DRIFT:..."), only the
/// controls that differ from the game's. A damaged line, an unknown control or a press that can't be bound is skipped,
/// so loading never throws and loses only that much. See docs/superpowers/specs/2026-10-23-control-rebinding-design.md
/// </summary>
public class BindingStore
{
    public const string FILE_NAME = "bindings.cfg";
    public const string PLAYER_MAP = "Player";

    static BindingStore current;

    /// <summary>The game's: bindings.cfg, read the first time it's needed</summary>
    public static BindingStore Current => current ?? (current = Load());

    /// <summary>Test players drive on the game's own controls, whatever this PC has saved (TestPlayers)</summary>
    public static void UseNoneSaved()
    {
        current = new BindingStore();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        current = null;
    }

    readonly Dictionary<string, string> lines = new Dictionary<string, string>(); // "seat1.keyboard" → its entries

    static string LineKey(int seat, BindingSet set)
    {
        return "seat" + seat + "." + (set == BindingSet.Keyboard ? "keyboard" : "controller");
    }

    /// <summary>Remembers a seat's (1-4) controls as they are in a player's map</summary>
    public void Keep(int seat, InputActionMap map)
    {
        if (seat < 1 || seat > Constants.MAX_PLAYERS)
            return;
        foreach (BindingSet set in new[] { BindingSet.Keyboard, BindingSet.Controller })
        {
            StringBuilder entries = new StringBuilder();
            foreach (RebindSlot slot in RebindRules.SlotsFor(map, set))
            {
                InputBinding binding = map.FindAction(slot.Action).bindings[slot.BindingIndex];
                if (binding.effectivePath == binding.path)
                    continue;
                if (entries.Length > 0)
                    entries.Append(';');
                entries.Append(slot.Name).Append(':').Append(binding.effectivePath);
            }
            if (entries.Length > 0)
                lines[LineKey(seat, set)] = entries.ToString();
            else
                lines.Remove(LineKey(seat, set));
        }
    }

    /// <summary>Gives a player's map a seat's (1-4) controls, in place of whatever it had</summary>
    public void Apply(int seat, InputActionMap map)
    {
        foreach (BindingSet set in new[] { BindingSet.Keyboard, BindingSet.Controller })
        {
            RebindRules.Reset(map, set);
            if (!lines.TryGetValue(LineKey(seat, set), out string entries))
                continue;
            foreach (string entry in entries.Split(';'))
            {
                int colon = entry.IndexOf(':');
                if (colon <= 0)
                    continue;
                string path = entry.Substring(colon + 1);
                if (RebindRules.CanBind(map, set, path))
                    RebindRules.Bind(map, set, entry.Substring(0, colon), path);
            }
        }
    }

    public string ToText()
    {
        StringBuilder text = new StringBuilder();
        foreach (KeyValuePair<string, string> line in lines)
            text.Append(line.Key).Append('=').Append(line.Value).Append('\n');
        return text.ToString();
    }

    /// <summary>Reads bindings.cfg's text. Lines it doesn't know are dropped</summary>
    public static BindingStore Parse(string text)
    {
        BindingStore store = new BindingStore();
        foreach (string raw in (text ?? "").Split('\n'))
        {
            string line = raw.Trim();
            int equals = line.IndexOf('=');
            if (equals <= 0)
                continue;
            string key = line.Substring(0, equals);
            for (int seat = 1; seat <= Constants.MAX_PLAYERS; seat++)
            {
                if (key == LineKey(seat, BindingSet.Keyboard) || key == LineKey(seat, BindingSet.Controller))
                    store.lines[key] = line.Substring(equals + 1);
            }
        }
        return store;
    }

    static string FilePath => Path.Combine(Application.persistentDataPath, FILE_NAME);

    /// <summary>bindings.cfg as saved, or nothing when it's missing or can't be read</summary>
    public static BindingStore Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return Parse(File.ReadAllText(FilePath));
        }
        catch (Exception e) // an unreadable file: every seat starts on the game's controls
        {
            Debug.LogWarning($"Could not load bindings from {FilePath}: {e.Message}");
        }
        return new BindingStore();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(FilePath, ToText());
        }
        catch (Exception e) // a full disk or read-only folder shouldn't break the pause menu
        {
            Debug.LogWarning($"Could not save bindings to {FilePath}: {e.Message}");
        }
    }
}

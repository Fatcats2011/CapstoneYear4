using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// The texts typed into scenes and prefabs (the Dispatch board's COLOUR, LEAVE…, Options' Off / On, "Employee of The
/// Month"): each is recognised by its English words (any case) and shows its key's text in another language; English
/// keeps the text as it was placed. Texts the code writes are left alone (they ask Loc themselves). DrawnWords runs it
/// over every scene and player
/// </summary>
public static class SceneTexts
{
    // The keys whose English a scene or prefab has typed in
    static readonly string[] KEYS =
    {
        "dispatch.colour", "dispatch.company", "dispatch.customize", "dispatch.done", "dispatch.hat", "dispatch.invite",
        "dispatch.leave", "dispatch.player", "dispatch.clockin", "dispatch.alldispatched", "dispatch.dispatched",
        "dispatch.gettingready", "dispatch.hiring", "matchmaking.friends", "word.on", "word.off", "results.employee",
        "hud.score", "hud.player1", "hud.goldenin", "menu.enabletutorial", "menu.credits",
    };

    static Dictionary<string, string> keyOfEnglish;

    /// <summary>The key a placed text's words are, or null</summary>
    public static string KeyOf(string placed)
    {
        if (string.IsNullOrWhiteSpace(placed))
            return null;
        if (keyOfEnglish == null)
        {
            keyOfEnglish = new Dictionary<string, string>();
            foreach (string key in KEYS)
            {
                string english = Loc.Csv.Get(key, LocaleCodes.ENGLISH);
                if (english != null)
                    keyOfEnglish[english.Trim().ToUpperInvariant()] = key;
            }
        }
        return keyOfEnglish.TryGetValue(placed.Trim().ToUpperInvariant(), out string found) ? found : null;
    }

    /// <summary>Makes a placed text follow the language, if its words are known (once)</summary>
    public static void Localize(TMP_Text text)
    {
        if (text == null || text.GetComponent<LocalizedText>() != null || text.GetComponent<PlacedText>() != null)
            return;
        string key = KeyOf(text.text);
        if (key != null)
            text.gameObject.AddComponent<PlacedText>().Begin(text, key);
    }

    /// <summary>Tests: forgets the English it learnt (a new CSV)</summary>
    internal static void Forget()
    {
        keyOfEnglish = null;
    }
}

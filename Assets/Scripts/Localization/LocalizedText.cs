using TMPro;
using UnityEngine;

/// <summary>
/// A text that shows a key's text (Loc) and follows the language: change it in Options and the text on screen changes
/// with it. Put on by code (On), never in a scene
/// </summary>
public class LocalizedText : MonoBehaviour
{
    TMP_Text text;
    string key;
    object[] args;
    bool listening;

    /// <summary>The key it shows</summary>
    public string Key => key;

    /// <summary>Makes a text show a key's text from now on (its own LocalizedText, made once)</summary>
    public static LocalizedText On(TMP_Text text, string key, params object[] args)
    {
        LocalizedText shown = text.GetComponent<LocalizedText>();
        if (shown == null)
            shown = text.gameObject.AddComponent<LocalizedText>();
        shown.text = text;
        shown.Set(key, args);
        return shown;
    }

    /// <summary>Shows another key (or the same one with other arguments)</summary>
    public void Set(string newKey, params object[] newArgs)
    {
        key = newKey;
        args = newArgs;
        if (!listening)
        {
            listening = true;
            Loc.Changed += Show;
        }
        Show();
    }

    void Show()
    {
        if (text != null && key != null)
            text.text = Loc.Get(key, args);
    }

    // Loc's event is static: a text gone with its scene never keeps listening
    void OnDestroy()
    {
        if (listening)
            Loc.Changed -= Show;
    }
}

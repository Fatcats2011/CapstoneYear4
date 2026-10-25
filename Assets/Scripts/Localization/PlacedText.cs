using TMPro;
using UnityEngine;

/// <summary>
/// A placed text following the language: its own words in English, its key's text otherwise (SceneTexts). Once code
/// writes something else there (a ticket's colour), that stays
/// </summary>
public class PlacedText : MonoBehaviour
{
    TMP_Text text;
    string key;
    string placed;
    string wrote; // what this last showed: anything else there, code wrote (a ticket's colour), and it stays

    internal void Begin(TMP_Text shown, string textKey)
    {
        text = shown;
        key = textKey;
        placed = shown.text;
        Loc.Changed += Show;
        Show();
    }

    void Show()
    {
        if (text == null || (wrote != null && text.text != wrote))
            return;
        wrote = Loc.Language == LocaleCodes.ENGLISH ? placed : Loc.Get(key);
        text.text = wrote;
    }

    void OnDestroy()
    {
        Loc.Changed -= Show;
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// The hand-lettered words (menu art: RESUME, OPTIONS, PAUSED…) in every language. In English the drawn art shows as it
/// always has; in another language the art hides and a text in Jack's Candlestick (the font the art was drawn with)
/// spells the word in its place, the art's off-white, shrinking to fit a longer word (down to 40% of the size the
/// English word fits). Known by sprite name (KeyOfSprite). Swapped at runtime, for every scene as it loads and every
/// player as they join: no scene or prefab carries it. See docs/superpowers/specs/2026-10-24-localization-design.md
/// </summary>
public static class DrawnWords
{
    public static readonly IReadOnlyDictionary<string, string> KeyOfSprite = new Dictionary<string, string>
    {
        { "MenuText_UI-sprites2_0", "word.results" },
        { "MenuText_UI-sprites2_1", "word.paused" },
        { "MenuText_UI-sprites2_2", "word.select" },
        { "MenuText_UI-sprites2_3", "word.menu" },
        { "MenuText_UI-sprites2_4", "word.on" },
        { "MenuText_UI-sprites2_5", "word.off" },
        { "MenuText_UI-sprites2_6", "word.return" },
        { "MenuText_UI-sprites2_7", "word.resume" },
        { "MenuText_UI-sprites2_8", "word.press" },
        { "MenuText_UI-sprites2_9", "word.toconfirm" },
        { "MenuText_UI-sprites2_10", "word.tocontinue" },
        { "MenuText_UI-sprites2_11", "word.toboost" },
        { "MenuNumber_UI-spritesUPDATED_0", "word.credits" },
        { "MenuNumber_UI-spritesUPDATED_1", "word.fullscreen" },
        { "MenuNumber_UI-spritesUPDATED_2", "word.play" },
        { "MenuNumber_UI-spritesUPDATED_3", "word.music" },
        { "MenuNumber_UI-spritesUPDATED_4", "word.quit" },
        { "MenuNumber_UI-spritesUPDATED_5", "word.sfx" },
        { "MenuNumber_UI-spritesUPDATED_6", "word.options" },
        { "MenuNumber_UI-spritesUPDATED_7", "word.go" },
        { "MenuNumber_UI-spritesUPDATED_8", "word.ready" },
        { "MenuNumber_UI-spritesUPDATED_9", "word.pressanybutton" },
        { "Local", "word.local" },
        { "Multiplayer", "word.multiplayer" },
    };

    static readonly Color OFF_WHITE = new Color32(0xF2, 0xF2, 0xF2, 0xFF);
    public const float SMALLEST = 0.4f; // a long word shrinks rather than spill

    /// <summary>Whether a sprite is word art (a word sheet's word, not one of its numbers): what the scan checks</summary>
    public static bool LooksLikeAWord(Sprite sprite)
    {
        string name = sprite.name;
        if (name.StartsWith("MenuText_UI-sprites2_") || name == "Local" || name == "Multiplayer")
            return true;
        const string NUMBERS = "MenuNumber_UI-spritesUPDATED_";
        return name.StartsWith(NUMBERS) && int.TryParse(name.Substring(NUMBERS.Length), out int index) && index <= 9;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void SwapAsScenesLoad()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            SwapAll(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i));
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SwapAll(scene);
    }

    /// <summary>Every drawn word and placed text in a scene (inactive ones too)</summary>
    public static void SwapAll(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            SwapUnder(root);
    }

    /// <summary>Every drawn word and placed text under an object (a player's menus as they join)</summary>
    public static void SwapUnder(GameObject root)
    {
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
            Swap(image);
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true))
            SceneTexts.Localize(text);
    }

    /// <summary>
    /// Gives a word image its text (once): shown instead of the art in a language other than English. Null for an image
    /// that isn't a known word
    /// </summary>
    public static TMP_Text Swap(Image image)
    {
        if (image == null || image.sprite == null || !KeyOfSprite.ContainsKey(image.sprite.name))
            return null;
        DrawnWord word = image.GetComponent<DrawnWord>();
        if (word == null)
            word = image.gameObject.AddComponent<DrawnWord>();
        return word.Begin(image, OFF_WHITE);
    }
}

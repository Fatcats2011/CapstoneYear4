using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gives every drawn button prompt (an Image showing one of MenUI_Buttons' Xbox buttons) a ButtonGlyph, so it shows the
/// PlayStation or keyboard button for whoever plays. Done as each scene loads and as each player joins, so no scene or
/// prefab has to hold one (playtest, 2026-10-24: keyboard players saw Xbox buttons; ButtonGlyphSetup's editor step had
/// never been run). The art comes from ButtonArtSet.glyphs
/// </summary>
public static class ButtonGlyphs
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void FollowScenes()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            OnSceneLoaded(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.isLoaded)
            return;
        foreach (GameObject root in scene.GetRootGameObjects())
            AttachUnder(root);
    }

    /// <summary>
    /// Every prompt Image under root (hidden ones too) that has no ButtonGlyph yet gets one. Returns how many did
    /// </summary>
    public static int AttachUnder(GameObject root, ButtonArtSet art = null)
    {
        art = art != null ? art : ButtonArtSet.Results();
        if (root == null || art == null || art.glyphs == null)
            return 0;

        int attached = 0;
        foreach (Image image in root.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == null || image.GetComponent<ButtonGlyph>() != null)
                continue;

            foreach (ButtonArtSet.Glyph glyph in art.glyphs)
            {
                if (glyph.xbox != image.sprite)
                    continue;

                ButtonGlyph added = image.gameObject.AddComponent<ButtonGlyph>();
                added.xbox = glyph.xbox;
                added.playStation = glyph.playStation;
                added.keyboard = glyph.keyboard;
                added.Refresh();
                attached++;
                break;
            }
        }
        return attached;
    }
}

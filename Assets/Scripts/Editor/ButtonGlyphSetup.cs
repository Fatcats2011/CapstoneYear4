using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the game's button prompts follow the device in use (ButtonGlyph): an Image showing one of MenUI_Buttons' Xbox
/// buttons gets that button's PlayStation and keyboard art (Assets/Sprites/UI/Buttons, drawn by tools/button-art.py).
/// Tools → Dead on Arrival → Add Button Glyphs To Open Scenes does every such Image in the open scenes; the Dispatch
/// board's builder does its own prompts
/// </summary>
public static class ButtonGlyphSetup
{
    const string XBOX_SHEET = "Assets/Sprites/UI/MenUI_Buttons.png";
    const string ART = "Assets/Sprites/UI/Buttons/";

    // Each Xbox button (MenUI_Buttons' sprites) as PlayStation and on the keyboard (KeyboardControls' keys)
    static readonly Dictionary<string, (string playStation, string keyboard)> Buttons = new Dictionary<string, (string, string)>
    {
        { "MenUI_Buttons_7", ("PS_Cross", "Key_Space") },     // A
        { "MenUI_Buttons_4", ("PS_Circle", "Key_Backspace") }, // B
        { "MenUI_Buttons_2", ("PS_Square", "Key_Q") },        // X
        { "MenUI_Buttons_3", ("PS_Triangle", "Key_Tab") },    // Y
        { "MenUI_Buttons_5", ("PS_L1", "Key_F") },            // LB
        { "MenUI_Buttons_6", ("PS_R1", "Key_R") },            // RB
    };

    [MenuItem("Tools/Dead on Arrival/Add Button Glyphs To Open Scenes", false, 70)]
    static void AttachInOpenScenes()
    {
        int done = 0;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            int inScene = 0;
            foreach (Image image in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Image>(true)))
            {
                if (Attach(image))
                    inScene++;
            }
            if (inScene > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            done += inScene;
        }
        Debug.Log("Button glyphs: " + done + " button prompts follow the device now. Save the scene(s) to keep it.");
    }

    /// <summary>
    /// Gives an Image showing one of the game's Xbox buttons a ButtonGlyph with its PlayStation and keyboard art (kept to
    /// its shape: a key can be wider than a button). Returns whether it was one; anything else is left alone
    /// </summary>
    public static bool Attach(Image image)
    {
        if (image == null || image.sprite == null || !Buttons.TryGetValue(image.sprite.name, out var art)
            || AssetDatabase.GetAssetPath(image.sprite) != XBOX_SHEET)
            return false;

        ButtonGlyph glyph = image.GetComponent<ButtonGlyph>();
        if (glyph == null)
            glyph = Undo.AddComponent<ButtonGlyph>(image.gameObject);

        SerializedObject so = new SerializedObject(glyph);
        so.FindProperty("xbox").objectReferenceValue = image.sprite;
        so.FindProperty("playStation").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ART + art.playStation + ".png");
        so.FindProperty("keyboard").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(ART + art.keyboard + ".png");
        so.ApplyModifiedPropertiesWithoutUndo();
        if (!image.preserveAspect)
        {
            Undo.RecordObject(image, "Button glyph");
            image.preserveAspect = true;
        }
        return true;
    }
}

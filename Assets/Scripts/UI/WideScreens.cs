using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ultrawide monitors: a canvas fits its view's height when that view is wider than 16:9. The menus and HUDs are laid out
/// for 1920 x 1080, and a canvas scaled to the width (or half and half) is shorter than 1080 in a wide view, so what's near
/// its top and bottom falls off. Each canvas goes by its own view: half an ultrawide in split screen is narrow, and scaling
/// that to the height would cut off its sides. A view at 16:9 or narrower has the canvas's own setting. Made at launch;
/// looks again now and then, as canvases come with scenes and players and the window changes
/// </summary>
public class WideScreens : MonoBehaviour
{
    const float SIXTEEN_NINE = 16f / 9f;
    const float LOOK_EVERY = 1f; // seconds, unscaled

    // The canvases fitted now, and the setting each had
    static readonly Dictionary<CanvasScaler, float> authored = new Dictionary<CanvasScaler, float>();
    static readonly List<CanvasScaler> gone = new List<CanvasScaler>();

    float nextLook;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject(nameof(WideScreens));
        DontDestroyOnLoad(go);
        go.AddComponent<WideScreens>();
    }

    /// <summary>Whether a view this size fits its canvases to the height</summary>
    internal static bool FitsHeight(float width, float height)
    {
        return height > 0f && width / height > SIXTEEN_NINE + 0.01f;
    }

    void Update()
    {
        if (Time.unscaledTime < nextLook)
            return;
        nextLook = Time.unscaledTime + LOOK_EVERY;
        if (authored.Count > 0 || FitsHeight(Screen.width, Screen.height))
            FitAll();
    }

    /// <summary>Every scaled canvas up now: fitted to the height in a wide view, back to its own setting otherwise</summary>
    internal static void FitAll()
    {
        gone.Clear();
        foreach (CanvasScaler scaler in authored.Keys)
            if (scaler == null)
                gone.Add(scaler);
        foreach (CanvasScaler scaler in gone)
            authored.Remove(scaler);

        foreach (CanvasScaler scaler in FindObjectsOfType<CanvasScaler>(true))
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                continue;
            Canvas canvas = scaler.GetComponent<Canvas>();
            Rect view = canvas != null && canvas.isActiveAndEnabled ? canvas.pixelRect : new Rect(0f, 0f, Screen.width, Screen.height);
            if (FitsHeight(view.width, view.height))
            {
                if (!authored.ContainsKey(scaler))
                    authored[scaler] = scaler.matchWidthOrHeight;
                scaler.matchWidthOrHeight = 1f;
            }
            else if (authored.TryGetValue(scaler, out float own))
            {
                scaler.matchWidthOrHeight = own;
                authored.Remove(scaler);
            }
        }
    }
}

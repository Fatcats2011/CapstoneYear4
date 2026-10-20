using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// When the mouse cursor shows. Nothing in the game uses the mouse, so in a build the cursor hides once the mouse has
/// been still for a moment (Steam Deck and controller play) and comes back when it moves. The editor always shows it.
/// </summary>
public static class CursorRules
{
    public const float HIDE_AFTER_SECONDS = 2f;

    public static bool Shows(float secondsSinceMouseMoved, bool isEditor)
    {
        return isEditor || secondsSinceMouseMoved < HIDE_AFTER_SECONDS;
    }
}

/// <summary>
/// Applies CursorRules every frame. Creates itself before the first scene loads, so no scene holds it.
/// </summary>
public class CursorHider : MonoBehaviour
{
    float lastMoved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Create()
    {
        if (Application.isEditor || Application.isBatchMode)
            return;
        GameObject go = new GameObject(nameof(CursorHider));
        DontDestroyOnLoad(go);
        go.AddComponent<CursorHider>();
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.delta.ReadValue() != Vector2.zero)
            lastMoved = Time.unscaledTime;

        bool shows = CursorRules.Shows(Time.unscaledTime - lastMoved, Application.isEditor);
        if (Cursor.visible != shows)
            Cursor.visible = shows;
    }
}

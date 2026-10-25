using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>A menu the mouse can work (MouseMenus): its rows, which is selected, and what a click and right-click do</summary>
public interface IMouseMenu
{
    /// <summary>The rows the pointer can pick, top to bottom (none: only right-click works)</summary>
    IReadOnlyList<RectTransform> MouseRows { get; }

    int MouseSelected { get; }

    /// <summary>Whether a row's band runs the screen's width (a row whose value sits far right of its label)</summary>
    bool MouseRowsSpanTheScreen { get; }

    /// <summary>The camera the menu's view is drawn in (a split-screen player's own), or null to take the canvas's</summary>
    Camera MouseCamera { get; }

    void MouseSelect(int row);

    /// <summary>A left click on a row (already selected): what A does there, or a step for a row with a value</summary>
    void MouseClick(int row, Vector2 point, Camera camera);

    /// <summary>A right click: what B does</summary>
    void MouseBack();
}

/// <summary>
/// The mouse in the menus: the title screen, Options (every page), Credits and the pause menu of whoever paused.
/// Moving the pointer over a row selects it; a left click does what A does there (a click on a row's value steps it,
/// right or left of where it shows); a right click does what B does. The menus keep their own selectors, so the pad and
/// keyboard still work alongside. Never for driving. Made before the first scene loads, so no scene holds it.
/// See docs/controls.md
/// </summary>
public class MouseMenus : MonoBehaviour
{
    readonly List<Rect> rects = new List<Rect>();
    float lastMoved = float.NegativeInfinity;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject(nameof(MouseMenus));
        DontDestroyOnLoad(go);
        go.AddComponent<MouseMenus>();
    }

    void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        bool moved = mouse.delta.ReadValue() != Vector2.zero;
        if (moved)
            lastMoved = Time.unscaledTime;
        bool click = mouse.leftButton.wasPressedThisFrame;
        bool back = mouse.rightButton.wasPressedThisFrame;
        if (!moved && !click && !back)
            return;

        IMouseMenu menu = Active();
        if (menu != null)
            Drive(menu, mouse.position.ReadValue(), moved, click, back, Time.unscaledTime - lastMoved);
    }

    /// <summary>
    /// The pointer at a point: moved, clicked and right-clicked, this long after it last moved (tests drive this). Clicks
    /// count only while the cursor shows
    /// </summary>
    internal void Drive(IMouseMenu menu, Vector2 point, bool moved, bool click, bool back, float secondsSinceMoved = 0f)
    {
        if (!MouseMenuRules.ClickCounts(secondsSinceMoved))
        {
            click = false;
            back = false;
        }
        IReadOnlyList<RectTransform> rows = menu.MouseRows;
        Camera camera = menu.MouseCamera;
        rects.Clear();
        if (rows != null)
        {
            foreach (RectTransform row in rows)
            {
                if (camera == null && row != null)
                    camera = CameraOf(row);
                Rect area = row != null && row.gameObject.activeInHierarchy ? MouseMenuRules.ScreenRect(row, camera) : Rect.zero;
                if (menu.MouseRowsSpanTheScreen && area != Rect.zero)
                    area = new Rect(0f, area.y, Screen.width, area.height);
                rects.Add(area);
            }
        }

        int hovered = MouseMenuRules.Hovered(rects, point);
        if (MouseMenuRules.Selects(moved || click, hovered, menu.MouseSelected))
            menu.MouseSelect(hovered);
        if (click && hovered >= 0)
            menu.MouseClick(hovered, point, camera);
        if (back)
            menu.MouseBack();
    }

    static Camera CameraOf(RectTransform row)
    {
        Canvas canvas = row.GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    // The menu on screen now: someone's pause, else the title screen's Options, Credits or entries
    static IMouseMenu Active()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null || players.PlayerCount == 0 || GameManager.Instance == null)
            return null;

        if (players.IsPaused)
        {
            foreach (PlayerSlot slot in players.Roster.LocalPlayers)
            {
                MenuInteractions menus = slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>();
                if (menus.hostPause)
                    return menus.pauseMenu;
            }
            return null;
        }

        switch (GameManager.Instance.MainState)
        {
            case GameState.Menu:
                return MainMenu.Instance;
            case GameState.Options:
                return OptionsMenu.Instance;
            case GameState.Credits:
                return CreditsMenu.Instance;
            default:
                return null;
        }
    }
}

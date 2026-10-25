using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The mouse in the menus (MouseMenus): the row under the pointer, which way a click on a row's value steps, and a
/// row's area on screen. The menus keep their own selectors: the mouse only moves them and presses for the player
/// </summary>
public static class MouseMenuRules
{
    /// <summary>The first row whose screen area holds the point, or -1</summary>
    public static int Hovered(IReadOnlyList<Rect> rows, Vector2 point)
    {
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Contains(point))
                return i;
        }
        return -1;
    }

    /// <summary>A click on a row with a value: right of where the value shows steps right, left of it steps left</summary>
    public static bool StepsRight(float clickX, float valueX)
    {
        return clickX >= valueX;
    }

    /// <summary>How far either side of a value's centre a click applies it rather than stepping (the Resolution size), in pixels</summary>
    public const float APPLY_HALF_WIDTH = 120f;

    /// <summary>
    /// Whether a click counts: only while the cursor shows (it moved lately, CursorRules). A click with it hidden is one
    /// that focused the window, or a stray one, and must never quit or leave a match
    /// </summary>
    public static bool ClickCounts(float secondsSinceMoved)
    {
        return CursorRules.Shows(secondsSinceMoved, false);
    }

    /// <summary>The choice (by its x on screen) nearest a click, or -1 without any: a drawn row's points (Off / On, the volumes)</summary>
    public static int Nearest(float[] choicesX, float clickX)
    {
        int best = -1;
        for (int i = 0; i < choicesX.Length; i++)
        {
            if (best < 0 || System.Math.Abs(choicesX[i] - clickX) < System.Math.Abs(choicesX[best] - clickX))
                best = i;
        }
        return best;
    }

    /// <summary>A click near a value: 0 on it (apply), 1 right of it, -1 left of it</summary>
    public static int Zone(float clickX, float valueX)
    {
        if (clickX > valueX + APPLY_HALF_WIDTH)
            return 1;
        if (clickX < valueX - APPLY_HALF_WIDTH)
            return -1;
        return 0;
    }

    /// <summary>Whether the pointer moves the selector: only when the mouse moved, onto another row</summary>
    public static bool Selects(bool mouseMoved, int hovered, int selected)
    {
        return mouseMoved && hovered >= 0 && hovered != selected;
    }

    /// <summary>
    /// Where a row is on screen: its own rect and every shown child's (a drawn row's art and a built row's text overhang
    /// the row's own rect). camera: the canvas's, or null for an overlay
    /// </summary>
    public static Rect ScreenRect(RectTransform row, Camera camera)
    {
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;
        Vector3[] corners = new Vector3[4];
        foreach (RectTransform part in row.GetComponentsInChildren<RectTransform>(false))
        {
            part.GetWorldCorners(corners);
            foreach (Vector3 corner in corners)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                min = any ? Vector2.Min(min, screen) : screen;
                max = any ? Vector2.Max(max, screen) : screen;
                any = true;
            }
        }
        return any ? Rect.MinMaxRect(min.x, min.y, max.x, max.y) : Rect.zero;
    }
}

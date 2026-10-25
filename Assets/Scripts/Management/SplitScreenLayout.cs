using UnityEngine;

/// <summary>
/// How two players share the screen (Options → Display → Split-screen). Three and four players always get quarters
/// </summary>
public enum SplitLayout
{
    /// <summary>Two 16:9 views stacked in the middle (the game's first layout)</summary>
    Boxed,
    /// <summary>Full width, top and bottom</summary>
    Stacked,
    /// <summary>Full height, left and right</summary>
    SideBySide,
}

/// <summary>
/// Viewport rects for local split-screen, in join order (index 0 is the first player to join)
/// </summary>
public static class SplitScreenLayout
{
    /// <summary>The layout picked for two players (saved in settings.cfg)</summary>
    public static SplitLayout TwoPlayers = SplitLayout.Boxed;

    /// <summary>The views for this many players, in the picked layout</summary>
    public static Rect[] ForPlayers(int playerCount)
    {
        return CalculateRects(playerCount, TwoPlayers);
    }

    ///<summary>
    /// Calculates the camera rects for when there are 1 - 4 players. The layout only changes two players' views
    ///</summary>
    public static Rect[] CalculateRects(int playerCount, SplitLayout twoPlayers = SplitLayout.Boxed)
    {
        Rect[] viewportRects = new Rect[playerCount];

        // 1 Player
        if (playerCount == 1)
        {
            viewportRects[0] = new Rect(0, 0, 1, 1);
        }
        else if (playerCount == 2 && twoPlayers == SplitLayout.Stacked)
        {
            viewportRects[0] = new Rect(0, 0.5f, 1, 0.5f);
            viewportRects[1] = new Rect(0, 0, 1, 0.5f);
        }
        else if (playerCount == 2 && twoPlayers == SplitLayout.SideBySide)
        {
            viewportRects[0] = new Rect(0, 0, 0.5f, 1);
            viewportRects[1] = new Rect(0.5f, 0, 0.5f, 1);
        }
        else if (playerCount == 2)
        {
            viewportRects[0] = new Rect(0.25f, 0.5f, 0.5f, 0.5f);
            viewportRects[1] = new Rect(0.25f, 0, 0.5f, 0.5f);
        }
        else if (playerCount == 3)
        {
            viewportRects[0] = new Rect(0, 0.5f, 0.5f, 0.5f);
            viewportRects[1] = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            viewportRects[2] = new Rect(0.25f, 0, 0.5f, 0.5f);
        }
        else if (playerCount == 4)
        {
            viewportRects[0] = new Rect(0, 0.5f, 0.5f, 0.5f);
            viewportRects[1] = new Rect(0.5f, 0.5f, 0.5f, 0.5f);
            viewportRects[2] = new Rect(0, 0, 0.5f, 0.5f);
            viewportRects[3] = new Rect(0.5f, 0, 0.5f, 0.5f);
        }

        return viewportRects;
    }
}
